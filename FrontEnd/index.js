import express from 'express';
import {URLSearchParams} from 'url';
import path from 'path';
import {fileURLToPath} from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const app = express();
app.set('view engine', 'ejs');
app.set('views', path.join(__dirname, 'views'));

const BID_ENGINE_URL = process.env.BID_ENGINE_URL || 'http://bid-engine:8081';

app.get('/', async (req, res) => {
  // Homepage: show description and up to two ads
  const userId = req.query.userId || 'user_local_123';
  const placementId = req.query.placementId || 'homepage_banner';

  try {
    // Fetch two bids in parallel to feature two ads
    const fetchBid = async () => {
      const response = await fetch(`${BID_ENGINE_URL}/api/bid`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ userId, placementId })
      });

      if (response.status === 204) return null;
      if (!response.ok) return null;
      return await response.json();
    };

    const [ad1, ad2] = await Promise.all([fetchBid(), fetchBid()]);
    const ads = [ad1, ad2].filter(Boolean);

    res.render('home', { ads, userId });
  } catch (err) {
    console.error('Error fetching bids:', err);
    res.status(502).send('Bid engine unreachable');
  }
});

// Click proxy route: records click with the bid engine, then redirects the user
app.get('/click', async (req, res) => {
  const { redirect, campaignId, adId, userId } = req.query;
  if (!redirect) return res.status(400).send('missing redirect');

  // Notify BidEngine of click
  try {
    const params = new URLSearchParams();
    if (campaignId) params.set('campaignId', campaignId);
    if (adId) params.set('adId', adId);
    if (userId) params.set('userId', userId);

    await fetch(`${BID_ENGINE_URL}/api/bid/User_Click_Event?${params.toString()}`);
  } catch (err) {
    console.error('Failed to notify bid engine about click', err);
    // continue to redirect regardless; we don't want to block the user
  }

  // Redirect the browser to the actual ad destination
  res.redirect(redirect.toString());
});

// Static assets
app.use('/static', express.static(path.join(__dirname, 'static')));

const port = process.env.PORT || 3000;
app.listen(port, () => {
  console.log(`FrontEnd listening on port ${port}, talking to ${BID_ENGINE_URL}`);
});
