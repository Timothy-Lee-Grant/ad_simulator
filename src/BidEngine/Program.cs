
using BidEngine.Data;
using BidEngine.Services;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

//add services to the container
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

//add entity framework
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));


//add redis
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "redis:6379";
var redis = ConnectionMultiplexer.Connect(redisConnectionString);
builder.Services.AddSingleton(redis);

//add custom services
builder.Services.AddScoped<CampaignCache>();
builder.Services.AddScoped<BidSelector>();
builder.Services.AddScoped<BudgetService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Database migration (optional - can also use SQL scripts)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // db.Database.Migrate(); // Uncomment if using EF Core migrations
}

//configure the http request pipeline
if(app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//prometheus metrics endpoint
app.UseRouting();
app.useEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapMetrics();
});

app.Run();