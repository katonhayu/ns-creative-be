using NsCreative.Core.Dtos.Dashboard;
using NsCreative.Core.Services;
using NsCreative.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NsCreative.Api.Controllers;

[ApiController]
[Authorize(Roles = Peran.Semua)]
[Route("api/dashboard")]
[Produces("application/json")]
public class DashboardController(IDashboardService dashboard) : ControllerBase
{
    /// <summary>Ringkasan halaman depan: event berlangsung dan terdekat, task mendekati deadline, vendor belum lunas, statistik tahun ini.</summary>
    /// <remarks>Task masuk jika deadline dalam 7 hari ke depan atau sudah lewat. Tanggal "hari ini" mengikuti WIB.</remarks>
    [HttpGet]
    [ProducesResponseType<DashboardDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardDto>> Get(CancellationToken ct) =>
        Ok(await dashboard.GetAsync(ct));
}
