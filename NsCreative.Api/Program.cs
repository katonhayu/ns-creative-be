using System.Text.Json;
using System.Text.Json.Serialization;
using NsCreative.Api.Auth;
using NsCreative.Api.Infrastruktur;
using NsCreative.Api.Json;
using NsCreative.Api.Middleware;
using NsCreative.Api.ModelBinding;
using NsCreative.Api.Storage;
using NsCreative.Core.Common;
using NsCreative.Core.Entities;
using NsCreative.Core.Services;
using NsCreative.Core.Validators.Clients;
using NsCreative.Data;
using NsCreative.Data.Seed;
using FluentValidation;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

const string CorsFrontend = "Frontend";

var builder = WebApplication.CreateBuilder(args);

// --- Logging ----------------------------------------------------------------
// Development: teks biasa agar mudah dibaca. Lainnya: JSON satu baris per log, dengan scope
// (TraceId) ikut tercatat, supaya bisa dikumpulkan dan difilter per field.
if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(o =>
    {
        o.IncludeScopes = true;
        o.UseUtcTimestamp = true;
        o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    });
}

// --- Database ---------------------------------------------------------------
// Development: appsettings.json. Production: environment variable ConnectionStrings__Default.
var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException(
        "Connection string 'Default' kosong. Isi di appsettings.json (development) " +
        "atau environment variable ConnectionStrings__Default (production).");

builder.Services.AddDbContext<AppDbContext>(options => options
    .UseNpgsql(connectionString)
    .UseSnakeCaseNamingConvention());
builder.Services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

// --- Auth -------------------------------------------------------------------
// Kunci JWT development di appsettings.json; production wajib lewat environment variable Jwt__Key.
var jwt = builder.Configuration.GetSection(JwtOptions.Bagian).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Bagian Jwt belum diisi di appsettings.json.");
jwt.Validasi();
builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton<IPembuatToken, PembuatTokenJwt>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IPenggunaSaatIni, PenggunaSaatIni>();
builder.Services.AddAuthNsCreative(jwt);

// --- Pengetatan: rate limit & health check -----------------------------------
builder.Services.AddRateLimitNsCreative(builder.Configuration);
builder.Services.AddHealthNsCreative();

// Di belakang reverse proxy (nginx/IIS), IP asli dan skema https dibaca dari header X-Forwarded-*.
// Hanya aktif jika dikonfigurasi, karena header ini bisa dipalsukan bila API diakses langsung.
var diBelakangProxy = builder.Configuration.GetValue<bool>("ReverseProxy:Aktif");
if (diBelakangProxy)
{
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        o.KnownNetworks.Clear();
        o.KnownProxies.Clear();
    });
}

// --- Service & validasi -------------------------------------------------------
builder.Services.AddValidatorsFromAssemblyContaining<CreateClientDtoValidator>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAksesEvent, AksesEvent>();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<ITalentService, TalentService>();
builder.Services.AddScoped<IVendorService, VendorService>();
builder.Services.AddScoped<ICrewService, CrewService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IEventTalentService, EventTalentService>();
builder.Services.AddScoped<IEventCrewService, EventCrewService>();
builder.Services.AddScoped<IEventVendorService, EventVendorService>();
builder.Services.AddScoped<IScheduleService, ScheduleService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IBudgetService, BudgetService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddSingleton<IPenyimpananFile, PenyimpananFileLokal>();

// --- API --------------------------------------------------------------------
builder.Services
    .AddControllers(options =>
    {
        // Wajib-tidaknya field ditentukan validator FluentValidation (pesan bahasa Indonesia),
        // bukan dari tipe non-nullable yang otomatis dianggap [Required] oleh MVC.
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
        options.ModelBinderProviders.Insert(0, new SnakeCaseEnumBinderProvider());
    })
    .AddJsonOptions(options =>
    {
        // Enum dikirim sebagai string snake_case (belum_bayar, dikonfirmasi), sama dengan nilai di database.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        options.JsonSerializerOptions.Converters.Add(new TimeOnlyJsonConverter());
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        // Body yang tidak bisa dibaca (JSON rusak, tipe salah) memakai format error yang sama
        // dengan kegagalan FluentValidation, dengan judul berbahasa Indonesia.
        options.InvalidModelStateResponseFactory = context =>
        {
            var problem = new ValidationProblemDetails(context.ModelState)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Data yang dikirim tidak valid.",
                Detail = "Format data tidak dapat dibaca. Periksa kembali isian yang dikirim.",
                Instance = context.HttpContext.Request.Path,
            };
            problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
            return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
        };
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Tipe TypeScript frontend di-generate dari dokumen ini (openapi-typescript), jadi
    // nullability harus akurat: string? → nullable, selain itu required.
    options.SupportNonNullableReferenceTypes();
    // Property bertipe DTO lain yang nullable (misalnya laporan: null) ikut ditandai nullable.
    options.UseAllOfToExtendReferenceSchemas();
    options.SchemaFilter<WajibSchemaFilter>();

    // Tombol Authorize di Swagger UI: tempel accessToken dari POST /api/auth/login.
    var skema = new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Tempel accessToken dari POST /api/auth/login (tanpa kata \"Bearer\").",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
    };
    options.AddSecurityDefinition("Bearer", skema);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [skema] = [] });
});

// Origin frontend dari konfigurasi Cors:Origins (development: http://localhost:5173).
var originFrontend = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(CorsFrontend, policy => policy
    .WithOrigins(originFrontend)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders("Content-Disposition", "Retry-After")));

var app = builder.Build();

// --- Pipeline ---------------------------------------------------------------
if (diBelakangProxy) app.UseForwardedHeaders();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    await SeedDevelopmentAsync(app);
}
else
{
    // Saat development frontend memanggil lewat http, jadi https hanya dipaksa di production.
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors(CorsFrontend);
app.UseAuthentication();
// Setelah autentikasi, supaya batas request dihitung per pengguna login.
app.UseRateLimiter();
app.UseAuthorization();
app.MapHealthNsCreative();
app.MapControllers();

app.Run();

// Seeder hanya dijalankan jika semua migration sudah diterapkan (lewat `dotnet ef database update`),
// supaya aplikasi tidak diam-diam mengubah skema database.
static async Task SeedDevelopmentAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
    if (pending.Count > 0)
    {
        app.Logger.LogWarning(
            "Seeder dilewati: {Jumlah} migration belum diterapkan ({Daftar}). Jalankan `dotnet ef database update`.",
            pending.Count, string.Join(", ", pending));
        return;
    }
    await DbSeeder.SeedAsync(db);
}
