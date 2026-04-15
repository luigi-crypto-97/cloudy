# Privacy e aggregazione tecnica

## Regole MVP
- Nessuna posizione individuale mostrata ai non amici.
- I breakdown demografici non sono esposti se `peopleEstimate < K`.
- Default K consigliato nello starter: `20`.
- Se K non è raggiunta:
  - mostrare solo densità qualitativa
  - nascondere JSON demografici
  - mantenere bubble intensity, ma senza dettaglio age/gender

## Bucketizzazione
- Snapshots ogni 15 minuti.
- Sorgenti:
  - check-in attivi
  - intenzioni compatibili con finestra temporale
- Formula MVP:
  - `estimated = activeCheckIns + activeIntentions`

## Bubble UI
- 0 -> nessuna nuvoletta
- 1-4 -> blu chiarissimo
- 5-14 -> blu chiaro
- 15-29 -> blu medio
- 30-59 -> blu pieno
- 60+ -> blu molto scuro

## Evoluzione futura
- weighting tra intention e check-in
- smoothing temporale
- confidence score
- anomaly detection
