using AssistantMoneyger.Api.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AssistantMoneyger.Api.Data;

// IdentityDbContext already gives us AspNetUsers, AspNetRoles, etc. —
// we only need to add our own four tables on top and configure their
// composite keys.
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>(options)
{
    public DbSet<Prediction> Predictions => Set<Prediction>();
    public DbSet<Result> Results => Set<Result>();
    public DbSet<ModelPerformanceLogEntry> ModelPerformanceLog => Set<ModelPerformanceLogEntry>();
    public DbSet<PredictionRun> PredictionRuns => Set<PredictionRun>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Prediction>().HasKey(p => new { p.Gw, p.PlayerId });
        builder.Entity<Result>().HasKey(r => new { r.Gw, r.PlayerId });
        builder.Entity<ModelPerformanceLogEntry>().HasKey(m => m.Gw);
        builder.Entity<PredictionRun>().HasKey(r => r.Gw);
    }
}
