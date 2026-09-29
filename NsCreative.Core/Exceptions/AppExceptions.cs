namespace NsCreative.Core.Exceptions;

/// <summary>Data yang diminta tidak ada (atau sudah dihapus). Dipetakan ke HTTP 404.</summary>
public class NotFoundException(string message) : Exception(message);

/// <summary>Permintaan valid secara format tapi melanggar aturan bisnis. Dipetakan ke HTTP 422.</summary>
public class BusinessRuleException(string message) : Exception(message);

/// <summary>Pengguna tidak berhak atas data ini. Dipetakan ke HTTP 403.</summary>
public class ForbiddenException(string message) : Exception(message);

/// <summary>Login gagal atau sesi (refresh token) tidak berlaku. Dipetakan ke HTTP 401.</summary>
public class UnauthorizedException(string message) : Exception(message);
