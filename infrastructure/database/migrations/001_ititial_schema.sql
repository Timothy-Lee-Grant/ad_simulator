-- ==========================================
-- 1. INFRASTRUCTURE SETUP
-- ==========================================
-- Enable the vector extension for semantic search
CREATE EXTENSION IF NOT EXISTS vector;

-- ==========================================
-- 2. SCHEMA DEFINITION
-- ==========================================

CREATE TABLE campaigns(
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    advertiser_id UUID NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'active' CHECK (status IN ('active', 'paused', 'ended')),
    cpm_bid DECIMAL(10, 4) NOT NULL CHECK (cpm_bid > 0),
    daily_budget DECIMAL(12, 2) NOT NULL CHECK (daily_budget > 0),
    spent_today DECIMAL(12, 2) DEFAULT 0 CHECK (spent_today >= 0),
    lifetime_budget DECIMAL(12, 2),
    lifetime_spent DECIMAL(12, 2) DEFAULT 0 CHECK (lifetime_spent >= 0),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE targeting_rules(
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    campaign_id UUID NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
    rule_type VARCHAR(50) NOT NULL,
    rule_value VARCHAR(255) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (campaign_id, rule_type, rule_value)
);

CREATE TABLE ads (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    campaign_id UUID NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
    title VARCHAR(255) NOT NULL,
    image_url VARCHAR(2048) NOT NULL,
    redirect_url VARCHAR(2048) NOT NULL,
    description TEXT,                -- Added for Semantic Context
    embedding vector(384),           -- Native vector column (Size 384 for common models)
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE daily_metrics (
    id BIGSERIAL PRIMARY KEY,
    campaign_id UUID NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
    date DATE NOT NULL,
    impressions BIGINT DEFAULT 0,
    clicks BIGINT DEFAULT 0,
    spend DECIMAL(12, 2) DEFAULT 0,
    UNIQUE (campaign_id, date)
);

-- ==========================================
-- 3. INDEXING
-- ==========================================
CREATE INDEX idx_campaigns_status ON campaigns(status);
CREATE INDEX idx_ads_campaign ON ads(campaign_id);
-- Vector Index (HNSW) for fast semantic search once you have lots of ads
CREATE INDEX idx_ads_embedding ON ads USING hnsw (embedding vector_cosine_ops);

-- ==========================================
-- 4. SEED DATA (Campaigns)
-- ==========================================
INSERT INTO campaigns (name, advertiser_id, status, cpm_bid, daily_budget)
VALUES 
    ('TechGear Campaign', gen_random_uuid(), 'active', 2.50, 1000.00),
    ('FashionBrand Campaign', gen_random_uuid(), 'active', 3.00, 1500.00),
    ('Test High-Bid Campaign', gen_random_uuid(), 'active', 88.00, 5000.00);

-- ==========================================
-- 5. SEED DATA (Ads with Semantic Info)
-- ==========================================
-- Add Tech Ad
INSERT INTO ads (campaign_id, title, description, image_url, redirect_url, embedding)
SELECT id, 'TechGear Pro Laptop', 'High-performance laptop for developers and creators', '/static/sunset2.png', 'https://example.com/tech', array_fill(0.1, ARRAY[384])::vector
FROM campaigns WHERE name = 'TechGear Campaign';

-- Add Fashion Ad
INSERT INTO ads (campaign_id, title, description, image_url, redirect_url, embedding)
SELECT id, 'Summer Trends', 'Latest sustainable fashion for the summer season', '/static/sunset3.png', 'https://example.com/fashion', array_fill(0.2, ARRAY[384])::vector
FROM campaigns WHERE name = 'FashionBrand Campaign';

-- Add Test High-Bid Ads
INSERT INTO ads (campaign_id, title, description, image_url, redirect_url, embedding)
SELECT id, 'Sunset Premium Ad', 'A high-priority test ad for auction verification', '/static/sunset1.png', 'https://example.com/test', array_fill(0.3, ARRAY[384])::vector
FROM campaigns WHERE name = 'Test High-Bid Campaign';