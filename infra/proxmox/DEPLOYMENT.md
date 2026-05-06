# Deployment su Proxmox

## Deploy rapido su VM Linux

Esempio su Ubuntu Server LTS dentro Proxmox:

```bash
sudo apt update
sudo apt install -y git ca-certificates curl gnupg

# Docker Engine + Compose plugin
sudo install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg \
  | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
sudo chmod a+r /etc/apt/keyrings/docker.gpg
echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" \
  | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
sudo apt update
sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
sudo usermod -aG docker "$USER"
newgrp docker

# Repo
git clone https://github.com/luigi-crypto-97/cloudy.git
cd cloudy

# Config segreta produzione
cp .env.local.example .env.local
nano .env.local

# Avvio unico: Postgres/PostGIS + Redis + API + Admin
./scripts/server-up.sh
```

Comandi utili:

```bash
docker compose -f infra/docker-compose.yml ps
docker compose -f infra/docker-compose.yml logs -f api admin
./scripts/server-down.sh
git pull --ff-only
./scripts/server-up.sh
```

Endpoint locali sulla VM:

- API: `http://127.0.0.1:8080`
- Swagger/API interface: `http://127.0.0.1:8080/swagger`
- Admin console: `http://127.0.0.1:8090`
- PostgreSQL: `127.0.0.1:5432`
- Redis: `127.0.0.1:6379`

## Porte da aprire

Scenario consigliato con Caddy/Nginx sulla VM:

- `22/tcp` solo dal tuo IP, per SSH.
- `80/tcp` pubblico, per HTTP/Let’s Encrypt redirect.
- `443/tcp` pubblico, per HTTPS API/Admin.
- `8080/tcp` chiusa verso Internet, usata solo dal reverse proxy locale.
- `8090/tcp` chiusa verso Internet, usata solo dal reverse proxy locale.
- `5432/tcp` chiusa verso Internet.
- `6379/tcp` chiusa verso Internet.

Solo per debug temporaneo, se non hai ancora configurato Caddy/Nginx, puoi aprire `8080/tcp` e `8090/tcp` al tuo IP pubblico. Non lasciarle aperte a `0.0.0.0/0`.

## Reverse proxy

Con Caddy puoi pubblicare API e admin così:

```caddyfile
api.iron-quote.it {
    reverse_proxy 127.0.0.1:8080
}

admin.iron-quote.it {
    reverse_proxy 127.0.0.1:8090
}
```

Poi:

```bash
sudo apt install -y debian-keyring debian-archive-keyring apt-transport-https curl
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' \
  | sudo gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' \
  | sudo tee /etc/apt/sources.list.d/caddy-stable.list
sudo apt update
sudo apt install -y caddy
sudo nano /etc/caddy/Caddyfile
sudo systemctl reload caddy
```

Nel DNS devi puntare `api.iron-quote.it` e, se lo usi, `admin.iron-quote.it` all’IP pubblico del server/VM.

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
