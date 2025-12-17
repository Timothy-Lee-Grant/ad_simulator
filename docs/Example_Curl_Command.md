curl -X POST http://localhost:8081/api/bid \
  -H "Content-Type: application/json" \
  -d '{
    "UserId": "user123",
    "PlacementId": "homepage_banner",
    "CountryCode": "US",
    "DeviceType": "mobile",
    "UserAttributes": {
      "interest": "sports",
      "premiumUser": true
    },
    "Age": 25,
    "AssumedGender": {
      "male": 80,
      "female": 20
    }
  }'
