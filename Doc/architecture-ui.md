# UI Architecture Guidelines

## Obiettivo

L'applicazione deve essere progettata in modo che la logica applicativa resti indipendente dal framework grafico utilizzato.

L'obiettivo è permettere:
- manutenzione più semplice
- testabilità migliore
- minore accoppiamento tra UI e logica
- possibilità futura di sostituire WPF con un'altra UI, ad esempio Avalonia, senza riscrivere i servizi e la business logic

## Principio generale

La UI deve essere un layer di presentazione.

La logica del dominio, i servizi applicativi, i repository e le regole di business non devono dipendere da classi o concetti specifici di WPF.

## Separazione dei livelli

### Domain / Core

Contiene:
- entità
- value object
- enum
- regole di dominio
- contratti stabili

Non deve contenere:
- riferimenti a WPF
- concetti grafici
- finestre, controlli, dialog o dispatcher

### Application / Services

Contiene:
- orchestrazione dei casi d'uso
- accesso a database, SQLite, legacy FM, integrazioni esterne
- logica di calcolo
- regole operative del modulo

Non deve contenere:
- codice visuale
- gestione diretta di controlli UI
- apertura diretta di finestre o popup

### ViewModel

Contiene:
- stato della schermata
- proprietà bindabili
- comandi
- coordinamento tra UI e servizi

Può contenere:
- stato di presentazione
- selezioni
- messaggi descrittivi
- richieste di apertura dialog tramite astrazioni

Non deve contenere:
- riferimenti diretti a `Window`, `UserControl`, `MessageBox`, `Application.Current`, `Dispatcher`
- logica di rendering
- istanziazione diretta di finestre WPF

### UI Layer

Contiene:
- file `.xaml`
- code-behind `.xaml.cs`
- stili
- template
- converter grafici
- dialog e modali concreti
- wiring degli eventi visuali

Questo è l'unico layer in cui sono ammessi riferimenti diretti a WPF.

## Regole per i file XAML

I file `.xaml` devono definire solo:
- layout
- binding
- stili
- risorse
- composizione visuale

Non devono contenere:
- logica di business
- regole operative
- accesso dati
- comportamento che non sia strettamente visuale

## Regole per i file xaml.cs

I file `.xaml.cs` devono contenere solo:
- inizializzazione view
- wiring di eventi UI
- gestione di interazioni strettamente visive
- apertura/chiusura finestre
- adattamenti locali legati al framework grafico

Non devono contenere:
- regole di business
- query database
- logica di calcolo
- flussi di dominio

## Gestione di modali e dialog

I modali sono consentiti, ma devono essere trattati come un dettaglio dell'host UI.

### Regola fondamentale

Il ViewModel non deve conoscere l'implementazione concreta del modale.

### Approcci consentiti

Un ViewModel può:
- sollevare un evento
- usare una callback
- usare un servizio astratto come `IDialogService` o `IModalService`
- esprimere l'intento di aprire una scelta o chiedere conferma

La UI concreta si occupa di:
- istanziare la finestra
- mostrare il modale
- raccogliere il risultato
- riportarlo al ViewModel

### Approcci vietati

Nel ViewModel non è ammesso:
- `new SomeWindow()`
- `MessageBox.Show(...)`
- accesso diretto a `Application.Current`
- uso diretto di API di visual tree o dispatcher WPF, salvo casi eccezionali esplicitamente isolati

## Navigazione e apertura schermate

La navigazione tra moduli deve essere mediata da:
- shell
- servizi di navigazione
- eventi di richiesta apertura
- interfacce dedicate

Un modulo non deve dipendere direttamente dalla concreta implementazione della shell WPF.

## Stato e comandi

I comandi devono rappresentare intenzioni applicative, non gesti grafici.

Esempi corretti:
- `SaveCommand`
- `RefreshHistoryCommand`
- `OpenDocumentRequested`
- `RecalculateCustomerBalanceCommand`

Esempi da evitare:
- `OpenWindowCommand`
- `ShowWpfDialogCommand`

## Portabilità futura verso Avalonia

Ogni nuovo modulo deve essere progettato assumendo che in futuro la UI possa essere sostituita.

Per questo:
- evitare riferimenti WPF fuori dal layer UI
- mantenere i ViewModel il più possibile framework-agnostic
- isolare popup, modali e dialog dietro astrazioni
- non legare i servizi applicativi a oggetti visuali

## Eccezioni

Eccezioni locali sono ammesse solo se:
- chiaramente motivate
- circoscritte
- documentate
- facilmente estraibili in un refactor successivo

Le eccezioni non devono diventare nuovo standard progettuale.

## Regola operativa per nuovi moduli

Ogni nuovo modulo deve essere costruito con questa struttura minima:
- logica e dati fuori dalla UI
- ViewModel senza dipendenze dirette da WPF
- modali gestiti tramite astrazione o host UI
- XAML limitato a struttura e presentazione

## Pattern per tab compatte da gestionale

Quando una schermata e` destinata a diventare una `tab` dentro una scheda piu` grande, l'impaginazione deve seguire un pattern compatto e denso, non da pagina dashboard.

### Regole pratiche

- la testata usa sempre due colonne:
  - sinistra: titolo e contesto sintetico;
  - destra: 2-3 azioni principali allineate a battuta destra.
- evitare card grandi di stato se basta una singola fascia informativa compatta.
- i testi di supporto devono essere brevi, non descrittivi: massimo una riga quando possibile.
- campi, textbox, combo, pulsanti e righe griglia devono usare altezze uniformi e compatte.
- una sezione deve occupare solo lo spazio che serve ai dati davvero utili.
- i pannelli secondari o provvisori non vanno raffinati come contenuto definitivo.
- il layout deve essere costruito a sezioni nette, ma senza spazio morto tra una sezione e l'altra.

### Densita` consigliata

- input compatti: altezza circa `26-30`
- toolbar sezione: pulsanti `28-34`
- header tab: titolo a sinistra, azioni a destra, senza fascia vuota centrale
- griglie/liste: righe compatte, header corti, niente colonne verbose se una label breve basta

### Cosa evitare

- card alte usate solo per mostrare due righe di testo
- descrizioni lunghe ripetute sopra a dati gia` autoesplicativi
- pulsanti sparsi in piu` punti della stessa schermata
- layout da pagina marketing o dashboard in moduli operativi

## Obiettivo finale

La sostituzione della UI deve richiedere la riscrittura della presentazione, non della logica.
