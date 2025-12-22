-- Create a high-bid test campaign
INSERT INTO campaigns (name, advertiser_id, status, cpm_bid, daily_budget, lifetime_budget)
VALUES (
    'Test Campaign',
    gen_random_uuid(),
    'active',
    88.00,          -- very high CPM bid so it wins
    1000.00,        -- daily budget
    50000.00        -- lifetime budget
);

-- Associate multiple ads with the Test Campaign
INSERT INTO ads (campaign_id, title, image_url, redirect_url)
SELECT id, 'Sunset Ad 1', '/static/sunset1.png', 'https://example.com/test1'
FROM campaigns WHERE name = 'Test Campaign'
UNION ALL
SELECT id, 'Sunset Ad 2', '/static/sunset2.png', 'https://example.com/test2'
FROM campaigns WHERE name = 'Test Campaign'
UNION ALL
SELECT id, 'Sunset Ad 3', '/static/sunset3.png', 'https://example.com/test3'
FROM campaigns WHERE name = 'Test Campaign';
