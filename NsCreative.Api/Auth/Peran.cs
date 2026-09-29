namespace NsCreative.Api.Auth;

/// <summary>
/// Nilai untuk <c>[Authorize(Roles = ...)]</c>, sesuai kolom Role di katalog API (CLAUDE.md bagian 7).
/// Beberapa atribut [Authorize] digabung dengan AND: atribut di action mempersempit atribut di controller.
/// </summary>
public static class Peran
{
    public const string Admin = "admin";
    public const string AdminPic = "admin,pic";
    public const string Semua = "admin,pic,crew";
}
