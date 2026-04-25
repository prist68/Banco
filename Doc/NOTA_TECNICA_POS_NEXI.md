# Nota Tecnica POS Nexi

## Scopo

Questa nota serve come documento di supporto rapido per future chat o debug sul POS Nexi integrato nel Banco.

Importante:
- **non** sostituisce `Doc/AGENTS.md`, `Doc/STRUTTURA_BANCO.md`, `Doc/DIARIO.md`;
- **non** e` una nuova fonte di verita` architetturale;
- serve solo a riportare in modo compatto la configurazione reale e le evidenze raccolte sul flusso POS.

---

## Terminale usato

- Dispositivo: `Nexi SmartPOS P61B`
- Modalita` integrazione: `Client`
- Protocollo: `Generico protocollo 17`
- Connessione: `ETH`
- IP POS: `192.168.1.233`
- Porta POS: `8081`
- Cash register ID: `00000001`
- IP cassa dichiarato lato config: `192.168.1.231`
- Porta cassa dichiarata lato config: `1470`

---

## Configurazione app Banco

### Build installata

Percorso config:
- `C:\Banco\Config\appsettings.user.json`

Valori visti in produzione/installato:
- `posIntegration.enabled = true`
- `posIpAddress = 192.168.1.233`
- `posPort = 8081`
- `cashRegisterId = 00000001`
- `printTicketOnEcr = true`
- `confirmAmountFromEcr = true`
- `amountExchangeRequired = true`

Nota:
- nella build installata e` comparso anche `checkTerminalId = true`
- nella build dev questa opzione era invece `false`

### Run da dev

Percorso config:
- `C:\Users\dileo\Desktop\test\Banco.UI.Wpf\bin\Debug\net10.0-windows\Config\appsettings.user.json`

Valori osservati in dev:
- `posIntegration.enabled = true`
- `posIpAddress = 192.168.1.233`
- `posPort = 8081`
- `cashRegisterId = 00000001`
- `printTicketOnEcr = true`
- `confirmAmountFromEcr = true`
- `amountExchangeRequired = true`
- `checkTerminalId = false`

---

## Log da controllare

### Build installata

- `C:\Banco\Log\Avvio.log`
- `C:\Banco\Log\Banco.log`
- `C:\Banco\Log\Pos.log`
- `C:\Banco\Log\Pos\Transazioni\*.log`
- `C:\Banco\Log\Fiscale.log`

### Run dev

- `C:\Users\dileo\Desktop\test\Banco.UI.Wpf\bin\Debug\net10.0-windows\Log\Avvio.log`
- `C:\Users\dileo\Desktop\test\Banco.UI.Wpf\bin\Debug\net10.0-windows\Log\Banco.log`
- `C:\Users\dileo\Desktop\test\Banco.UI.Wpf\bin\Debug\net10.0-windows\Log\Pos.log`
- `C:\Users\dileo\Desktop\test\Banco.UI.Wpf\bin\Debug\net10.0-windows\Log\Pos\Transazioni\*.log`
- `C:\Users\dileo\Desktop\test\Banco.UI.Wpf\bin\Debug\net10.0-windows\Log\Fiscale.log`

### WinEcr / fiscalizzazione

- `C:\tmp\AutoRun.txt`
- `C:\tmp\AutoRunErr.txt`

---

## Flusso corretto atteso

Per pagamento carta il comportamento corretto e`:

1. Banco apre la sessione TCP verso il POS
2. invia comando ricevuta `E` se la ricevuta e` gestita da ECR
3. riceve `ACK` sul comando ricevuta
4. invia pagamento `X`
5. il POS deve avviare davvero la richiesta pagamento sul terminale
6. se il pagamento viene accettato, il POS deve restituire l'esito finale applicativo
7. solo dopo l'esito positivo il Banco pubblica il documento e fa partire il fiscale

Regola operativa:
- se il POS **non** avvia davvero il nuovo pagamento, lo scontrino **non** deve partire
- un `NAK` sulla richiesta corrente non deve fiscalizzare

---

## Significato pratico dei frame visti nei log

### `ACK`

Significa:
- il terminale ha ricevuto correttamente il frame tecnico

Non significa:
- pagamento approvato

### `NAK`

Significa:
- il terminale ha rifiutato la richiesta corrente

Nel Banco:
- il pagamento corrente viene considerato **non avviato validamente**
- lo scontrino non parte

### `E00`

Significa:
- esito finale positivo del pagamento

