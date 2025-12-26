
using BidEngine.Data;
using BidEngine.Services;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using StackExchange.Redis;
using Npgsql; // Add this for NpgsqlDataSourceBuilder
using Pgvector.EntityFrameworkCore; // Add this for vector support
using System.IO.Compression;

var builder = WebApplication.CreateBuilder(args);

// Configure embedding options from configuration/env
builder.Services.Configure<BidEngine.Services.EmbeddingOptions>(builder.Configuration.GetSection("Embeddings"));

//add services to the container
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

//get aws rds connection string
//Tim Grant - This works with AWS RDS (but will comment out to force local postgres container to be the one used.)
//var awsConnectionString = builder.Configuration.GetConnectionString("AwsConnection");


// 2. Create the Data Source with the "Secret Sauce"
// This teaches the low-level driver how to handle the vector type
//var dataSourceBuilder = new NpgsqlDataSourceBuilder(awsConnectionString);
var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.UseVector(); 
var dataSource = dataSourceBuilder.Build();

//add entity framework
// Ensure EF provider knows how to map the pgvector type by enabling UseVector()
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(dataSource, npgsqlOptions => npgsqlOptions.UseVector())
);


//add redis
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var redisConnectionString = builder.Configuration["Redis__ConnectionString"] ?? "redis:6379";
    // Make the multiplexer tolerant to transient startup failures so the
    // application can continue (useful for one-off operations like seeding).
    var opts = StackExchange.Redis.ConfigurationOptions.Parse(redisConnectionString);
    opts.AbortOnConnectFail = false; // don't throw if Redis isn't immediately available
    opts.ConnectRetry = 5;
    return ConnectionMultiplexer.Connect(opts);
});



//add custom services
builder.Services.AddScoped<CampaignCache>();
builder.Services.AddScoped<BidSelector>();
builder.Services.AddScoped<BudgetService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

//check Input arguments to See if we need to seed the data for vectorization of the ads and videos. 
if(args.Contains("--seed-vectors"))
{
    using (var scope = app.Services.CreateScope())
    {
        var service = scope.ServiceProvider.GetRequiredService<CampaignCache>();
        try
        {
            // If a model URL is provided via config or env, try to download it into /app/model
            var modelUrl = builder.Configuration["Embeddings:ModelUrl"] ?? Environment.GetEnvironmentVariable("MODEL_URL");
            var modelDir = Path.Combine(AppContext.BaseDirectory, "model");
            if (!string.IsNullOrEmpty(modelUrl) && (!Directory.Exists(modelDir) || !Directory.EnumerateFileSystemEntries(modelDir).Any()))
            {
                Console.WriteLine($"Downloading embedder model from {modelUrl} to {modelDir}...");
                try
                {
                    Directory.CreateDirectory(modelDir);
                    using var http = new System.Net.Http.HttpClient();
                    using var resp = await http.GetAsync(modelUrl);
                    resp.EnsureSuccessStatusCode();
                    var contentType = resp.Content.Headers.ContentType?.MediaType ?? string.Empty;
                    var tmp = Path.GetTempFileName();
                    await using (var fs = File.OpenWrite(tmp))
                    {
                        await resp.Content.CopyToAsync(fs);
                    }
                    // If it's a zip, extract; otherwise attempt to move files into model dir.
                    if (tmp != null && File.Exists(tmp))
                    {
                        try
                        {
                            ZipFile.ExtractToDirectory(tmp, modelDir);
                        }
                        catch
                        {
                            // Not a zip or extraction failed; try moving the file as-is into model dir
                            var dest = Path.Combine(modelDir, Path.GetFileName(modelUrl));
                            File.Move(tmp, dest, overwrite: true);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Failed to download model from {modelUrl}: {ex.Message}");
                }
            }
            // Insert sample data points for local testing (only when tables are empty)
            await service.SeedSampleDataAsync();

            await service.GenerateEmbeddingsForAllVideos();
            Console.WriteLine("Seed vectors generation completed successfully.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error while generating embeddings: {ex}");
            throw;
        }
        //await service.GenerateEmbeddingsForAllAds();
        return;
    }
}

// Database migration (optional - can also use SQL scripts)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate(); // Uncomment if using EF Core migrations
}

//configure the http request pipeline
if(app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//prometheus metrics endpoint
app.UseRouting();
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapMetrics();
});

app.Run();