# Authenticated Admin API + Campaign Management Implementation Guide

(Note to self, this is the prompt that I used to get this output. In the past I had a lot of problems getting the llm to acutally create a detailed implementation guide, but this one seems really complete.
"Create a full and extremely detailed implementation guide of how to complete the '1. **Authenticated Admin API + campaign management**' in #file:ROADMAP.md , put the implementation guide inside of #file:Authenticated_Admin_API.md ")

## Overview

This implementation guide provides a comprehensive, step-by-step approach to adding JWT-based authentication and role-based authorization to the BidEngine, along with full CRUD operations for campaign management. This feature transforms the BidEngine from a read-only bidding service into a production-ready admin platform with proper security, validation, and audit trails.

## Prerequisites

- .NET 9 / ASP.NET Core
- PostgreSQL with pgvector extension
- Redis for caching
- Existing BidEngine codebase with Campaign, Ad, and TargetingRule models

## Implementation Phases

### Phase 1: Authentication Infrastructure

#### Step 1.1: Add Required NuGet Packages

Add the following packages to `src/BidEngine/BidEngine.csproj`:

```xml
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="9.0.0" />
<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="9.0.0" />
<PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.0.1" />
<PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />
<PackageReference Include="FluentValidation.AspNetCore" Version="11.3.0" />
```

#### Step 1.2: Create User and Role Models

Create `src/Shared/User.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace BidEngine.Shared;

public class User : IdentityUser<Guid>
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public override string UserName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public override string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string LastName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    public string GetFullName() => $"{FirstName} {LastName}".Trim();
}

public class Role : IdentityRole<Guid>
{
    public string Description { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Custom claim types
public static class CustomClaims
{
    public const string FullName = "full_name";
    public const string IsActive = "is_active";
}
```

#### Step 1.3: Create Authentication DTOs

Create `src/Shared/AuthDtos.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using FluentValidation;

namespace BidEngine.Shared;

public class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserDto User { get; set; } = null!;
}

public class RegisterRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [Compare("Password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class UserDto
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public List<string> Roles { get; set; } = new();
}

public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [Compare("NewPassword")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

// Validators
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(6).WithMessage("Password must be at least 6 characters");
    }
}

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required")
            .MinimumLength(2).WithMessage("First name must be at least 2 characters")
            .MaximumLength(100).WithMessage("First name cannot exceed 100 characters");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required")
            .MinimumLength(1).WithMessage("Last name must be at least 1 character")
            .MaximumLength(100).WithMessage("Last name cannot exceed 100 characters");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one number");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("Passwords do not match");
    }
}
```

#### Step 1.4: Create JWT Configuration Options

Create `src/BidEngine/Services/JwtOptions.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace BidEngine.Services;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 32)]
    public string SecretKey { get; set; } = string.Empty;

    [Range(1, 525600)] // Max 1 year in minutes
    public int ExpirationMinutes { get; set; } = 60; // 1 hour default

    [Range(1, 525600)]
    public int RefreshTokenExpirationMinutes { get; set; } = 10080; // 7 days default
}
```

#### Step 1.5: Create Authentication Service

Create `src/BidEngine/Services/AuthService.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BidEngine.Shared;
using BidEngine.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BidEngine.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request);
    Task<UserDto> RegisterAsync(RegisterRequest request);
    Task<UserDto> GetCurrentUserAsync(Guid userId);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
    Task<IEnumerable<UserDto>> GetAllUsersAsync();
    Task<bool> DeactivateUserAsync(Guid userId);
    Task<bool> ActivateUserAsync(Guid userId);
}

public class AuthService : IAuthService
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<Role> _roleManager;
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<User> userManager,
        RoleManager<Role> roleManager,
        IOptions<JwtOptions> jwtOptions,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _jwtOptions = jwtOptions.Value;
        _logger = logger;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !user.IsActive)
        {
            throw new UnauthorizedAccessException("Invalid credentials or account is deactivated");
        }

        var isValidPassword = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!isValidPassword)
        {
            _logger.LogWarning("Failed login attempt for user {Email}", request.Email);
            throw new UnauthorizedAccessException("Invalid credentials");
        }

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var token = await GenerateJwtTokenAsync(user);
        var userDto = await MapToUserDtoAsync(user);

        return new LoginResponse
        {
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpirationMinutes),
            User = userDto
        };
    }

    public async Task<UserDto> RegisterAsync(RegisterRequest request)
    {
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            throw new InvalidOperationException("User with this email already exists");
        }

        var user = new User
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            EmailConfirmed = true // For demo purposes - in production, require email confirmation
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create user: {errors}");
        }

        // Assign default role
        await _userManager.AddToRoleAsync(user, "User");

        _logger.LogInformation("New user registered: {Email}", request.Email);

        return await MapToUserDtoAsync(user);
    }

    public async Task<UserDto> GetCurrentUserAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        return await MapToUserDtoAsync(user);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to change password: {errors}");
        }

        _logger.LogInformation("Password changed for user {UserId}", userId);
    }

    public async Task<IEnumerable<UserDto>> GetAllUsersAsync()
    {
        var users = _userManager.Users.OrderBy(u => u.CreatedAt);
        var userDtos = new List<UserDto>();

        foreach (var user in users)
        {
            userDtos.Add(await MapToUserDtoAsync(user));
        }

        return userDtos;
    }

    public async Task<bool> DeactivateUserAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return false;
        }

        user.IsActive = false;
        var result = await _userManager.UpdateAsync(user);

        if (result.Succeeded)
        {
            _logger.LogInformation("User {UserId} deactivated", userId);
            return true;
        }

        return false;
    }

    public async Task<bool> ActivateUserAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return false;
        }

        user.IsActive = true;
        var result = await _userManager.UpdateAsync(user);

        if (result.Succeeded)
        {
            _logger.LogInformation("User {UserId} activated", userId);
            return true;
        }

        return false;
    }

    private async Task<string> GenerateJwtTokenAsync(User user)
    {
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email!),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(CustomClaims.FullName, user.GetFullName()),
            new Claim(CustomClaims.IsActive, user.IsActive.ToString())
        };

        // Add roles as claims
        var roles = await _userManager.GetRolesAsync(user);
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtOptions.ExpirationMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<UserDto> MapToUserDtoAsync(User user)
    {
        var roles = await _userManager.GetRolesAsync(user);

        return new UserDto
        {
            Id = user.Id,
            UserName = user.UserName!,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
            Roles = roles.ToList()
        };
    }
}
```

#### Step 1.6: Update AppDbContext for Identity

Update `src/BidEngine/Data/AppDbContext.cs`:

```csharp
using BidEngine.Shared;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using Npgsql;

namespace BidEngine.Data;

public class AppDbContext : IdentityDbContext<User, Role, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {}

    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<Ad> Ads => Set<Ad>();
    public DbSet<TargetingRule> TargetingRules => Set<TargetingRule>();
    public DbSet<Video> Videos => Set<Video>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // Required for Identity

        // Note: pgvector extension is optional for now. We persist embeddings as jsonb
        // and will enable pgvector (and call HasPostgresExtension("vector")) when the
        // Postgres instance supports the extension in the environment.

        modelBuilder.HasPostgresExtension("vector");

        // Explicitly map to lowercase table names
        modelBuilder.Entity<Campaign>().ToTable("campaigns");
        modelBuilder.Entity<Ad>().ToTable("ads");
        modelBuilder.Entity<TargetingRule>().ToTable("targeting_rules");
        modelBuilder.Entity<Video>().ToTable("videos");
        modelBuilder.Entity<AuditEvent>().ToTable("audit_events");

        // Identity table names
        modelBuilder.Entity<User>().ToTable("users");
        modelBuilder.Entity<Role>().ToTable("roles");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");

        // Campaign configuration
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

            // Relationships
            entity.HasMany(e => e.Ads)
                .WithOne(e => e.Campaign)
                .HasForeignKey("campaign_id")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.TargetingRules)
                .WithOne(e => e.Campaign)
                .HasForeignKey("campaign_id")
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Ad configuration
        modelBuilder.Entity<Ad>(entity =>
        {
            entity.ToTable("ads");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CampaignId).HasColumnName("campaign_id");
            entity.Property(e => e.Title).HasColumnName("title").IsRequired();
            entity.Property(e => e.ImageUrl).HasColumnName("image_url").IsRequired();
            entity.Property(e => e.RedirectUrl).HasColumnName("redirect_url").IsRequired();
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            // Vector embedding
            entity.Property(e => e.Embedding)
                .HasColumnName("embedding")
                .HasColumnType("vector(384)");
        });

        // Targeting rule configuration
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

        // Video configuration
        modelBuilder.Entity<Video>(entity =>
        {
            entity.ToTable("videos");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Title).HasColumnName("title").IsRequired();
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            // Vector embedding
            entity.Property(e => e.Embedding)
                .HasColumnName("embedding")
                .HasColumnType("vector(384)");
        });

        // Audit event configuration
        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.ToTable("audit_events");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Action).HasColumnName("action").IsRequired();
            entity.Property(e => e.EntityType).HasColumnName("entity_type").IsRequired();
            entity.Property(e => e.EntityId).HasColumnName("entity_id");
            entity.Property(e => e.OldValues).HasColumnName("old_values");
            entity.Property(e => e.NewValues).HasColumnName("new_values");
            entity.Property(e => e.Timestamp).HasColumnName("timestamp");
            entity.Property(e => e.IpAddress).HasColumnName("ip_address");
            entity.Property(e => e.UserAgent).HasColumnName("user_agent");
        });
    }
}
```

#### Step 1.7: Create Audit Event Model

Create `src/Shared/AuditEvent.cs`:

```csharp
using System.Text.Json;

namespace BidEngine.Shared;

public class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty; // CREATE, UPDATE, DELETE, LOGIN, etc.
    public string EntityType { get; set; } = string.Empty; // Campaign, Ad, TargetingRule, User, etc.
    public Guid? EntityId { get; set; }
    public string? OldValues { get; set; } // JSON serialized old state
    public string? NewValues { get; set; } // JSON serialized new state
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    // Helper methods for serialization
    public void SetOldValues<T>(T oldObject)
    {
        OldValues = oldObject != null ? JsonSerializer.Serialize(oldObject) : null;
    }

    public void SetNewValues<T>(T newObject)
    {
        NewValues = JsonSerializer.Serialize(newObject);
    }

    public T? GetOldValues<T>()
    {
        return OldValues != null ? JsonSerializer.Deserialize<T>(OldValues) : default;
    }

    public T? GetNewValues<T>()
    {
        return NewValues != null ? JsonSerializer.Deserialize<T>(NewValues) : default;
    }
}
```

#### Step 1.8: Create Audit Service

Create `src/BidEngine/Services/AuditService.cs`:

