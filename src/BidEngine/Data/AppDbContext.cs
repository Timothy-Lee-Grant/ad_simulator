using BidEngine.Shared;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using Npgsql;

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
        // Note: pgvector extension is optional for now. We persist embeddings as jsonb
        // and will enable pgvector (and call HasPostgresExtension("vector")) when the
        // Postgres instance supports the extension in the environment.

        modelBuilder.HasPostgresExtension("vector");
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
            entity.Property(e => e.Description).HasColumnName("description");

            // Map Embedding differently depending on the provider.
            var embeddingProp = entity.Property(e => e.Embedding).HasColumnName("embedding");

            // When using Npgsql with pgvector enabled we want the native vector column
            // Otherwise (tests using InMemory or other providers) use a ValueConverter to
            // store the vector as a float[] (or JSON) so model validation succeeds.
            if (Database.IsNpgsql())
            {
                embeddingProp.HasColumnType("vector(384)");
            }
            else
            {
                // Some providers (eg. InMemory used in unit tests) don't understand
                // Pgvector.Vector or database-specific vector/array types. To avoid
                // EF attempting to compose multiple converters we serialize the
                // vector to JSON (string) at the provider layer and store it as
                // JSON. This keeps the CLR type as Pgvector.Vector while preserving
                // value equality semantics for comparisons in tests.
                var vectorToJsonConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<Pgvector.Vector?, string?>(
                    v => v == null ? null : System.Text.Json.JsonSerializer.Serialize<float[]>(v.ToArray(), (System.Text.Json.JsonSerializerOptions?)null),
                    s => string.IsNullOrEmpty(s) ? null : new Pgvector.Vector(System.Text.Json.JsonSerializer.Deserialize<float[]>(s, (System.Text.Json.JsonSerializerOptions?)null)!)
                );

                var vectorComparer = new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<Pgvector.Vector?>(
                    (a, b) => (a == null && b == null) || (a != null && b != null && System.Linq.Enumerable.SequenceEqual(a.ToArray(), b.ToArray())),
                    a => a == null ? 0 : a.ToArray().Aggregate(0, (hash, val) => HashCode.Combine(hash, val.GetHashCode())),
                    a => a == null ? null : new Pgvector.Vector(a.ToArray())
                );

                embeddingProp.HasConversion(vectorToJsonConverter).HasColumnType("jsonb").Metadata.SetValueComparer(vectorComparer);
            }
            /*
            // Persist embeddings as JSON (jsonb) for compatibility across dev environments.
            // We use a ValueConverter to serialize float[] into JSON for storage.
            var floatArrayToJsonConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<float[]?, string?>(
                v => v == null ? null : System.Text.Json.JsonSerializer.Serialize<float[]>(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => string.IsNullOrEmpty(v) ? null : System.Text.Json.JsonSerializer.Deserialize<float[]>(v, (System.Text.Json.JsonSerializerOptions?)null)
            );

            var floatArrayComparer = new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<float[]?>(
                (a, b) => (a == null && b == null) || (a != null && b != null && System.Linq.Enumerable.SequenceEqual(a, b)),
                a => a == null ? 0 : a.Aggregate(0, (hash, val) => HashCode.Combine(hash, val.GetHashCode())),
                a => a == null ? null : a.ToArray()
            );

            var embeddingProp = entity.Property(e => e.Embedding)
                .HasColumnName("embedding")
                .HasConversion(floatArrayToJsonConverter)
                .HasColumnType("jsonb");

            // Ensure EF compares the array contents rather than reference equality.
            embeddingProp.Metadata.SetValueComparer(floatArrayComparer);
            */
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

        modelBuilder.Entity<Video>(entity =>
        {
            entity.ToTable("videos");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Title).HasColumnName("title");
            entity.Property(e => e.Description).HasColumnName("description");
            var videoEmbeddingProp = entity.Property(e => e.Embedding).HasColumnName("embedding");
            if (Database.IsNpgsql())
            {
                videoEmbeddingProp.HasColumnType("vector(384)");
            }
            else
            {
                // Mirror the Ad conversion for test-friendly storage (serialize to JSON)
                var vectorToJsonConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<Pgvector.Vector?, string?>(
                    v => v == null ? null : System.Text.Json.JsonSerializer.Serialize<float[]>(v.ToArray(), (System.Text.Json.JsonSerializerOptions?)null),
                    s => string.IsNullOrEmpty(s) ? null : new Pgvector.Vector(System.Text.Json.JsonSerializer.Deserialize<float[]>(s, (System.Text.Json.JsonSerializerOptions?)null)!)
                );

                var vectorComparer = new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<Pgvector.Vector?>(
                    (a, b) => (a == null && b == null) || (a != null && b != null && System.Linq.Enumerable.SequenceEqual(a.ToArray(), b.ToArray())),
                    a => a == null ? 0 : a.ToArray().Aggregate(0, (hash, val) => HashCode.Combine(hash, val.GetHashCode())),
                    a => a == null ? null : new Pgvector.Vector(a.ToArray())
                );

                videoEmbeddingProp.HasConversion(vectorToJsonConverter).HasColumnType("jsonb").Metadata.SetValueComparer(vectorComparer);
            }
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });
    }
}