Nel Banco:
- il pagamento POS viene considerato autorizzato
- il documento puo` proseguire verso publish/fiscalizzazione

---

## Tabella rapida di diagnosi

| Segnale visto | Significato pratico | Effetto nel Banco | Azione operativa |
|---|---|---|---|
| `ACK` sul solo comando `E` | Il POS ha ricevuto il frame tecnico ricevuta | Nessuna autorizzazione ancora | Attendere il seguito del flusso |
| `NAK` subito dopo `TX PAYMENT` | Il terminale rifiuta la richiesta corrente e non avvia davvero il nuovo pagamento | Lo scontrino non deve partire | Riportare il POS alla schermata iniziale e riprovare solo a terminale pulito |
| `E01 Cancelled` | Il terminale ha visto la richiesta ma la transazione e` stata annullata dal POS/cliente | Pagamento non autorizzato | Nessuno scontrino; ripetere solo se il cliente vuole davvero ripagare |
| `ACK` sul pagamento, poi `RX STREAM CLOSED` senza `E00` | Il terminale ha iniziato il flusso ma non ha consegnato un esito finale leggibile al Banco | Banco ferma il processo prima dello scontrino | Controllare stato terminale e ultimo log transazione; non considerare il pagamento buono senza conferma visibile |
| `E00` | Esito finale positivo | Banco puo` pubblicare il documento e passare al fiscale | Se poi non stampa, il problema non e` piu` il POS ma il ramo fiscale/WinEcr |
| POS OK ma `WinEcr` KO / `AutoRunErr` | Il pagamento e` passato ma il registratore non ha fiscalizzato | Documento pubblicato, scontrino non completato | Controllare `Fiscale.log`, `C:\tmp\AutoRunErr.txt`, collegamento registratore |
| Pulsante `Annulla` dal popup Banco | Annullo richiesto dall'operatore dal gestionale | Vendita aperta, nessuno scontrino | Continuare sulla scheda aggiungendo articoli o cambiando pagamento |

---

## Casi reali gia` osservati

### Caso OK

Nei log installati/dev e` stato osservato il flusso corretto:
- comando ricevuta confermato
- pagamento avviato
- esito finale positivo
- scontrino partito

### Caso `NAK` immediato

Esempio reale in dev:
- file transazione:
  - `C:\Users\dileo\Desktop\test\Banco.UI.Wpf\bin\Debug\net10.0-windows\Log\Pos\Transazioni\20260416_124847_433_192_168_1_233_8081_1740.log`

Sequenza:
- `TX E`
- `ACK` su `E`
- `TX PAYMENT`
- `NAK`

Interpretazione:
- rete OK
- IP/porta OK
- terminale raggiungibile
- **il POS ha rifiutato la richiesta corrente**

Quindi:
- il problema non e` “POS non disponibile” come rete
- il problema e` lo **stato operativo del terminale** al momento della prova

### Caso timeout

Gia` visto in passato:
- il POS prende il pagamento lato terminale
- Banco riceve solo `ACK`
- non arriva il finale entro la finestra utile
- il Banco non fiscalizza

Questo e` diverso dal `NAK`:
- nel `NAK` la richiesta corrente viene proprio rifiutata
- nel timeout il problema e` sul ritorno finale / sincronizzazione del finale POS

---

## Differenza fondamentale tra installato e dev

Quando si analizza un problema POS bisogna sempre chiarire **dove** e` avvenuta la prova:

### Installato

- usa `C:\Banco\...`
- log sotto `C:\Banco\Log`
- config sotto `C:\Banco\Config`

### Dev

- usa `C:\Users\dileo\Desktop\test\Banco.UI.Wpf\bin\Debug\net10.0-windows\...`
- log sotto `...\\Log`
- config sotto `...\\Config`

Errore classico da evitare:
- leggere i log della build installata quando la prova e` stata fatta in dev
- oppure viceversa

---

## Cosa controllare subito se il POS "non va"

### 1. Verificare se il POS e` raggiungibile

Controllare in `Avvio.log`:
- `Configurazione POS: 192.168.1.233:8081`

Se nei log transazione compare `ACK` sul comando `E`, la rete c'e`.

### 2. Verificare se il terminale rifiuta la richiesta

Controllare nel file transazione:
- presenza di `RX PACKET: NAK`

Se c'e` `NAK`:
- il terminale non ha accettato la richiesta corrente
- lo scontrino non deve partire

### 3. Verificare se il problema e` fiscale e non POS

Se il POS risulta autorizzato ma il documento non stampa:
- controllare `Fiscale.log`
- controllare `C:\tmp\AutoRunErr.txt`

Esempio reale gia` visto:
- `WinEcr ha segnalato un errore del registratore`
- dettaglio:
  - `I/O Errror <Verificare Collegamento>`

Questo e` un problema fiscale/registratore, non POS.

### 4. Verificare se il terminale era in stato corretto

Prima di riprovare:
- riportare il POS alla schermata iniziale / scambio importo
- chiudere eventuali transazioni pendenti
- evitare di rilanciare subito una nuova richiesta sopra una sessione sporca

---

## Regola pratica per future chat

Quando si apre una nuova chat per il POS, conviene passare subito:

1. se la prova e` stata fatta su:
- build installata
- run dev

2. uno di questi file:
- ultimo `Pos.log`
- ultimo file in `Pos\\Transazioni`
- ultime righe di `Banco.log`

3. il comportamento visivo del POS:
- ha mostrato la richiesta pagamento?
- ha mostrato pagamento accettato?
- e` rimasto fermo?
- ha dato errore?

Se manca il comportamento visivo del terminale, la sola lettura del log puo` non bastare a capire se il POS:
- ha rifiutato la richiesta corrente
- ha avviato il pagamento ma non ha chiuso bene il ritorno
- oppure non era nello stato operativo corretto.
