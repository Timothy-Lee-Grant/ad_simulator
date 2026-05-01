
using BidEngine.Data;
using BidEngine.Services;
using BidEngine.Services.Interfaces;
using BidEngine.Shared;
using BidEngine.Shared.DTOs;
using BidEngine.Validators;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
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
builder.Services.AddScoped<CampaignReadCacheService>();
builder.Services.AddScoped<VideoEmbeddingService>();
builder.Services.AddScoped<SemanticQueryService>();
builder.Services.AddScoped<BudgetService>();

// Register bidding strategies
builder.Services.AddScoped<HighestCpmStrategy>();
builder.Services.AddScoped<SemanticOnlyStrategy>();
builder.Services.AddScoped<HybridWeightedStrategy>();

// Configure bidding strategy options
builder.Services.Configure<BiddingStrategyOptions>(
    builder.Configuration.GetSection("BiddingStrategy")
);

// Register strategy factory
builder.Services.AddScoped<BiddingStrategyFactory>();

// Register the selected strategy via factory
builder.Services.AddScoped<IBiddingStrategy>(sp =>
{
    var factory = sp.GetRequiredService<BiddingStrategyFactory>();
    return factory.CreateStrategy();
});

// Register BidSelector with strategy injection
builder.Services.AddScoped<BidSelector>();

// Experimentation framework configuration and services
builder.Services.Configure<ExperimentOptions>(builder.Configuration.GetSection("Experiments"));
builder.Services.AddScoped<IExperimentConfigurationProvider, ExperimentConfigurationProvider>();
builder.Services.AddScoped<IExperimentAssignmentService, ExperimentAssignmentService>();
builder.Services.AddScoped<IExperimentService, ExperimentService>();
builder.Services.AddScoped<IExperimentEventLogger, ExperimentEventLogger>();
builder.Services.AddScoped<IExperimentContextAccessor, ExperimentContextAccessor>();

// Add authentication and authorization services
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// Configure JWT settings
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));

// Register authentication services
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<DatabaseInitializer>();

// Add JWT authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "Bearer";
    options.DefaultChallengeScheme = "Bearer";
})
.AddJwtBearer("Bearer", options =>
{
    var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>();
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings?.Issuer,
        ValidAudience = jwtSettings?.Audience,
        IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(jwtSettings?.SecretKey ?? throw new InvalidOperationException("JWT SecretKey not configured")))
    };
});

// Add authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("UserOrAdmin", policy => policy.RequireRole("User", "Admin"));
});

// Register validators manually
builder.Services.AddScoped<IValidator<LoginRequestDto>, LoginRequestValidator>();
builder.Services.AddScoped<IValidator<RegisterRequestDto>, RegisterRequestValidator>();
builder.Services.AddScoped<IValidator<ChangePasswordDto>, ChangePasswordValidator>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

//check Input arguments to See if we need to seed the data for vectorization of the ads and videos. 
if(args.Contains("--seed-vectors"))
{
    using (var scope = app.Services.CreateScope())
    {
        var service = scope.ServiceProvider.GetRequiredService<VideoEmbeddingService>();
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

    // Initialize database with roles and admin user
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync();
}

//configure the http request pipeline
if(app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Add authentication and authorization middleware
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

//prometheus metrics endpoint
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapMetrics();
});

app.Run();