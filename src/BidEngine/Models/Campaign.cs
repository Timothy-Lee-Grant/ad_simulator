using System;

namespace BidEngine.Models;

/*
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    advertiser_id UUID NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'active' CHECK (status IN ('active', 'paused', 'ended')),
    cpm_bid DECIMAL(10, 4) NOT NULL CHECK (cpm_bid>0),
    daily_budget DECIMAL(12, 2) NOT NULL CHECK (daily_budget > 0),
    lifetime_budget DECIMAL(12, 2),
    spent_today DECIMAL(12, 2) DEFAULT 0 CHECK (spent_today >= 0),
    lifetime_spent DECIMAL(12, 2) DEFAULT 0 CHECK (lifetime_spent >= 0),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
*/
/// <summary>
/// Represents an advertising campaign.
/// Example: A campaign to promote "TechGear Pro" with a $2.50 CPM bid.
/// </summary>
public class Campaign
{
    public Guid Id {get; set;}
    public string Name {get; set;} = string.Empty;
    public Guid AdvertiserId {get; set;}
    public string Status {get; set;} = "active";
    
    /// <summary>
    /// Cost Per Mille (CPM) - the bid price per 1,000 impressions
    /// Example: 2.50 means $2.50 per 1,000 users who see this ad
    /// </summary>
    public decimal CpmBid {get; set;}
    
    /// <summary>
    /// Daily budget limit in dollars
    /// </summary>
    public decimal DailyBudget { get; set; }
    
    /// <summary>
    /// Total budget across entire campaign lifetime
    /// </summary>
    public decimal? LifetimeBudget { get; set; }
    
    /// <summary>
    /// How much we've spent today in dollars
    /// </summary>
    public decimal SpentToday { get; set; }
    
    /// <summary>
    /// Total lifetime spending
    /// </summary>
    public decimal LifetimeSpent { get; set; }

    public DateTime CreatedAt { get; set; }
    
    public DateTime UpdatedAt { get; set; }

    //create a list of ads which belong to this specific campain
    public List<Ad> Ads {get; set;}= new();

    //create a list of ____
    public List<TargetingRule> TargetingRules { get; set; } = new();

    public bool CanServe =>
    Status == "active" &&
    DailyBudget > SpentToday &&
    (LifetimeBudget == null || LifetimeBudget < LifetimeSpent.Value);

}

/*
CREATE TABLE ads (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    campaign_id UUID NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
    title VARCHAR(255) NOT NULL,
    image_url VARCHAR(2048) NOT NULL,
    redirect_url VARCHAR(2048) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
*/
public class Ad
{
    public Guid Id {get; set;}
    public Guid CampaignId {get; set;}
    public string Title {get; set;}
    public string ImageUrl {get; set;} = string.Empty;
    public string RedirectUrl {get; set;} = string.Empty;
    public DateTime CreatedAt {get; set;}

    public Campaign Campaign {get; set;} = null!;
}

/*
CREATE TABLE campaign_targeting_rules(
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    campaign_id UUID NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
    rule_type VARCHAR(50) NOT NULL,
    rule_value VARCHAR(255) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (campaign_id, rule_type, rule_value)
);
*/
public class TargetingRule
{
    public Guid Id {get; set;}
    public Guid CampaignId {get; set;}
    public string RuleType {get; set;}
    public string RuleValue {get; set;}
    public DateTime CreatedAt {get; set;}
    public Campaign Campaign {get; set;} = null!;
}