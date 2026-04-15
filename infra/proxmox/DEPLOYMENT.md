# Deployment su Proxmox

## VM consigliate
### VM 1 - edge/app
- Ubuntu Server LTS
- Nginx
- .NET runtime
- FriendMap.Api
- FriendMap.Admin

### VM 2 - data
- PostgreSQL + PostGIS
- Redis
- backup agent

## Backup
- snapshot Proxmox giornaliero
- dump PostgreSQL notturno
- retention minima 14 giorni

## Sicurezza
- firewall host + firewall guest
- SSH key only
- fail2ban
- reverse proxy con rate limiting su `/api/social/*`
- logging centralizzato

## Osservabilità
- health endpoint
- log applicativi JSON
- metriche CPU/RAM/IO per VM
- alert su spazio disco e riavvii inattesi

## Scaling iniziale
- verticale prima di orizzontale
- separare DB dalla VM app
- cache Redis per map responses e venue cards
