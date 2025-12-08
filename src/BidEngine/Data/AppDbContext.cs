using BidEngine.Models;
using Microsoft.EntityFrameworkCore;

namespace BidEngine.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options ) : base(options)
    {}

    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<Ad> Ads => Set<Ad>();
    public DbSet<TargetingRule> TargetingRules => Set<TargetingRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Campaign>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasMany(e => e.Ads)
                .WithOne(e => e.Campaign)
                .HasForeignKey(e => e.CampaignId);

            entity.HasIndex(e => e.Status);
        }) 
    }


}