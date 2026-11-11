using GLMS.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace GLMS.Web.Data;

public class GlmsDbContext : DbContext
{
    public GlmsDbContext(DbContextOptions<GlmsDbContext> options) : base(options)
    {
    }

    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Client>(e =>
        {
            e.HasIndex(c => c.Name);
        });

        modelBuilder.Entity<Contract>(e =>
        {
            e.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.ServiceLevel).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.StartDate).HasColumnType("date");
            e.Property(c => c.EndDate).HasColumnType("date");

            e.HasOne(c => c.Client)
                .WithMany(c => c.Contracts)
                .HasForeignKey(c => c.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes on the fields used by the search/filter screen (see Part 1, section 4.3).
            e.HasIndex(c => c.Status);
            e.HasIndex(c => c.EndDate);
            e.HasIndex(c => c.ClientId);
        });

        modelBuilder.Entity<ServiceRequest>(e =>
        {
            e.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(r => r.OriginalAmount).HasPrecision(18, 2);
            e.Property(r => r.ExchangeRate).HasPrecision(18, 6);
            e.Property(r => r.CostZar).HasPrecision(18, 2);

            e.HasOne(r => r.Contract)
                .WithMany(c => c.ServiceRequests)
                .HasForeignKey(r => r.ContractId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(r => r.ContractId);
            e.HasIndex(r => r.Status);
        });
    }
}
