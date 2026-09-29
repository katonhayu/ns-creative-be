using NsCreative.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace NsCreative.Core.Common;

/// <summary>
/// Akses database untuk service di Core. Diimplementasikan AppDbContext di project
/// Data, sehingga Core tidak perlu mereferensikan project lain.
/// </summary>
public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Client> Clients { get; }
    DbSet<Talent> Talents { get; }
    DbSet<Crew> Crews { get; }
    DbSet<Vendor> Vendors { get; }
    DbSet<Event> Events { get; }
    DbSet<EventTalent> EventTalents { get; }
    DbSet<EventCrew> EventCrews { get; }
    DbSet<EventVendor> EventVendors { get; }
    DbSet<Schedule> Schedules { get; }
    DbSet<TaskItem> Tasks { get; }
    DbSet<Budget> Budgets { get; }
    DbSet<Document> Documents { get; }
    DbSet<Report> Reports { get; }
    DbSet<RefreshToken> RefreshTokens { get; }

    /// <summary>Dipakai endpoint transaksional (⚡) untuk <c>BeginTransactionAsync()</c>.</summary>
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
