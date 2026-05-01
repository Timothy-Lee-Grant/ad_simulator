using System.ComponentModel.DataAnnotations;

namespace BidEngine.Shared.DTOs;

public class CampaignDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid AdvertiserId { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal CpmBid { get; set; }
    public decimal DailyBudget { get; set; }
    public decimal? LifetimeBudget { get; set; }
    public decimal SpentToday { get; set; }
    public decimal LifetimeSpent { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<AdDto> Ads { get; set; } = new();
    public List<TargetingRuleDto> TargetingRules { get; set; } = new();
}

public class CreateCampaignRequest
{
    [Required]
    [StringLength(255, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public Guid AdvertiserId { get; set; }

    [Required]
    [RegularExpression("^(active|paused|ended)$", ErrorMessage = "Status must be active, paused, or ended")]
    public string Status { get; set; } = "active";

    [Range(0.01, 1000)]
    public decimal CpmBid { get; set; }

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

    [RegularExpression("^(active|paused|ended)$", ErrorMessage = "Status must be active, paused, or ended")]
    public string? Status { get; set; }

    [Range(0.01, 1000)]
    public decimal? CpmBid { get; set; }

    [Range(0.01, 100000)]
    public decimal? DailyBudget { get; set; }

    [Range(0.01, 1000000)]
    public decimal? LifetimeBudget { get; set; }
}

public class PagedCampaignListResponse
{
    public IEnumerable<CampaignDto> Campaigns { get; set; } = new List<CampaignDto>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
}

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
