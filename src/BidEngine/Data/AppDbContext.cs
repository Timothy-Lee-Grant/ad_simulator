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
            entity.Property(e=>e.Name).IsRequired();
            entity.Property(e=>e.Status).IsRequired();
            entity.Property(e=>e.CpmBid).HasColumnType("numeric(10,4)");
            entity.Property(e=>e.DailyBudget).HasColumnType("numeric(12,2)");
            entity.Property(e=>e.SpentToday).HasColumnType("numeric(12,2)");

            entity.HasMany(e => e.Ads)
                .WithOne(e => e.Campaign)
                .HasForeignKey(e => e.CampaignId);

            entity.HasMany(e => e.TargetingRules)
                .WithOne(e => e.Campaign)
                .HasForeignKey(e => e.CampaignId);

            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.AdvertiserId);
        }); 

        modelBuilder.Entity<Ad>(entity =>
        {
            entity.HasKey(e => e.Id);
            //entity.Property(e=>e.CampainId).IsRequired(); //why is this not required? because in the database schema it is listed as required, but the example doesn't have it as reqired??
            entity.Property(e=>e.Title).IsRequired();
            entity.Property(e=>e.RedirectUrl).IsRequired();
            entity.Property(e=>e.ImageUrl).IsRequired();

            entity.HasIndex(e=>e.CampaignId);
        });

        modelBuilder.Entity<TargetingRules>(entity=>
        {
            entity.HasKey(e=>e.Id);
            //entity.Property(e=>e.CampaignId).IsRequired();
            entity.Property(e=>e.RuleType).IsRequired();
            entity.Property(e=>e.RuleValue).IsRequired();

            entity.HasIndex(e=>e.CampaignId);
            entity.HasIndex(e => new { e.
            CampaignId, e.RuleType, e.RuleValue}).
            IsUnique();
        });
    }


}