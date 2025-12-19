using BidEngine.Shared;
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

        // Explicitly map to lowercase table names
        modelBuilder.Entity<Campaign>().ToTable("campaigns");
        modelBuilder.Entity<Ad>().ToTable("ads");
        modelBuilder.Entity<TargetingRule>().ToTable("targeting_rules");

        modelBuilder.Entity<Campaign>(entity =>
        {
            entity.ToTable("campaigns");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.AdvertiserId).HasColumnName("advertiser_id");
            entity.Property(e => e.Status).HasColumnName("status").IsRequired();
            entity.Property(e => e.CpmBid).HasColumnName("cpm_bid").HasColumnType("numeric(10,4)");
            entity.Property(e => e.DailyBudget).HasColumnName("daily_budget").HasColumnType("numeric(12,2)");
            entity.Property(e => e.LifetimeBudget).HasColumnName("lifetime_budget").HasColumnType("numeric(12,2)");
            entity.Property(e => e.SpentToday).HasColumnName("spent_today").HasColumnType("numeric(12,2)");
            entity.Property(e => e.LifetimeSpent).HasColumnName("lifetime_spent").HasColumnType("numeric(12,2)");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });
        
        modelBuilder.Entity<Ad>(entity =>
        {
            entity.ToTable("ads");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CampaignId).HasColumnName("campaign_id");
            entity.Property(e => e.Title).HasColumnName("title").IsRequired();
            entity.Property(e => e.ImageUrl).HasColumnName("image_url").IsRequired();
            entity.Property(e => e.RedirectUrl).HasColumnName("redirect_url").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<TargetingRule>(entity =>
        {
            entity.ToTable("targeting_rules");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CampaignId).HasColumnName("campaign_id");
            entity.Property(e => e.RuleType).HasColumnName("rule_type").IsRequired();
            entity.Property(e => e.RuleValue).HasColumnName("rule_value").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });
    }
}