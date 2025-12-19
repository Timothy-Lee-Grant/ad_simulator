# FrontEnd microservice

Simple Node.js Express frontend that requests an ad from the BidEngine and renders it on a homepage.

How it works:
- GET / -> POST to `${BID_ENGINE_URL}/api/bid` (defaults to `http://bid-engine:8081`) with `userId` and `placementId`.
- It renders the ad (title, image, bid price) if present.
- When the user clicks the ad, the link points to `/click` which notifies the BidEngine click endpoint and redirects the user to the ad's redirect URL.

Running locally (with docker-compose):

1. Start the stack:

   ```bash
   docker compose up --build
   ```

2. Visit http://localhost:3000 to see the homepage and served ad.

Notes:
- Frontend expects the BidEngine to be available at `http://bid-engine:8081` (inside compose). Use `BID_ENGINE_URL` env var to override.
