










INSERT INTO ads (campaign_id, title, image_url, redirect_url)
SELECT id, 'TechGear Ultra Laptop', 'https://cdn.example.com/techgear-ultra.jpg', 'https://techgear.example.com/ultra'
FROM campaigns
WHERE name = 'TechGear Banner Campaign';

-- New Fashion Brand advert
INSERT INTO ads (campaign_id, title, image_url, redirect_url)
SELECT id, 'Summer Collection 2025', 'https://cdn.example.com/fashion-summer.jpg', 'https://fashionbrand.example.com/summer' 
FROM campaigns 
WHERE name = 'FashionBrand Mobile Campaign';

-- New GameStudio ad 
INSERT INTO ads (campaign_id, title, image_url, redirect_url) 
SELECT id, 'Play Galaxy Quest Now!', 'https://cdn.example.com/galaxy-quest.jpg', 'https://gamestudio.example.com/quest' 
FROM campaigns 
WHERE name = 'GameStudio App Install Campaign';

