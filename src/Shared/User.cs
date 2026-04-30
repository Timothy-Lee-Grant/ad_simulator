using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BidEngine.Shared;

/// <summary>
/// Application user extending ASP.NET Core Identity User
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>
    /// First name of the user
    /// </summary>
    [Required]
    [StringLength(50)]
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Last name of the user
    /// </summary>
    [Required]
    [StringLength(50)]
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Full name computed property
    /// </summary>
    [NotMapped]
    public string FullName => $"{FirstName} {LastName}".Trim();

    /// <summary>
    /// When the user was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the user was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether the user is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Navigation property for audit logs
    /// </summary>
    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}

/// <summary>
/// Application role extending ASP.NET Core Identity Role
/// </summary>
public class ApplicationRole : IdentityRole<Guid>
{
    /// <summary>
    /// Description of the role
    /// </summary>
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// When the role was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the role was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Custom user role junction for additional properties
/// </summary>
public class ApplicationUserRole : IdentityUserRole<Guid>
{
    /// <summary>
    /// When the user was assigned this role
    /// </summary>
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Who assigned this role to the user
    /// </summary>
    public Guid? AssignedBy { get; set; }
}

/// <summary>
/// Audit log for tracking admin operations
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// User who performed the action
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Navigation property to the user
    /// </summary>
    public virtual ApplicationUser User { get; set; } = null!;

    /// <summary>
    /// Action performed (CREATE, UPDATE, DELETE, LOGIN, etc.)
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Entity type affected (Campaign, User, etc.)
    /// </summary>
    [Required]
    [StringLength(100)]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// ID of the entity affected
    /// </summary>
    public Guid? EntityId { get; set; }

    /// <summary>
    /// Old values (JSON serialized)
    /// </summary>
    public string? OldValues { get; set; }

    /// <summary>
    /// New values (JSON serialized)
    /// </summary>
    public string? NewValues { get; set; }

    /// <summary>
    /// IP address of the request
    /// </summary>
    [StringLength(45)]
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent string
    /// </summary>
    [StringLength(500)]
    public string? UserAgent { get; set; }

    /// <summary>
    /// When the action occurred
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether the action was successful
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// Error message if the action failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}