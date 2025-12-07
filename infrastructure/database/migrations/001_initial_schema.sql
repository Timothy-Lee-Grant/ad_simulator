





CREATE TABLE campaigns(
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
);

-- Add targeting rules for first campaign (TechGear)
CREATE TABLE campaign_targeting_rules(
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    campaign_id UUID NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
    rule_type VARCHAR(50) NOT NULL,
    rule_value VARCHAR(255) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (campaign_id, rule_type, rule_value)
);

-- Create sample ads for campaigns
CREATE TABLE ads (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    campaign_id UUID NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
    title VARCHAR(255) NOT NULL,
    image_url VARCHAR(2048) NOT NULL,
    redirect_url VARCHAR(2048) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);


CREATE TABLE daily_metrics (
  id BIGSERIAL PRIMARY KEY,
  campaign_id UUID NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
  date DATE NOT NULL,
  impressions BIGINT DEFAULT 0 CHECK (impressions >= 0),
  clicks BIGINT DEFAULT 0 CHECK (clicks >= 0),
  spend DECIMAL(12, 2) DEFAULT 0 CHECK (spend >= 0),
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  UNIQUE (campaign_id, date)
);


CREATE TABLE events_log (
  id BIGSERIAL PRIMARY KEY,
  event_id UUID NOT NULL UNIQUE,
  event_type VARCHAR(20) NOT NULL CHECK (event_type IN ('impression', 'click')),
  campaign_id UUID NOT NULL REFERENCES campaigns(id),
  ad_id UUID NOT NULL REFERENCES ads(id),
  user_id VARCHAR(255) NOT NULL,
  placement_id VARCHAR(255) NOT NULL,
  bid_price DECIMAL(10, 4),
  timestamp TIMESTAMP NOT NULL,
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- need to investigate 
CREATE INDEX idx_campaigns_advertiser ON campaigns(advertiser_id);
CREATE INDEX idx_campaigns_status ON campaigns(status);
CREATE INDEX idx_targeting_campaign ON campaign_targeting_rules(campaign_id);
CREATE INDEX idx_targeting_type ON campaign_targeting_rules(rule_type);
CREATE INDEX idx_ads_campaign ON ads(campaign_id);
CREATE INDEX idx_metrics_campaign_date ON daily_metrics(campaign_id, date);
CREATE INDEX idx_metrics_date ON daily_metrics(date);
CREATE INDEX idx_events_campaign ON events_log(campaign_id);
CREATE INDEX idx_events_timestamp ON events_log(timestamp DESC);
CREATE INDEX idx_events_type ON events_log(event_type);


-- test data insertion 

-- Seed Data: Create Sample Campaigns
INSERT INTO campaigns (name, advertiser_id, status, cpm_bid, daily_budget, lifetime_budget)
VALUES
  (
    'TechGear Banner Campaign',
    gen_random_uuid(),
    'active',
    2.50,
    1000.00,
    50000.00
  ),
  (
    'FashionBrand Mobile Campaign',
    gen_random_uuid(),
    'active',
    3.00,
    1500.00,
    75000.00
  ),
  (
    'GameStudio App Install Campaign',
    gen_random_uuid(),
    'paused',
    1.80,
    500.00,
    25000.00
  );

-- Add targeting rules for first campaign (TechGear)
INSERT INTO campaign_targeting_rules (campaign_id, rule_type, rule_value)
SELECT id, 'country', 'US' FROM campaigns WHERE name = 'TechGear Banner Campaign'
UNION ALL
SELECT id, 'device_type', 'desktop' FROM campaigns WHERE name = 'TechGear Banner Campaign';

-- Create sample ads for campaigns
INSERT INTO ads (campaign_id, title, image_url, redirect_url)
SELECT id, 'Check Out TechGear Pro', 'https://cdn.example.com/techgear-pro.jpg', 'https://techgear.example.com/pro'
FROM campaigns WHERE name = 'TechGear Banner Campaign'
UNION ALL
SELECT id, 'Latest Fashion Trends', 'https://cdn.example.com/fashion-banner.jpg', 'https://fashionbrand.example.com/new'
FROM campaigns WHERE name = 'FashionBrand Mobile Campaign';