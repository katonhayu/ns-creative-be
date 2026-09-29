namespace NsCreative.Core.Common;

/// <summary>
/// Query string standar endpoint list: <c>?page=1&amp;pageSize=20&amp;search=&amp;sortBy=nama&amp;sortDir=asc</c>.
/// Nilai di luar batas dinormalkan, tidak ditolak.
/// </summary>
public class QueryParams
{
    public const int PageSizeDefault = 20;
    public const int PageSizeMaks = 100;

    /// <summary>Jumlah maksimal baris endpoint lookup (dropdown).</summary>
    public const int LookupMaks = 50;

    private int _page = 1;
    private int _pageSize = PageSizeDefault;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? PageSizeDefault : Math.Min(value, PageSizeMaks);
    }

    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public string? SortDir { get; set; }

    /// <summary>Teks pencarian yang sudah dipangkas; null jika kosong.</summary>
    public string? SearchBersih => string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();

    public bool Menurun => string.Equals(SortDir, "desc", StringComparison.OrdinalIgnoreCase);
}
