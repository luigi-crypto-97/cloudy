# Endpoint summary

## Public / app
- `GET /api/venues/map?minLat=&minLng=&maxLat=&maxLng=`
- `GET /api/venues/{venueId}`

## Auth
- `POST /api/auth/dev-login`
- `GET /api/auth/me`

## Authenticated app
- `POST /api/social/intentions`
- `POST /api/social/check-ins`
- `POST /api/social/tables`
- `POST /api/social/tables/{tableId}/join?userId=`
- `POST /api/notifications/device-tokens`
- `POST /api/notifications/test`

## Admin
- `GET /api/admin/dashboard`
- `GET /api/admin/moderation/queue`
- `POST /api/admin/moderation/{reportId}/resolve`
- `GET /api/admin/venues?q=`
- `POST /api/admin/venues`
- `PUT /api/admin/venues/{venueId}`
- `DELETE /api/admin/venues/{venueId}`

## Endpoint non ancora inclusi ma consigliati
- auth/signup
- auth/apple-login
- auth/refresh-token
- users/{id}/friends
- venues/{id}/claim
- admin/users/search
- admin/users/{id}/suspend
- admin/venues/{id}/feature
