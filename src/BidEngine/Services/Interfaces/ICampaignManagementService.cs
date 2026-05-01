using BidEngine.Shared.DTOs;

namespace BidEngine.Services.Interfaces;

public interface ICampaignManagementService
{
    Task<PagedCampaignListResponse> ListCampaignsAsync(int page = 1, int pageSize = 20, string? status = null, Guid? advertiserId = null);
    Task<CampaignDto?> GetCampaignAsync(Guid campaignId);
    Task<CampaignDto> CreateCampaignAsync(CreateCampaignRequest request, Guid userId);
    Task<CampaignDto?> UpdateCampaignAsync(Guid campaignId, UpdateCampaignRequest request, Guid userId);
    Task<bool> DeleteCampaignAsync(Guid campaignId, Guid userId);

    Task<AdDto> CreateAdAsync(Guid campaignId, CreateAdRequest request, Guid userId);
    Task<AdDto?> UpdateAdAsync(Guid adId, UpdateAdRequest request, Guid userId);
    Task<bool> DeleteAdAsync(Guid adId, Guid userId);

    Task<TargetingRuleDto> CreateTargetingRuleAsync(Guid campaignId, CreateTargetingRuleRequest request, Guid userId);
    Task<TargetingRuleDto?> UpdateTargetingRuleAsync(Guid targetingRuleId, UpdateTargetingRuleRequest request, Guid userId);
    Task<bool> DeleteTargetingRuleAsync(Guid targetingRuleId, Guid userId);
}
