-- Insert TechGear ad using local static image
INSERT INTO ads (campaign_id, title, image_url, redirect_url)
SELECT id, 'Check Out TechGear Pro', '/static/sunset2.png', 'https://techgear.example.com/pro'
FROM campaigns
WHERE name = 'FashionBrand Mobile Campaign';

-- Insert FashionBrand ad using local static image
INSERT INTO ads (campaign_id, title, image_url, redirect_url)
SELECT id, 'Latest Fashion Trends', '/static/sunset3.png', 'https://fashionbrand.example.com/new'
FROM campaigns
WHERE name = 'FashionBrand Mobile Campaign';

-- Insert GameStudio ad using local static image
INSERT INTO ads (campaign_id, title, image_url, redirect_url)
SELECT id, 'Play Galaxy Quest Now!', '/static/sunset1.png', 'https://gamestudio.example.com/quest'
FROM campaigns
WHERE name = 'FashionBrand Mobile Campaign';