```csharp
using BidEngine.Shared;
using BidEngine.Data;

namespace BidEngine.Services;

public interface IAuditService
{
    Task LogAsync(AuditEvent auditEvent);
    Task LogAsync(string action, string entityType, Guid? entityId = null, object? oldValues = null, object? newValues = null);
    Task<IEnumerable<AuditEvent>> GetAuditTrailAsync(string entityType, Guid entityId, int page = 1, int pageSize = 50);
    Task<IEnumerable<AuditEvent>> GetUserAuditTrailAsync(Guid userId, int page = 1, int pageSize = 50);
}

public class AuditService : IAuditService
{
    private readonly AppDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditService> _logger;

    public AuditService(
        AppDbContext context,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditService> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task LogAsync(AuditEvent auditEvent)
    {
        // Enrich with request context
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            auditEvent.IpAddress = GetClientIpAddress(httpContext);
            auditEvent.UserAgent = httpContext.Request.Headers["User-Agent"].ToString();

            // If UserId not set, try to get from claims
            if (!auditEvent.UserId.HasValue)
            {
                var userIdClaim = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
                {
                    auditEvent.UserId = userId;
                }
            }
        }

        _context.AuditEvents.Add(auditEvent);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Audit event logged: {Action} on {EntityType} by user {UserId}",
            auditEvent.Action, auditEvent.EntityType, auditEvent.UserId);
    }

    public async Task LogAsync(string action, string entityType, Guid? entityId = null, object? oldValues = null, object? newValues = null)
    {
        var auditEvent = new AuditEvent
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId
        };

        if (oldValues != null)
        {
            auditEvent.SetOldValues(oldValues);
        }

        if (newValues != null)
        {
            auditEvent.SetNewValues(newValues);
        }

        await LogAsync(auditEvent);
    }

    public async Task<IEnumerable<AuditEvent>> GetAuditTrailAsync(string entityType, Guid entityId, int page = 1, int pageSize = 50)
    {
        return await _context.AuditEvents
            .Where(e => e.EntityType == entityType && e.EntityId == entityId)
            .OrderByDescending(e => e.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<IEnumerable<AuditEvent>> GetUserAuditTrailAsync(Guid userId, int page = 1, int pageSize = 50)
    {
        return await _context.AuditEvents
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    private string? GetClientIpAddress(HttpContext context)
    {
        // Check for forwarded headers first (common in proxy/load balancer scenarios)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',').First().Trim();
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        // Fall back to connection remote IP
        return context.Connection.RemoteIpAddress?.ToString();
    }
}
```

#### Step 1.9: Update Program.cs for Authentication

Update `src/BidEngine/Program.cs`:

```csharp
using BidEngine.Data;
using BidEngine.Services;
using BidEngine.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Prometheus;
using StackExchange.Redis;
using System.Text;

// ... existing code ...

// Add Identity services
builder.Services.AddIdentity<User, Role>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;

    options.User.RequireUniqueEmail = true;
    options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";

    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// Configure JWT
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

var jwtSettings = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>();
if (jwtSettings == null)
{
    throw new InvalidOperationException("JWT settings are not configured");
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            // Log authentication failures
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogWarning("JWT authentication failed: {Message}", context.Exception.Message);
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            // Check if user is active
            var claimsIdentity = (System.Security.Claims.ClaimsIdentity)context.Principal!.Identity!;
            var isActiveClaim = claimsIdentity.FindFirst(CustomClaims.IsActive);

            if (isActiveClaim == null || !bool.TryParse(isActiveClaim.Value, out var isActive) || !isActive)
            {
                context.Fail("User account is deactivated");
            }

            return Task.CompletedTask;
        }
    };
});

// Add authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("UserOrAdmin", policy => policy.RequireRole("User", "Admin"));
    options.AddPolicy("RequireActiveUser", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim(CustomClaims.IsActive, "True");
    });
});

// Add HttpContext accessor for audit logging
builder.Services.AddHttpContextAccessor();

// Add FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();

// ... existing services ...

// Register auth and audit services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAuditService, AuditService>();

// ... existing code ...

var app = builder.Build();

// ... existing code ...

// Add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// ... existing code ...

// Seed initial admin user and roles
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await SeedDataAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database");
    }
}

// ... existing code ...

static async Task SeedDataAsync(IServiceProvider services)
{
    var userManager = services.GetRequiredService<UserManager<User>>();
    var roleManager = services.GetRequiredService<RoleManager<Role>>();
    var logger = services.GetRequiredService<ILogger<Program>>();

    // Create roles
    var roles = new[] { "Admin", "User" };
    foreach (var roleName in roles)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            var role = new Role
            {
                Name = roleName,
                Description = roleName == "Admin" ? "Full system access" : "Standard user access"
            };
            await roleManager.CreateAsync(role);
            logger.LogInformation("Created role: {Role}", roleName);
        }
    }

    // Create default admin user
    var adminEmail = "admin@bidengine.local";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    if (adminUser == null)
    {
        adminUser = new User
        {
            UserName = adminEmail,
            Email = adminEmail,
            FirstName = "System",
            LastName = "Administrator",
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(adminUser, "Admin123!");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
            logger.LogInformation("Created default admin user: {Email}", adminEmail);
        }
        else
        {
            logger.LogError("Failed to create admin user: {Errors}",
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
}
```

#### Step 1.10: Update Configuration

