using System;

namespace BidEngine.Models;

public class BidRequest
{
    /// <summary>
    /// Unique user identifier
    /// </summary>
    public string UserId { get; set; } = string.Empty;
    
    /// <summary>
    /// Where the ad will be displayed (e.g., "homepage_banner", "sidebar_300x250")
    /// </summary>
    public string PlacementId { get; set; } = string.Empty;
    
    /// <summary>
    /// User's country code (ISO 3166-1 alpha-2)
    /// Example: "US", "GB", "DE"
    /// </summary>
    public string? CountryCode { get; set; }
    
    /// <summary>
    /// Device type: "mobile", "tablet", or "desktop"
    /// </summary>
    public string? DeviceType { get; set; }
    
    /// <summary>
    /// User interests or demographic data
    /// </summary>
    public Dictionary<string, object> UserAttributes { get; set; } = new();
}

public class BidResponse
{
    public Guid CampainId {get; set;}
    public Guid AdId {get; set;}
    public AdContent AdContent  {get; set;} = null!;
    public decimal BidPrice  {get; set;}
    public double Confidence {get; set;}

}

public class AdContent
{
    public string ImageUrl {get; set;} = string.Empty;
    public string Title {get; set;} = string.Empty;
    public string RedirectUrl {get; set;} = string.Empty;
}