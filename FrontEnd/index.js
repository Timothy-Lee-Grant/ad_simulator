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
  try {
    // Example: userId and placementId could come from session/cookies or query params
    const userId = req.query.userId || 'user_local_123';
    const placementId = req.query.placementId || 'homepage_banner';

    const response = await fetch(`${BID_ENGINE_URL}/api/bid`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ userId, placementId })
    });

    if (response.status === 204) {
      return res.render('index', { ad: null });
    }

    if (!response.ok) {
      return res.status(502).send('Bid engine error');
    }

    const ad = await response.json();

    res.render('index', { ad, userId });
  } catch (err) {
    console.error('Error fetching bid:', err);
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
