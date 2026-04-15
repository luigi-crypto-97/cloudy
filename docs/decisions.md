# Architectural decisions

1. **iOS-first**: mobile first, Android in stand-by.
2. **No satellite maps**: stile vettoriale pulito.
3. **Affluenza visiva**: nuvoletta/blobbino blu sopra venue con intensità progressiva.
4. **No background location in MVP**: solo check-in manuale e foreground.
5. **Privacy first**: breakdown demografico solo sopra soglia K.
6. **Hosting Proxmox**: VM Linux, Nginx reverse proxy, Postgres/PostGIS, Redis.
7. **Admin separato**: web dashboard distinta dall'app.
8. **Friend-only visibility**: posizione/intenzione dettagliata solo tra amici.
