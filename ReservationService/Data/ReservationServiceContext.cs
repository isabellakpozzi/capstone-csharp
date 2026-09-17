using Microsoft.EntityFrameworkCore;
using ReservationService.Models;

namespace ReservationService.Data;

public class ReservationServiceContext : DbContext
{
    public ReservationServiceContext(DbContextOptions<ReservationServiceContext> options)
        : base(options)
    {
    }

    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<Waitlist> WaitlistEntries => Set<Waitlist>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.Property(r => r.Status).HasConversion<string>();
            entity.Property(r => r.Condition).HasConversion<string>();

            entity.HasIndex(r => new { r.UserId, r.Status });
            entity.HasIndex(r => new { r.BookId, r.Status });
        });

        modelBuilder.Entity<Waitlist>(entity =>
        {
            entity.Property(w => w.Status).HasConversion<string>();

            entity.HasIndex(w => new { w.BookId, w.Status, w.JoinedAt });
            entity.HasIndex(w => new { w.UserId, w.Status });
        });
    }

    public override int SaveChanges()
    {
        ApplyAuditInfo();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditInfo();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyAuditInfo()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<Reservation>())
        {
            if (entry.State == EntityState.Added) { entry.Entity.CreatedAt = now; entry.Entity.UpdatedAt = now; }
            else if (entry.State == EntityState.Modified) { entry.Entity.UpdatedAt = now; }
        }

        foreach (var entry in ChangeTracker.Entries<Waitlist>())
        {
            if (entry.State == EntityState.Added) { entry.Entity.CreatedAt = now; entry.Entity.UpdatedAt = now; }
            else if (entry.State == EntityState.Modified) { entry.Entity.UpdatedAt = now; }
        }
    }
}