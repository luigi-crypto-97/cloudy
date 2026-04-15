# Endpoint summary

## Public / app
- `GET /api/venues/map?minLat=&minLng=&maxLat=&maxLng=`
- `GET /api/venues/{venueId}`
- `POST /api/social/intentions`
- `POST /api/social/check-ins`
- `POST /api/social/tables`
- `POST /api/social/tables/{tableId}/join?userId=`

## Admin
- `GET /api/admin/dashboard`
- `GET /api/admin/moderation/queue`
- `POST /api/admin/moderation/{reportId}/resolve`

## Endpoint non ancora inclusi ma consigliati
- auth/signup
- auth/apple-login
- auth/refresh-token
- users/{id}/friends
- venues/{id}/claim
- admin/users/search
- admin/users/{id}/suspend
- admin/venues/{id}/feature
