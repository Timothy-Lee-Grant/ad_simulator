using BidEngine.Shared.DTOs;
using FluentValidation;

namespace BidEngine.Validators;

public class CreateCampaignRequestValidator : AbstractValidator<CreateCampaignRequest>
{
    public CreateCampaignRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Campaign name is required")
            .MaximumLength(255).WithMessage("Campaign name cannot exceed 255 characters");

        RuleFor(x => x.AdvertiserId)
            .NotEmpty().WithMessage("AdvertiserId is required");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Campaign status is required")
            .Matches("^(active|paused|ended)$").WithMessage("Status must be active, paused, or ended");

        RuleFor(x => x.CpmBid)
            .GreaterThan(0).WithMessage("CPM bid must be greater than zero")
            .LessThanOrEqualTo(1000).WithMessage("CPM bid must be 1000 or less");

        RuleFor(x => x.DailyBudget)
            .GreaterThan(0).WithMessage("Daily budget must be greater than zero")
            .LessThanOrEqualTo(100000).WithMessage("Daily budget must be 100000 or less");

        RuleFor(x => x.LifetimeBudget)
            .GreaterThan(0).When(x => x.LifetimeBudget.HasValue)
            .LessThanOrEqualTo(1000000).When(x => x.LifetimeBudget.HasValue);

        RuleForEach(x => x.Ads).SetValidator(new CreateAdRequestValidator());
        RuleForEach(x => x.TargetingRules).SetValidator(new CreateTargetingRuleRequestValidator());
    }
}

public class UpdateCampaignRequestValidator : AbstractValidator<UpdateCampaignRequest>
{
    public UpdateCampaignRequestValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(255).WithMessage("Campaign name cannot exceed 255 characters");

        RuleFor(x => x.Status)
            .Matches("^(active|paused|ended)$").When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage("Status must be active, paused, or ended");

        RuleFor(x => x.CpmBid)
            .GreaterThan(0).When(x => x.CpmBid.HasValue)
            .LessThanOrEqualTo(1000).When(x => x.CpmBid.HasValue);

        RuleFor(x => x.DailyBudget)
            .GreaterThan(0).When(x => x.DailyBudget.HasValue)
            .LessThanOrEqualTo(100000).When(x => x.DailyBudget.HasValue);

        RuleFor(x => x.LifetimeBudget)
            .GreaterThan(0).When(x => x.LifetimeBudget.HasValue)
            .LessThanOrEqualTo(1000000).When(x => x.LifetimeBudget.HasValue);
    }
}

public class CreateAdRequestValidator : AbstractValidator<CreateAdRequest>
{
    public CreateAdRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Ad title is required")
            .MaximumLength(255).WithMessage("Ad title cannot exceed 255 characters");

        RuleFor(x => x.ImageUrl)
            .NotEmpty().WithMessage("Image URL is required")
            .MaximumLength(2048).WithMessage("Image URL cannot exceed 2048 characters")
            .Must(uri => Uri.IsWellFormedUriString(uri, UriKind.Absolute)).WithMessage("Image URL must be a valid absolute URL");

        RuleFor(x => x.RedirectUrl)
            .NotEmpty().WithMessage("Redirect URL is required")
            .MaximumLength(2048).WithMessage("Redirect URL cannot exceed 2048 characters")
            .Must(uri => Uri.IsWellFormedUriString(uri, UriKind.Absolute)).WithMessage("Redirect URL must be a valid absolute URL");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters");
    }
}

public class UpdateAdRequestValidator : AbstractValidator<UpdateAdRequest>
{
    public UpdateAdRequestValidator()
    {
        RuleFor(x => x.Title)
            .MaximumLength(255).WithMessage("Ad title cannot exceed 255 characters");

        RuleFor(x => x.ImageUrl)
            .MaximumLength(2048).WithMessage("Image URL cannot exceed 2048 characters")
            .Must(uri => string.IsNullOrWhiteSpace(uri) || Uri.IsWellFormedUriString(uri, UriKind.Absolute)).WithMessage("Image URL must be a valid absolute URL");

        RuleFor(x => x.RedirectUrl)
            .MaximumLength(2048).WithMessage("Redirect URL cannot exceed 2048 characters")
            .Must(uri => string.IsNullOrWhiteSpace(uri) || Uri.IsWellFormedUriString(uri, UriKind.Absolute)).WithMessage("Redirect URL must be a valid absolute URL");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters");
    }
}

public class CreateTargetingRuleRequestValidator : AbstractValidator<CreateTargetingRuleRequest>
{
    private static readonly string[] AllowedRuleTypes = new[] { "age_range", "gender", "location", "interests", "device_type", "browser", "operating_system", "time_of_day", "day_of_week" };

    public CreateTargetingRuleRequestValidator()
    {
        RuleFor(x => x.RuleType)
            .NotEmpty().WithMessage("Rule type is required")
            .Must(type => AllowedRuleTypes.Contains(type.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Rule type must be one of: {string.Join(", ", AllowedRuleTypes)}");

        RuleFor(x => x.RuleValue)
            .NotEmpty().WithMessage("Rule value is required")
            .MaximumLength(500).WithMessage("Rule value cannot exceed 500 characters");
    }
}

public class UpdateTargetingRuleRequestValidator : AbstractValidator<UpdateTargetingRuleRequest>
{
    private static readonly string[] AllowedRuleTypes = new[] { "age_range", "gender", "location", "interests", "device_type", "browser", "operating_system", "time_of_day", "day_of_week" };

    public UpdateTargetingRuleRequestValidator()
    {
        RuleFor(x => x.RuleType)
            .Must(type => string.IsNullOrWhiteSpace(type) || AllowedRuleTypes.Contains(type.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Rule type must be one of: {string.Join(", ", AllowedRuleTypes)}");

        RuleFor(x => x.RuleValue)
            .MaximumLength(500).WithMessage("Rule value cannot exceed 500 characters");
    }
}
