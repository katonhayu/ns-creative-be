using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace NsCreative.Core.Common;

/// <summary>Pola query yang dipakai semua endpoint list: pencarian, urutan, dan paginasi.</summary>
public static class QueryableExtensions
{
    /// <summary>Hitung total, ambil satu halaman, lalu bungkus sebagai <see cref="PagedResult{T}"/>.</summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query, QueryParams param, CancellationToken ct = default)
    {
        var totalItems = await query.CountAsync(ct);
        var items = await query
            .Skip((param.Page - 1) * param.PageSize)
            .Take(param.PageSize)
            .ToListAsync(ct);
        var totalPages = (int)Math.Ceiling(totalItems / (double)param.PageSize);
        return new PagedResult<T>(items, param.Page, param.PageSize, totalItems, totalPages);
    }

    /// <summary>
    /// Pencarian tidak peka huruf besar/kecil pada beberapa kolom teks sekaligus (digabung OR).
    /// Kolom yang null dilewati. Teks kosong berarti tidak ada filter.
    /// </summary>
    public static IQueryable<T> Cari<T>(
        this IQueryable<T> query, string? search, params Expression<Func<T, string?>>[] kolom)
    {
        var kata = string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLower();
        if (kata is null || kolom.Length == 0) return query;

        var x = Expression.Parameter(typeof(T), "x");
        // Dibungkus objek supaya EF mengirimnya sebagai parameter SQL, bukan literal.
        var nilaiKata = Expression.Property(Expression.Constant(new Wadah(kata)), nameof(Wadah.Nilai));
        var toLower = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
        var contains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;

        Expression? kondisi = null;
        foreach (var k in kolom)
        {
            var nilai = new GantiParameter(k.Parameters[0], x).Visit(k.Body);
            var cocok = Expression.AndAlso(
                Expression.NotEqual(nilai, Expression.Constant(null, typeof(string))),
                Expression.Call(Expression.Call(nilai, toLower), contains, nilaiKata));
            kondisi = kondisi is null ? cocok : Expression.OrElse(kondisi, cocok);
        }

        return query.Where(Expression.Lambda<Func<T, bool>>(kondisi!, x));
    }

    /// <summary>Terapkan urutan dari <c>sortBy</c>/<c>sortDir</c> memakai daftar kolom yang diizinkan.</summary>
    public static IQueryable<T> Urutkan<T>(this IQueryable<T> query, QueryParams param, PetaUrutan<T> peta) =>
        peta.Terapkan(query, param);

    private sealed record Wadah(string Nilai);

    private sealed class GantiParameter(ParameterExpression lama, ParameterExpression baru) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == lama ? baru : base.VisitParameter(node);
    }
}

/// <summary>
/// Daftar kolom yang boleh dipakai untuk <c>sortBy</c>. Nama kolom yang tidak dikenal
/// kembali ke kolom bawaan, bukan error. Nama tidak peka huruf besar/kecil.
/// </summary>
public sealed class PetaUrutan<T>
{
    private readonly Dictionary<string, Func<IQueryable<T>, bool, IQueryable<T>>> _kolom =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _menurunBawaan = new(StringComparer.OrdinalIgnoreCase);
    private string? _bawaan;

    /// <param name="nama">Nilai <c>sortBy</c>, misalnya "nama".</param>
    /// <param name="kunci">Kolom yang diurutkan.</param>
    /// <param name="bawaan">Dipakai jika <c>sortBy</c> kosong atau tidak dikenal.</param>
    /// <param name="nullDiBawah">Nilai null selalu di akhir, apa pun arah urutannya.</param>
    /// <param name="lanjutan">Urutan kedua saat nilai kolom sama, misalnya nama.</param>
    /// <param name="menurunBawaan">Arah jika <c>sortDir</c> tidak dikirim, misalnya tanggal terbaru dulu.</param>
    public PetaUrutan<T> Kolom<TKey>(
        string nama,
        Expression<Func<T, TKey>> kunci,
        bool bawaan = false,
        bool nullDiBawah = false,
        Expression<Func<T, string>>? lanjutan = null,
        bool menurunBawaan = false)
    {
        _menurunBawaan[nama] = menurunBawaan;
        _kolom[nama] = (q, turun) =>
        {
            IOrderedQueryable<T> urut;
            if (nullDiBawah)
            {
                var kosong = Expression.Lambda<Func<T, bool>>(
                    Expression.Equal(kunci.Body, Expression.Constant(null, kunci.Body.Type)),
                    kunci.Parameters);
                urut = q.OrderBy(kosong);
                urut = turun ? urut.ThenByDescending(kunci) : urut.ThenBy(kunci);
            }
            else
            {
                urut = turun ? q.OrderByDescending(kunci) : q.OrderBy(kunci);
            }
            return lanjutan is null ? urut : urut.ThenBy(lanjutan);
        };
        if (bawaan || _bawaan is null) _bawaan = nama;
        return this;
    }

    public IQueryable<T> Terapkan(IQueryable<T> query, QueryParams param)
    {
        var nama = param.SortBy is not null && _kolom.ContainsKey(param.SortBy) ? param.SortBy : _bawaan;
        if (nama is null) return query;
        var menurun = string.IsNullOrWhiteSpace(param.SortDir) ? _menurunBawaan[nama] : param.Menurun;
        return _kolom[nama](query, menurun);
    }
}