Update `src/BidEngine/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=postgres;Port=5432;Database=ads_db;User Id=postgres;Password=postgres"
  },
  "Redis": {
    "ConnectionString": "redis:6379"
  },
  "Jwt": {
    "Issuer": "BidEngine",
    "Audience": "BidEngineUsers",
    "SecretKey": "your-super-secret-jwt-key-that-is-at-least-32-characters-long",
    "ExpirationMinutes": 60,
    "RefreshTokenExpirationMinutes": 10080
  },
  "BiddingStrategy": {
    "Strategy": "HighestCpm"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

**Security Note:** In production, move the JWT secret to environment variables or Azure Key Vault.

#### Step 1.11: Create Authentication Controller

Create `src/BidEngine/Controllers/AuthController.cs`:

```csharp
using BidEngine.Shared;
using BidEngine.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BidEngine.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IAuditService _auditService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        IAuditService auditService,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/auth/login
    /// Authenticates a user and returns a JWT token
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            var response = await _authService.LoginAsync(request);

            // Audit successful login
            await _auditService.LogAsync("LOGIN", "User", response.User.Id);

            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            // Audit failed login attempt
            await _auditService.LogAsync("LOGIN_FAILED", "User", null, new { Email = request.Email });

            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed for {Email}", request.Email);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// POST /api/auth/register
    /// Registers a new user account
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<UserDto>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var user = await _authService.RegisterAsync(request);

            // Audit user registration
            await _auditService.LogAsync("REGISTER", "User", user.Id, null, user);

            return CreatedAtAction(nameof(GetCurrentUser), new { id = user.Id }, user);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration failed for {Email}", request.Email);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// GET /api/auth/me
    /// Gets the current authenticated user's profile
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        try
        {
            var userId = GetCurrentUserId();
            var user = await _authService.GetCurrentUserAsync(userId);
            return Ok(user);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "User not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get current user");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// PUT /api/auth/change-password
    /// Changes the current user's password
    /// </summary>
    [HttpPut("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _authService.ChangePasswordAsync(userId, request);

            // Audit password change
            await _auditService.LogAsync("CHANGE_PASSWORD", "User", userId);

            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "User not found" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Password change failed for user {UserId}", GetCurrentUserId());
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// GET /api/auth/users
    /// Gets all users (Admin only)
    /// </summary>
    [HttpGet("users")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAllUsers()
    {
        try
        {
            var users = await _authService.GetAllUsersAsync();
            return Ok(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all users");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// PUT /api/auth/users/{id}/deactivate
    /// Deactivates a user account (Admin only)
    /// </summary>
    [HttpPut("users/{id}/deactivate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeactivateUser(Guid id)
    {
        try
        {
            var success = await _authService.DeactivateUserAsync(id);
            if (!success)
            {
                return NotFound(new { message = "User not found" });
            }

            // Audit user deactivation
            await _auditService.LogAsync("DEACTIVATE_USER", "User", id);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deactivate user {UserId}", id);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// PUT /api/auth/users/{id}/activate
    /// Activates a user account (Admin only)
    /// </summary>
    [HttpPut("users/{id}/activate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ActivateUser(Guid id)
    {
        try
        {
            var success = await _authService.ActivateUserAsync(id);
            if (!success)
            {
                return NotFound(new { message = "User not found" });
            }

            // Audit user activation
            await _auditService.LogAsync("ACTIVATE_USER", "User", id);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate user {UserId}", id);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid user token");
        }
        return userId;
    }
}
```

### Phase 2: Campaign Management API

#### Step 2.1: Create Campaign DTOs

Create `src/Shared/CampaignDtos.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using FluentValidation;

namespace BidEngine.Shared;

public class CampaignDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid AdvertiserId { get; set; }
    public string Status { get; set; } = "active";
    public decimal CpmBid { get; set; }
    public decimal DailyBudget { get; set; }
    public decimal? LifetimeBudget { get; set; }
    public decimal SpentToday { get; set; }
    public decimal LifetimeSpent { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<AdDto> Ads { get; set; } = new();
    public List<TargetingRuleDto> TargetingRules { get; set; } = new();
    public bool CanServe => Status == "active" &&
                           DailyBudget > SpentToday &&
                           (LifetimeBudget == null || LifetimeSpent < LifetimeBudget);
}

public class CreateCampaignRequest
{
    [Required]
    [StringLength(255, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public Guid AdvertiserId { get; set; }

    [Required]
    [Range(0.01, 1000)]
    public decimal CpmBid { get; set; }

    [Required]
    [Range(0.01, 100000)]
    public decimal DailyBudget { get; set; }

    [Range(0.01, 1000000)]
    public decimal? LifetimeBudget { get; set; }

    public List<CreateAdRequest> Ads { get; set; } = new();
    public List<CreateTargetingRuleRequest> TargetingRules { get; set; } = new();
}

public class UpdateCampaignRequest
{
    [StringLength(255, MinimumLength = 1)]
    public string? Name { get; set; }

    [Range(0.01, 1000)]
    public decimal? CpmBid { get; set; }

    [Range(0.01, 100000)]
    public decimal? DailyBudget { get; set; }

    [Range(0.01, 1000000)]
    public decimal? LifetimeBudget { get; set; }

    public string? Status { get; set; } // active, paused, ended
}

public class CampaignListResponse
{
    public IEnumerable<CampaignDto> Campaigns { get; set; } = new List<CampaignDto>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

// Validators
public class CreateCampaignRequestValidator : AbstractValidator<CreateCampaignRequest>
{
    public CreateCampaignRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Campaign name is required")
            .MinimumLength(1).WithMessage("Campaign name cannot be empty")
            .MaximumLength(255).WithMessage("Campaign name cannot exceed 255 characters");

        RuleFor(x => x.AdvertiserId)
            .NotEmpty().WithMessage("Advertiser ID is required");

        RuleFor(x => x.CpmBid)
            .GreaterThan(0).WithMessage("CPM bid must be greater than 0")
            .LessThanOrEqualTo(1000).WithMessage("CPM bid cannot exceed $1000");

        RuleFor(x => x.DailyBudget)
            .GreaterThan(0).WithMessage("Daily budget must be greater than 0")
            .LessThanOrEqualTo(100000).WithMessage("Daily budget cannot exceed $100,000");

        RuleFor(x => x.LifetimeBudget)
            .GreaterThan(0).When(x => x.LifetimeBudget.HasValue)
            .WithMessage("Lifetime budget must be greater than 0")
            .LessThanOrEqualTo(1000000).When(x => x.LifetimeBudget.HasValue)
            .WithMessage("Lifetime budget cannot exceed $1,000,000");

        RuleFor(x => x.LifetimeBudget)
            .GreaterThanOrEqualTo(x => x.DailyBudget)
            .When(x => x.LifetimeBudget.HasValue)
            .WithMessage("Lifetime budget must be greater than or equal to daily budget");
    }
}

public class UpdateCampaignRequestValidator : AbstractValidator<UpdateCampaignRequest>
{
    public UpdateCampaignRequestValidator()
    {
        RuleFor(x => x.Name)
            .MinimumLength(1).When(x => x.Name != null)
            .WithMessage("Campaign name cannot be empty")
            .MaximumLength(255).When(x => x.Name != null)
            .WithMessage("Campaign name cannot exceed 255 characters");

        RuleFor(x => x.CpmBid)
            .GreaterThan(0).When(x => x.CpmBid.HasValue)
            .WithMessage("CPM bid must be greater than 0")
            .LessThanOrEqualTo(1000).When(x => x.CpmBid.HasValue)
            .WithMessage("CPM bid cannot exceed $1000");

        RuleFor(x => x.DailyBudget)
            .GreaterThan(0).When(x => x.DailyBudget.HasValue)
            .WithMessage("Daily budget must be greater than 0")
            .LessThanOrEqualTo(100000).When(x => x.DailyBudget.HasValue)
            .WithMessage("Daily budget cannot exceed $100,000");

        RuleFor(x => x.LifetimeBudget)
            .GreaterThan(0).When(x => x.LifetimeBudget.HasValue)
            .WithMessage("Lifetime budget must be greater than 0")
            .LessThanOrEqualTo(1000000).When(x => x.LifetimeBudget.HasValue)
            .WithMessage("Lifetime budget cannot exceed $1,000,000");

        RuleFor(x => x.Status)
            .Must(status => new[] { "active", "paused", "ended" }.Contains(status))
            .When(x => x.Status != null)
            .WithMessage("Status must be one of: active, paused, ended");
    }
}
```

#### Step 2.2: Create Ad DTOs

Create `src/Shared/AdDtos.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using FluentValidation;

namespace BidEngine.Shared;

public class AdDto
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string RedirectUrl { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CreateAdRequest
{
    [Required]
    [StringLength(255, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [Url]
    [StringLength(2048)]
    public string ImageUrl { get; set; } = string.Empty;

    [Required]
    [Url]
    [StringLength(2048)]
    public string RedirectUrl { get; set; } = string.Empty;

    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;
}

public class UpdateAdRequest
{
    [StringLength(255, MinimumLength = 1)]
    public string? Title { get; set; }

    [Url]
    [StringLength(2048)]
    public string? ImageUrl { get; set; }

    [Url]
    [StringLength(2048)]
    public string? RedirectUrl { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }
}

// Validators
public class CreateAdRequestValidator : AbstractValidator<CreateAdRequest>
{
    public CreateAdRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Ad title is required")
            .MinimumLength(1).WithMessage("Ad title cannot be empty")
            .MaximumLength(255).WithMessage("Ad title cannot exceed 255 characters");

        RuleFor(x => x.ImageUrl)
            .NotEmpty().WithMessage("Image URL is required")
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("Image URL must be a valid absolute URL")
            .MaximumLength(2048).WithMessage("Image URL cannot exceed 2048 characters");

        RuleFor(x => x.RedirectUrl)
            .NotEmpty().WithMessage("Redirect URL is required")
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("Redirect URL must be a valid absolute URL")
            .MaximumLength(2048).WithMessage("Redirect URL cannot exceed 2048 characters");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters");
    }
}

public class UpdateAdRequestValidator : AbstractValidator<UpdateAdRequest>
{
    public UpdateAdRequestValidator()
    {
        RuleFor(x => x.Title)
            .MinimumLength(1).When(x => x.Title != null)
            .WithMessage("Ad title cannot be empty")
            .MaximumLength(255).When(x => x.Title != null)
            .WithMessage("Ad title cannot exceed 255 characters");

        RuleFor(x => x.ImageUrl)
            .Must(url => url == null || Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("Image URL must be a valid absolute URL")
            .MaximumLength(2048).When(x => x.ImageUrl != null)
            .WithMessage("Image URL cannot exceed 2048 characters");

        RuleFor(x => x.RedirectUrl)
            .Must(url => url == null || Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("Redirect URL must be a valid absolute URL")
            .MaximumLength(2048).When(x => x.RedirectUrl != null)
            .WithMessage("Redirect URL cannot exceed 2048 characters");

        RuleFor(x => x.Description)
            .MaximumLength(1000).When(x => x.Description != null)
            .WithMessage("Description cannot exceed 1000 characters");
    }
}
```

#### Step 2.3: Create Targeting Rule DTOs

Create `src/Shared/TargetingRuleDtos.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using FluentValidation;

namespace BidEngine.Shared;

public class TargetingRuleDto
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public string RuleType { get; set; } = string.Empty;
    public string RuleValue { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CreateTargetingRuleRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string RuleType { get; set; } = string.Empty;

    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string RuleValue { get; set; } = string.Empty;
}

public class UpdateTargetingRuleRequest
{
    [StringLength(100, MinimumLength = 1)]
    public string? RuleType { get; set; }

    [StringLength(500, MinimumLength = 1)]
    public string? RuleValue { get; set; }
}

// Validators
public class CreateTargetingRuleRequestValidator : AbstractValidator<CreateTargetingRuleRequest>
{
    private static readonly string[] ValidRuleTypes = {
        "age_range", "gender", "location", "interests", "device_type",
        "browser", "operating_system", "time_of_day", "day_of_week"
    };

    public CreateTargetingRuleRequestValidator()
    {
        RuleFor(x => x.RuleType)
            .NotEmpty().WithMessage("Rule type is required")
            .Must(type => ValidRuleTypes.Contains(type.ToLower()))
            .WithMessage($"Rule type must be one of: {string.Join(", ", ValidRuleTypes)}");

        RuleFor(x => x.RuleValue)
            .NotEmpty().WithMessage("Rule value is required")
            .MinimumLength(1).WithMessage("Rule value cannot be empty")
            .MaximumLength(500).WithMessage("Rule value cannot exceed 500 characters");

        // Specific validations based on rule type
        RuleFor(x => x.RuleValue)
            .Must((rule, value) => ValidateRuleValue(rule.RuleType, value))
            .WithMessage("Invalid rule value for the specified rule type");
    }

    private bool ValidateRuleValue(string ruleType, string ruleValue)
    {
        return ruleType.ToLower() switch
        {
            "age_range" => System.Text.RegularExpressions.Regex.IsMatch(ruleValue, @"^\d{1,3}-\d{1,3}$"),
            "gender" => new[] { "male", "female", "other", "any" }.Contains(ruleValue.ToLower()),
            "device_type" => new[] { "mobile", "desktop", "tablet", "any" }.Contains(ruleValue.ToLower()),
            "time_of_day" => System.Text.RegularExpressions.Regex.IsMatch(ruleValue, @"^(?:[01]\d|2[0-3]):[0-5]\d-(?:[01]\d|2[0-3]):[0-5]\d$"),
            "day_of_week" => new[] { "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday", "weekdays", "weekends", "any" }.Contains(ruleValue.ToLower()),
            _ => true // For other rule types, accept any value
        };
    }
}

public class UpdateTargetingRuleRequestValidator : AbstractValidator<UpdateTargetingRuleRequest>
{
    private static readonly string[] ValidRuleTypes = {
        "age_range", "gender", "location", "interests", "device_type",
        "browser", "operating_system", "time_of_day", "day_of_week"
    };

    public UpdateTargetingRuleRequestValidator()
    {
        RuleFor(x => x.RuleType)
            .Must(type => type == null || ValidRuleTypes.Contains(type.ToLower()))
            .When(x => x.RuleType != null)
            .WithMessage($"Rule type must be one of: {string.Join(", ", ValidRuleTypes)}");

        RuleFor(x => x.RuleValue)
            .MinimumLength(1).When(x => x.RuleValue != null)
            .WithMessage("Rule value cannot be empty")
            .MaximumLength(500).When(x => x.RuleValue != null)
            .WithMessage("Rule value cannot exceed 500 characters");

        // Specific validations based on rule type
        RuleFor(x => x.RuleValue)
            .Must((rule, value) => rule.RuleType == null || ValidateRuleValue(rule.RuleType, value))
            .When(x => x.RuleValue != null)
            .WithMessage("Invalid rule value for the specified rule type");
    }

    private bool ValidateRuleValue(string ruleType, string ruleValue)
    {
        return ruleType.ToLower() switch
        {
            "age_range" => System.Text.RegularExpressions.Regex.IsMatch(ruleValue, @"^\d{1,3}-\d{1,3}$"),
            "gender" => new[] { "male", "female", "other", "any" }.Contains(ruleValue.ToLower()),
            "device_type" => new[] { "mobile", "desktop", "tablet", "any" }.Contains(ruleValue.ToLower()),
            "time_of_day" => System.Text.RegularExpressions.Regex.IsMatch(ruleValue, @"^(?:[01]\d|2[0-3]):[0-5]\d-(?:[01]\d|2[0-3]):[0-5]\d$"),
            "day_of_week" => new[] { "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday", "weekdays", "weekends", "any" }.Contains(ruleValue.ToLower()),
            _ => true // For other rule types, accept any value
        };
    }
}
```

#### Step 2.4: Create Campaign Management Service

Create `src/BidEngine/Services/CampaignManagementService.cs`:

```csharp
using BidEngine.Shared;
using BidEngine.Data;

namespace BidEngine.Services;

public interface ICampaignManagementService
{
    Task<CampaignDto> CreateCampaignAsync(CreateCampaignRequest request);
    Task<CampaignDto?> GetCampaignAsync(Guid id);
    Task<CampaignListResponse> GetCampaignsAsync(int page = 1, int pageSize = 20, string? status = null, Guid? advertiserId = null);
    Task<CampaignDto> UpdateCampaignAsync(Guid id, UpdateCampaignRequest request);
    Task<bool> DeleteCampaignAsync(Guid id);

    Task<AdDto> CreateAdAsync(Guid campaignId, CreateAdRequest request);
    Task<AdDto?> GetAdAsync(Guid id);
    Task<IEnumerable<AdDto>> GetAdsByCampaignAsync(Guid campaignId);
    Task<AdDto> UpdateAdAsync(Guid id, UpdateAdRequest request);
    Task<bool> DeleteAdAsync(Guid id);

    Task<TargetingRuleDto> CreateTargetingRuleAsync(Guid campaignId, CreateTargetingRuleRequest request);
    Task<TargetingRuleDto?> GetTargetingRuleAsync(Guid id);
    Task<IEnumerable<TargetingRuleDto>> GetTargetingRulesByCampaignAsync(Guid campaignId);
    Task<TargetingRuleDto> UpdateTargetingRuleAsync(Guid id, UpdateTargetingRuleRequest request);
    Task<bool> DeleteTargetingRuleAsync(Guid id);
}

public class CampaignManagementService : ICampaignManagementService
{
    private readonly AppDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ILogger<CampaignManagementService> _logger;

    public CampaignManagementService(
        AppDbContext context,
        IAuditService auditService,
        ILogger<CampaignManagementService> logger)
    {
        _context = context;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<CampaignDto> CreateCampaignAsync(CreateCampaignRequest request)
    {
        var campaign = new Campaign
        {
            Name = request.Name,
            AdvertiserId = request.AdvertiserId,
            CpmBid = request.CpmBid,
            DailyBudget = request.DailyBudget,
            LifetimeBudget = request.LifetimeBudget,
            Status = "active",
            SpentToday = 0,
            LifetimeSpent = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Add ads
        foreach (var adRequest in request.Ads)
        {
            var ad = new Ad
            {
                Title = adRequest.Title,
                ImageUrl = adRequest.ImageUrl,
                RedirectUrl = adRequest.RedirectUrl,
                Description = adRequest.Description,
                CreatedAt = DateTime.UtcNow
            };
            campaign.Ads.Add(ad);
        }

        // Add targeting rules
        foreach (var ruleRequest in request.TargetingRules)
        {
            var rule = new TargetingRule
            {
                RuleType = ruleRequest.RuleType,
                RuleValue = ruleRequest.RuleValue,
                CreatedAt = DateTime.UtcNow
            };
            campaign.TargetingRules.Add(rule);
        }

        _context.Campaigns.Add(campaign);
        await _context.SaveChangesAsync();

        // Audit campaign creation
        await _auditService.LogAsync("CREATE", "Campaign", campaign.Id, null, campaign);

        _logger.LogInformation("Created campaign {CampaignId} for advertiser {AdvertiserId}",
            campaign.Id, campaign.AdvertiserId);

        return MapToCampaignDto(campaign);
    }

    public async Task<CampaignDto?> GetCampaignAsync(Guid id)
    {
        var campaign = await _context.Campaigns
            .Include(c => c.Ads)
            .Include(c => c.TargetingRules)
            .FirstOrDefaultAsync(c => c.Id == id);

        return campaign != null ? MapToCampaignDto(campaign) : null;
    }

    public async Task<CampaignListResponse> GetCampaignsAsync(int page = 1, int pageSize = 20, string? status = null, Guid? advertiserId = null)
    {
        var query = _context.Campaigns
            .Include(c => c.Ads)
            .Include(c => c.TargetingRules)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(c => c.Status == status);
        }

        if (advertiserId.HasValue)
        {
            query = query.Where(c => c.AdvertiserId == advertiserId.Value);
        }

        var totalCount = await query.CountAsync();
        var campaigns = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new CampaignListResponse
        {
            Campaigns = campaigns.Select(MapToCampaignDto),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CampaignDto> UpdateCampaignAsync(Guid id, UpdateCampaignRequest request)
    {
        var campaign = await _context.Campaigns.FindAsync(id);
        if (campaign == null)
        {
            throw new KeyNotFoundException("Campaign not found");
        }

        var oldCampaign = campaign.Clone(); // Assuming you add a Clone method

        // Update fields
        if (!string.IsNullOrEmpty(request.Name))
            campaign.Name = request.Name;

        if (request.CpmBid.HasValue)
            campaign.CpmBid = request.CpmBid.Value;

        if (request.DailyBudget.HasValue)
            campaign.DailyBudget = request.DailyBudget.Value;

        if (request.LifetimeBudget.HasValue)
            campaign.LifetimeBudget = request.LifetimeBudget.Value;

        if (!string.IsNullOrEmpty(request.Status))
            campaign.Status = request.Status;

        campaign.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Audit campaign update
        await _auditService.LogAsync("UPDATE", "Campaign", campaign.Id, oldCampaign, campaign);

        _logger.LogInformation("Updated campaign {CampaignId}", campaign.Id);

        return await GetCampaignAsync(id) ?? throw new InvalidOperationException("Failed to retrieve updated campaign");
    }

    public async Task<bool> DeleteCampaignAsync(Guid id)
    {
        var campaign = await _context.Campaigns.FindAsync(id);
        if (campaign == null)
        {
            return false;
        }

        // Check if campaign has any spending
        if (campaign.LifetimeSpent > 0)
        {
            throw new InvalidOperationException("Cannot delete campaign with existing spend. Set status to 'ended' instead.");
        }

        _context.Campaigns.Remove(campaign);
        await _context.SaveChangesAsync();

        // Audit campaign deletion
        await _auditService.LogAsync("DELETE", "Campaign", campaign.Id, campaign, null);

        _logger.LogInformation("Deleted campaign {CampaignId}", campaign.Id);

        return true;
    }

    public async Task<AdDto> CreateAdAsync(Guid campaignId, CreateAdRequest request)
    {
        var campaign = await _context.Campaigns.FindAsync(campaignId);
        if (campaign == null)
        {
            throw new KeyNotFoundException("Campaign not found");
        }

        var ad = new Ad
        {
            CampaignId = campaignId,
            Title = request.Title,
            ImageUrl = request.ImageUrl,
            RedirectUrl = request.RedirectUrl,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow
        };

        _context.Ads.Add(ad);
        await _context.SaveChangesAsync();

        // Audit ad creation
        await _auditService.LogAsync("CREATE", "Ad", ad.Id, null, ad);

        _logger.LogInformation("Created ad {AdId} for campaign {CampaignId}", ad.Id, campaignId);

        return MapToAdDto(ad);
    }

    public async Task<AdDto?> GetAdAsync(Guid id)
    {
        var ad = await _context.Ads.FindAsync(id);
        return ad != null ? MapToAdDto(ad) : null;
    }

    public async Task<IEnumerable<AdDto>> GetAdsByCampaignAsync(Guid campaignId)
    {
        var ads = await _context.Ads
            .Where(a => a.CampaignId == campaignId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();

        return ads.Select(MapToAdDto);
    }

    public async Task<AdDto> UpdateAdAsync(Guid id, UpdateAdRequest request)
    {
        var ad = await _context.Ads.FindAsync(id);
        if (ad == null)
        {
            throw new KeyNotFoundException("Ad not found");
        }

        var oldAd = ad.Clone(); // Assuming you add a Clone method

        // Update fields
        if (!string.IsNullOrEmpty(request.Title))
            ad.Title = request.Title;

        if (!string.IsNullOrEmpty(request.ImageUrl))
            ad.ImageUrl = request.ImageUrl;

        if (!string.IsNullOrEmpty(request.RedirectUrl))
            ad.RedirectUrl = request.RedirectUrl;

        if (request.Description != null)
            ad.Description = request.Description;

        await _context.SaveChangesAsync();

        // Audit ad update
        await _auditService.LogAsync("UPDATE", "Ad", ad.Id, oldAd, ad);

        _logger.LogInformation("Updated ad {AdId}", ad.Id);

        return MapToAdDto(ad);
    }

    public async Task<bool> DeleteAdAsync(Guid id)
    {
        var ad = await _context.Ads.FindAsync(id);
        if (ad == null)
        {
            return false;
        }

        _context.Ads.Remove(ad);
        await _context.SaveChangesAsync();

        // Audit ad deletion
        await _auditService.LogAsync("DELETE", "Ad", ad.Id, ad, null);

        _logger.LogInformation("Deleted ad {AdId}", ad.Id);

        return true;
    }

    public async Task<TargetingRuleDto> CreateTargetingRuleAsync(Guid campaignId, CreateTargetingRuleRequest request)
    {
        var campaign = await _context.Campaigns.FindAsync(campaignId);
        if (campaign == null)
        {
            throw new KeyNotFoundException("Campaign not found");
        }

        var rule = new TargetingRule
        {
            CampaignId = campaignId,
            RuleType = request.RuleType,
            RuleValue = request.RuleValue,
            CreatedAt = DateTime.UtcNow
        };

        _context.TargetingRules.Add(rule);
        await _context.SaveChangesAsync();

        // Audit targeting rule creation
        await _auditService.LogAsync("CREATE", "TargetingRule", rule.Id, null, rule);

        _logger.LogInformation("Created targeting rule {RuleId} for campaign {CampaignId}", rule.Id, campaignId);

        return MapToTargetingRuleDto(rule);
    }

    public async Task<TargetingRuleDto?> GetTargetingRuleAsync(Guid id)
    {
        var rule = await _context.TargetingRules.FindAsync(id);
        return rule != null ? MapToTargetingRuleDto(rule) : null;
    }

    public async Task<IEnumerable<TargetingRuleDto>> GetTargetingRulesByCampaignAsync(Guid campaignId)
    {
        var rules = await _context.TargetingRules
            .Where(r => r.CampaignId == campaignId)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();

        return rules.Select(MapToTargetingRuleDto);
    }

    public async Task<TargetingRuleDto> UpdateTargetingRuleAsync(Guid id, UpdateTargetingRuleRequest request)
    {
        var rule = await _context.TargetingRules.FindAsync(id);
        if (rule == null)
        {
            throw new KeyNotFoundException("Targeting rule not found");
        }

        var oldRule = rule.Clone(); // Assuming you add a Clone method

        // Update fields
        if (!string.IsNullOrEmpty(request.RuleType))
            rule.RuleType = request.RuleType;

        if (!string.IsNullOrEmpty(request.RuleValue))
            rule.RuleValue = request.RuleValue;

        await _context.SaveChangesAsync();

        // Audit targeting rule update
        await _auditService.LogAsync("UPDATE", "TargetingRule", rule.Id, oldRule, rule);

        _logger.LogInformation("Updated targeting rule {RuleId}", rule.Id);

        return MapToTargetingRuleDto(rule);
    }

    public async Task<bool> DeleteTargetingRuleAsync(Guid id)
    {
        var rule = await _context.TargetingRules.FindAsync(id);
        if (rule == null)
        {
            return false;
        }

        _context.TargetingRules.Remove(rule);
        await _context.SaveChangesAsync();

        // Audit targeting rule deletion
        await _auditService.LogAsync("DELETE", "TargetingRule", rule.Id, rule, null);

        _logger.LogInformation("Deleted targeting rule {RuleId}", rule.Id);

        return true;
    }

    private static CampaignDto MapToCampaignDto(Campaign campaign)
    {
        return new CampaignDto
        {
            Id = campaign.Id,
            Name = campaign.Name,
            AdvertiserId = campaign.AdvertiserId,
            Status = campaign.Status,
            CpmBid = campaign.CpmBid,
            DailyBudget = campaign.DailyBudget,
            LifetimeBudget = campaign.LifetimeBudget,
            SpentToday = campaign.SpentToday,
            LifetimeSpent = campaign.LifetimeSpent,
            CreatedAt = campaign.CreatedAt,
            UpdatedAt = campaign.UpdatedAt,
            Ads = campaign.Ads.Select(MapToAdDto).ToList(),
            TargetingRules = campaign.TargetingRules.Select(MapToTargetingRuleDto).ToList()
        };
    }

    private static AdDto MapToAdDto(Ad ad)
    {
        return new AdDto
        {
            Id = ad.Id,
            CampaignId = ad.CampaignId,
            Title = ad.Title,
            ImageUrl = ad.ImageUrl,
            RedirectUrl = ad.RedirectUrl,
            Description = ad.Description,
            CreatedAt = ad.CreatedAt
        };
    }

    private static TargetingRuleDto MapToTargetingRuleDto(TargetingRule rule)
    {
        return new TargetingRuleDto
        {
            Id = rule.Id,
            CampaignId = rule.CampaignId,
            RuleType = rule.RuleType,
            RuleValue = rule.RuleValue,
            CreatedAt = rule.CreatedAt
        };
    }
}

// Extension methods for cloning (add to a separate file)
public static class EntityExtensions
{
    public static Campaign Clone(this Campaign campaign)
    {
        return new Campaign
        {
            Id = campaign.Id,
            Name = campaign.Name,
            AdvertiserId = campaign.AdvertiserId,
            Status = campaign.Status,
            CpmBid = campaign.CpmBid,
            DailyBudget = campaign.DailyBudget,
            LifetimeBudget = campaign.LifetimeBudget,
            SpentToday = campaign.SpentToday,
            LifetimeSpent = campaign.LifetimeSpent,
            CreatedAt = campaign.CreatedAt,
            UpdatedAt = campaign.UpdatedAt
        };
    }

    public static Ad Clone(this Ad ad)
    {
        return new Ad
        {
            Id = ad.Id,
            CampaignId = ad.CampaignId,
            Title = ad.Title,
            ImageUrl = ad.ImageUrl,
            RedirectUrl = ad.RedirectUrl,
            Description = ad.Description,
            CreatedAt = ad.CreatedAt,
            Embedding = ad.Embedding
        };
    }

    public static TargetingRule Clone(this TargetingRule rule)
    {
        return new TargetingRule
        {
            Id = rule.Id,
            CampaignId = rule.CampaignId,
            RuleType = rule.RuleType,
            RuleValue = rule.RuleValue,
            CreatedAt = rule.CreatedAt
        };
    }
}
```

#### Step 2.5: Create Admin Controllers

Create `src/BidEngine/Controllers/AdminCampaignsController.cs`:

```csharp
using BidEngine.Shared;
using BidEngine.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BidEngine.Controllers;

[ApiController]
[Route("api/admin/campaigns")]
[Authorize(Roles = "Admin")]
public class AdminCampaignsController : ControllerBase
{
    private readonly ICampaignManagementService _campaignService;
    private readonly IAuditService _auditService;
    private readonly ILogger<AdminCampaignsController> _logger;

    public AdminCampaignsController(
        ICampaignManagementService campaignService,
        IAuditService auditService,
        ILogger<AdminCampaignsController> logger)
    {
        _campaignService = campaignService;
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/admin/campaigns
    /// Gets all campaigns with pagination and filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<CampaignListResponse>> GetCampaigns(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] Guid? advertiserId = null)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var response = await _campaignService.GetCampaignsAsync(page, pageSize, status, advertiserId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get campaigns");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// GET /api/admin/campaigns/{id}
    /// Gets a specific campaign by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<CampaignDto>> GetCampaign(Guid id)
    {
        try
        {
            var campaign = await _campaignService.GetCampaignAsync(id);
            if (campaign == null)
            {
                return NotFound(new { message = "Campaign not found" });
            }

            return Ok(campaign);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get campaign {CampaignId}", id);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// POST /api/admin/campaigns
    /// Creates a new campaign
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CampaignDto>> CreateCampaign([FromBody] CreateCampaignRequest request)
    {
        try
        {
            var campaign = await _campaignService.CreateCampaignAsync(request);
            return CreatedAtAction(nameof(GetCampaign), new { id = campaign.Id }, campaign);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create campaign");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// PUT /api/admin/campaigns/{id}
    /// Updates an existing campaign
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<CampaignDto>> UpdateCampaign(Guid id, [FromBody] UpdateCampaignRequest request)
    {
        try
        {
            var campaign = await _campaignService.UpdateCampaignAsync(id, request);
            return Ok(campaign);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Campaign not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update campaign {CampaignId}", id);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// DELETE /api/admin/campaigns/{id}
    /// Deletes a campaign (only if no spend exists)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCampaign(Guid id)
    {
        try
        {
            var success = await _campaignService.DeleteCampaignAsync(id);
            if (!success)
            {
                return NotFound(new { message = "Campaign not found" });
            }

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete campaign {CampaignId}", id);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
}
```

Create `src/BidEngine/Controllers/AdminAdsController.cs`:

```csharp
using BidEngine.Shared;
using BidEngine.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BidEngine.Controllers;

[ApiController]
[Route("api/admin/ads")]
[Authorize(Roles = "Admin")]
public class AdminAdsController : ControllerBase
{
    private readonly ICampaignManagementService _campaignService;
    private readonly IAuditService _auditService;
    private readonly ILogger<AdminAdsController> _logger;

    public AdminAdsController(
        ICampaignManagementService campaignService,
        IAuditService auditService,
        ILogger<AdminAdsController> logger)
    {
        _campaignService = campaignService;
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/admin/ads/{id}
    /// Gets a specific ad by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<AdDto>> GetAd(Guid id)
    {
        try
        {
            var ad = await _campaignService.GetAdAsync(id);
            if (ad == null)
            {
                return NotFound(new { message = "Ad not found" });
            }

            return Ok(ad);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get ad {AdId}", id);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// GET /api/admin/ads/campaign/{campaignId}
    /// Gets all ads for a specific campaign
    /// </summary>
    [HttpGet("campaign/{campaignId}")]
    public async Task<ActionResult<IEnumerable<AdDto>>> GetAdsByCampaign(Guid campaignId)
    {
        try
        {
            var ads = await _campaignService.GetAdsByCampaignAsync(campaignId);
            return Ok(ads);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get ads for campaign {CampaignId}", campaignId);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// POST /api/admin/ads/campaign/{campaignId}
    /// Creates a new ad for a campaign
    /// </summary>
    [HttpPost("campaign/{campaignId}")]
    public async Task<ActionResult<AdDto>> CreateAd(Guid campaignId, [FromBody] CreateAdRequest request)
    {
        try
        {
            var ad = await _campaignService.CreateAdAsync(campaignId, request);
            return CreatedAtAction(nameof(GetAd), new { id = ad.Id }, ad);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Campaign not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create ad for campaign {CampaignId}", campaignId);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// PUT /api/admin/ads/{id}
    /// Updates an existing ad
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<AdDto>> UpdateAd(Guid id, [FromBody] UpdateAdRequest request)
    {
        try
        {
            var ad = await _campaignService.UpdateAdAsync(id, request);
            return Ok(ad);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Ad not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update ad {AdId}", id);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// DELETE /api/admin/ads/{id}
    /// Deletes an ad
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAd(Guid id)
    {
        try
        {
            var success = await _campaignService.DeleteAdAsync(id);
            if (!success)
            {
                return NotFound(new { message = "Ad not found" });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete ad {AdId}", id);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
}
```

Create `src/BidEngine/Controllers/AdminTargetingRulesController.cs`:

```csharp
using BidEngine.Shared;
using BidEngine.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BidEngine.Controllers;

[ApiController]
[Route("api/admin/targeting-rules")]
[Authorize(Roles = "Admin")]
public class AdminTargetingRulesController : ControllerBase
{
    private readonly ICampaignManagementService _campaignService;
    private readonly IAuditService _auditService;
    private readonly ILogger<AdminTargetingRulesController> _logger;

    public AdminTargetingRulesController(
        ICampaignManagementService campaignService,
        IAuditService auditService,
        ILogger<AdminTargetingRulesController> logger)
    {
        _campaignService = campaignService;
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/admin/targeting-rules/{id}
    /// Gets a specific targeting rule by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<TargetingRuleDto>> GetTargetingRule(Guid id)
    {
        try
        {
            var rule = await _campaignService.GetTargetingRuleAsync(id);
            if (rule == null)
            {
                return NotFound(new { message = "Targeting rule not found" });
            }

            return Ok(rule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get targeting rule {RuleId}", id);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// GET /api/admin/targeting-rules/campaign/{campaignId}
    /// Gets all targeting rules for a specific campaign
    /// </summary>
    [HttpGet("campaign/{campaignId}")]
    public async Task<ActionResult<IEnumerable<TargetingRuleDto>>> GetTargetingRulesByCampaign(Guid campaignId)
    {
        try
        {
            var rules = await _campaignService.GetTargetingRulesByCampaignAsync(campaignId);
            return Ok(rules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get targeting rules for campaign {CampaignId}", campaignId);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// POST /api/admin/targeting-rules/campaign/{campaignId}
    /// Creates a new targeting rule for a campaign
    /// </summary>
    [HttpPost("campaign/{campaignId}")]
    public async Task<ActionResult<TargetingRuleDto>> CreateTargetingRule(Guid campaignId, [FromBody] CreateTargetingRuleRequest request)
    {
        try
        {
            var rule = await _campaignService.CreateTargetingRuleAsync(campaignId, request);
            return CreatedAtAction(nameof(GetTargetingRule), new { id = rule.Id }, rule);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Campaign not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create targeting rule for campaign {CampaignId}", campaignId);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// PUT /api/admin/targeting-rules/{id}
    /// Updates an existing targeting rule
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<TargetingRuleDto>> UpdateTargetingRule(Guid id, [FromBody] UpdateTargetingRuleRequest request)
    {
        try
        {
            var rule = await _campaignService.UpdateTargetingRuleAsync(id, request);
            return Ok(rule);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Targeting rule not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update targeting rule {RuleId}", id);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// DELETE /api/admin/targeting-rules/{id}
    /// Deletes a targeting rule
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTargetingRule(Guid id)
    {
        try
        {
            var success = await _campaignService.DeleteTargetingRuleAsync(id);
            if (!success)
            {
                return NotFound(new { message = "Targeting rule not found" });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete targeting rule {RuleId}", id);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
}
```

#### Step 2.6: Create Audit Controller

Create `src/BidEngine/Controllers/AdminAuditController.cs`:

```csharp
using BidEngine.Shared;
using BidEngine.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BidEngine.Controllers;

[ApiController]
[Route("api/admin/audit")]
[Authorize(Roles = "Admin")]
public class AdminAuditController : ControllerBase
{
    private readonly IAuditService _auditService;
    private readonly ILogger<AdminAuditController> _logger;

    public AdminAuditController(
        IAuditService auditService,
        ILogger<AdminAuditController> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/admin/audit/entity/{entityType}/{entityId}
    /// Gets audit trail for a specific entity
    /// </summary>
    [HttpGet("entity/{entityType}/{entityId}")]
    public async Task<ActionResult<IEnumerable<AuditEvent>>> GetEntityAuditTrail(
        string entityType,
        Guid entityId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 200) pageSize = 50;

            var auditTrail = await _auditService.GetAuditTrailAsync(entityType, entityId, page, pageSize);
            return Ok(auditTrail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get audit trail for {EntityType} {EntityId}", entityType, entityId);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// GET /api/admin/audit/user/{userId}
    /// Gets audit trail for a specific user
    /// </summary>
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<IEnumerable<AuditEvent>>> GetUserAuditTrail(
        Guid userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 200) pageSize = 50;

            var auditTrail = await _auditService.GetUserAuditTrailAsync(userId, page, pageSize);
            return Ok(auditTrail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get audit trail for user {UserId}", userId);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
}
```

#### Step 2.7: Update Program.cs for New Services

Update `src/BidEngine/Program.cs`:

```csharp
// ... existing code ...

// Register campaign management and audit services
builder.Services.AddScoped<ICampaignManagementService, CampaignManagementService>();
builder.Services.AddScoped<IAuditService, AuditService>();

// ... existing code ...
```

#### Step 2.8: Create Database Migration

Create a new migration for the audit events table and any Identity tables:

```bash
cd src/BidEngine
dotnet ef migrations add AddAuthenticationAndAudit
dotnet ef database update
```

The migration should include:

```csharp
// Migration file content (auto-generated)
protected override void Up(MigrationBuilder migrationBuilder)
{
    // Identity tables
    migrationBuilder.CreateTable(
        name: "users",
        columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
            NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
            Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
            NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
            EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
            PasswordHash = table.Column<string>(type: "text", nullable: true),
            SecurityStamp = table.Column<string>(type: "text", nullable: true),
            ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
            PhoneNumber = table.Column<string>(type: "text", nullable: true),
            PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
            TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
            LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
            AccessFailedCount = table.Column<int>(type: "integer", nullable: false),
            FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
            LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
            IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
            CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
        },
        constraints: table =>
        {
            table.PrimaryKey("PK_users", x => x.Id);
        });

    migrationBuilder.CreateTable(
        name: "roles",
        columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
            NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
            ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
            Description = table.Column<string>(type: "text", nullable: false),
            CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        },
        constraints: table =>
        {
            table.PrimaryKey("PK_roles", x => x.Id);
        });

    // Audit events table
    migrationBuilder.CreateTable(
        name: "audit_events",
        columns: table => new
        {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            user_id = table.Column<Guid>(type: "uuid", nullable: true),
            action = table.Column<string>(type: "text", nullable: false),
            entity_type = table.Column<string>(type: "text", nullable: false),
            entity_id = table.Column<Guid>(type: "uuid", nullable: true),
            old_values = table.Column<string>(type: "jsonb", nullable: true),
            new_values = table.Column<string>(type: "jsonb", nullable: true),
            timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            ip_address = table.Column<string>(type: "text", nullable: true),
            user_agent = table.Column<string>(type: "text", nullable: true)
        },
        constraints: table =>
        {
            table.PrimaryKey("PK_audit_events", x => x.id);
        });

    // Identity relationship tables
    migrationBuilder.CreateTable(
        name: "user_roles",
        columns: table => new
        {
            UserId = table.Column<Guid>(type: "uuid", nullable: false),
            RoleId = table.Column<Guid>(type: "uuid", nullable: false)
        },
        constraints: table =>
        {
            table.PrimaryKey("PK_user_roles", x => new { x.UserId, x.RoleId });
            table.ForeignKey(
                name: "FK_user_roles_roles_RoleId",
                column: x => x.RoleId,
                principalTable: "roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
            table.ForeignKey(
                name: "FK_user_roles_users_UserId",
                column: x => x.UserId,
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        });

    migrationBuilder.CreateTable(
        name: "user_claims",
        columns: table => new
        {
            Id = table.Column<int>(type: "integer", nullable: false)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            UserId = table.Column<Guid>(type: "uuid", nullable: false),
            ClaimType = table.Column<string>(type: "text", nullable: true),
            ClaimValue = table.Column<string>(type: "text", nullable: true)
        },
        constraints: table =>
        {
            table.PrimaryKey("PK_user_claims", x => x.Id);
            table.ForeignKey(
                name: "FK_user_claims_users_UserId",
                column: x => x.UserId,
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        });

    migrationBuilder.CreateTable(
        name: "user_logins",
        columns: table => new
        {
            LoginProvider = table.Column<string>(type: "text", nullable: false),
            ProviderKey = table.Column<string>(type: "text", nullable: false),
            ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
            UserId = table.Column<Guid>(type: "uuid", nullable: false)
        },
        constraints: table =>
        {
            table.PrimaryKey("PK_user_logins", x => new { x.LoginProvider, x.ProviderKey });
            table.ForeignKey(
                name: "FK_user_logins_users_UserId",
                column: x => x.UserId,
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        });

    migrationBuilder.CreateTable(
        name: "role_claims",
        columns: table => new
        {
            Id = table.Column<int>(type: "integer", nullable: false)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            RoleId = table.Column<Guid>(type: "uuid", nullable: false),
            ClaimType = table.Column<string>(type: "text", nullable: true),
            ClaimValue = table.Column<string>(type: "text", nullable: true)
        },
        constraints: table =>
        {
            table.PrimaryKey("PK_role_claims", x => x.Id);
            table.ForeignKey(
                name: "FK_role_claims_roles_RoleId",
                column: x => x.RoleId,
                principalTable: "roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        });

    migrationBuilder.CreateTable(
        name: "user_tokens",
        columns: table => new
        {
            UserId = table.Column<Guid>(type: "uuid", nullable: false),
            LoginProvider = table.Column<string>(type: "text", nullable: false),
            Name = table.Column<string>(type: "text", nullable: false),
            Value = table.Column<string>(type: "text", nullable: true)
        },
        constraints: table =>
        {
            table.PrimaryKey("PK_user_tokens", x => new { x.UserId, x.LoginProvider, x.Name });
            table.ForeignKey(
                name: "FK_user_tokens_users_UserId",
                column: x => x.UserId,
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        });

    // Indexes
    migrationBuilder.CreateIndex(
        name: "IX_audit_events_entity_type_entity_id",
        table: "audit_events",
        columns: new[] { "entity_type", "entity_id" });

    migrationBuilder.CreateIndex(
        name: "IX_audit_events_timestamp",
        table: "audit_events",
        column: "timestamp");

    migrationBuilder.CreateIndex(
        name: "IX_audit_events_user_id",
        table: "audit_events",
        column: "user_id");

    migrationBuilder.CreateIndex(
        name: "EmailIndex",
        table: "users",
        column: "NormalizedEmail");

    migrationBuilder.CreateIndex(
        name: "UserNameIndex",
        table: "users",
        column: "NormalizedUserName");

    migrationBuilder.CreateIndex(
        name: "IX_user_claims_UserId",
        table: "user_claims",
        column: "UserId");

    migrationBuilder.CreateIndex(
        name: "IX_user_logins_UserId",
        table: "user_logins",
        column: "UserId");

    migrationBuilder.CreateIndex(
        name: "IX_user_roles_RoleId",
        table: "user_roles",
        column: "RoleId");

    migrationBuilder.CreateIndex(
        name: "IX_role_claims_RoleId",
        table: "role_claims",
        column: "RoleId");

    migrationBuilder.CreateIndex(
        name: "IX_user_tokens_UserId",
        table: "user_tokens",
        column: "UserId");
}
```

### Phase 3: Testing and Validation

#### Step 3.1: Create Unit Tests

Create `tests/BidEngine.Tests/AuthServiceTests.cs`:

```csharp
using BidEngine.Shared;
using BidEngine.Services;
using BidEngine.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BidEngine.Tests.Services;

public class AuthServiceTests
{
    private readonly Mock<UserManager<User>> _userManagerMock;
    private readonly Mock<RoleManager<Role>> _roleManagerMock;
    private readonly Mock<ILogger<AuthService>> _loggerMock;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _userManagerMock = new Mock<UserManager<User>>(
            Mock.Of<IUserStore<User>>(), null, null, null, null, null, null, null, null);

        _roleManagerMock = new Mock<RoleManager<Role>>(
            Mock.Of<IRoleStore<Role>>(), null, null, null, null);

        var jwtOptions = new JwtOptions
        {
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            SecretKey = "super-secret-key-that-is-at-least-32-characters-long-for-testing",
            ExpirationMinutes = 60
        };

        _loggerMock = new Mock<ILogger<AuthService>>();

        _authService = new AuthService(
            _userManagerMock.Object,
            _roleManagerMock.Object,
            Options.Create(jwtOptions),
            _loggerMock.Object);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsToken()
    {
        // Arrange
        var loginRequest = new LoginRequest { Email = "test@example.com", Password = "password123" };
        var user = new User { Id = Guid.NewGuid(), Email = loginRequest.Email, IsActive = true };

        _userManagerMock.Setup(x => x.FindByEmailAsync(loginRequest.Email))
            .ReturnsAsync(user);
        _userManagerMock.Setup(x => x.CheckPasswordAsync(user, loginRequest.Password))
            .ReturnsAsync(true);
        _userManagerMock.Setup(x => x.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "User" });

        // Act
        var result = await _authService.LoginAsync(loginRequest);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Token);
        Assert.Equal(user.Id, result.User.Id);
        Assert.Contains("User", result.User.Roles);
    }

    [Fact]
    public async Task LoginAsync_InvalidCredentials_ThrowsException()
    {
        // Arrange
        var loginRequest = new LoginRequest { Email = "test@example.com", Password = "wrongpassword" };
        var user = new User { Email = loginRequest.Email, IsActive = true };

        _userManagerMock.Setup(x => x.FindByEmailAsync(loginRequest.Email))
            .ReturnsAsync(user);
        _userManagerMock.Setup(x => x.CheckPasswordAsync(user, loginRequest.Password))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _authService.LoginAsync(loginRequest));
    }

    [Fact]
    public async Task RegisterAsync_ValidRequest_CreatesUser()
    {
        // Arrange
        var registerRequest = new RegisterRequest
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        };

        var newUser = new User
        {
            Id = Guid.NewGuid(),
            Email = registerRequest.Email,
            FirstName = registerRequest.FirstName,
            LastName = registerRequest.LastName
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync(registerRequest.Email))
            .ReturnsAsync((User)null!);
        _userManagerMock.Setup(x => x.CreateAsync(It.IsAny<User>(), registerRequest.Password))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(x => x.AddToRoleAsync(It.IsAny<User>(), "User"))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _authService.RegisterAsync(registerRequest);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(registerRequest.Email, result.Email);
        Assert.Equal(registerRequest.FirstName, result.FirstName);
        Assert.Equal(registerRequest.LastName, result.LastName);
    }
}
```

Create `tests/BidEngine.Tests/CampaignManagementServiceTests.cs`:

```csharp
using BidEngine.Shared;
using BidEngine.Services;
using BidEngine.Data;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace BidEngine.Tests.Services;

public class CampaignManagementServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Mock<IAuditService> _auditServiceMock;
    private readonly Mock<ILogger<CampaignManagementService>> _loggerMock;
    private readonly CampaignManagementService _service;

    public CampaignManagementServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _auditServiceMock = new Mock<IAuditService>();
        _loggerMock = new Mock<ILogger<CampaignManagementService>>();

        _service = new CampaignManagementService(
            _context,
            _auditServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task CreateCampaignAsync_ValidRequest_CreatesCampaign()
    {
        // Arrange
        var request = new CreateCampaignRequest
        {
            Name = "Test Campaign",
            AdvertiserId = Guid.NewGuid(),
            CpmBid = 2.50m,
            DailyBudget = 100.00m,
            LifetimeBudget = 1000.00m,
            Ads = new List<CreateAdRequest>
            {
                new CreateAdRequest
                {
                    Title = "Test Ad",
                    ImageUrl = "https://example.com/image.jpg",
                    RedirectUrl = "https://example.com",
                    Description = "Test ad description"
                }
            },
            TargetingRules = new List<CreateTargetingRuleRequest>
            {
                new CreateTargetingRuleRequest
                {
                    RuleType = "age_range",
                    RuleValue = "18-65"
                }
            }
        };

        // Act
        var result = await _service.CreateCampaignAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(request.Name, result.Name);
        Assert.Equal(request.CpmBid, result.CpmBid);
        Assert.Equal(request.DailyBudget, result.DailyBudget);
        Assert.Equal(request.LifetimeBudget, result.LifetimeBudget);
        Assert.Single(result.Ads);
        Assert.Single(result.TargetingRules);

        // Verify audit was called
        _auditServiceMock.Verify(x => x.LogAsync(
            "CREATE",
            "Campaign",
            It.IsAny<Guid>(),
            It.IsAny<object>(),
            It.IsAny<object>()),
            Times.Once);
    }

    [Fact]
    public async Task GetCampaignAsync_ExistingId_ReturnsCampaign()
    {
        // Arrange
        var campaign = new Campaign
        {
            Id = Guid.NewGuid(),
            Name = "Test Campaign",
            AdvertiserId = Guid.NewGuid(),
            CpmBid = 2.50m,
            DailyBudget = 100.00m,
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Campaigns.Add(campaign);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetCampaignAsync(campaign.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(campaign.Id, result!.Id);
        Assert.Equal(campaign.Name, result.Name);
    }

    [Fact]
    public async Task UpdateCampaignAsync_ValidRequest_UpdatesCampaign()
    {
        // Arrange
        var campaign = new Campaign
        {
            Id = Guid.NewGuid(),
            Name = "Original Name",
            AdvertiserId = Guid.NewGuid(),
            CpmBid = 1.00m,
            DailyBudget = 50.00m,
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Campaigns.Add(campaign);
        await _context.SaveChangesAsync();

        var updateRequest = new UpdateCampaignRequest
        {
            Name = "Updated Name",
            CpmBid = 2.50m,
            DailyBudget = 100.00m
        };

        // Act
        var result = await _service.UpdateCampaignAsync(campaign.Id, updateRequest);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(updateRequest.Name, result.Name);
        Assert.Equal(updateRequest.CpmBid, result.CpmBid);
        Assert.Equal(updateRequest.DailyBudget, result.DailyBudget);

        // Verify audit was called
        _auditServiceMock.Verify(x => x.LogAsync(
            "UPDATE",
            "Campaign",
            campaign.Id,
            It.IsAny<object>(),
            It.IsAny<object>()),
            Times.Once);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
```

#### Step 3.2: Create Integration Tests

Create `tests/BidEngine.Tests/Controllers/AdminCampaignsControllerTests.cs`:

```csharp
using BidEngine.Shared;
using BidEngine.Services;
using BidEngine.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace BidEngine.Tests.Controllers;

public class AdminCampaignsControllerTests
{
    private readonly Mock<ICampaignManagementService> _campaignServiceMock;
    private readonly Mock<IAuditService> _auditServiceMock;
    private readonly Mock<ILogger<AdminCampaignsController>> _loggerMock;
    private readonly AdminCampaignsController _controller;

    public AdminCampaignsControllerTests()
    {
        _campaignServiceMock = new Mock<ICampaignManagementService>();
        _auditServiceMock = new Mock<IAuditService>();
        _loggerMock = new Mock<ILogger<AdminCampaignsController>>();

        _controller = new AdminCampaignsController(
            _campaignServiceMock.Object,
            _auditServiceMock.Object,
            _loggerMock.Object);

        // Setup admin user context
        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "Admin")
        }, "mock"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    [Fact]
    public async Task GetCampaigns_ReturnsCampaignList()
    {
        // Arrange
        var campaigns = new List<CampaignDto>
        {
            new CampaignDto { Id = Guid.NewGuid(), Name = "Campaign 1" },
            new CampaignDto { Id = Guid.NewGuid(), Name = "Campaign 2" }
        };

        var response = new CampaignListResponse
        {
            Campaigns = campaigns,
            TotalCount = 2,
            Page = 1,
            PageSize = 20
        };

        _campaignServiceMock.Setup(x => x.GetCampaignsAsync(1, 20, null, null))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.GetCampaigns();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedResponse = Assert.IsType<CampaignListResponse>(okResult.Value);
        Assert.Equal(2, returnedResponse.TotalCount);
        Assert.Equal(2, returnedResponse.Campaigns.Count());
    }

    [Fact]
    public async Task GetCampaign_ExistingId_ReturnsCampaign()
    {
        // Arrange
        var campaignId = Guid.NewGuid();
        var campaign = new CampaignDto
        {
            Id = campaignId,
            Name = "Test Campaign",
            Status = "active"
        };

        _campaignServiceMock.Setup(x => x.GetCampaignAsync(campaignId))
            .ReturnsAsync(campaign);

        // Act
        var result = await _controller.GetCampaign(campaignId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedCampaign = Assert.IsType<CampaignDto>(okResult.Value);
        Assert.Equal(campaignId, returnedCampaign.Id);
        Assert.Equal("Test Campaign", returnedCampaign.Name);
    }

    [Fact]
    public async Task GetCampaign_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        var campaignId = Guid.NewGuid();
        _campaignServiceMock.Setup(x => x.GetCampaignAsync(campaignId))
            .ReturnsAsync((CampaignDto)null!);

        // Act
        var result = await _controller.GetCampaign(campaignId);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateCampaign_ValidRequest_ReturnsCreated()
    {
        // Arrange
        var request = new CreateCampaignRequest
        {
            Name = "New Campaign",
            AdvertiserId = Guid.NewGuid(),
            CpmBid = 2.50m,
            DailyBudget = 100.00m
        };

        var createdCampaign = new CampaignDto
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            AdvertiserId = request.AdvertiserId,
            CpmBid = request.CpmBid,
            DailyBudget = request.DailyBudget,
            Status = "active"
        };

        _campaignServiceMock.Setup(x => x.CreateCampaignAsync(request))
            .ReturnsAsync(createdCampaign);

        // Act
        var result = await _controller.CreateCampaign(request);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var returnedCampaign = Assert.IsType<CampaignDto>(createdResult.Value);
        Assert.Equal(createdCampaign.Id, returnedCampaign.Id);
        Assert.Equal(request.Name, returnedCampaign.Name);
    }

    [Fact]
    public async Task UpdateCampaign_ValidRequest_ReturnsOk()
    {
        // Arrange
        var campaignId = Guid.NewGuid();
        var updateRequest = new UpdateCampaignRequest
        {
            Name = "Updated Campaign Name",
            CpmBid = 3.00m
        };

        var updatedCampaign = new CampaignDto
        {
            Id = campaignId,
            Name = updateRequest.Name,
            CpmBid = updateRequest.CpmBid!.Value
        };

        _campaignServiceMock.Setup(x => x.UpdateCampaignAsync(campaignId, updateRequest))
            .ReturnsAsync(updatedCampaign);

        // Act
        var result = await _controller.UpdateCampaign(campaignId, updateRequest);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedCampaign = Assert.IsType<CampaignDto>(okResult.Value);
        Assert.Equal(updateRequest.Name, returnedCampaign.Name);
        Assert.Equal(updateRequest.CpmBid, returnedCampaign.CpmBid);
    }

    [Fact]
    public async Task DeleteCampaign_ExistingId_ReturnsNoContent()
    {
        // Arrange
        var campaignId = Guid.NewGuid();
        _campaignServiceMock.Setup(x => x.DeleteCampaignAsync(campaignId))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.DeleteCampaign(campaignId);

        // Assert
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteCampaign_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        var campaignId = Guid.NewGuid();
        _campaignServiceMock.Setup(x => x.DeleteCampaignAsync(campaignId))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.DeleteCampaign(campaignId);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }
}
```

#### Step 3.3: Update Swagger Configuration

Update `Program.cs` to include JWT authentication in Swagger:

```csharp
// ... existing code ...

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "BidEngine API", Version = "v1" });

    // Add JWT Authentication
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ... existing code ...
```

#### Step 3.4: Create API Documentation

Create `docs/03_API_DOCUMENTATION.md` (update existing):

```markdown
# API Documentation

## Authentication

### Login
```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "admin@bidengine.local",
  "password": "Admin123!"
}
```

Response:
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2024-01-01T12:00:00Z",
  "user": {
    "id": "guid",
    "userName": "admin@bidengine.local",
    "email": "admin@bidengine.local",
    "firstName": "System",
    "lastName": "Administrator",
    "isActive": true,
    "createdAt": "2024-01-01T00:00:00Z",
    "roles": ["Admin"]
  }
}
```

### Register
```http
POST /api/auth/register
Content-Type: application/json

{
  "firstName": "John",
  "lastName": "Doe",
  "email": "john.doe@example.com",
  "password": "SecurePass123!",
  "confirmPassword": "SecurePass123!"
}
```

## Admin Campaign Management

All admin endpoints require `Authorization: Bearer {token}` header with Admin role.

### Get Campaigns
```http
GET /api/admin/campaigns?page=1&pageSize=20&status=active&advertiserId={guid}
```

### Create Campaign
```http
POST /api/admin/campaigns
Content-Type: application/json
Authorization: Bearer {token}

{
  "name": "Summer Sale Campaign",
  "advertiserId": "guid",
  "cpmBid": 2.50,
  "dailyBudget": 500.00,
  "lifetimeBudget": 5000.00,
  "ads": [
    {
      "title": "Amazing Summer Deals!",
      "imageUrl": "https://example.com/summer-sale.jpg",
      "redirectUrl": "https://example.com/summer-sale",
      "description": "Up to 70% off on summer items"
    }
  ],
  "targetingRules": [
    {
      "ruleType": "age_range",
      "ruleValue": "18-65"
    },
    {
      "ruleType": "location",
      "ruleValue": "United States"
    }
  ]
}
```

### Update Campaign
```http
PUT /api/admin/campaigns/{id}
Content-Type: application/json
Authorization: Bearer {token}

{
  "name": "Updated Campaign Name",
  "cpmBid": 3.00,
  "status": "paused"
}
```

### Delete Campaign
```http
DELETE /api/admin/campaigns/{id}
Authorization: Bearer {token}
```

## Admin Ad Management

### Create Ad
```http
POST /api/admin/ads/campaign/{campaignId}
Content-Type: application/json
Authorization: Bearer {token}

{
  "title": "New Product Launch",
  "imageUrl": "https://example.com/new-product.jpg",
  "redirectUrl": "https://example.com/new-product",
  "description": "Check out our latest innovation"
}
```

### Update Ad
```http
PUT /api/admin/ads/{id}
Content-Type: application/json
Authorization: Bearer {token}

{
  "title": "Updated Product Title",
  "description": "Revised product description"
}
```

## Admin Targeting Rules

### Create Targeting Rule
```http
POST /api/admin/targeting-rules/campaign/{campaignId}
Content-Type: application/json
Authorization: Bearer {token}

{
  "ruleType": "gender",
  "ruleValue": "female"
}
```

Valid rule types: `age_range`, `gender`, `location`, `interests`, `device_type`, `browser`, `operating_system`, `time_of_day`, `day_of_week`

## Audit Trail

### Get Entity Audit Trail
```http
GET /api/admin/audit/entity/Campaign/{entityId}?page=1&pageSize=50
Authorization: Bearer {token}
```

### Get User Audit Trail
```http
GET /api/admin/audit/user/{userId}?page=1&pageSize=50
Authorization: Bearer {token}
```

## Error Responses

All endpoints return standardized error responses:

```json
{
  "message": "Error description"
}
```

Common HTTP status codes:
- `400` - Bad Request (validation errors)
- `401` - Unauthorized (invalid/missing token)
- `403` - Forbidden (insufficient permissions)
- `404` - Not Found
- `500` - Internal Server Error
```

## Validation Rules

### Campaign Validation
- Name: 1-255 characters, required
- CPM Bid: 0.01-1000, required
- Daily Budget: 0.01-100,000, required
- Lifetime Budget: 0.01-1,000,000, optional (must be >= daily budget if provided)
- Status: active, paused, ended

### Ad Validation
- Title: 1-255 characters, required
- Image URL: Valid absolute URL, max 2048 characters, required
- Redirect URL: Valid absolute URL, max 2048 characters, required
- Description: Max 1000 characters, optional

### Targeting Rule Validation
- Rule Type: Must be from predefined list
- Rule Value: 1-500 characters, format depends on rule type
  - `age_range`: "min-max" (e.g., "18-65")
  - `gender`: "male", "female", "other", "any"
  - `device_type`: "mobile", "desktop", "tablet", "any"
  - `time_of_day`: "HH:mm-HH:mm" (e.g., "09:00-17:00")
  - `day_of_week`: "monday", "tuesday", etc., "weekdays", "weekends", "any"
```

## Rate Limiting

Admin endpoints are rate limited to prevent abuse:
- 100 requests per minute per user for read operations
- 20 requests per minute per user for write operations

## Security Notes

- All admin endpoints require JWT authentication with Admin role
- Passwords must be at least 8 characters with uppercase, lowercase, and numeric characters
- Failed login attempts are logged and can trigger account lockout
- All changes are audited with full before/after state tracking
- User accounts can be deactivated by admins
- JWT tokens expire after 60 minutes
```

#### Step 3.5: Create Postman Collection

Create `docs/BidEngine_Admin_API.postman_collection.json`:

```json
{
  "info": {
    "name": "BidEngine Admin API",
    "description": "Complete API collection for BidEngine admin operations",
    "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
  },
  "auth": {
    "type": "bearer",
    "bearer": [
      {
        "key": "token",
        "value": "{{jwt_token}}",
        "type": "string"
      }
    ]
  },
  "variable": [
    {
      "key": "base_url",
      "value": "http://localhost:8081"
    },
    {
      "key": "jwt_token",
      "value": ""
    },
    {
      "key": "campaign_id",
      "value": ""
    },
    {
      "key": "ad_id",
      "value": ""
    }
  ],
  "item": [
    {
      "name": "Authentication",
      "item": [
        {
          "name": "Login",
          "request": {
            "method": "POST",
            "header": [
              {
                "key": "Content-Type",
                "value": "application/json"
              }
            ],
            "body": {
              "mode": "raw",
              "raw": "{\n  \"email\": \"admin@bidengine.local\",\n  \"password\": \"Admin123!\"\n}"
            },
            "url": {
              "raw": "{{base_url}}/api/auth/login",
              "host": ["{{base_url}}"],
              "path": ["api", "auth", "login"]
            },
            "event": [
              {
                "listen": "test",
                "script": {
                  "exec": [
                    "if (pm.response.code === 200) {",
                    "    const response = pm.response.json();",
                    "    pm.collectionVariables.set('jwt_token', response.token);",
                    "    console.log('JWT Token saved:', response.token);",
                    "}"
                  ]
                }
              }
            ]
          }
        },
        {
          "name": "Register User",
          "request": {
            "method": "POST",
            "header": [
              {
                "key": "Content-Type",
                "value": "application/json"
              }
            ],
            "body": {
              "mode": "raw",
              "raw": "{\n  \"firstName\": \"John\",\n  \"lastName\": \"Doe\",\n  \"email\": \"john.doe@example.com\",\n  \"password\": \"SecurePass123!\",\n  \"confirmPassword\": \"SecurePass123!\"\n}"
            },
            "url": {
              "raw": "{{base_url}}/api/auth/register",
              "host": ["{{base_url}}"],
              "path": ["api", "auth", "register"]
            }
          }
        },
        {
          "name": "Get Current User",
          "request": {
            "method": "GET",
            "header": [],
            "url": {
              "raw": "{{base_url}}/api/auth/me",
              "host": ["{{base_url}}"],
              "path": ["api", "auth", "me"]
            }
          }
        }
      ]
    },
    {
      "name": "Campaign Management",
      "item": [
        {
          "name": "Get Campaigns",
          "request": {
            "method": "GET",
            "header": [],
            "url": {
              "raw": "{{base_url}}/api/admin/campaigns?page=1&pageSize=20",
              "host": ["{{base_url}}"],
              "path": ["api", "admin", "campaigns"],
              "query": [
                {
                  "key": "page",
                  "value": "1"
                },
                {
                  "key": "pageSize",
                  "value": "20"
                }
              ]
            }
          }
        },
        {
          "name": "Create Campaign",
          "request": {
            "method": "POST",
            "header": [
              {
                "key": "Content-Type",
                "value": "application/json"
              }
            ],
            "body": {
              "mode": "raw",
              "raw": "{\n  \"name\": \"Summer Sale Campaign\",\n  \"advertiserId\": \"550e8400-e29b-41d4-a716-446655440000\",\n  \"cpmBid\": 2.50,\n  \"dailyBudget\": 500.00,\n  \"lifetimeBudget\": 5000.00,\n  \"ads\": [\n    {\n      \"title\": \"Amazing Summer Deals!\",\n      \"imageUrl\": \"https://example.com/summer-sale.jpg\",\n      \"redirectUrl\": \"https://example.com/summer-sale\",\n      \"description\": \"Up to 70% off on summer items\"\n    }\n  ],\n  \"targetingRules\": [\n    {\n      \"ruleType\": \"age_range\",\n      \"ruleValue\": \"18-65\"\n    }\n  ]\n}"
            },
            "url": {
              "raw": "{{base_url}}/api/admin/campaigns",
              "host": ["{{base_url}}"],
              "path": ["api", "admin", "campaigns"]
            },
            "event": [
              {
                "listen": "test",
                "script": {
                  "exec": [
                    "if (pm.response.code === 201) {",
                    "    const response = pm.response.json();",
                    "    pm.collectionVariables.set('campaign_id', response.id);",
                    "    console.log('Campaign ID saved:', response.id);",
                    "}"
                  ]
                }
              }
            ]
          }
        },
        {
          "name": "Get Campaign",
          "request": {
            "method": "GET",
            "header": [],
            "url": {
              "raw": "{{base_url}}/api/admin/campaigns/{{campaign_id}}",
              "host": ["{{base_url}}"],
              "path": ["api", "admin", "campaigns", "{{campaign_id}}"]
            }
          }
        },
        {
          "name": "Update Campaign",
          "request": {
            "method": "PUT",
            "header": [
              {
                "key": "Content-Type",
                "value": "application/json"
              }
            ],
            "body": {
              "mode": "raw",
              "raw": "{\n  \"name\": \"Updated Summer Sale Campaign\",\n  \"cpmBid\": 3.00\n}"
            },
            "url": {
              "raw": "{{base_url}}/api/admin/campaigns/{{campaign_id}}",
              "host": ["{{base_url}}"],
              "path": ["api", "admin", "campaigns", "{{campaign_id}}"]
            }
          }
        },
        {
          "name": "Delete Campaign",
          "request": {
            "method": "DELETE",
            "header": [],
            "url": {
              "raw": "{{base_url}}/api/admin/campaigns/{{campaign_id}}",
              "host": ["{{base_url}}"],
              "path": ["api", "admin", "campaigns", "{{campaign_id}}"]
            }
          }
        }
      ]
    },
    {
      "name": "Ad Management",
      "item": [
        {
          "name": "Get Ads by Campaign",
          "request": {
            "method": "GET",
            "header": [],
            "url": {
              "raw": "{{base_url}}/api/admin/ads/campaign/{{campaign_id}}",
              "host": ["{{base_url}}"],
              "path": ["api", "admin", "ads", "campaign", "{{campaign_id}}"]
            }
          }
        },
        {
          "name": "Create Ad",
          "request": {
            "method": "POST",
            "header": [
              {
                "key": "Content-Type",
                "value": "application/json"
              }
            ],
            "body": {
              "mode": "raw",
              "raw": "{\n  \"title\": \"New Product Launch\",\n  \"imageUrl\": \"https://example.com/new-product.jpg\",\n  \"redirectUrl\": \"https://example.com/new-product\",\n  \"description\": \"Check out our latest innovation\"\n}"
            },
            "url": {
              "raw": "{{base_url}}/api/admin/ads/campaign/{{campaign_id}}",
              "host": ["{{base_url}}"],
              "path": ["api", "admin", "ads", "campaign", "{{campaign_id}}"]
            },
            "event": [
              {
                "listen": "test",
                "script": {
                  "exec": [
                    "if (pm.response.code === 201) {",
                    "    const response = pm.response.json();",
                    "    pm.collectionVariables.set('ad_id', response.id);",
                    "    console.log('Ad ID saved:', response.id);",
                    "}"
                  ]
                }
              }
            ]
          }
        },
        {
          "name": "Update Ad",
          "request": {
            "method": "PUT",
            "header": [
              {
                "key": "Content-Type",
                "value": "application/json"
              }
            ],
            "body": {
              "mode": "raw",
              "raw": "{\n  \"title\": \"Updated Product Title\",\n  \"description\": \"Revised product description\"\n}"
            },
            "url": {
              "raw": "{{base_url}}/api/admin/ads/{{ad_id}}",
              "host": ["{{base_url}}"],
              "path": ["api", "admin", "ads", "{{ad_id}}"]
            }
          }
        },
        {
          "name": "Delete Ad",
          "request": {
            "method": "DELETE",
            "header": [],
            "url": {
              "raw": "{{base_url}}/api/admin/ads/{{ad_id}}",
              "host": ["{{base_url}}"],
              "path": ["api", "admin", "ads", "{{ad_id}}"]
            }
          }
        }
      ]
    },
    {
      "name": "Audit Trail",
      "item": [
        {
          "name": "Get Campaign Audit Trail",
          "request": {
            "method": "GET",
            "header": [],
            "url": {
              "raw": "{{base_url}}/api/admin/audit/entity/Campaign/{{campaign_id}}",
              "host": ["{{base_url}}"],
              "path": ["api", "admin", "audit", "entity", "Campaign", "{{campaign_id}}"]
            }
          }
        }
      ]
    }
  ]
}
```

### Phase 4: Deployment and Production Considerations

#### Step 4.1: Environment Configuration

Create `.env.example`:

```bash
# Database Configuration
ConnectionStrings__DefaultConnection=Server=postgres;Port=5432;Database=ads_db;User Id=postgres;Password=postgres

# Redis Configuration
Redis__ConnectionString=redis:6379

# JWT Configuration
Jwt__Issuer=BidEngine
Jwt__Audience=BidEngineUsers
Jwt__SecretKey=your-super-secret-jwt-key-that-is-at-least-32-characters-long
Jwt__ExpirationMinutes=60
Jwt__RefreshTokenExpirationMinutes=10080

# Application Configuration
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=http://+:8081

# Logging
Logging__LogLevel__Default=Information
Logging__LogLevel__Microsoft.AspNetCore=Warning
```

#### Step 4.2: Docker Compose Updates

Update `docker-compose.yml` to include environment variables:

```yaml
version: '3.8'

services:
  bid-engine:
    build:
      context: .
      dockerfile: src/BidEngine/Dockerfile
    ports:
      - "8081:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ASPNETCORE_URLS=http://+:8080
      - ConnectionStrings__DefaultConnection=Server=postgres;Port=5432;Database=ads_db;User Id=postgres;Password=postgres
      - Redis__ConnectionString=redis:6379
      - Jwt__Issuer=BidEngine
      - Jwt__Audience=BidEngineUsers
      - Jwt__SecretKey=your-super-secret-jwt-key-that-is-at-least-32-characters-long
      - Jwt__ExpirationMinutes=60
      - Jwt__RefreshTokenExpirationMinutes=10080
    depends_on:
      - postgres
      - redis
    networks:
      - bidengine-network

  # ... existing services ...
```

#### Step 4.3: Security Hardening

Create `src/BidEngine/Middleware/SecurityHeadersMiddleware.cs`:

```csharp
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace BidEngine.Middleware;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Security headers
        context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Add("X-Frame-Options", "DENY");
        context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
        context.Response.Headers.Add("Content-Security-Policy", "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline';");

        // Remove server header
        context.Response.Headers.Remove("Server");

        await _next(context);
    }
}
```

Update `Program.cs`:

```csharp
// ... existing code ...

// Add security headers middleware
app.UseMiddleware<SecurityHeadersMiddleware>();

// ... existing code ...
```

#### Step 4.4: Health Checks

Update `Program.cs` to add health checks:

```csharp
// ... existing code ...

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!)
    .AddRedis(builder.Configuration["Redis__ConnectionString"]!)
    .AddDbContextCheck<AppDbContext>();

// ... existing code ...

app.MapHealthChecks("/health");
app.MapHealthChecks("/ready", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

// ... existing code ...
```

#### Step 4.5: Final Testing

Run comprehensive tests:

```bash
# Build and test
dotnet build
dotnet test

# Run with Docker Compose
docker compose up --build

# Test authentication
curl -X POST http://localhost:8081/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@bidengine.local","password":"Admin123!"}'

# Test admin endpoints with JWT token
curl -X GET http://localhost:8081/api/admin/campaigns \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"

# Test health checks
curl http://localhost:8081/health
curl http://localhost:8081/ready
```

### Summary

This implementation provides a complete, production-ready authenticated admin API for campaign management with:

- **JWT Authentication**: Secure token-based authentication with role-based authorization
- **Full CRUD Operations**: Create, read, update, delete campaigns, ads, and targeting rules
- **Comprehensive Validation**: Input validation with detailed error messages
- **Audit Trail**: Complete change tracking with before/after states
- **Security**: Rate limiting, security headers, input sanitization
- **Testing**: Unit tests, integration tests, and API documentation
- **Production Ready**: Health checks, environment configuration, Docker support

The implementation demonstrates advanced backend engineering skills including authentication systems, data modeling, API design, security practices, and operational readiness. This represents a significant upgrade from a simple bidding service to a full-featured admin platform suitable for production use.
