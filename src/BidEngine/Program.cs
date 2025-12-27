
using BidEngine.Data;
using BidEngine.Services;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using StackExchange.Redis;
using Npgsql; // Add this for NpgsqlDataSourceBuilder
using Pgvector.EntityFrameworkCore; // Add this for vector support

var builder = WebApplication.CreateBuilder(args);

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
    return ConnectionMultiplexer.Connect(redisConnectionString);
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
        await service.GenerateEmbeddingsForAllVideos();
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