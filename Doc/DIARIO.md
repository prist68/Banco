# DIARIO.md

## Regole di programmazione
FIX E PROPOSTE MODULO
- Non comportarti come un esecutore di task, ma come uno sviluppatore senior responsabile della qualità logica, operativa e architetturale della soluzione. Nei fix devi cercare la causa reale ed evitare toppe locali. Nei nuovi moduli devi analizzare la proposta, individuare debolezze e suggerire miglioramenti concreti, operativi e coerenti con il progetto, senza aspettare che venga chiesto un parere.

## 2026-04-17 - Retry immediato del pagamento POS solo sugli errori sicuri

Decisione chiusa:
- quando il pagamento POS fallisce con errore rapido e non ambiguo (`NAK`, terminale non raggiungibile, errore tecnico), il popup Banco mostra `Riprova` e permette di rilanciare subito il pagamento senza aspettare la chiusura automatica del messaggio;
- il retry immediato non viene mai offerto sui casi ambigui o pericolosi (`FinalResultNotConfirmed`, warning manuale, rifiuto carta, annullo operatore), per evitare doppi addebiti.

Regola viva:
- il popup POS deve distinguere chiaramente tra `attesa`, `annullo` e `retry immediato`;
- ogni percorso di retry deve restare confinato ai casi in cui e` certo che il terminale non abbia ancora completato un incasso valido.

## 2026-04-18 - Scontrino fiscale con nominativo cliente e saldo punti prima/dopo

Decisione chiusa:
- lo scontrino fiscale WinEcr stampa ora nel footer il nominativo pulito del cliente reale, senza riusare l'etichetta tecnica Banco con `OID - ...`;
- se il cliente ha la raccolta punti attiva, il footer stampa anche `Punti prima` e `Punti dopo`;
- `Punti dopo` viene calcolato come saldo storico + punti maturati dal documento, meno gli eventuali punti richiesti da un premio gia` applicato sullo stesso documento.

Regola viva:
- i dati loyalty stampati sullo scontrino devono essere derivati dal legacy e dal documento corrente, non da stringhe UI temporanee;
- il footer fiscale deve restare compatto, leggibile e coerente con il saldo effettivo mostrato al cliente a fine acquisto.

## 2026-04-18 - Scansione barcode Banco consumata in modo atomico

Decisione chiusa:
- nella ricerca articoli del Banco, una scansione barcode riconosciuta deve essere consumata subito come token atomico del flusso reale;
- il riconoscimento barcode invalida la ricerca live pendente e libera immediatamente il box per la scansione successiva, cosi` due letture ravvicinate non vengono concatenate nello stesso campo.

Regola viva:
- il fix barcode va nel punto di riconoscimento/consumo della scansione, non in un clear tardivo della textbox;
- la ricerca descrittiva live multi-termine deve restare invariata.

## 2026-04-18 - Banco e modali riallineati a brush semantiche condivise

Decisione chiusa:
- la schermata `Vendita Banco`, i popup operativi collegati e le principali modali Banco non devono piu` dipendere da hardcode locali di colore per superfici, input, banner, stati e overlay;
- i colori Banco usati da header, pagamenti, shortcut, highlight e dialog devono vivere in brush semantiche condivise dell'applicazione, richiamate via `DynamicResource`.

Regola viva:
- i futuri ritocchi UI Banco non devono reintrodurre colori hardcoded dentro view e dialog quando esiste gia` una brush condivisa equivalente;
- la predisposizione tema chiaro/scuro del modulo Banco passa prima da resource semantiche condivise, non da override locali sparsi.

## 2026-04-17 - Listino esplicito nella testata Banco e vendita coerente col legacy

Decisione chiusa:
- la schermata `Vendita Banco` espone ora il listino come scelta operativa esplicita nella propria testata, usando un controllo compatto coerente col design system e mostrando solo il nome listino;
- il default della nuova vendita e` il listino `Web` letto dal legacy `listino`, senza piu` lasciare la scelta prezzo implicita dentro il solo resolver articolo;
- se il cliente selezionato ha un `Clientelistino` sul legacy, Banco seleziona automaticamente quel listino;
- il documento Banco mantiene ora il `Listino` come dato proprio: la riapertura da `documento`, la persistenza locale tecnica, il ricalcolo prezzi riga e il publish su `db_diltech` restano allineati allo stesso listino scelto.

Regola viva:
- ogni cambio listino deve riallineare la vendita corrente, non solo gli articoli aggiunti dopo;
- nuovi listini creati sul legacy devono comparire nell'elenco Banco senza hardcode locale;
- il listino scelto va trattato come dato reale del documento Banco, non come dettaglio UI temporaneo o dedotto al volo dai prezzi articolo.

## 2026-04-17 - Modale cassa dal pulsante rapido centrale del Banco

Decisione chiusa:
- il pulsante rapido centrale in basso a destra della scheda `Banco` non apre piu` la configurazione fiscale in shell, ma una modale operativa dedicata alle opzioni cassa;
- la modale usa lo stesso linguaggio visivo dei dialog applicativi Banco e mostra il registratore reale `Ditron Elsi Retail R1`;
- la prima versione espone:
  - scelta `Giornale giornaliero`: `Corta` (default), `Media`, `Lunga`;
  - azione `Stampa giornale`;
  - azione `Stampa e chiusura cassa` con invio AdE.
- il collegamento fiscale reale passa da `WinEcrAutoRunService` sullo stesso file `AutoRun` gia` usato per gli scontrini:
  - `REPORT num=2/4/3, modo=0` per giornale `corto/medio/lungo`;
  - `AZZGIO tipo=2/3/1` per chiusura fiscale `corta/media/lunga`.

Regola viva:
- le opzioni cassa operative del Banco devono vivere in una modale applicativa dedicata, non in popup improvvisati o in redirect automatici verso schermate tecniche di configurazione;
- la modale puo` crescere con nuove opzioni nei prossimi step senza cambiare il punto di ingresso del pulsante rapido centrale.

## 2026-04-17 - Chiusura Banco riallineata al riciclo della stessa tab e quick action stampa

Decisione chiusa:
- la chiusura operativa di `Salva`, `Cortesia` e `Scontrino` usa ora la stessa regola: a operazione conclusa la tab Banco corrente viene riciclata in-place come `Nuovo documento`;
- il flusso standard non apre piu` automaticamente una nuova pagina Banco e non mantiene la vendita chiusa come scheda separata di riferimento;
- il comando rapido `Stampa (F10)` del pannello destro usa ora una larghezza coerente e non deve piu` risultare tagliato nella card azioni.

Regola viva:
- in Banco una vendita conclusa deve lasciare l'operatore gia` pronto nella stessa tab, resettata come nuova vendita;
- i pulsanti rapidi del pannello destro non vanno compressi fino a troncare etichette operative gia` approvate.

## 2026-04-17 - Taglie compatte condivise per schermate operative e refactor reale `Lista riordino`

Decisione chiusa:
- `Banco.UI.Wpf` espone ora stili condivisi compatti per pulsanti, toggle toolbar, textbox, combobox, card e griglie operative, invece di costringere ogni view a ridefinire localmente densita`, padding e altezze;
- lo stesso pacchetto condiviso copre ora anche `DatePicker` e `CheckBox` compatti, necessari per barre filtri e pannelli laterali delle schermate gestionali;
- il design system condiviso copre ora anche la variante `CompactGreenButtonStyle`, cosi` i comandi di conferma positivi possono restare coerenti anche nelle schermate operative piu` dense;
- la schermata `Lista riordino` e` stata riallineata a questi standard: toolbar piu` corta e ordinata, input e pulsanti coerenti, area liste fornitore resa laterale e compatta, griglia allineata al linguaggio visivo di `Documenti`.
- la schermata `Documenti` usa ora lo stesso pacchetto compatto su testata, toolbar filtri rapidi, pannello laterale e card di dettaglio, cosi` lista, footer e pannello destro parlano finalmente la stessa lingua visiva.
- la zona `BancoView` della schermata `Banco` usa ora lo stesso pacchetto compatto su testata, ricerca articolo, cliente, testata griglia, empty state, riepilogo totali, pagamenti e scorciatoie rapide, senza toccare la logica operativa del flusso Banco.
- la toolbar di `Documenti` non cresce piu` come fila piatta di pulsanti: i filtri stanno ora sulla stessa linea ma raggruppati per significato (`Periodo` con `Oggi`, `Settimana`, `Mese`; `Vista` con `Completa`, `Scontrinati`, `Cortesia`), cosi` l'aggiunta di nuovi preset non sporca la barra operativa.

Regola viva:
- per schermate gestionali dense non vanno piu` creati localmente pulsanti, input, combo, card o griglie oversize quando esiste gia` la variante compatta condivisa;
- se una view richiede piu` densita`, il primo passo corretto resta estendere il design system comune e solo dopo applicarlo alla pagina.

## 2026-04-16 - Regola ferrea: sul legacy va rispettata tutta la struttura reale

Decisione chiusa:
- quando si lavora su `db_diltech`, non e` ammesso ragionare sulla sola tabella principale del caso d'uso;
- ogni lettura o scrittura Banco deve verificare e rispettare anche tabelle collegate, vincoli, lookup e codifiche realmente usate da `Facile Manager`.

---

Regola viva:
- non si introducono valori "plausibili" senza verifica sul DB reale;
- se il gestionale usa piu` tabelle per rappresentare un concetto, Banco deve seguire quella stessa struttura;
- i fix legacy non sono chiusi finche` non e` stato controllato il comportamento dell'intero gruppo di tabelle coinvolte, non solo della tabella piu` visibile.

## 2026-04-16 - Base nuovo modulo `Stampa` separata dal runtime corrente

Decisione chiusa:
- la sostituzione progressiva del modulo stampa parte da un nuovo progetto root `Banco.Stampa`, separato dal modulo stampa legacy;
- `QuestPDF` resta operativo finche` il nuovo flusso non e` pronto davvero;
- il nuovo modulo nasce gia` con catalogo layout, percorsi dedicati nella cartella `Stampa` e lettura stampanti di sistema, senza toccare ancora gli entry point operativi attuali.

Regola viva:
- la migrazione stampa va chiusa step by step, senza introdurre uno switch improvviso sul flusso Banco attuale;
- i futuri layout del nuovo modulo dovranno poter usare solo dati reali del documento e delle tabelle legacy collegate davvero coinvolte.

## 2026-04-16 - Architettura FastReport integrata in Banco

Decisione chiusa:
- il nuovo modulo `Banco.Stampa` espone gia` contratti distinti per runtime FastReport, apertura designer, anteprima, test stampa e schema dati dei documenti;
- il runtime FastReport viene pensato come componente interna del programma, non come procedura esterna separata;
- lo schema dati iniziale del documento stampa dichiara esplicitamente testata, cliente, righe, pagamenti e totali, con riferimento alle tabelle legacy reali coinvolte.

Regola viva:
- prima di attivare il designer reale o il preview reale FastReport, va completata la verifica del pacchetto/licenza definitiva;
- i campi esposti ai layout devono restare legati a dati veri del DB e del writer Banco, non a semantiche inventate per comodita` grafica.

## 2026-04-16 - Prima schermata `FastReport` dentro la shell Banco

Decisione chiusa:
- la shell Banco espone ora una tab dedicata `FastReport`, separata dalla schermata storica `Modelli stampa`;
- la nuova schermata mostra diagnostica runtime, assembly rilevati, cartella layout, catalogo e schema dati del documento;
- il probe runtime FastReport controlla davvero la presenza degli assembly nei percorsi standard invece di restare un semplice placeholder statico.

Regola viva:
- la schermata `FastReport` puo` dichiarare solo cio` che rileva davvero: runtime presente, designer presente, stampanti disponibili, assembly trovati o mancanti;
- finche` il designer/preview reali non sono collegati al pacchetto definitivo, i comandi devono restare prudenziali e non simulare operazioni concluse.

## 2026-04-16 - `Pos.repx` entra come blueprint legacy del nuovo FastReport

Decisione chiusa:
- il report legacy di riferimento per il POS 80 mm e` `C:\Facile Manager\DILTECH\Report\Pos.repx`;
- la pagina `FastReport` espone ora anche il blueprint legacy del report, con file sorgente, sezioni, parametri, binding principali e regole `BeforePrint` da replicare;
- lo schema dati del nuovo modulo e` stato esteso per coprire i campi realmente usati dal report legacy, inclusi etichetta documento, indirizzo completo cliente, telefono, email, codice fiscale e ordinamento riga.

Regola viva:
- la migrazione dei report dal modulo stampa legacy a `FastReport` non passa da conversione automatica integrale;
- ogni `.repx` va letto come sorgente tecnica di verita` del layout storico e ricodificato nel nuovo modulo in modo esplicito e verificabile.

## 2026-04-17 - Rimosso il vecchio modulo stampa e agganciata `Cortesia` a `FastReport`

Decisione chiusa:
- il progetto non contiene piu` il vecchio modulo stampa storico ne` le sue view, viewmodel e servizi UI dedicati;
- la schermata `Banco` usa ora `Banco.Stampa` anche per `Cortesia`, sia in anteprima sia nel comando operativo di stampa del layout `receipt-80-db`;
- il catalogo layout tecnico promuove `FastReport` come layout operativo di `Cortesia` e non mantiene piu` il vecchio layout di fallback.

Regola viva:
- ogni evoluzione della stampa Banco deve passare dal modulo `Banco.Stampa`;
- i file `.repx` restano solo come blueprint legacy da leggere, non come modulo runtime attivo del progetto;
- i documenti attivi del progetto non devono piu` citare il vecchio modulo stampa come componente installata o supportata.

## 2026-04-16 - Tab `FastReport` estesa a stampanti e comandi runtime

Decisione chiusa:
- la tab `FastReport` espone ora anche le stampanti di sistema, la stampante associata al layout selezionato e il salvataggio dell'associazione nel catalogo layout;
- la stessa schermata espone i comandi `Anteprima`, `Apri designer` e `Test stampa`, collegati al runtime del modulo `Banco.Stampa`;
- in assenza degli assembly `FastReport` sulla postazione, i comandi non simulano esiti positivi: restituiscono un messaggio esplicito di blocco runtime.

Regola viva:
- non serve un secondo modulo stampa per preview/designer/stampa test: questi passi devono restare dentro `Banco.Stampa` e nella sua schermata dedicata;
- finche` il runtime FastReport non e` installato o referenziato davvero, l'app puo` gestire catalogo, blueprint legacy e associazioni stampante, ma non deve fingere di aprire l'anteprima reale del `.frx`.

## 2026-04-16 - Runtime `FastReport Open Source` agganciato davvero

Decisione chiusa:
- `Banco.Stampa` usa ora il pacchetto `FastReport.OpenSource` ufficiale e lo collega davvero ai comandi del modulo;
- se manca il file layout, il modulo crea automaticamente un primo `.frx` valido (`Pos.frx`) nella cartella `Stampa\\Layouts`;
- `Anteprima` genera una preview HTML locale del report con dati demo Banco registrati nel runtime;
- `Test stampa` genera un HTML stampabile dal browser, invece di fingersi una stampa nativa verso una stampante specifica che il runtime Open Source base non governa direttamente;
- `Apri designer` prova ad avviare un Community Designer esterno solo se rilevato sulla macchina; in caso contrario restituisce un messaggio esplicito di limite runtime.

Regola viva:
- con `FastReport Open Source` il flusso realistico dentro Banco e` `carica .frx -> registra dati -> esporta preview/test locale`;
- per un designer visuale interno o per una stampa desktop nativa con targeting diretto della stampante servirebbe una linea diversa da valutare separatamente, non da fingere dentro il modulo corrente.

## 2026-04-16 - Primo `Pos.frx` pilota con resa POS leggibile

Decisione chiusa:
- il file `Pos.frx` generato dal modulo non e` piu` un template vuoto: nasce ora con una prima struttura POS 80 mm ispirata a `Pos.repx`;
- il layout pilota include intestazione documento, blocco anagrafica/contatti, banda righe e footer con totale/pagato;
- la preview usa campi visivi gia` composti nel payload demo Banco, cosi` quantita`, prezzo, sconto e importo possono essere nascosti o mostrati in modo coerente con la logica del report legacy.

Regola viva:
- il primo `Pos.frx` deve essere trattato come base di migrazione iterativa del `Pos.repx`, non come layout definitivo;
- i prossimi affinamenti devono concentrarsi sulla fedelta` visiva e sulle regole di esposizione campi, non sulla creazione di nuovi flussi paralleli di stampa.

## 2026-04-16 - Fix lock catalogo layout e prima mappa tecnica POS

Decisione chiusa:
- il lock sul file `layouts.catalog.json` durante il refresh della tab `FastReport` era causato dal salvataggio del catalogo mentre lo stream di lettura era ancora aperto nello stesso servizio;
- il servizio catalogo ora chiude la lettura prima di eventuali riscritture, quindi la tab puo` aggiornarsi senza l'errore sul file occupato;
- il modulo espone ora anche una prima scheda tecnica del contratto report POS, con famiglia report, contesto dominio, parametri runtime e mappa `zona -> campo Banco -> sorgente FM -> livello di confidenza`.

Regola viva:
- la migrazione report non deve basarsi solo sul layout legacy: ogni famiglia di stampa deve avere anche una mappa tecnica che separi dati certi, deduzioni forti e punti ancora da verificare;
- il fix di un errore UI/tecnico nel modulo stampa non e` chiuso se la schermata continua a fallire al refresh o a perdere le selezioni utili al lavoro operativo.

## 2026-04-16 - Tab `FastReport` resa davvero usabile in shell

Decisione chiusa:
- la pagina `FastReport` aveva ancora un problema reale di usabilita`: le sezioni basse della schermata non erano raggiungibili in viewport compatte per assenza di scroll verticale nel contenitore della view;
- il comando `Apri layout selezionato` poteva fallire con `Il file richiesto non esiste` perche` provava ad aprire direttamente `Pos.frx` anche quando il template pilota non era ancora stato materializzato su disco;
- la view usa ora un `ScrollViewer` dedicato e l'apertura layout passa prima dal runtime `Banco.Stampa`, che crea il `.frx` pilota se manca e solo dopo lo apre.

Regola viva:
- un fix UI del modulo stampa non e` chiuso se l'operatore non riesce fisicamente a raggiungere le sezioni o i comandi presenti nella schermata;
- i comandi operativi del modulo `FastReport` non devono limitarsi a comporre un path teorico: devono verificare o materializzare le risorse tecniche minime necessarie prima di aprirle.

## 2026-04-16 - Preview `Pos.frx` riallineata al runtime reale FastReport

Decisione chiusa:
- la preview del layout pilota falliva perche` il template `Pos.frx` usava espressioni `DocumentoBanco.*` e parametri non dichiarati, non coerenti con il modo in cui il runtime Open Source stava registrando i dati;
- il runtime registra ora direttamente il datasource `Righe` e valorizza testata e piede scrivendo i `TextObject` nominati del layout, senza affidarsi a parametri o business object non risolti dal template pilota;
- se `Pos.frx` esiste gia` ma contiene ancora le vecchie espressioni incompatibili, il modulo lo rigenera automaticamente prima dell'uso.

Regola viva:
- il layout pilota FastReport deve usare solo binding davvero compatibili con il runtime che Banco espone in quel momento;
- se il runtime cambia, anche il template pilota gestito dal modulo va riallineato automaticamente, senza lasciare all'operatore un `.frx` tecnico rotto da sistemare a mano.

## 2026-04-16 - Banda righe `Pos.frx` riallineata al datasource esplicito

Decisione chiusa:
- dopo il fix su testata e piede, la preview falliva ancora nella `DataBand` perche` il template usava campi nudi come `[QuantitaVisuale]` e `[Descrizione]`, non risolti dal contesto espressioni del runtime;
- il layout pilota usa ora binding espliciti sul datasource `Righe`, ad esempio `[Righe.QuantitaVisuale]` e `[Righe.Descrizione]`;
- la rigenerazione automatica di `Pos.frx` intercetta anche questa versione intermedia del template.

Regola viva:
- nella migrazione `FastReport Open Source` la banda righe deve sempre dichiarare in modo esplicito il datasource usato dal layout pilota;
- non basta che la `DataBand` sia collegata runtime: i campi esposti nel `.frx` devono restare compatibili con il parser espressioni effettivo del motore.

## 2026-04-16 - Annullamento manuale del pagamento POS dal popup operativo

Decisione chiusa:
- durante il pagamento carta l'operatore deve poter fermare la richiesta POS dal Banco quando il cliente cambia idea o deve aggiungere altri articoli.
- il popup operativo del POS espone ora un pulsante `Annulla` solo nella fase di invio al terminale.
- l'annullo usa il `CancellationToken` del flusso POS, quindi interrompe la richiesta corrente senza trattarla come timeout e senza tentare recuperi ambigui dell'ultimo esito.

Regola viva:
- se l'operatore annulla il pagamento POS, la vendita resta aperta;
- non parte alcuno scontrino;
- il Banco deve mostrare un esito pulito `Pagamento annullato` e permettere di continuare a lavorare sulla stessa scheda;
- resta invariata la regola principale: solo un pagamento POS davvero avviato e confermato puo` sbloccare lo scontrino.
- la nota tecnica POS mantiene anche una tabella rapida `segnale -> significato -> azione` per classificare subito `NAK`, `Cancelled`, `ACK + stream closed`, `E00` e problemi fiscali.

## 2026-04-16 - Dopo scontrino riuscito la stessa pagina Banco viene riciclata

Decisione chiusa:
- dopo uno scontrino riuscito l'operatore non deve restare ancorato al documento appena fiscalizzato nella stessa esperienza operativa;
- il Banco ricicla quindi la stessa pagina come `Nuovo documento`, senza chiedere alla shell di aprire una nuova tab.

Regola viva:
- la pagina che ha appena fiscalizzato non resta come riferimento separato nel flusso standard;
- la nuova vendita riparte sulla stessa scheda Banco gia` resettata.

## 2026-04-16 - Nota tecnica FM su accoppiamento report e stampa

Decisione chiusa:
- e` stata aperta una nota tecnica dedicata a `Facile Manager` per documentare la meccanica di accoppiamento dei report partendo dal dump IL, senza tentare di riusare direttamente il motore interno di FM.
- la nota separa i fatti certi dalle deduzioni forti e fissa il contratto minimo osservato del motore report: `XPCollection`, `Tabella.EnumTabella`, `Reportpersonalizzato`, `EnumTiporeport`, soggetto, periodo e parametri extra.

Regola viva:
- per progettare i futuri layout `FastReport` non basta conoscere il nome del report o il file `.repx`;
- bisogna identificare anche famiglia report, tabella dominio, dataset sorgente, testata, corpo, piede e parametri runtime;
- i report `Fattura`, `Lista`, `Etichetta`, `Ultimo scontrino` e `POS/cortesia` vanno trattati come famiglie separate finche` non e` dimostrato il contrario dal flusso reale FM.

## 2026-04-16 - Preview FastReport agganciata a dati veri legacy e designer root

Decisione chiusa:
- la preview POS pilota non usa piu` solo dati demo fittizi: `Banco.Stampa` legge ora l'intestazione negozio dal legacy, usando i valori `config` di `Impostazioni di base` e il riferimento scontrino dedicato al `modellodocumento = 27`;
- il documento esempio della preview e` ora una vendita Banco reale, numero `2308` (OID `25391`), cosi` righe, totale, pagamenti e punti cliente derivano da un documento vero del legacy e non da un payload artificiale;
- il rilevamento del designer FastReport considera anche una cartella `FastReport` presente nella root del progetto o dell'installazione, e apre direttamente `Designer.exe` se disponibile.

Regola viva:
- per i layout POS pilota si devono preferire dati reali verificati del legacy ogni volta che il flusso Banco li rende accessibili senza introdurre un secondo dominio di stampa;
- la cartella tecnica `FastReport` puo` stare accanto al progetto o all'eseguibile pubblicato, ma il modulo deve trovarla autonomamente senza chiedere all'operatore passaggi esterni manuali.

## Scopo

Questo file è il diario operativo vivo del progetto `Banco`.

Serve a tenere traccia di:
- decisioni chiuse;
- bug importanti risolti;
- routine rilevanti introdotte o cambiate;
- moduli o aree funzionali modificate in modo sostanziale;
- stato reale del progetto;
- prossimi step.

Questo file **non** deve diventare un accumulo caotico di note minute.  
Qui si registra solo ciò che sposta davvero lo stato del progetto.

---

## Regola di aggiornamento

Vanno registrati qui:
- bug importanti chiusi che cambiano il comportamento reale;
- routine rilevanti introdotte o modificate;
- moduli nuovi o modificati in modo significativo;
- cambiamenti architetturali;
- cambiamenti di dominio;
- step chiusi;
- decisioni operative che devono restare vive nelle prossime chat.

Non vanno registrati qui:
- micro-fix senza impatto reale;
- ritocchi di layout minori;
- pulizie locali di codice;
- refusi o rinomine irrilevanti;
- sperimentazioni non consolidate.

---

## Stato corrente sintetico

Il progetto `Banco` è una applicazione desktop WPF integrata con `db_diltech`.

Verità attiva attuale:
- documento ufficiale Banco su `db_diltech`
- `modellodocumento = 27`
- numerazione ufficiale solo legacy
- `Salva` pubblica/aggiorna il documento ufficiale
- `Cortesia` e `Scontrino` sono documenti ufficiali Banco
- WinEcr è il passo fiscale separato
- recupero sullo stesso `DocumentoGestionaleOid`
- lista documenti coerente con documenti ufficiali e stati utente reali

---

## Stato già chiuso

### Base architetturale
Chiusi:
- bootstrap WPF funzionante
- shell principale operativa
- letture reali da `db_diltech`
- writer legacy Banco attivo
- integrazione WinEcr separata
- lista documenti unificata
- recupero di documenti già pubblicati

### Regole di dominio già chiuse
Chiusi:
- `Cortesia` non è separata dal documento ufficiale
- `Scontrino` e `Cortesia` usano la stessa numerazione legacy
- `Sospeso` è componente aggiuntiva, non categoria principale
- `Scontrinonumero` e `Scontrinodata` non sono criteri primari
- nessun progressivo tecnico visibile come numero documento
- `Salva` non è più una bozza finale

---

## Decisioni operative chiuse

## 2026-04-12 — Riallineamento Banco a DB reale e WinEcr

### Decisione
Il Banco lavora sul documento ufficiale legacy:
- writer su `db_diltech`
- numerazione ufficiale solo legacy
- WinEcr separato
- recupero sullo stesso `DocumentoGestionaleOid`

### Conseguenze
- niente numerazioni ufficiali parallele
- niente classificazione primaria basata su `Scontrinonumero`
- lista documenti riallineata su documento reale + stato fiscale + componente `Sospeso`

---

## 2026-04-13 — Rimozione del progressivo tecnico visibile

### Decisione
Il progetto non usa più alcun numero tecnico visibile come identificativo documento.

### Conseguenze
- la scheda Banco non mostra numeri tecnici
- la testata mostra solo numero legacy quando il documento esiste
- la lista documenti non usa più etichette provvisorie come numero documento

---

## 2026-04-13 — Riallineamento definitivo di `Salva`

### Stato storico superato
- `Salva` concludeva fuori dal dominio ufficiale
- la UI lasciava intendere un esito non coerente col Banco reale

### Decisione chiusa
`Salva`:
- pubblica o aggiorna il documento ufficiale Banco su `db_diltech`
- converge sul workflow ufficiale Banco
- non genera un esito utente separato dal documento ufficiale

### Stato utente finale
- `Pubblicato Banco`

### Regole chiuse
`Pubblicato Banco`:
- è un documento ufficiale Banco valido
- non è `Cortesia`
- non è `Scontrino`
- compare in `Completa`
- non entra in `Solo cortesia`
- non entra in `Solo scontrinati`

---

## 2026-04-13 — Step 1 chiuso: gating pulsanti Banco dopo `Salva`

### Problema
Dopo `Salva`, `Anteprima` e `Cortesia` risultavano disabilitati mentre `Scontrino` restava attivo.

### Causa
Il gating trattava il solo publish legacy non fiscalizzato come blocco eccessivo.

### Correzione chiusa
- rimosso il blocco improprio su `Pubblicato Banco`
- `Anteprima` e `Cortesia` tornano a seguire:
  - modificabilità reale
  - presenza righe
  - assenza operazioni concorrenti

### Fuori perimetro esplicitamente lasciato fuori
- refresh lista
- riapertura documento
- logica pagamenti completa
- gestione controllata dei fiscalizzati

---

## 2026-04-13 — Step 2 chiuso: publish reale + refresh lista

### Problemi chiusi
- post-publish tab Banco non uniforme
- assenza di refresh automatico lista dopo publish/update

### Punto di verità chiuso
Esiti distinti e riusabili:
- publish legacy riuscito
- publish legacy riuscito con warning tecnico
- publish legacy riuscito ma WinEcr non completato
- publish legacy fallito

### Correzione chiusa
- `Salva`, `Cortesia`, `Scontrino` convergono sullo stesso segnale applicativo di refresh lista
- refresh automatico anche sugli update
- `Pubblicato Banco` non resta invisibile
- `Scontrino` con WinEcr incompleto resta documento ufficiale in lista

---

## 2026-04-13 — Step 2 bis chiuso: rifinitura lista documenti

### Obiettivi chiusi
- rendere coerenti riepiloghi e griglia
- distinguere graficamente `Cortesia` e `Pubblicato Banco`
- mantenere i totali sempre visibili
- pulire toolbar e UX lista

### Regole chiuse
- riepiloghi standard basati sui documenti ufficiali legacy coerenti col filtro
- `Cortesia` = verde chiaro
- `Pubblicato Banco` = verde chiaro più neutro
- `Sospeso` = rosso chiaro con priorità visiva
- riepilogo economico principale sempre visibile
- `Ctrl+T` = focus riepilogo, non hide/show

---

## 2026-04-13 — Chiusura residui Step 2/2 Bis: schermata `Documenti`

### Problemi chiusi
- pulsante `Del` per-riga
- click destro non allineato alla selezione reale
- micro descrizioni inutili
- footer totali non sempre coerente
- stato fantasma post-delete

### Correzioni chiuse
- `Del` per-riga operativo secondo il dominio reale
- click destro riallineato alla stessa pipeline della selezione normale
- micro descrizioni non operative rimosse
- footer totali riallineato al set filtrato reale
- flusso post-delete unico, senza doppia riconciliazione

### Regole chiuse
- `Del` mai sui fiscalizzati/scontrinati
- nessuna seconda logica colori per il right-click
- nessun dettaglio fantasma dopo delete
- griglia, selezione, dettaglio e footer devono descrivere lo stesso stato reale

---

## 2026-04-13 — Fix chiusura programma dalla `X`

### Problema
Eccezione WPF durante chiusura finestra per dialog aperto nel ciclo di `Closing`.

### Correzione chiusa
- chiusura annullata subito
- valutazione guardia rinviata fuori dall’evento `Closing`
- close programmatica solo dopo esito reale della guardia

---

## 2026-04-13 — Step 3 chiuso: apertura / recupero / modificabilità documenti pubblicati

### Problema
L’apertura da lista non distingueva bene:
- recuperabile
- consultazione bloccata
- fiscalizzato
- non fiscalizzato

### Decisione chiusa
Introdotto resolver unico della modalità scheda Banco.

### Regole chiuse
- documento ufficiale non fiscalizzato = recuperabile / operativo
- documento fiscalizzato = consultazione bloccata
- fallback prudenziale se i segnali non bastano
- `Apri nel Banco` resta entry point unico, ma con semantica chiara

### Gerarchia fonti chiusa
1. stato applicativo coerente già disponibile sul documento collegato
2. segnali legacy affidabili disponibili
3. fallback prudenziale conservativo

---

## 2026-04-13 â€” Fix schermata `Documenti`: non scontrinati legacy via `Buoni`

### Problema
La schermata `Documenti` lasciava i documenti legacy non scontrinati con pagamento `Buoni` in stato indeterminato:
- bucket `Solo cortesia` incompleto
- colorazione riga non coerente
- riepiloghi economici parziali o a zero

### Causa
La lista usava ancora come segnali principali i soli metadati locali o i soli segnali fiscali, mentre nel gestionale reale `PagatoBuoni` viene usato come segnale pratico per distinguere i non scontrinati letti dal DB legacy.

### Correzione chiusa
- classificazione runtime della lista riallineata al segnale legacy `Buoni`
- colorazione riga dei non scontrinati legacy riallineata alla stessa classificazione runtime
- footer e pannello riepiloghi convergono su un solo punto di veritÃ  nel `DocumentListViewModel`

### Regole chiuse
- nella schermata `Documenti`, `PagatoBuoni` puÃ² identificare un documento legacy non scontrinato
- nella stessa schermata, un documento legacy con incasso `Contanti` / `Carta` / `Web` e senza `PagatoBuoni` torna nel bucket runtime degli scontrinati
- il filtro `Solo cortesia` usa anche questa classificazione runtime
- il riepilogo "Contanti cortesia / non scontrinati" somma i non scontrinati secondo il comportamento reale del DB legacy
- griglia, bucket filtro e footer non devono divergere su questi documenti

---

## Regole permanenti emerse dai lavori chiusi

### 1. `Salva`
`Salva` non è una bozza finale e non deve mai tornare ad esserlo.

### 2. `Del`
`Del` dipende da:
- non scontrinato
- non fiscalizzato
- realmente eliminabile secondo il dominio

Non dipende da semantiche tecniche parallele.

### 3. Lista documenti
La lista deve restare coerente tra:
- righe
- selezione
- dettaglio
- footer
- stato utente

### 4. UI
Un task UI non è chiuso se il risultato:
- non è visibile davvero;
- non è riproducibile davvero;
- non è verificato su scenario reale.

---

## Prossimi step aperti

### 1. Prove end-to-end reali
Da completare:
- casi reali con WinEcr
- casi reali di recupero su operatori
- casi reali di consultazione vs recupero

### 2. Pagamenti

---

## 2026-04-16 - Fix writer varianti legacy Banco e popup errore leggibile

### Problema
Le chiusure `Salva`, `Cortesia` e `Scontrino` potevano fallire sul legacy con:
- `Cannot add or update a child row`
- tabella `documentorigacombinazionevarianti`

Il popup finale errore si chiudeva inoltre troppo in fretta e rendeva il difetto quasi illeggibile in postazione.

### Causa
Il writer Banco inseriva `Documentorigacombinazionevariantiordine = 1`, ma nel DB reale Facile Manager questo campo e` una FK verso la stessa tabella e, per le righe variante normali, viene lasciato `NULL`.

### Correzione chiusa
- writer legacy Banco riallineato al formato reale FM:
  - `Documentorigacombinazionevariantiordine = NULL`
  - `Seleziona = 0`
- popup finale Banco reso leggibile:
  - successo visibile per `2s`
  - errore visibile per `6s`

### Regola viva
Le combinazioni variante del documento Banco devono essere scritte nel legacy seguendo il formato reale prodotto da Facile Manager, non con valori placeholder inventati.
Da consolidare ancora:
- default `Contanti`
- split pagamento
- cambio pagamento prima della chiusura finale

### 3. Gestione controllata fiscalizzati
Tema futuro separato:
- casi ammessi su scontrinati/fiscalizzati
- eventuale cambio merce di pari importo
- nessuna riapertura libera

### 4. Split fiscale / non fiscale
Da affrontare in fase successiva.

---

## Stato aperto / chiuso rapido

### Chiusi
- publish legacy corretto
- `Salva` riallineato
- lista documenti coerente
- footer e griglia coerenti
- `Del` per-riga
- click destro
- resolver unico di apertura/recupero
- blocco fiscalizzati
- guardia uscita coerente

### Aperti
- test operativi end-to-end più completi
- pagamenti da consolidare
- casi controllati sui fiscalizzati
- split fiscale/non fiscale

---

## Regola finale di questo diario

Questo file deve restare:
- sintetico;
- operativo;
- aggiornato;
- senza accumulare note obsolete.

Quando un passaggio cambia davvero lo stato del progetto, va registrato qui.  
Quando è solo rumore tecnico, no.
---

## 2026-04-13 - Non scontrinati DB e cancellazione legacy reale

Problema chiuso:
- documenti Banco letti dal DB come `2132/2026` e `2133/2026` risultavano non scontrinati nel gestionale reale, ma in lista non andavano verdi, non erano recuperabili correttamente e non mostravano il `Del` per riga;
- il `Del` e `Cancella scheda` erano ancora limitati al solo supporto locale SQLite, quindi non riproducevano il comportamento reale di Facile Manager.

Regola viva:
- nella schermata `Documenti`, un documento Banco legacy senza `Scontrinonumero` valorizzato resta runtime un non scontrinato recuperabile;
- `PagatoBuoni` resta un segnale utile dei non scontrinati, ma non e` piu` l'unico caso;
- il verde riga e il bucket `Solo cortesia` coprono il gruppo operativo dei non scontrinati / recuperabili;
- il pulsante `Del` in lista e `Cancella` in `Vendita Banco` cancellano il documento dal `db_diltech` con conferma esplicita, e rimuovono anche l'eventuale supporto locale collegato.

Aggiornamento finale della regola:
- nella schermata `Documenti` non si usa piu` `Scontrinonumero` come verita` primaria della classificazione;
- `Contanti`, `Carta` e `Web` identificano i documenti scontrinati;
- tutte le altre forme pagamento legacy del DB restano nel bucket dei non scontrinati Banco, inclusi `Salva`, `Cortesia`, `Sospeso` e i casi della vecchia vendita Banco;
- colore riga, filtro, apertura nel Banco, `Del` e riepiloghi economici devono usare questa stessa regola unica.

## 2026-04-13 - Chiusura Banco solo su DB e progressivo come anteprima

Problema chiuso:
- i flussi `Salva`, `Cortesia` e `Scontrino` salvavano ancora la scheda Banco nel repository locale prima della pubblicazione, creando una persistenza parallela non coerente con il Banco reale;
- su una nuova scheda il numero mostrato poteva essere letto come definitivo lato client, mentre in realta` con due PC il progressivo deve restare solo un'anteprima fino alla scrittura su `db_diltech`.

Regola viva:
- la chiusura di una vendita Banco non deve piu` salvare il documento nel local store;
- il documento ufficiale di vendita nasce e si aggiorna solo nel `db_diltech`;
- il numero mostrato su una scheda nuova e` una anteprima letta dal DB, non una prenotazione locale;
- al momento della pubblicazione il workflow rilegge sempre il prossimo progressivo reale dal legacy, cosi` due postazioni non si contendono numeri gia` occupati.

## 2026-04-13 - Lista prudenziale, scheda Banco operativa

Problema chiuso:
- i pagamenti `Contanti`, `Carta` e `Web` venivano usati anche per bloccare la scheda Banco in consultazione, quindi documenti di prova o casi da correggere non erano modificabili e il pulsante `Cancella scheda` spariva.

Regola viva:
- `Contanti`, `Carta` e `Web` continuano a classificare i documenti come scontrinati per lista e riepiloghi economici;
- questa classificazione non basta da sola a bloccare l'apertura della scheda Banco;
- la lista `Documenti` resta prudenziale: per gli scontrinati il `Del` puo` restare nascosto;
- la scheda `Vendita Banco` resta invece il punto operativo da cui recuperare, modificare e cancellare i documenti non bloccati da uno stato fiscale concluso.

Aggiornamento correttivo:
- `Cancella scheda` non deve bloccare un documento solo perche` classificato scontrinato in lista dai pagamenti;
- il blocco resta solo sui casi con stato fiscale concluso davvero;
- il popup di conferma in `Vendita Banco` non deve piu` parlare solo di "documento non scontrinato", ma della cancellazione del documento dal `db_diltech`.

Aggiornamento finale:
- la cancellazione legacy non deve piu` fermarsi solo perche` il documento ha pagamenti `Contanti`, `Carta` o `Web`;
- il blocco di `GestionaleDocumentDeleteService` resta solo per il controllo di appartenenza al modello Banco e per gli stati fiscalmente conclusi;
- i documenti di prova e le vendite non ancora chiuse fiscalmente possono essere cancellati dalla scheda Banco anche se in lista risultano scontrinati.

Aggiornamento UI:
- dopo la cancellazione da `Vendita Banco`, la tab `Documenti` deve rinfrescarsi subito in modo che la riga sparisca in tempo reale;
- il refresh live della lista e` agganciato all'evento di cancellazione della scheda Banco, non a un reload manuale dell'utente.

Aggiornamento documento mancante:
- se dalla lista si prova ad aprire una vendita che Facile Manager ha gia` cancellato, la scheda Banco non deve aprirsi vuota;
- il Banco deve mostrare subito il popup `Documento inesistente` e fermare il caricamento della scheda.

Aggiornamento routine griglia:
- il menu colori righe delle griglie Banco e Documenti e` ora centralizzato in una routine condivisa;
- la modifica del menu comune passa da un solo punto di verita`, senza duplicare la logica nelle view.

## 2026-04-13 - Colore intestazione griglia per singola lista

Problema chiuso:
- le griglie Banco e Documenti avevano bisogno di un colore intestazione indipendente, salvato per griglia, senza condividere la stessa impostazione visiva.

Regola viva:
- ogni griglia mantiene il proprio colore della parte superiore in modo separato;
- il menu della griglia offre palette acquerello e una scelta libera del colore;
- il colore scelto viene salvato localmente per la singola griglia e ricaricato all'avvio;
- il bordo superiore delle griglie principali usa angoli arrotondati per migliorare la resa visiva.

## 2026-04-13 - Cortesia con stampa POS80 e scheda aperta

Problema chiuso:
- il pulsante `Cortesia` pubblicava il documento Banco sul legacy ma non avviava la stampa termica, quindi l'utente doveva usare solo l'anteprima o una stampa separata.

Regola viva:
- il pulsante `Cortesia` deve pubblicare o aggiornare il documento sul `db_diltech`;
- subito dopo deve inviare la ricevuta di cortesia alla `POS-80`;
- la scheda Banco deve restare aperta sullo stesso documento, cosi` l'utente puo` continuare a modificarlo o ristamparlo;
- `F2` resta il comando esplicito per aprire una nuova scheda Banco;
- se la stampa POS80 fallisce, il publish legacy non va annullato: la scheda resta aperta con messaggio di warning.

## 2026-04-13 - Pagamento contanti predefinito in chiusura

Problema chiuso:
- `Salva`, `Cortesia` e `Scontrino` potevano partire senza alcun importo inserito nei pagamenti, lasciando la chiusura incoerente o bloccata da validazioni successive.

Regola viva:
- se l'operatore non ha inserito alcun importo nei pagamenti, prima di `Salva`, `Cortesia` o `Scontrino` il Banco propone di valorizzare automaticamente l'intero importo in `Contanti`;
- il popup di conferma usa `Si` come pulsante principale e conferma con `Invio`;
- la regola vale sia dai pulsanti a video sia dalle scorciatoie tastiera;
- dopo l'autocompilazione il documento resta comunque modificabile e, nel caso della cortesia, la scheda continua a restare aperta.

## 2026-04-13 - Scheda allineata dopo publish riuscito

Problema chiuso:
- dopo una `Cortesia` o un publish riuscito, cliccando `Lista documenti` poteva comparire un popup di uscita con "modifiche correnti" anche senza ulteriori variazioni reali.

Regola viva:
- dopo un publish riuscito (`Salva`, `Cortesia`, `Scontrino`) la scheda deve risultare allineata al documento ufficiale;
- il flag delle modifiche correnti va azzerato subito dopo il publish;
- il popup di uscita deve comparire solo se l'operatore modifica di nuovo la scheda dopo l'ultimo publish riuscito.

## 2026-04-13 - Modulo amministrativo per import backup DB

Problema chiuso:
- per riallineare il DB locale al backup reale del server serviva una procedura esterna, poco comoda da ripetere durante i test del Banco.

Regola viva:
- in `Amministrazione` esiste una voce `Importa backup`;
- il modulo accetta backup `zip`, `bak` o `sql` prodotti dal gestionale;
- se l'input e` uno zip, il Banco estrae automaticamente il dump DB corretto e lo importa sul `db_diltech` configurato nella postazione corrente;
- il restore e` pensato per riallineare il database locale prima dei test e richiede il riavvio di Banco dopo l'importazione.

Aggiornamento correttivo:
- il modulo import backup mostra ora avanzamento reale con progress bar, fase corrente e messaggio stato coerente col tema del programma;
- il parser del dump supporta anche i blocchi con `DELIMITER` presenti nei backup di Facile Manager, cosi` trigger e procedure non bloccano piu` il restore con l'errore mostrato da `MySqlConnector`.

## 2026-04-13 - Cliente richiamato e punti live in scheda Banco

Problema chiuso:
- in scheda Banco il campo cliente poteva restare con il valore spurio `1` invece del nominativo richiamato;
- i punti mostrati in vendita usavano una somma storica grezza del DB (`iniziali + assegnati`) invece del saldo operativo cliente e dei punti maturati sul documento corrente.

Regola viva:
- il textbox cliente deve mostrare il nominativo del cliente richiamato o confermato, non l'OID del generico;
- in vendita Banco il badge `PUNTI ATTUALI` mostra il saldo disponibile del cliente;
- il badge `MATURA` mostra in tempo reale i punti che la vendita corrente genera secondo la campagna attiva;
- il richiamo cliente da documento legacy non deve sporcare la scheda come modifica manuale.

Aggiornamento correttivo:
- `MATURA` non deve comparire quando si apre una vendita gia` conclusa dal DB senza nuove modifiche correnti;
- il box cliente non deve ripetere il nominativo gia` visibile nel textbox di ricerca;
- il pulsante `Storico` va tenuto nel box punti cliente, non duplicato nella riga superiore.

## 2026-04-13 - Configurazione server per app pubblicata TSPlus

Problema chiuso:
- l'app pubblicata via `TSPlus .connect` poteva leggere una configurazione utente diversa da quella usata nell'avvio diretto sul server, lasciando il Banco vuoto o ambiguo da diagnosticare.

Regola viva:
- Banco usa come configurazione primaria la cartella `Config` accanto all'eseguibile pubblicato;
- il file attivo principale diventa quindi `<cartella programma>\\Config\\appsettings.user.json`;
- se la cartella `Config` o il file non esistono ancora, Banco li crea automaticamente;
- al primo avvio utile il programma semina il file server copiando il vecchio `appsettings.user.json` dal profilo utente, se presente;
- il supporto tecnico SQLite non deve finire dentro `Config`: Banco riallinea automaticamente `LocalStore` nella cartella sorella `<cartella programma>\\LocalStore`;
- la diagnostica deve mostrare sia il file configurazione attivo sia la connessione `host:porta/database` del DB legacy, oltre al percorso SQLite locale.

## 2026-04-14 - Script base Inno Setup

Problema chiuso:
- mancava uno script installer dedicato per Banco, con aggiornamento controllato e preservazione dei dati locali del programma.

Regola viva:
- il progetto contiene ora uno script `Banco.iss` per Inno Setup;
- l'installer prende i file dal publish `publish\\Banco`;
- l'installazione standard va in `C:\Banco`;
- gli aggiornamenti non devono cancellare `Config` e `LocalStore`, che restano preservati tra una versione e l'altra.

## 2026-04-14 - Icone Banco nel programma e nel setup

Problema chiuso:
- il Banco pubblicato e l'installer non usavano ancora l'identita` grafica definitiva del progetto.

Regola viva:
- la cartella `Banco.UI.Wpf\\Immagini` contiene gli asset immagine ufficiali del programma;
- `Banco.ico` e` l'icona usata dall'eseguibile WPF e dal setup Inno;
- l'header della shell Banco usa il logo applicativo, non piu` un placeholder testuale.

## 2026-04-14 - Migrazione piattaforma a .NET 10 LTS

Problema chiuso:
- Banco era ancora allineato a `.NET 8`, con il rischio di rimandare la migrazione piattaforma a un momento futuro piu` costoso e piu` rischioso.

Regola viva:
- i progetti attivi di Banco usano ora `.NET 10` come base comune;
- i moduli WPF usano `net10.0-windows`;
- i moduli di libreria usano `net10.0`;
- publish, installer e server devono quindi essere allineati al runtime `.NET 10` quando il pacchetto non e` `self-contained`.

## 2026-04-14 - Log Banco nel percorso programma

Problema chiuso:
- i log applicativi Banco finivano ancora in `C:\tmp\Log`, mischiandosi con percorsi tecnici usati invece dal flusso fiscale.

Regola viva:
- i log applicativi Banco vengono scritti nella cartella `Log` accanto all'eseguibile del programma;
- la cartella `Log` viene creata automaticamente se manca;
- i file fiscali WinEcr e gli XML scontrino restano invece nei percorsi tecnici `C:\tmp` gia` configurati.

## 2026-04-14 - Publish compatto Banco

Problema chiuso:
- la cartella pubblicata del programma risultava troppo rumorosa, con molte dipendenze sparse in root e file debug inutili per la distribuzione.

Regola viva:
- il profilo `FolderProfile` pubblica Banco in modalita` `single-file` framework-dependent su `publish\\Banco`;
- le librerie native vengono inglobate nel pacchetto del single-file;
- i `.pdb` non devono restare nel publish finale;
- la root pubblicata deve restare compatta, con solo file realmente funzionali al deploy.

## 2026-04-14 - Default vivi per DB, POS e fiscale

Problema chiuso:
- su configurazioni nuove o parziali il Banco poteva mostrare campi DB/POS/registratore vuoti, costringendo a reinserire manualmente valori che il progetto usa sempre come base operativa.

Regola viva:
- il loader configurazione normalizza sempre i default reali di `db_diltech`, POS Nexi e WinEcr quando i campi sono vuoti o mancanti;
- la password DB di default del progetto viene seminata automaticamente nelle configurazioni nuove o parziali;
- il pacchetto di installazione semina al primo setup anche un `Config\\appsettings.user.json` con DB, POS e registratore gia` valorizzati;
- nella schermata `Configurazione DB`, se l'host viene lasciato vuoto Banco salva e usa automaticamente `127.0.0.1`; sulle postazioni client l'host va valorizzato con l'IP del server MySQL;
- se una macchina aveva gia` un `appsettings.user.json` parziale o sporco, Banco lo normalizza e lo riscrive automaticamente al primo avvio utile, invece di lasciare i campi POS/WinEcr vuoti;
- l'aggiornamento di una `Cortesia` legacy deve prima eliminare i record figli di `documentorigacombinazionevarianti` e solo dopo le `documentoriga`, altrimenti il writer fallisce sui vincoli FK;
- i servizi legacy di lettura Banco non devono tenere in cache parametri DB obsoleti dopo un cambio configurazione.

## 2026-04-14 - Recovery automatico configurazione corrotta

Problema chiuso:
- un `appsettings.user.json` corrotto o troncato bloccava completamente l'avvio di Banco con eccezione `System.Text.Json.JsonException`, costringendo a interventi manuali sul file.

Regola viva:
- se il loader incontra una configurazione JSON non valida, Banco non deve chiudersi;
- il file corrotto va rinominato automaticamente in `appsettings.user.corrupted-YYYYMMDD-HHMMSS.json`;
- Banco deve rigenerare subito una configurazione valida usando i default vivi del progetto per DB, POS e WinEcr;
- il recovery deve valere sia per l'avvio normale sia per le installazioni locali/server con cartella `Config` accanto all'eseguibile.

## 2026-04-14 - Log separati per area funzionale

Problema chiuso:
- il file unico `BancoPosProcess.log` mescolava bootstrap, Banco, Documenti, amministrazione, POS, fiscale e stampa, rendendo poco leggibili diagnosi e verifiche operative.

Regola viva:
- la cartella `Log` del programma deve contenere file separati per area funzionale;
- i log applicativi principali sono ora distinti almeno in `Avvio.log`, `Banco.log`, `Documenti.log`, `Amministrazione.log`, `Stampa.log`, `Pos.log`, `Fiscale.log` e `Generale.log`;
- i log transazione POS devono stare sotto `Log\\Pos\\Transazioni`, non in `C:\\tmp`;
- il servizio log resta unico, ma instrada il file corretto in base al modulo sorgente;
- `Documenti`, configurazioni amministrative, import backup e modelli stampa devono lasciare tracce autonome nei rispettivi file log.

## 2026-04-14 - Avvio Banco con privilegi amministrativi

Problema chiuso:
- in ambiente server la schermata `Documenti` e altri flussi Banco risultavano incoerenti se l'app veniva aperta senza elevazione, costringendo a ricordarsi ogni volta l'avvio manuale come amministratore.

Regola viva:
- `Banco.exe` richiede ora privilegi amministrativi tramite manifest applicativo;
- in `Debug` il progetto usa un manifest separato `asInvoker` per non bloccare il lavoro in Visual Studio;
- in `Release` e nei publish resta attivo il manifest `requireAdministrator`;
- l'elevazione non dipende piu` da scorciatoie esterne o proprieta` del collegamento;
- publish e installer devono distribuire il manifest insieme all'applicazione WPF;
- il post-install di Inno deve avviare Banco con `shellexec`, altrimenti il lancio automatico fallisce con errore 740 su un exe che richiede elevazione.

## 2026-04-14 - Cambio configurazione DB con refresh live

Problema chiuso:
- cambiando host o altri parametri del DB dalla schermata `Configurazione DB`, alcune parti del programma continuavano a usare il vecchio indirizzo fino al riavvio;
- la lista `Documenti`, il modulo `Punti` e alcune routine singleton del Banco potevano restare agganciate a cache obsolete.

Regola viva:
- quando l'operatore salva una nuova configurazione `db_diltech`, il programma deve adottarla subito senza riavvio;
- i servizi legacy singleton non devono trattenere in cache host/porta/database superati;
- la schermata `Documenti`, la diagnostica, il modulo `Punti` e gli stati runtime del `Banco` che dipendono dal DB devono riallinearsi in tempo reale alla nuova configurazione salvata;
- anche i salvataggi successivi di preferenze UI devono partire dalla configurazione aggiornata, senza poter ripristinare accidentalmente il vecchio host.

## 2026-04-14 - Documenti: tasti mese corrente e nuova vendita Banco

Problema chiuso:
- dalla schermata `Documenti` mancavano scorciatoie dirette e visibili per filtrare le vendite del mese corrente e per aprire rapidamente una nuova vendita Banco;
- `F2` apriva la nuova scheda solo se la tab attiva era gia` `Banco` e non gestiva il caso richiesto della seconda scheda vuota quando la vendita corrente contiene ancora merce o modifiche.

Regola viva:
- la schermata `Documenti` espone un pulsante rapido `Mese corrente` che filtra la lista dal primo all'ultimo giorno del mese attuale;
- la stessa schermata espone un pulsante `Nuova vendita (F2)` collocato nella toolbar della lista;
- `F2` deve funzionare come scorciatoia globale di apertura nuova vendita Banco;
- se la scheda Banco corrente e` vuota, `F2` la riusa e apre direttamente una nuova vendita;
- se la scheda Banco contiene righe o modifiche correnti, il programma deve mostrare il popup di conferma e permettere di aprire una seconda tab Banco vuota oppure annullare l'operazione;
- la vendita gia` aperta non deve andare persa quando l'operatore sceglie di aprire la seconda scheda.

## 2026-04-14 - Sidebar modulare con registro di navigazione condiviso

Problema chiuso:
- la vecchia sidebar WPF era una colonna unica con categorie espandibili interne alla shell e non consentiva di avvicinarsi al layout operativo richiesto con rail icone + pannello contestuale;
- la ricerca menu e la mappa delle destinazioni erano di fatto implicite nella shell e troppo accoppiate ai viewmodel concreti.

Regola viva:
- la navigazione principale del programma passa ora da un modulo root dedicato `Banco.Sidebar`, separato dalla shell WPF;
- il contratto condiviso `INavigationRegistry` e` la fonte di verita` delle destinazioni navigabili, dei gruppi contestuali, degli alias e delle keyword di ricerca;
- la sidebar legge il registro e non apre direttamente view o viewmodel concreti: la shell resta il punto unico che risolve la chiave di destinazione e apre la tab reale;
- la ricerca della sidebar resta una ricerca globale di navigazione/workspace, non una ricerca del contenuto della pagina;
- i risultati ricerca devono poter attivare la macro-sezione corretta, evidenziare la voce trovata e aprire direttamente la destinazione registrata;
- le voci non ancora implementate possono restare visibili ma disattivate solo quando servono a mantenere coerenza col layout di riferimento in questa fase;
- le sezioni senza modulo reale non devono introdurre percorsi finti o concetti paralleli al dominio Banco;
- il tasto destro sulla sidebar apre la personalizzazione di aspetto, link, posizioni e colori usando solo destinazioni reali registrate.

## 2026-04-14 - Punti Banco allineati al saldo legacy reale

Problema chiuso:
- nella scheda `Banco` il saldo punti cliente mostrava il valore iniziale o combinato della carta fedelta` invece del saldo reale disponibile del gestionale legacy;
- il popup premio poteva segnalare la soglia alta raggiunta ma mostrare nel dettaglio la descrizione di un premio diverso;
- quando il cliente raggiunge piu` soglie premio, Banco applicava in automatico una sola regola senza lasciare scelta all'operatore.

Regola viva:
- in vendita il saldo punti disponibile deve riflettere il campo operativo reale del legacy (`Punticartafedelta`), non i punti iniziali della carta;
- il riepilogo premio in Banco puo` mostrare la soglia migliore raggiunta, ma il popup deve restare coerente con il premio effettivamente selezionabile;
- se piu` regole premio sono contemporaneamente raggiunte, Banco deve far scegliere all'operatore quale premio applicare sul documento corrente;
- l'applicazione e lo storico del premio devono usare sempre la regola realmente scelta, non una regola implicita diversa da quella mostrata.

## 2026-04-14 - Banco con notifiche stabili e testata non ballerina

Problema chiuso:
- la schermata `Banco` cambiava altezza durante la vendita quando banner, stato documento, punti, promo o sospeso comparivano e scomparivano in punti diversi della pagina;
- le segnalazioni operative erano disperse tra testata, box cliente e riquadro documento, con un effetto visivo instabile e fastidioso.

Regola viva:
- tutte le notifiche operative della scheda `Banco` devono confluire nella fascia alta centrale della testata;
- la fascia notifiche della testata deve avere altezza fissa e testo su una sola riga con troncamento, cosi` la pagina non cambia altezza;
- nel box cliente non va piu` mostrata la dicitura separata della carta fedelta`: il riepilogo deve stare su una sola riga compatta con punti, promo e sospeso;
- il riquadro documento non deve piu` ospitare notifiche variabili che facciano ballare la schermata durante l'uso reale.

## 2026-04-14 - Uscita Banco senza popup su scheda vuota

Problema chiuso:
- il popup `Documento Banco non concluso` compariva anche su schede Banco senza righe in griglia, ad esempio dopo la sola conferma del cliente o altre modifiche minime senza merce.

Regola viva:
- per le schede Banco operative non ufficiali il popup di uscita, chiusura tab o nuova scheda deve comparire solo se esistono righe in griglia;
- una scheda senza righe non va trattata come lavoro da proteggere in uscita, anche se sono presenti modifiche locali leggere non legate a merce inserita.

## 2026-04-14 - Ricerca articoli multi-termine stile catalogo

Problema chiuso:
- la ricerca articoli Banco poteva fermarsi troppo presto al primo sottoinsieme trovato, senza unire correttamente varianti, match esatti e risultati catalogo coerenti con tutti i termini digitati;
- ricerche composte come `elfliq 20 ml` non si comportavano in modo abbastanza vicino ai cataloghi e-commerce, soprattutto nell'ordine dei risultati e nella copertura del set atteso.

Regola viva:
- la ricerca articoli Banco deve trattare l'input come ricerca multi-termine: ogni parola significativa deve contribuire al match del prodotto;
- il risultato finale deve unire match esatti, varianti e catalogo generale, poi deduplicare e ordinare per rilevanza;
- l'ordinamento deve favorire barcode/codici esatti, descrizioni che contengono la frase cercata e articoli che combinano correttamente tutti i termini digitati.

## 2026-04-14 - Banco stabile, uscita pulita e scorciatoie riallineate

Problema chiuso:
- la schermata `Banco` continuava a muoversi per messaggi e card dinamiche distribuite in punti diversi della pagina;
- il popup di uscita compariva anche su schede senza righe in griglia;
- la documentazione visuale e operativa non era ancora allineata del tutto al comportamento reale di scorciatoie e ricerca.

Regola viva:
- le notifiche operative della scheda `Banco` stanno nella testata centrale e il box cliente usa un riepilogo compatto su una sola riga;
- se la griglia documento e` vuota, chiusura tab, chiusura programma e nuova scheda non devono mostrare il popup di perdita lavoro;
- la ricerca articoli va descritta come campo ricerca live multi-termine, non come vecchio "tasto Cerca";
- la scorciatoia `Canc` in griglia non elimina la riga: converte la riga selezionata in manuale e lascia il codice articolo vuoto;
- l'intestazione della griglia documento mantiene bordi completi coerenti con il contenitore e il pulsante `Nuovo (F2)` deve restare leggibile e centrato.

## 2026-04-14 - Recupero prudente esito POS dopo `NAK`

Problema chiuso:
- in caso di `NAK` dal terminale POS il Banco fermava subito il flusso con errore secco, senza verificare se il terminale avesse comunque un ultimo esito leggibile;
- questo lasciava un caso fragile lato operatore proprio durante la vendita al banco, pur senza bloccare l'applicazione.

Regola viva:
- se il terminale POS risponde `NAK`, Banco non deve bloccare la scheda e non deve tentare un retry cieco del pagamento;
- prima di chiudere il caso come errore, il flusso POS deve interrogare in modo prudente l'ultimo esito disponibile del terminale;
- se l'ultimo esito e` recuperabile, Banco deve usarlo come verita` operativa della transazione;
- se non esiste un esito finale confermato, il messaggio operatore deve restare chiaro: la scheda resta aperta e il pagamento puo` essere ritentato senza chiudere il documento.

## 2026-04-14 - `Salva` operativo con nuova scheda pronta

Problema chiuso:
- dopo `Salva` la scheda restava aperta sul documento ufficiale appena pubblicato, con percezione di blocco operativo e senza riportare subito l'operatore a una pagina vuota;
- il popup finale di `Salva` rallentava inutilmente il ritorno alla vendita successiva.

Regola viva:
- a publish riuscito tramite `Salva`, Banco deve aprire subito una nuova scheda vuota pronta per la vendita successiva;
- `Salva` non deve lasciare l'operatore fermo sulla scheda appena pubblicata se il flusso operativo atteso e` la registrazione rapida al banco;
- il messaggio finale di `Salva` deve essere sobrio e non deve bloccare la ripartenza immediata su una pagina vuota.

## 2026-04-14 - Banco cassa-first su pagamenti, ricerche e focus

Problema chiuso:
- il cambio tipo pagamento (`Contanti`, `Carta`, `Sospeso`) era percepito lento e a tratti arrivava in ritardo, soprattutto subito dopo barcode o refresh secondari;
- le ricerche live articolo/cliente potevano tornare fuori tempo e sporcare la schermata con aggiornamenti tardivi;
- il focus della ricerca articolo era troppo aggressivo e poteva rientrare in momenti non richiesti, disturbando pagamenti e input operativi.

Regola viva:
- il click sui tipi pagamento deve restare locale e immediato: il commit completo del documento avviene solo su eventi stabili come `Invio`, perdita focus o comandi finali (`Salva`, `Cortesia`, `Scontrino`);
- i totali Banco devono riflettere subito i valori digitati nei campi pagamento, senza attendere la sincronizzazione completa del modello locale;
- una ricerca live articolo o cliente puo` aggiornare popup e selezione solo se e` ancora la richiesta attiva: risultati vecchi o arrivati in ritardo vanno scartati in silenzio;
- quick info ultimo acquisto e refresh promo sono accessori e non devono competere con il percorso cassa o rubare focus all'operatore;
- il focus automatico verso la ricerca articolo va usato solo nei punti espliciti del flusso Banco, non durante l'uso normale dei pagamenti.

## 2026-04-14 - Accorpamento righe articolo uguali in vendita Banco

Problema chiuso:
- sparando o aggiungendo due volte lo stesso articolo, Banco apriva piu` righe duplicate invece di comportarsi come una cassa operativa compatta.

Regola viva:
- se l'operatore inserisce di nuovo lo stesso articolo standard gia` presente nel documento, Banco deve aumentare la quantita` sulla riga esistente invece di creare una nuova riga duplicata;
- l'accorpamento va applicato solo alle righe articolo normali del Banco, senza sporcare righe manuali o righe promo;
- la riga aggiornata deve restare selezionata e il messaggio operatore deve chiarire che la quantita` e` stata accorpata sulla riga esistente.

## 2026-04-14 - Colonna `U.M.` Banco riallineata al legacy reale

Problema chiuso:
- la colonna `U.M.` nel documento Banco non era ancora agganciata in modo chiaro alla misura principale reale dell'articolo legacy.

Regola viva:
- la colonna `U.M.` della griglia documento Banco deve mostrare la misura principale reale dell'articolo, non una scelta libera scollegata dal legacy;
- l'eventuale logica di confezione o seconda unita` va trattata come supporto informativo o regola listino, non come una seconda semantica operativa esposta in griglia.

## 2026-04-14 - Quantita` Banco guidata da `U.M.` principale legacy e listino per quantita`

Problema chiuso:
- la riga Banco usava ancora `PZ` fisso in inserimento articolo e non era collegata in modo credibile alle fasce prezzo quantita` del legacy;
- la mini combo libera su `U.M.` non rappresentava il meccanismo reale `articolo + U.M. principale + scaglioni prezzo quantita`` del gestionale.

Regola viva:
- la griglia Banco deve mostrare una sola `U.M.` principale reale dell'articolo, letta dal legacy, senza introdurre unita` operative parallele lato utente;
- se l'articolo ha fasce prezzo per quantita`, quantita` minima di vendita o multipli di vendita, Banco deve proporre un modale leggero che elenca le soglie prezzo legacy e permette di confermare la quantita` da inserire;
- l'eventuale seconda unita` legacy resta solo informazione tecnica di supporto nel modale, non una nuova scelta libera in griglia;
- quando la stessa referenza viene reinserita, Banco deve accorpare la riga esistente, aggiornare la quantita` e ricalcolare il prezzo unitario sulla fascia quantita` corretta;
- anche le variazioni quantita` sulla riga articolo devono provare a riallineare il prezzo alla fascia quantita` del legacy, senza cambiare dominio o introdurre listini paralleli.

## 2026-04-15 - Cache charset MySQL per ridurre timeout sporadici Banco

Problema chiuso:
- alcune operazioni Banco ad alta frequenza, in particolare ricerca/inserimento articoli, potevano rifare piu` aperture MySQL ravvicinate;
- ogni apertura rifaceva anche la sonda charset sul DB legacy prima della connessione reale, aumentando il numero totale di handshake e rendendo piu` fragile il flusso in caso di risposta lenta del server MySQL.

Regola viva:
- la risoluzione automatica del charset MySQL deve essere cacheata per host/porta/database/utente, cosi` il Banco non ripete la sonda a ogni nuova connessione verso lo stesso gestionale;
- il probing charset resta consentito solo al primo aggancio o quando cambia davvero la configurazione DB;
- nei flussi Banco ad uso cassa la stabilita` della connessione deve avere priorita` rispetto a controlli ridondanti ripetuti su ogni query.

## 2026-04-15 - Prezzi articolo Banco allineati ai listini vendita, non ad `Acquisto`

Problema chiuso:
- nella ricerca articolo e nel modale quantita`, Banco poteva prendere il prezzo massimo da `articololistino` senza distinguere il listino di vendita dal listino `Acquisto`;
- questo portava casi reali come `HC0125` a mostrare `8,784` ivato dal listino acquisto invece del prezzo vendita corretto (`4,00` a q.ta 1 e `3,00` da q.ta 5).

Regola viva:
- i prezzi articolo Banco devono essere letti dal listino di vendita preferito coerente col gestionale, privilegiando i listini vendita rispetto ad `Acquisto`;
- il prezzo base della ricerca articolo deve riflettere la prima fascia quantita` del listino vendita scelto;
- il modale quantita` deve mostrare solo le fasce del listino vendita selezionato, senza mescolare scaglioni o valori provenienti da listini di acquisto.
## 2026-04-15 - Lookup articoli Ricerca acquisti ripulito per varianti e tastiera

- Nella finestra `Ricerca acquisti`, il lookup articolo ora nasconde automaticamente l'articolo padre quando nei risultati sono presenti varianti figlie dello stesso articolo: l'operatore vede e seleziona direttamente le varianti reali, senza righe ambigue o duplicate.
- Il textbox `Articolo` passa ora con `Freccia giu` / `Freccia su` direttamente alla lista risultati, selezionando subito la prima o l'ultima riga disponibile.
- La griglia del lookup articoli e` stata alleggerita visivamente con header piu` pulito, righe alternate morbide e allineamenti piu` leggibili per prezzo e quantita`.

## 2026-04-15 - Routine Banco per giacenza zero o negativa in inserimento articolo

- In `Banco`, se l'articolo selezionato ha giacenza zero o negativa, l'inserimento non prosegue piu` in modo implicito: viene aperta una scelta operativa con tre esiti.
- Esiti disponibili:
  - `Scarica comunque`: la riga resta articolo ufficiale con codice e riferimento anagrafico.
  - `Riga manuale`: Banco inserisce una riga manuale senza codice articolo e senza legame anagrafico, mantenendo descrizione, prezzo e IVA della selezione.
  - `Annulla`: l'inserimento viene annullato e il focus torna alla ricerca articolo.
- La routine vale anche per barcode e auto-add, cosi` le giacenze non disponibili non vengono piu` scaricate in silenzio.

## 2026-04-15 - Decompilatore come supporto tecnico di riferimento

Regola viva:
- il decompilatore di `Facile Manager` puo` essere usato come supporto tecnico per analisi mirate del gestionale reale;
- serve a ricavare riferimenti utili su classi, proprieta`, flussi, query, segnali legacy e routine effettivamente usate dal programma;
- e` uno strumento di reverse engineering tecnico, non una nuova fonte di verita` del dominio Banco;
- non sostituisce `Doc/AGENTS.md`, `Doc/STRUTTURA_BANCO.md` e `Doc/DIARIO.md`, che restano la fonte primaria del progetto;
- ogni informazione ricavata dal decompilato va interpretata come supporto tecnico interno e non deve introdurre semantiche parallele, stati utente tecnici o percorsi alternativi al documento ufficiale su `db_diltech`.

## 2026-04-15 - POS carta: il `NAK` non puo` piu` fiscalizzare

Problema chiuso:
- il fallback tecnico sull'ultimo esito POS (`G`) poteva promuovere come valido un pagamento carta non realmente avviato sul terminale corrente, facendo partire lo scontrino anche senza richiesta visibile sul POS.

Regola viva:
- per `Scontrino` con pagamento `Carta`, il POS deve intercettare davvero la richiesta corrente e confermarla come nuova transazione;
- un `NAK` sulla richiesta corrente non puo` piu` sbloccare la fiscalizzazione;
- in caso di `NAK` il Banco deve fermare lo scontrino, lasciare la scheda aperta e chiedere un nuovo tentativo reale sul terminale;
- il recupero dell'ultimo esito POS puo` restare solo diagnostico in futuro, ma non deve essere usato come semaforo verde per la fiscalizzazione del documento corrente.

Aggiornamento correttivo:
- esistono anche casi reali in cui il POS completa il pagamento ma il finale applicativo verso il Banco arriva tardi o viene perso entro la prima finestra di attesa;
- il flusso POS Banco deve quindi usare una finestra di attesa piu` larga e, solo in caso di timeout senza `NAK`, provare un recupero mirato dell'ultimo esito;
- il recupero dopo timeout e` valido solo se l'esito recuperato e` approvato e l'importo restituito dal terminale coincide con l'importo del pagamento corrente;
- il `NAK` resta comunque bloccante: non puo` essere promosso a pagamento valido e non puo` fiscalizzare il documento.

Aggiornamento operativo:
- se il Banco non riesce a confermare automaticamente l'esito finale POS ma il caso resta ambiguo, la scheda `Banco` deve avvisare esplicitamente l'operatore;
- l'avviso deve dire di non reinviare il pagamento quando il terminale mostra gia` la transazione accettata;
- in quel caso il percorso corretto e` la stampa manuale del registratore, non un nuovo invio automatico al POS;
- i KO netti del terminale (`NAK`, rifiuto esplicito, connessione assente) restano invece casi da ritentare solo come nuovo pagamento reale.

## 2026-04-15 - `Annulla contenuto` con scelta operativa a tre vie

Problema chiuso:
- il pulsante `Annulla contenuto` nella scheda `Vendita Banco` offriva solo una conferma binaria, troppo rigida per il flusso cassa reale.

Regola viva:
- `Annulla contenuto` deve aprire un popup operativo con tre scelte:
- `Rimanda`: non modifica la scheda corrente;
- `Aggiungi articolo`: lascia invariata la scheda e riporta il focus alla ricerca articolo;
- `Annulla`: azzera davvero righe e pagamenti della scheda corrente mantenendo la scheda aperta.

## 2026-04-15 - Accorpamento Banco varianti solo con stesso barcode reale

Problema chiuso:
- due prodotti figli dello stesso articolo padre potevano finire sulla stessa riga Banco perche` condividevano `OID` padre e `CodiceArticolo`, con il rischio di ricalcolare la riga al prezzo del figlio piu` recente.

Regola viva:
- nel Banco l'accorpamento automatico delle righe articolo deve considerare uguali le varianti solo se condividono lo stesso barcode reale selezionato;
- due figli diversi dello stesso padre non devono piu` fondersi sulla stessa riga solo per `OID` o `CodiceArticolo` uguali;
- per gli articoli standard senza barcode variante dedicato resta valido l'accorpamento sulla stessa riga articolo;
- il fix deve restare dentro il flusso Banco collegato al legacy, senza reintrodurre persistenza SQLite come appoggio della vendita.

## 2026-04-15 - Regola esplicita: SQLite fuori dal flusso vendita Banco

Chiarimento architetturale:
- nel Banco operativo la vendita deve dipendere dal legacy `db_diltech` per interrogazione, prezzi, documento e pubblicazione;
- SQLite / local store non deve essere usato come appoggio del flusso vendita Banco;
- il supporto locale resta ammesso solo per moduli separati dal dominio vendita legacy, come liste o funzioni interne non appartenenti al gestionale Facile Manager.

## 2026-04-15 - Prezzo varianti Banco legato al figlio selezionato

Problema chiuso:
- alcune varianti figlie con stesso padre, come casi tipo `BS0021` / `BS0022`, potevano perdere il proprio prezzo specifico perche` il Banco rileggeva il dettaglio prezzi solo per `OID` padre.

Regola viva:
- quando l'articolo selezionato e` una variante con barcode proprio, il prezzo Banco deve restare legato al figlio selezionato e non essere sostituito dal listino generico del padre;
- la cache prezzi del Banco non deve piu` condividere lo stesso dettaglio tra varianti diverse che hanno barcode diversi;
- i ricalcoli quantita` di una variante devono preservare il prezzo coerente con la variante stessa, non con l'articolo padre.

Aggiornamento correttivo:
- nel legacy esistono casi reali come `BS0021` / `BS0022` in cui il prezzo variante non vive sul multicodice ma nelle righe `articololistino` marcate come `>> Variante`;
- la lettura prezzi Banco deve quindi filtrare `articololistino` anche per `Variantedettaglio1/2` del figlio selezionato;
- solo in assenza di una riga variante-specifica e` ammesso il fallback al prezzo base del padre;
- questa regola deve valere sia nel prezzo iniziale della ricerca articolo sia nel dettaglio prezzi usato per il ricalcolo quantita` della riga Banco.

## 2026-04-15 - `Lista riordino` spostata fuori da `Documenti` e collocata sotto `Magazzino`

Decisione chiusa:
- `Lista riordino` non deve piu` vivere come sezione o tab della pagina `Documenti`;
- la sua collocazione corretta e` sotto `Magazzino`, come modulo autonomo apribile in una scheda propria e chiudibile;
- l'accesso rapido da `Banco` resta ammesso come scorciatoia operativa verso la stessa destinazione reale.

Regola viva:
- `Documenti` deve restare focalizzato sui documenti ufficiali e locali gia` previsti dal suo dominio, senza diventare contenitore di moduli tecnici eterogenei;
- `Lista riordino` e` un supporto locale separato, destinato ad accumulare fabbisogni per fornitore e a preparare il passaggio operativo successivo su `Facile Manager`;
- il riordino puo` crescere sotto `Magazzino` con funzioni come raggruppamento per fornitore, stampa prospetto e stato `Processato su FM`, ma senza diventare ordine ufficiale legacy.

## 2026-04-15 - Primo step `Articolo magazzino` con dati legacy + parametri SQLite

Step chiuso:
- sotto `Magazzino` esiste ora una prima scheda `Articolo magazzino`;
- la schermata cerca gli articoli reali del legacy, apre la scheda sullo stesso `ArticoloOid` e mostra in modo separato i dati ufficiali letti dal DB;
- i parametri locali di riordino vengono salvati su SQLite per lo stesso articolo, senza creare una seconda anagrafica o scrivere nel legacy.

Regola viva:
- nella maschera articolo di magazzino i campi legacy devono restare riconoscibili visivamente rispetto ai campi locali;
- i dati ufficiali letti dal legacy sono solo consultati e ricaricabili;
- i dati SQLite servono a completare cio` che `Facile Manager` non esprime bene, come confezione, pezzo singolo, multipli e giorni copertura del riordino.

## 2026-04-15 - Modulo root `Banco.Magazzino`

Decisione chiusa:
- la logica della scheda `Articolo magazzino` non deve crescere dentro il solo progetto `Banco.UI.Wpf`;
- esiste ora un modulo root dedicato `Banco.Magazzino`, destinato a ospitare la logica applicativa di articolo, varianti, barcode e funzioni future di magazzino;
- `Banco.UI.Wpf` resta host della vista WPF e della navigazione, mentre `Banco.Magazzino` diventa la casa naturale della scheda articolo;
- la chiave locale di riordino articolo non e` piu` solo `ArticoloOid`, ma articolo + variante/barcode reale, cosi` le varianti restano indipendenti.

## 2026-04-15 - Articolo magazzino con legacy editabile e default padre sulle varianti

Problema chiuso:
- la scheda `Articolo magazzino` era nata troppo prudente sul lato legacy, mentre la crescita futura del modulo richiede modifica diretta dei campi legacy gia` gestiti;
- sui parametri locali di riordino non era ancora esplicito il comportamento corretto tra padre e varianti.

Regola viva:
- nella scheda `Articolo magazzino` i campi legacy gia` supportati dal modulo sono modificabili sia sul padre sia sulla variante selezionata;
- i parametri locali salvati sul padre diventano il default di tutte le varianti senza override locale, incluse quelle aggiunte in futuro;
- se una variante salva i propri parametri locali, quell'override resta confinato solo a quella variante e non si propaga al resto della famiglia.

## 2026-04-15 - Disponibilita` Banco letta da `articolosituazione`

Problema chiuso:
- su piu` articoli e varianti la disponibilita` mostrata in ricerca e in griglia Banco poteva risultare `0` anche quando il DB legacy riportava una giacenza reale diversa.

Causa:
- la ricerca Banco privilegiava le tabelle `valorizzazionearticolo` e `valorizzazionearticolovariante`, ma su diversi articoli il dato corrente vive invece in `articolosituazione` e `articolosituazionecombinazionevariante`.

Regola viva:
- per la disponibilita` articolo Banco la fonte prioritaria deve essere `articolosituazione`;
- per la disponibilita` variante Banco la fonte prioritaria deve essere `articolosituazionecombinazionevariante`;
- le tabelle `valorizzazione*` restano solo fallback tecnico quando la situazione corrente non e` disponibile.

## 2026-04-15 - Recupero Banco modificabile e conferma cliente con `Invio`

Problema chiuso:
- alcune vendite Banco richiamate dal legacy potevano aprirsi troppo prudentemente senza possibilita` di variazione, anche se non risultavano fiscalizzate davvero;
- nel box cliente, premendo `Invio` dopo la ricerca il nominativo non veniva confermato sul documento e quindi intestazione cliente e punti restavano non aggiornati.

Regola viva:
- una scheda Banco richiamata dal legacy deve restare operativa finche` non esiste un segnale fiscale affidabile di chiusura reale;
- i soli pagamenti scontrinati o metadati locali storici non devono bloccare da soli la modifica di una vendita Banco recuperabile;
- nel campo cliente, `Invio` deve confermare automaticamente il miglior match utile della ricerca, cosi` nominativo e saldo punti si riallineano subito sulla scheda corrente.

## 2026-04-15 - Click pagamenti Banco stabilizzati

Problema chiuso:
- il click su `Contanti`, `Carta`, `Sospeso` poteva risultare poco reattivo o apparentemente bloccato pur senza coinvolgere POS, fiscalizzazione o publish legacy.

Causa:
- i tre tipi pagamento erano gestiti come area cliccabile generica con `Border` + `MouseLeftButtonDown`, quindi con un'interazione meno stabile del pulsante WPF dedicato e piu` esposta a interferenze di focus con le textbox pagamento.

Correzione:
- il tipo pagamento per `Contanti`, `Carta`, `Sospeso` usa ora un vero `Button` dedicato.
- lasciato invariato il resto del flusso: calcolo importi, invio POS, scontrino automatico, pubblicazione legacy e fiscalizzazione.

Comportamento atteso:
- il click sul tipo pagamento deve valorizzare subito il relativo importo senza tempi morti anomali;
- l'input manuale nelle textbox pagamento resta invariato.

## 2026-04-16 - Popup `Articolo inesistente` solo su inserimento diretto

Problema chiuso:
- nella scheda `Vendita Banco`, un barcode o un codice articolo inesistente lasciava solo il messaggio in barra stato, senza avviso esplicito all'operatore.

Regola viva:
- se l'operatore conferma una ricerca articolo diretta (barcode, codice o token tecnico singolo) e il legacy non restituisce alcun articolo, Banco deve mostrare un popup `Articolo inesistente`;
- il popup non deve comparire durante la ricerca descrittiva live multi-termine, per non disturbare l'uso normale del catalogo;
- il comportamento resta quindi distinto tra ricerca descrittiva operativa e tentativo di inserimento diretto di un codice non presente.

## 2026-04-15 - Cliente Banco con nominativo completo in conferma

Problema chiuso:
- dopo la selezione cliente il box di ricerca poteva restare sul testo digitato invece di mostrare il nominativo confermato;
- per i clienti persona fisica non era garantita la visualizzazione completa `Nome Cognome`.

Correzione:
- la conferma cliente riallinea sempre il testo del box al nominativo confermato;
- il nominativo mostrato usa ora la composizione completa `Nome Cognome` quando entrambi i campi legacy sono presenti.

Comportamento atteso:
- dopo conferma cliente deve comparire il nominativo completo nel box cliente;
- banner e riferimenti UI devono restare coerenti con lo stesso nominativo.

## 2026-04-15 - Promo punti riproposta al raggiungimento di nuova soglia

Problema chiuso:
- durante la vendita il Banco poteva non riproporre il premio anche se, aggiungendo articoli, il cliente raggiungeva davvero una soglia punti utile sul documento corrente.

Causa:
- la soppressione del popup premio considerava in modo troppo rigido il solo tipo dell'ultimo evento storico del documento, senza distinguere se la vendita aveva poi raggiunto una soglia nuova o piu` alta.

Correzione:
- la valutazione promo tiene ora conto anche di regola, punti disponibili e punti richiesti dell'ultimo evento registrato;
- il popup viene riproposto quando la vendita corrente raggiunge una soglia premio nuova o superiore rispetto a quella gia` valutata sul documento.

Comportamento atteso:
- se durante la vendita il cliente raggiunge con gli articoli correnti i punti necessari a un premio, il Banco deve proporre la promo;
- se la stessa identica soglia e` gia` stata valutata sullo stesso documento, il popup non deve ripetersi inutilmente.

## 2026-04-15 - Messaggio promo incompleta reso esplicito

Problema chiuso:
- in vendita il Banco mostrava il messaggio generico `Regole premio incomplete`, senza dire quale campo mancasse davvero nella configurazione della regola.

Correzione:
- il messaggio promo indica ora il motivo concreto della non compatibilita`, ad esempio punti richiesti mancanti, articolo premio mancante o sconto non valorizzato.

Comportamento atteso:
- quando una regola attiva non e` completa, il Banco deve mostrare subito quale dato manca nella configurazione.

## 2026-04-15 - Focus su ricerca articoli dopo conferma cliente

Problema chiuso:
- dopo la conferma cliente il focus poteva restare nell'area cliente invece di tornare subito alla ricerca articoli.

Correzione:
- la conferma cliente richiede ora esplicitamente il ritorno del focus sulla textbox ricerca articoli.

Comportamento atteso:
- dopo aver confermato il cliente, l'operatore deve poter continuare subito con la ricerca articolo senza click aggiuntivi.

## 2026-04-15 - Resize colonne Banco ripristinato dopo vendita

Problema chiuso:
- nella griglia documento del Banco, dopo una vendita la regolazione della larghezza colonne poteva smettere di funzionare.

Causa:
- il template personalizzato della testata colonna non esponeva i `Thumb` standard usati dal `DataGridColumnHeader` per il resize.

Correzione:
- reinseriti i `PART_LeftHeaderGripper` e `PART_RightHeaderGripper` nel template header, mantenendo l'aspetto grafico esistente.

Comportamento atteso:
- la larghezza colonne deve restare regolabile in modo stabile anche dopo vendite e refresh della scheda.

Nota UI:
- i `Thumb` di resize della testata restano presenti ma visivamente invisibili, cosi` la griglia non mostra separatori marcati o artefatti neri.

## 2026-04-15 - Sblocco manuale fiscalizzati dalla scheda Banco

Problema chiuso:
- i documenti fiscalizzati/scontrinati chiusi si aprivano solo in consultazione bloccata, senza un punto operativo chiaro per abilitare una correzione manuale controllata.

Correzione:
- la scheda `Banco` mostra ora un pulsante esplicito `Abilita modifica` solo sui documenti fiscalizzati aperti in consultazione;
- lo sblocco rende modificabile la scheda corrente senza riattivare nuove emissioni fiscali dalla stessa schermata;
- `Salva` / `F4` resta il comando di chiusura senza stampa aggiuntiva, usato anche dopo lo sblocco manuale.

Regola viva:
- documento fiscalizzato = consultazione iniziale prudenziale;
- modifica possibile solo dopo sblocco esplicito dalla scheda Banco;
- nessuna nuova ristampa fiscale automatica nel flusso di correzione;
- nessuna cancellazione libera o nuova emissione `Scontrino` / `Cortesia` dalla stessa scheda fiscalizzata sbloccata.

## 2026-04-15 - Writer Banco riallineato su U.M. e varianti documento

Problema chiuso:
- alcune righe Banco recenti venivano scritte su `documentoriga` senza `Unitadimisura` e senza record in `documentorigacombinazionevarianti`, quindi `FM` mostrava U.M. vuota e perdeva il contesto variante su documenti riaperti o corretti.

Verifica reale:
- sul DB `db_diltech`, una riga Banco recente di `PD0120_1` mostrava `Unitadimisura = NULL` e nessuna combinazione variante;
- righe Banco precedenti corrette dello stesso articolo avevano invece `Unitadimisura = 1` (`PZ`) e la relativa combinazione variante con barcode.

Correzione:
- la richiesta fiscale ora porta anche `UnitaMisura`, barcode e OID variante;
- il writer ufficiale valorizza `documentoriga.Unitadimisura`, `documentoriga.Valorebase`, `documentoriga.Valoreivato`, `documentoriga.Codiceabarre` e inserisce la riga in `documentorigacombinazionevarianti` quando serve;
- la lettura dettaglio documento legacy ricarica gli stessi dati, cosi` una vendita recuperata non perde piu` U.M. e variante al salvataggio successivo.

## 2026-04-15 - Propagazione esplicita parametri locali padre -> varianti

Problema chiuso:
- il solo salvataggio dei parametri locali sul padre garantiva l'ereditarieta` per le varianti senza override, ma non offriva un'azione esplicita per riscrivere subito anche le varianti gia` presenti in lista.

Correzione:
- nella sezione `SQLITE INTERNO` della scheda `Articolo magazzino` c'e` ora il pulsante `Applica ai figli`;
- il comando salva automaticamente prima il padre e poi propaga gli stessi parametri locali a tutte le varianti correnti della famiglia articolo;
- le varianti future continuano comunque a ereditare dal padre finche` non ricevono un override proprio.

## 2026-04-16 - `Lista riordino` riallineata a conferma ordine e `Q.ta da ordinare`

Problema chiuso:
- la lista riordino aveva ancora semantica vecchia `ordinato/aperto`, mentre il flusso operativo deciso e` conferma della riga per il successivo ordine fornitore;
- mancava una `Q.ta da ordinare` distinta dalla quantita` accumulata dal Banco.

Correzione:
- ogni riga mantiene ora sia `Quantita` raccolta sia `QuantitaDaOrdinare` locale modificabile;
- la schermata usa etichette coerenti con il flusso reale: `Da confermare`, `Confermate`, `Conferma selezionati`, `Riapri selezionati`;
- il check della colonna `Ok` resta il toggle rapido della riga, ma con semantica utente di conferma per ordine fornitore, non di merce arrivata.

## 2026-04-16 - `Lista riordino` con raggruppamento operativo per fornitore

Step chiuso:
- la schermata `Lista riordino` puo` ora essere vista raggruppata per fornitore, con intestazioni gruppo e contatore righe;
- il gruppo usa come chiave il fornitore scelto sulla riga e, se manca, ripiega sul fornitore suggerito;
- resta disponibile anche la vista piatta, cosi` il raggruppamento non forza un'unica modalita` di lavoro.

## 2026-04-16 - Liste fornitore generate dai confermati di `Lista riordino`

Step chiuso:
- le righe confermate della `Lista riordino` generano ora automaticamente liste locali per fornitore;
- ogni lista mostra fornitore, data corrente e contatore locale, senza creare ancora ordini sul legacy o su `Facile Manager`;
- il totale della lista fornitore si basa sulla `Q.ta da ordinare` della riga, non solo sulla quantita` raccolta dal Banco.

## 2026-04-16 - `Crea ordine su FM` ora scrive il vero `Ordine Fornitore`

Step chiuso:
- il pulsante `Crea ordine su FM` della lista fornitore non aggiorna piu` solo uno stato locale: crea davvero un documento legacy `Ordine Fornitore`;
- il writer usa `modellodocumento = 9`, numerazione progressiva del modello e righe `documentoriga` costruite dalla `Q.ta da ordinare`, dal prezzo suggerito e dall'`IvaOid` salvato nella lista riordino;
- solo dopo la creazione documento su FM la lista fornitore passa a stato `Creato su FM` e conserva il riferimento numero/anno del documento generato.

## 2026-04-16 - Rimozione conferma intermedia su `Salva` Banco

Problema chiuso:
- la scheda `Banco` mostrava un popup intermedio `Registra su legacy` prima di `Salva`, introducendo ambiguita` su un punto che nel dominio e` invece obbligatorio.

Correzione:
- il comando `Salva` non chiede piu` conferma per la pubblicazione legacy;
- la vendita Banco continua a pubblicare o aggiornare direttamente il documento ufficiale su `db_diltech`;
- resta invariato il controllo pagamenti di default prima del salvataggio, ma senza dialog aggiuntivi sul fatto che il legacy sia opzionale.

Regola viva:
- le vendite Banco vanno sempre sul legacy;
- `Salva` e` un publish/update diretto del documento ufficiale, non una scelta da confermare.

## 2026-04-16 - Premio punti `Buono sconto` riallineato al `Tipo articolo` legacy

Problema chiuso:
- il premio punti configurato come articolo `Buono sconto` entrava nel Banco a `0,00`, anche se sul legacy l'articolo ha listino proprio e un `Tipoarticolo` dedicato da buono/gift card.

Verifica reale su `db_diltech`:
- l'articolo `Buono 2.5` ha `Tipoarticolo = 7` e prezzo vendita `2,50`;
- il Banco prima salvava nella regola premio solo codice, descrizione e IVA, quindi al momento dell'applicazione il premio articolo nasceva sempre con prezzo `0`.

Correzione:
- la selezione articolo premio memorizza ora anche `Tipoarticolo` e prezzo vendita dell'articolo legacy;
- quando il premio articolo usa il tipo legacy buono/gift card (`Tipoarticolo = 7`), il Banco crea la riga premio con prezzo unitario negativo pari al listino dell'articolo;
- i premi articolo normali restano invece a importo `0`, come articolo omaggio.

Regola viva:
- i buoni sconto gestiti come articolo legacy non devono entrare come riga neutra a `0,00`;
- il comportamento Banco deve seguire il `Tipoarticolo` del legacy per i premi buono/gift card.

## 2026-04-16 - Operatori Banco riallineati alla tabella `operatore`

Problema chiuso:
- nella scheda Banco la combo operatore poteva mostrare valori sporchi come `1` o `3`, perche` gli operatori venivano letti dai valori storici di `documento.Utente`;
- sul DB reale gli operatori attivi corretti stanno invece nella tabella `operatore`, con nomi come `Admin`, `R`, `A`.

Correzione:
- la lettura operatori Banco usa ora come fonte primaria la tabella legacy `operatore`, limitata agli operatori attivi;
- i codici numerici storici presenti nei documenti (`1`, `3`, ecc.) vengono normalizzati verso il nome operatore corretto usando gli identificativi legacy dell'operatore;
- in assenza di selezione esplicita, Banco preferisce `Admin` come default se presente tra gli operatori attivi.

Regola viva:
- la UI Banco non deve proporre operatori ricavati dai documenti storici come se fossero l'anagrafica ufficiale;
- l'anagrafica operatori viva va letta dalla tabella `operatore`, non da `documento`.

## 2026-04-16 - Check `Lista riordino` riallineato a selezione batch

Problema chiuso:
- nella `Lista riordino` il check della prima colonna cambiava subito stato riga (`Da confermare` / `Confermata`);
- con il filtro `Da confermare` attivo la riga spariva immediatamente dalla vista, dando l'impressione di essere stata persa;
- il comportamento era incoerente con il flusso deciso, dove la conferma deve partire solo dai pulsanti della schermata.

Correzione:
- il check della griglia e` ora solo una selezione operativa batch;
- le azioni reali `Conferma selezionati`, `Riapri selezionati` e `Rimuovi` lavorano prima sulle righe checkate e, in assenza di check, sulla riga attiva;
- il click diretto sul check non cambia piu` da solo lo stato della riga e non genera piu` spostamenti invisibili dovuti al filtro corrente.

Regola viva:
- nella `Lista riordino` il check non deve avere effetti di dominio immediati;
- le modifiche di stato della riga devono partire solo da pulsanti espliciti o dal menu destro coerente della stessa schermata.

## 2026-04-16 - `Lista riordino`: modifica diretta `Q.ta da ordinare` e rimozione lista fornitore

Problema chiuso:
- nella griglia `Lista riordino` la cella `Q.ta da ordinare` poteva restare selezionabile ma non entrare davvero in modifica;
- mancava un comando esplicito per eliminare un'intera lista fornitore locale con tutte le sue righe.

Correzione:
- il click sulla cella `Q.ta da ordinare` apre ora l'editor numerico della griglia e seleziona subito il valore;
- il dettaglio lista fornitore espone ora `Elimina lista`, con conferma esplicita;
- l'eliminazione lista rimuove dal local store sia il draft fornitore sia tutte le righe locali appartenenti a quel gruppo fornitore.

Regola viva:
- finche` una lista fornitore non e` registrata su FM, l'operatore deve poter correggere la `Q.ta da ordinare` delle sue righe e puo` anche eliminare l'intera lista locale;
- `Elimina lista` non deve creare, annullare o toccare documenti su FM: agisce solo sul supporto locale di riordino.

## 2026-04-16 - Grouping `Lista riordino` sicuro durante edit riga

Problema chiuso:
- cambiando fornitore nella `Lista riordino`, il refresh del raggruppamento poteva partire mentre la `DataGrid` era ancora in transazione `EditItem`;
- WPF sollevava `InvalidOperationException` su `DeferRefresh`, bloccando la schermata durante l'uso della combo fornitore.

Correzione:
- il refresh del grouping ora prova subito il ricalcolo normale;
- se la griglia e` ancora in edit e WPF blocca `DeferRefresh`, il ricalcolo viene rimandato al dispatcher appena la transazione di modifica e` chiusa.

Regola viva:
- nella `Lista riordino` il cambio fornitore non deve far cadere la schermata;
- raggruppamento e riepiloghi devono aggiornarsi in modo resiliente anche mentre l'operatore sta chiudendo l'edit della cella.

## 2026-04-16 - `Conferma selezionati` non deve cambiare lista corrente

Problema chiuso:
- confermando tutte le righe della `Lista riordino`, il repository marcava la lista come `Ordinata`;
- al refresh il sistema apriva subito una nuova lista `Aperta` vuota, facendo sparire dalla schermata la lista appena confermata;
- per l'operatore sembrava che le righe fossero sparite del tutto.

Correzione:
- la conferma delle righe non autochiude piu` la lista corrente;
- la lista resta `Aperta` finche` non viene processata/chiusa esplicitamente nel flusso fornitore;
- se esiste gia` una lista vuota aperta creata dal vecchio comportamento e l'ultima lista utile era stata chiusa solo in automatico, il repository riapre quella lista utile e scarta la lista vuota senza righe.

Regola viva:
- `Conferma selezionati` non deve spostare l'utente su una nuova lista vuota;
- la lista corrente deve restare la stessa finche` l'operatore non conclude davvero il ciclo di lavorazione.

## 2026-04-16 - `Elimina lista` e rebuild liste fornitore coerenti con l'azione reale

Problema chiuso:
- nella `Lista riordino`, dopo `Elimina lista` il blocco `Liste fornitore generate` poteva mostrare card duplicate o riesumare stati locali vecchi;
- il messaggio utente indicava una rimozione riuscita, ma la UI non restava allineata all'atto effettivo;
- il problema nasceva da refresh/rebuild concorrenti della schermata e da possibili draft duplicati nel local store per lo stesso fornitore.

Correzione:
- il refresh generale della `Lista riordino` e il rebuild delle liste fornitore sono ora serializzati, per evitare doppie ricostruzioni concorrenti;
- il rebuild UI delle liste fornitore costruisce prima uno snapshot consistente e poi sostituisce la collezione visibile in un solo passaggio;
- il repository ripulisce automaticamente eventuali draft duplicati dello stesso fornitore, mantenendo solo lo stato piu` recente.

Regola viva:
- nella `Lista riordino` ogni azione utente deve produrre un effetto visibile unico e coerente;
- `Elimina lista` non deve mai generare nuove card o riattivare stati vecchi;
- riepilogo, card fornitore e griglia devono riflettere lo stesso set dati reale dopo ogni refresh.

## 2026-04-16 - `Lista riordino` riallineata a sorgente righe + liste fornitore

Problema chiuso:
- la schermata mischiava in modo ambiguo le righe sorgente di riordino e le liste fornitore generate;
- i comandi globali in alto sembravano agire sulle card fornitore, ma in realta` lavoravano sulle righe della griglia;
- mancava un inserimento manuale nativo di articoli nella lista riordino, senza passare da una vendita Banco.

Correzione:
- la parte operativa delle righe sorgente espone ora ricerca live articolo e `Aggiungi articolo` direttamente nella pagina;
- i comandi `Conferma`, `Riapri`, `Modifica`, `Rimuovi` sono stati riallineati alla selezione reale della griglia righe;
- la selezione righe usa la selezione nativa della `DataGrid` e non richiede piu` checkbox dedicate;
- il blocco inferiore resta focalizzato solo sulle liste fornitore generate e sulle loro azioni di stato.

Regola viva:
- nella `Lista riordino` la parte alta rappresenta la sorgente delle righe da lavorare, provenienti dal Banco o inserite manualmente;
- la parte bassa rappresenta l'output organizzato per fornitore;
- i pulsanti devono stare nella zona su cui agiscono davvero e devono attivarsi solo quando l'azione e` possibile.

## 2026-04-17 - `FastReport` designer esterno riallineato a datasource persistente

Problema chiuso:
- il Community Designer esterno apriva `Pos.frx`, ma al salvataggio segnalava `Righe: Table is not connected to the data`;
- la causa reale non era nei binding testuali del layout, ma nel fatto che `Pos.frx` conteneva solo una `TableDataSource` alimentata da `RegisterData(...)` a runtime dentro Banco;
- aprendo il `.frx` direttamente nel designer esterno, quella tabella non risultava davvero connessa, quindi la validazione falliva.

Correzione:
- il layout pilota `Pos.frx` usa ora una `CsvDataConnection` persistente, non piu` una tabella isolata registrata solo in memoria;
- Banco genera e aggiorna prima dell'apertura designer il file `Stampa\\DesignData\\Pos.righe.csv`, con le righe reali della vendita esempio `2308`;
- prima di preview, test stampa o `Apri designer`, il modulo riallinea il `.frx` alla connessione CSV corrente e collega di nuovo `DataRighe` al datasource `Righe`;
- il designer FastReport copiato nella root del workspace continua a essere rilevato automaticamente, ma va aperto dal programma Banco per avere il datasource coerente.

Verifica tecnica:
- build pulita di `Banco.UI.Wpf` su output separato;
- probe runtime separata riuscita con generazione reale di `Pos.frx` contenente `CsvDataConnection Name="RigheCsv"` e file `Pos.righe.csv` popolato.

Regola viva:
- per il nuovo modulo stampa, il designer FastReport non va considerato un editor standalone del solo `.frx`;
- il flusso affidabile resta `Banco -> prepara dati design-time -> apre designer`.

## 2026-04-17 - `Pos.frx` esposto al designer con parametri persistenti

Problema chiuso:
- nel Community Designer, dopo il fix del datasource `Righe`, restavano visibili solo i campi della vendita riga;
- intestazione negozio, testata documento, cliente e totali venivano ancora applicati solo scrivendo i `TextObject` a runtime, quindi il designer non li mostrava come sorgenti riutilizzabili.

Correzione:
- `Pos.frx` espone ora i campi dinamici non-riga come `Parameter` persistiti nel report;
- i `TextObject` principali del layout pilota usano espressioni tipo `[StoreRagioneSociale]`, `[TestataPagamentoLabel]`, `[ClienteNominativo]`, `[TotaliTotaleDocumentoVisuale]` e simili;
- il runtime Banco valorizza questi parametri con `SetParameterValue(...)` prima della preview/stampa;
- la rigenerazione del template pilota verifica anche la presenza dei parametri chiave, cosi` un vecchio `Pos.frx` viene riallineato automaticamente.

Verifica tecnica:
- probe runtime separata confermata con `Pos.frx` generato contenente sia `CsvDataConnection Name="RigheCsv"` sia l'elenco dei `Parameter` persistiti nel `Dictionary`.

Regola viva:
- nel nuovo modulo stampa, i dati dinamici che l'operatore deve poter riusare nel designer non vanno lasciati solo come testo impostato da codice;
- devono comparire come sorgenti esplicite del report (`DataSource` o `Parameter`) gia` presenti nel `.frx`.

## 2026-04-17 - base generale `famiglie report` oltre il solo POS

Problema chiuso:
- il modulo stampa stava crescendo bene sul caso `POS`, ma senza una base generale avrebbe richiesto interventi ad hoc per ogni nuova stampa (`Elenco clienti`, `Lista articoli`, `Catalogo articoli`, ecc.);
- mancava un punto unico che dicesse, per ogni famiglia report, quali gruppi campi esistono e dove cercarli logicamente tra testata, corpo e sommario.

Correzione:
- `Banco.Stampa` espone ora un catalogo famiglie report dedicato, separato dal solo contratto del layout POS;
- sono state introdotte le famiglie iniziali `POS / Vendita banco`, `Elenco clienti` e `Lista articoli`, ognuna con gruppi campi e livello di confidenza del mapping;
- la tab `FastReport` mostra ora anche la sezione `Famiglie report`, cosi` il progetto ha gia` un punto tecnico leggibile per capire dove cercare campi come punti, cliente, prezzi, giacenze o footer;
- lo schema dati del modulo non e` piu` limitato a `receipt-80-db`: sono stati aggiunti anche gli schemi base `customers-list` e `articles-list`.

Regola viva:
- nuovi report futuri non devono nascere come eccezioni locali o layout con campi inventati;
- devono entrare nella famiglia corretta, con gruppi logici espliciti e sorgenti legacy dichiarate come certe, dedotte forti o da verificare.

## 2026-04-17 - Parametri POS raggruppati davvero nel designer FastReport

Problema chiuso:
- nel layout POS `Pos.frx` i campi righe erano gia` esposti come datasource `Righe`, ma intestazione negozio, testata documento, totali, cliente e footer comparivano ancora nel designer come parametri piatti senza sezioni reali;
- questo rendeva incoerente la promessa fatta nella pagina `FastReport`: il catalogo famiglie mostrava gruppi logici, ma il Community Designer non li rifletteva davvero nel file `.frx`.

Correzione:
- il runtime POS crea ora parametri annidati veri nel `Dictionary` del report, usando gruppi `Negozio`, `Testata`, `Totali`, `Cliente`, `Punti` e `Footer`;
- i `TextObject` principali del template pilota sono stati riallineati a binding come `[Negozio.RagioneSociale]`, `[Testata.PagamentoLabel]`, `[Totali.TotaleDocumentoVisuale]`, `[Cliente.Nominativo]` e `[Footer.Website]`;
- durante la sincronizzazione del layout, il modulo rimuove i vecchi parametri piatti legacy e aggiorna automaticamente le espressioni dei campi noti del `Pos.frx`, senza lasciare doppioni nel designer;
- il catalogo famiglia `POS / Vendita banco` e` stato riallineato agli stessi nomi annidati, cosi` la documentazione tecnica mostrata in UI e il `.frx` reale parlano la stessa lingua.

Verifica tecnica:
- probe isolata su `FastReport.OpenSource` confermata con serializzazione reale di parametri annidati nel `.frx` (`<Parameter Name="Negozio"> ... </Parameter>`) e valorizzazione via `SetParameterValue("Negozio.RagioneSociale", ...)`.

Regola viva:
- se nel modulo stampa una famiglia report dichiara gruppi logici espliciti, il layout FastReport corrispondente deve esporli davvero nel designer come gruppi annidati o datasource separati;
- non e` accettabile che la UI tecnica mostri sezioni concettuali mentre il `.frx` reale resta piatto e costringe l'operatore a cercare i campi senza struttura.

## 2026-04-17 - Datasource `Righe` POS esteso oltre i soli 5 campi minimi

Problema chiuso:
- nel designer POS il datasource `Righe` esponeva solo `QuantitaVisuale`, `Descrizione`, `PrezzoUnitarioVisuale`, `ScontoVisuale`, `ImportoRigaVisuale`;
- diversi campi gia` presenti nel backend Banco (`CodiceArticolo`, `Barcode`, `OrdineRiga`, `UnitaMisura`, valori numerici riga) non comparivano nel layout design-time, quindi l'operatore non poteva usarli senza nuovo codice.

Correzione:
- il file dati design-time `Stampa\\DesignData\\Pos.righe.csv` viene ora scritto con tutto il set campi riga gia` disponibile in `FastReportPreviewRow`;
- la sincronizzazione del layout aggiorna anche lo schema della `TableDataSource Righe`, aggiungendo i campi mancanti senza ricreare l'intero report;
- il contratto famiglia `POS / Vendita banco` e` stato allargato agli stessi campi riga reali, cosi` UI tecnica e designer restano allineati.

Campi aggiunti a `Righe`:
- `RigaOid`
- `CodiceArticolo`
- `Barcode`
- `OrdineRiga`
- `Quantita`
- `UnitaMisura`
- `PrezzoUnitario`
- `ScontoPercentuale`
- `ImportoRiga`
- `AliquotaIva`

Verifica tecnica:
- `Banco.Stampa` compila con `0` errori e `0` warning dopo l'estensione del datasource;
- la build completa `Banco.UI.Wpf` resta sensibile ai lock delle DLL quando `Banco` o Visual Studio tengono aperta la cartella `bin`, quindi per vedere lo schema aggiornato serve riavvio pulito dell'app.

Regola viva:
- per i layout gestionali il designer deve esporre non solo i campi visuali pronti da stampa, ma anche i valori tecnici/numerici gia` disponibili nel backend quando sono realmente usabili nella famiglia report corrente.

## 2026-04-17 - POS con collezione `Pagamenti` separata nel designer

Problema chiuso:
- nel report POS i pagamenti erano leggibili solo attraverso i riepiloghi in `Totali`, ma non come collezione autonoma riusabile nel designer;
- per chi vuole costruire layout piu` ricchi o stampare dettagli pagamento, questo costringeva a riusare campi derivati invece del dato tecnico gia` presente nel backend.

Correzione:
- il design-time POS genera ora anche `Stampa\\DesignData\\Pos.pagamenti.csv`;
- il `.frx` viene sincronizzato con una connessione persistente `PagamentiCsv` e con la `TableDataSource` `Pagamenti`;
- il datasource `Pagamenti` espone almeno `Tipo`, `Importo` e `ImportoVisuale`;
- il catalogo famiglia POS e lo schema tecnico documento sono stati aggiornati agli stessi campi.

Nota operativa:
- al momento nei campi articolo/vendita POS non e` stato aggiunto nessun campo immagine, perche` nel flusso documento Banco oggi non e` ancora esposto un riferimento immagine articolo certo e stabile verso il designer;
- un eventuale campo immagine andra` aggiunto solo dopo avere verificato la sorgente reale legacy/applicativa e il formato utilizzabile nel report.

## 2026-04-17 - POS riallineato a `Pos.repx` su carta fedelta` e seconda scontistica

Problema chiuso:
- il report POS nuovo esponeva gia` righe, pagamenti e gruppi principali, ma restavano ancora scoperte alcune informazioni che il blueprint legacy `Pos.repx` usa davvero;
- in particolare mancavano nel designer il secondo sconto di riga (`Documentoriga.Sconto2`) e il codice carta fedelta` cliente (`Soggetto.Codicecartafedelta`), che nel legacy alimenta anche il barcode nel footer;
- il blocco `Totali` non esponeva ancora in modo leggibile l'importo totale pagato e l'importo carta come campi separati del contratto POS.

Correzione:
- il datasource `Righe` del POS esporta ora anche `Sconto2` e `Sconto2Visuale`, gia` presenti nel dettaglio Banco reale;
- il gruppo `Cliente` espone ora anche `CodiceCartaFedelta`, con valori design-time salvati nel `.frx` prima dell'apertura del designer;
- il gruppo `Totali` espone ora anche `TotalePagatoVisuale` e `PagatoCartaVisuale`, derivati dal dettaglio documento reale;
- il template `Pos.frx` viene rigenerato automaticamente se manca ancora il nuovo blocco `txtCodiceCartaFedelta`, cosi` una postazione con layout pilota vecchio non resta a meta` aggiornamento;
- il contratto tecnico e il catalogo famiglia POS sono stati riallineati agli stessi campi, cosi` repx legacy, UI tecnica Banco e designer FastReport restano coerenti.

Regola viva:
- quando `Pos.repx` mostra un campo reale gia` disponibile nel backend Banco, il nuovo POS deve esporlo esplicitamente nel contratto FastReport e non lasciarlo implicito o accessibile solo da codice;
- i layout pilota gia` creati devono sapersi riallineare da soli quando il contratto POS viene esteso in modo significativo.

## 2026-04-17 - Footer POS con barcode fidelity reale nel layout FastReport

Problema chiuso:
- nel footer POS il codice carta fedelta` era stato esposto come parametro e come testo, ma mancava ancora un oggetto barcode vero come nel blueprint `Pos.repx`;
- questo lasciava il nuovo `Pos.frx` meno fedele del legacy proprio nella parte fidelity, che in FM usa `Soggetto.Codicecartafedelta` anche come barcode nel piede documento.

Correzione:
- il layout POS crea ora anche un `BarcodeObject` reale (`barcodeFidelityCard`) legato a `[Cliente.CodiceCartaFedelta]`;
- il barcode viene sincronizzato anche sui template gia` esistenti senza richiedere reset completo del `.frx`, cosi` eventuali ritocchi manuali sul pilota non vengono persi;
- il footer POS mantiene anche il valore testuale della carta fedelta`, utile come fallback leggibile nel designer e nelle prove.

Verifica tecnica:
- `Banco.Stampa` compila con `0` errori e `0` warning anche con `FastReport.Barcode.BarcodeObject` e `Barcode128`, quindi il runtime Open Source effettivamente in uso supporta il barcode come oggetto nativo nel report.

Regola viva:
- quando il runtime FastReport disponibile supporta davvero un oggetto nativo coerente col blueprint legacy, Banco deve preferirlo a soluzioni simulate o solo testuali;
- le aggiunte strutturali al layout POS devono cercare di preservare il piu` possibile le modifiche manuali gia` fatte sul `.frx` pilota.

## 2026-04-17 - POS con sezione `Articoli` esplicita nel designer

Problema chiuso:
- nel designer POS i campi articolo esistevano, ma vivevano solo sotto la collezione tecnica `Righe`;
- questo rendeva poco leggibile la costruzione del layout per chi cerca mentalmente una sezione `Articoli` separata dalle altre aree del report.

Correzione:
- il design-time POS espone ora anche una seconda sorgente dati `Articolo`, alimentata dallo stesso file righe della vendita;
- la nuova sorgente ha gli stessi campi articolo/riga gia` disponibili in `Righe`, ma compare nel pannello sinistro del designer come sezione dedicata e piu` intuitiva da trovare;
- la banda operativa del report resta agganciata alla pipeline reale `Righe`, quindi non cambia il comportamento del report Banco, ma migliora la leggibilita` del designer.

Regola viva:
- quando una collezione tecnica interna ha un nome poco intuitivo per l'operatore, il designer puo` esporre anche un alias piu` leggibile pur mantenendo invariata la pipeline runtime reale del report.

## 2026-04-17 - Righe condizionali e footer POS riallineati piu` vicino a `Pos.repx`

Problema chiuso:
- il POS FastReport era gia` funzionante, ma su alcuni dettagli visivi restava ancora piu` povero del blueprint `Pos.repx`;
- in particolare le righe non rispettavano ancora del tutto la logica legacy di soppressione dei valori a zero, e il footer non mostrava ancora un blocco dedicato `Carta` prima della sezione cliente/fidelity.

Correzione:
- la mappatura riga Banco nasconde ora `QuantitaVisuale` quando la quantita` vale zero, e di conseguenza svuota anche prezzo/importo nei casi non stampabili, avvicinandosi alla logica `BeforePrint` del repx;
- il footer POS espone ora una riga `Carta` dedicata con `[Totali.PagatoCartaVisuale]`, separata dal pagamento principale e dal resto;
- gli oggetti dal blocco cliente in poi sono stati riposizionati per lasciare spazio coerente al nuovo riepilogo pagamenti senza sovrapposizioni;
- il barcode fidelity e il codice carta restano presenti e allineati nel piede finale.

Regola viva:
- nel POS FastReport i campi visuali preparati dal runtime devono rispettare il piu` possibile le regole di pulizia del report legacy, soprattutto per valori zero o non significativi;
- quando il repx storico separa chiaramente un blocco di pagamento nel footer, il nuovo layout deve cercare di mantenere la stessa leggibilita` prima della sezione cliente/fidelity.

## 2026-04-17 - Quick actions Banco in basso a destra

Problema chiuso:
- nella scheda `Banco` il riquadro azioni inferiore aveva solo `Salva` e `Nuovo`, lasciando scoperte scorciatoie operative utili che l'utente usa come supporto rapido al flusso di cassa;
- mancava anche un comando diretto `F10` per stampare il POS80 del documento corrente senza passare da `Cortesia`.

Correzione:
- il riquadro inferiore ora espone tre pulsanti aggiuntivi, affiancati e con colori distinti:
  - `Impostazioni`, che apre la configurazione Banco gia` presente in shell;
  - `Registratore di cassa`, solo icona, che apre la sezione fiscale/registratore;
  - `Stampa (F10)`, che lancia la stampa POS80 diretta del documento corrente;
- `F10` e` ora intercettato nella schermata `Banco` e richiama la stessa stampa POS80 diretta del nuovo pulsante;
- la stampa POS80 rapida non pubblica nuovamente il documento e non passa da anteprima intermedia.

Regola viva:
- nel Banco le scorciatoie del riquadro inferiore devono restare operative e immediate, non duplicare configurazioni tecniche sparse nella sidebar;
- `Cortesia` e `Stampa (F10)` restano flussi di stampa diretta POS80, mentre l'anteprima resta un'azione separata e distinta.

## 2026-04-17 - Anteprima POS80 modale e stampa diretta senza browser interattivo

Problema chiuso:
- l'anteprima POS80 del Banco usciva ancora nel browser esterno, spezzando il flusso dell'operatore e uscendo dal perimetro dell'applicazione;
- la stampa POS80 passava ancora dalla UI di stampa del browser, quindi non era una stampa diretta coerente con il comportamento richiesto;
- i tre pulsanti rapidi `Impostazioni`, `Registratore di cassa` e `Stampa (F10)` erano stati inseriti nel riquadro sbagliato invece che nel box vuoto in basso del pannello destro.

Correzione:
- il runtime FastReport restituisce ora anche il percorso del file preview/stampa generato, senza aprirlo automaticamente nel browser;
- la scheda `Banco` apre l'anteprima POS80 in una finestra modale interna `Pos80PreviewWindow`, che carica l'HTML locale dentro Banco tramite controllo `WebBrowser`;
- la prima correzione di stampa diretta via `printto` e `rundll32 + mshtml.dll,PrintHTML` e` stata superata: la pipeline reale ora prepara il file HTML nel runtime e affida la stampa a un helper WPF interno e nascosto (`Pos80PrintWindow`) che stampa dalla postazione senza browser esterni;
- i pulsanti rapidi richiesti sono stati spostati nel box inferiore reale del pannello destro, lasciando `Salva` e `Nuovo` nel riquadro superiore gia` esistente.

Regola viva:
- l'anteprima POS80 di Banco deve restare dentro l'app, in una finestra modale dedicata, e non deve piu` aprire browser esterni;
- la stampa POS80 operativa non deve richiedere l'interazione con UI del browser e non deve dipendere dall'associazione `.html` della postazione;
- i pulsanti rapidi aggiunti dall'operatore vanno collocati nel riquadro vuoto inferiore del pannello destro se sono scorciatoie operative di cassa/postazione.

## 2026-04-17 - Spostata la stampa POS80 dentro la UI Banco dopo verifica del limite runtime

Problema chiuso:
- il canale `printto` non era affidabile sulla postazione: rimuoveva l'errore di associazione `.html`, ma non produceva job di stampa reali verso `POS-80`;
- il pacchetto `FastReport Open Source` usato nel progetto non espone `PrintSettings` / `PrintPrepared()`, quindi il runtime non puo` chiudere la stampa termica da solo.

Correzione:
- `Banco.Stampa` prepara il file HTML di stampa e restituisce anche la stampante assegnata al layout;
- `BancoViewModel` non considera piu` conclusa la stampa appena il runtime genera il file: aspetta l'esito del callback UI `Pos80PrintRequested`;
- la view `Banco` usa `Pos80PrintWindow`, helper WPF nascosto che:
  - carica il file HTML locale in un `WebBrowser` interno;
  - imposta temporaneamente la stampante assegnata come predefinita se necessario;
  - lancia la stampa silenziosa via ActiveX `ExecWB`;
  - ripristina poi la stampante predefinita precedente.

Regola viva:
- per la POS80 la pipeline corretta e` `runtime prepara file -> helper WPF Banco stampa`, non `runtime -> browser/shell`;
- il flag `Succeeded` del runtime stampa non chiude il flusso Banco finche` la stampa reale della postazione non e` stata tentata dal helper UI.

## 2026-04-17 - Corretto il formato stampa POS80 troppo piccolo

Problema chiuso:
- la stampa POS80 usciva in formato minuscolo, con intestazioni tipo `Page 1 of 1` e margini da pagina normale, non da rotolo termico;
- la `POS-80` risultava correttamente configurata su carta termica 80 mm lunga, quindi il difetto non era nel driver carta ma nel `PageSetup` del motore `WebBrowser/IE` usato dalla stampa silenziosa.

Correzione:
- `Pos80PrintWindow` forza temporaneamente le impostazioni `PageSetup` del motore IE/WebBrowser prima della stampa:
  - `header/footer` vuoti;
  - margini a `0`;
  - `Shrink_To_Fit = no`;
  - `Print_Background = yes`;
- al termine della stampa Banco ripristina i valori precedenti della postazione.

Regola viva:
- se la stampa POS80 passa dal `WebBrowser` interno, Banco deve neutralizzare e poi ripristinare il `PageSetup` IE della postazione per evitare riduzioni, header e footer non coerenti con la termica.

## 2026-04-17 - Conservate le modifiche manuali al layout `Pos.frx`

Problema chiuso:
- aprendo `Pos.frx` da Banco, salvando modifiche nel designer e riaprendolo, alcune personalizzazioni venivano perse;
- la causa reale non era il salvataggio del designer, ma il riallineamento Banco del layout POS, che in alcune fasi:
  - poteva ricreare il template pilota da zero;
  - riscriveva posizioni, dimensioni, font e testi di oggetti gia` esistenti nel report.

Correzione:
- la rigenerazione automatica del pilota e` stata resa meno aggressiva: non dipende piu` dalla presenza di singoli oggetti del layout;
- il riallineamento Banco del `Pos.frx` resta attivo solo per:
  - datasource CSV design-time;
  - parametri necessari;
  - oggetti mancanti da creare;
- se gli oggetti del layout esistono gia`, Banco non deve piu` spostarne coordinate, dimensioni o font e non deve sovrascrivere le espressioni se non sono chiaramente legacy/vuote.

Regola viva:
- `Pos.frx` aperto dal designer Banco deve mantenere le personalizzazioni manuali dell'operatore;
- il modulo `Stampa` puo` aggiungere solo il minimo tecnico mancante, ma non deve ripristinare il layout pilota sopra un layout gia` personalizzato.

## 2026-04-17 - Allineato l'installer Inno Setup al nuovo modulo stampa

Problema chiuso:
- lo script `Banco.iss` copiava il publish in modo generico, ma non dichiarava esplicitamente la struttura operativa del nuovo modulo `Stampa`;
- il deploy non includeva in modo chiaro la cartella `FastReport` esterna usata dal designer Community;
- in aggiornamento potevano restare residui del vecchio modulo `Banco.Repx`.

Correzione:
- `Banco.iss` ora crea e preserva in installazione:
  - `Stampa\Layouts`
  - `Stampa\DesignData`
  - `Stampa\Anteprime`
  - `FastReport`
- il publish applicativo continua a essere copiato sotto `{app}`, ma vengono esclusi gli output runtime volatili:
  - `Stampa\Anteprime`
  - `Stampa\DesignData`
  - lock file personalizzazione layout
  - export temporanei `pdf/xps/html`
- la cartella root `FastReport` del workspace viene inclusa in `{app}\FastReport`;
- in fase di aggiornamento installazione vengono rimossi i residui del modulo legacy `Banco.Repx`.

Regola viva:
- l'installer di Banco deve distribuire sia il runtime applicativo sia il supporto operativo del nuovo modulo stampa;
- i layout personalizzati e i file runtime di `Stampa` non devono essere sovrascritti da export temporanei o residui del vecchio modulo report.

## 2026-04-17 - Esclusi dall'installer i residui `QuestPDF`

Problema chiuso:
- nel `publish` risultavano ancora presenti file `QuestPDF` e output `questpdf-*` pur non essendoci piu` riferimenti attivi nei progetti principali del workspace;
- il rischio era portare in installazione residui del vecchio flusso stampa solo perche` il `publish` non era stato ancora rigenerato in modo pulito.

Correzione:
- `Banco.iss` ora esclude esplicitamente:
  - `QuestPDF.dll`
  - `QuestPdfSkia.dll` nelle runtime Windows
  - `Stampa\questpdf-*`
  - `Stampa\questpdf-layout-settings.json`
- in aggiornamento installazione questi residui vengono anche cancellati con `InstallDelete`.

Regola viva:
- `QuestPDF` non e` piu` parte attiva del nuovo modulo `FastReport`;
- eventuali residui di `publish/bin` vanno trattati come sporcizia di build e non devono rientrare nel setup finale.

## 2026-04-17 - Publish pulito e update installer con chiusura automatica di Banco

Problema chiuso:
- l'installer doveva aggiornare `Banco` anche se l'applicazione risultava ancora aperta sulla postazione;
- il `publish` conteneva ancora file storici di `QuestPDF` solo perche` era stato costruito sopra una cartella gia` sporca.

Correzione:
- `Banco.iss` ora usa:
  - `CloseApplications=yes`
  - `RestartApplications=no`
  - `CloseApplicationsFilter=Banco.exe`
- il `publish\Banco` e` stato rigenerato dopo rimozione completa della cartella precedente, cosi` l'output finale non contiene piu` residui `QuestPDF` o `Banco.Repx`.

Regola viva:
- prima di generare il setup finale, il `publish` va rifatto su cartella pulita;
- l'update installer di Banco deve chiudere automaticamente `Banco.exe` e solo dopo procedere con la sostituzione dei file.

Nota compatibilita`:
- la direttiva `ForceCloseApplications` non e` supportata dalla versione di Inno Setup presente sulla postazione e non va usata nello script `Banco.iss`;
- la linea compatibile da mantenere e` `CloseApplications=yes` con `CloseApplicationsFilter=Banco.exe`.

## 2026-04-17 - Inserita la configurazione del percorso immagini articoli FM

Problema chiuso:
- Banco non aveva ancora una configurazione persistente e riusabile per sapere dove cercare la cartella che contiene le immagini articoli del contenuto legacy FM;
- la scheda impostazioni era ancora percepita come sola `Configurazione DB`, mentre il progetto aveva gia` bisogno di ospitare anche configurazioni strutturali piu` ampie.

Correzione:
- e` stata introdotta in `AppSettings` la nuova sezione `FmContent`, con:
  - `RootDirectory`
  - `ArticleImagesDirectory`
- il servizio configurazione file normalizza e salva anche questi valori, con default coerenti:
  - `C:\Facile Manager\DILTECH`
  - cartella immagini articoli derivata dalla root FM;
- la vista impostazioni e` stata estesa in modo coerente con i pattern UI esistenti, aggiungendo una card `Percorsi FM` dentro `Configurazioni generali`;
- la diagnostica mostra ora anche:
  - radice contenuti FM
  - cartella immagini articoli attiva.

Regola viva:
- i percorsi dei contenuti legacy FM devono vivere in configurazione applicativa e non in costanti hardcoded;
- il primo percorso FM configurabile introdotto in Banco e` la cartella immagini articoli;
- il punto corretto dove modificare questi valori e` `Configurazioni generali`, non una schermata tecnica isolata.

## 2026-04-17 - Configurazioni generali spostate sul brand header

Problema chiuso:
- `Configurazioni generali` era ancora raggiungibile come voce primaria nella sidebar `Impostazioni`, mentre il progetto aveva gia` fissato che le configurazioni strutturali rare non devono stare nello stesso livello delle scorciatoie tecniche;
- nella scheda impostazioni le textbox risultavano troppo piccole e poco leggibili rispetto alla densita` professionale richiesta per Banco.

Correzione:
- il blocco brand dell'header (`logo + Banco Operativo`) e` diventato cliccabile e apre direttamente `Configurazioni generali`;
- la voce `Configurazioni generali` e` stata rimossa dalle entry principali della sidebar, lasciando in sidebar solo accessi rapidi tecnici come `FastReport`, `Configurazione POS`, `Configurazione fiscale`, `Diagnostica / Percorsi` e backup;
- la vista `Configurazioni generali` usa ora uno style locale condiviso per le textbox della pagina, con font e resa piu` leggibili ma ancora compatti da gestionale desktop.

Regola viva:
- le configurazioni amministrative strutturali di Banco si aprono dal brand header, non dalla sidebar tecnica;
- il brand header non e` solo decorazione: e` un ingresso funzionale coerente alla configurazione generale del programma;
- nella scheda `Configurazioni generali` gli input devono restare leggibili, densi e coerenti col resto della UI, evitando testi minuscoli o resa da schermata tecnica provvisoria.

## 2026-04-17 - Stesso comportamento di riciclo anche sulla tab Banco iniziale

Problema chiuso:
- nella tab `Banco` iniziale, dopo `Salva`, `Cortesia` o `Scontrino`, il reset della vendita non risultava coerente con l'esperienza operativa attesa;
- il flusso corretto deve essere identico su tab iniziale e tab Banco successive: stessa scheda riciclata come `Nuovo documento`.

Correzione:
- la chiusura riuscita non dipende piu` dall'apertura di nuove tab Banco;
- anche la tab principale deve applicare la stessa pipeline di reset in-place del documento corrente.

Regola viva:
- nessuna differenza di comportamento e` ammessa tra la tab Banco iniziale e le tab Banco create successivamente;
- se un flusso `Salva`, `Cortesia` o `Scontrino` si conclude con successo, la stessa scheda Banco deve sempre risultare gia` pronta come nuova vendita.

## 2026-04-18 - Lista documenti confermata su DataGrid nativo custom

Problema chiuso:
- il tentativo di sostituire la griglia `Documenti` con un controllo terzo tipo `FlexGrid` non era sostenibile nel progetto per dipendenze/licensing esterni non coerenti con la build locale;
- la schermata `Documenti` ha bisogno di mantenere persistente layout colonne, footer allineato, menu colonne, selezione multipla e azioni riga senza introdurre regressioni operative.

Correzione:
- la lista documenti e` stata riportata su una base `DataGrid` WPF nativa, mantenendo la toolbar compatta e i flussi applicativi esistenti;
- il footer resta sincronizzato alle colonne visibili, alle larghezze e al riordino della griglia principale;
- e` stata rimossa la dipendenza sperimentale da componenti terzi, lasciando il progetto compilabile senza tool o licensing aggiuntivi.

Regola viva:
- per schermate operative dense come `Documenti`, prima si privilegia una `DataGrid` nativa customizzata e integrata col design system del progetto;
- controlli terzi per la griglia vanno introdotti solo se compatibili con build, licensing e comportamento operativo reale, non solo per potenzialita` teoriche;
- layout colonne, footer, menu colonne e selezione devono restare allineati e persistenti anche quando la resa visuale viene rifattorizzata.

Aggiornamento:
- l'ingresso shell/sidebar `Documenti` ora apre la UI ufficiale `Documenti`, agganciata allo stesso `DocumentListViewModel`;
- il vecchio modulo legacy della lista documenti e` stato escluso dalla navigazione utente e non resta piu` esposto come percorso separato.

## 2026-04-18 - Menu contestuale strutturale nel modulo griglia condiviso

Problema chiuso:
- il supporto al menu contestuale delle griglie era ancora diviso tra modulo condiviso e code-behind delle singole view;
- per aggiungere una voce contestuale serviva ancora riscrivere pipeline di click destro, apertura menu o sync selezione nella schermata;
- `Documenti` aveva gia` un modulo griglia condiviso per colonne/footer/layout, ma il menu contestuale non era ancora una capacita` strutturale dello stesso modulo.

Correzione:
- e` stato introdotto un controller condiviso del modulo griglia che centralizza click destro, classificazione `header/body/empty`, sync selezione coerente e composizione del menu contestuale;
- `DataGridColumnManager` produce ora solo il nodo del menu colonne, senza conoscere `ContextMenu` o lifecycle UI;
- `BancoGridMenuService` espone le voci shared di appearance/layout come provider riusabile;
- la UI ufficiale `Documenti` dichiara solo l'azione contestuale da esporre e non reimplementa piu` la pipeline del tasto destro.

Regola viva:
- il menu contestuale delle griglie si dichiara tramite contratto del modulo condiviso, non tramite pipeline locale della view;
- il click destro deve riusare la stessa logica strutturale del modulo per selezione, current cell e apertura menu;
- le view possono dichiarare le azioni contestuali da esporre, ma non devono ricostruire manualmente comportamento, `ContextMenu` o lifecycle del tasto destro.

## 2026-04-18 - Update installer con pulizia preventiva binari obsoleti

Problema chiuso:
- il profilo di publish ripuliva la cartella `publish`, ma l'installer continuava a copiare sopra `{app}` lasciando possibili file e cartelle applicative obsolete dalle versioni precedenti.

Correzione:
- `Banco.iss` esegue ora una pulizia preventiva della cartella applicativa prima della copia dei nuovi file;
- la pulizia preserva solo le directory dati persistenti (`Config`, `LocalStore`, `Log`, `Stampa`, `FastReport`) e i file `unins*`;
- i binari, le cartelle runtime e le risorse applicative non piu` distribuite vengono rimossi automaticamente durante l'update, senza dover inseguire i singoli file legacy uno per uno.

## 2026-04-18 - CashRegisterOptionsDialogWindow riallineata al perimetro UI3

Problema chiuso:
- `CashRegisterOptionsDialogWindow` era ancora costruita con un impianto visuale locale da dialog legacy, poco coerente con le dialog piu` recenti del progetto;
- la finestra doveva essere riallineata al nuovo stack UI senza toccare code-behind, flussi o semantica operativa della chiusura cassa.

Correzione:
- la dialog e` stata migrata solo in XAML riusando brush e stili del perimetro UI3 gia` presenti (`SurfaceBrush`, `SurfaceAltBrush`, `BorderBrushBase`, `HairlineBrush`, `Text*Brush`, `PrimaryButtonStyle`, `SecondaryButtonStyle`);
- header, gruppo opzioni e footer sono stati ricomposti con gerarchia piu` chiara, spaziature piu` compatte e risorse theme-aware per restare coerenti in light e dark theme;
- i tre `RadioButton` esistenti sono rimasti controlli standard con gli stessi `x:Name`, cosi` la lettura di `IsChecked` e gli handler del code-behind restano invariati.

Regola viva:
- nelle migrazioni UI di Banco, se la sostituzione del controllo rischia di rompere naming o comportamento, si mantiene il controllo esistente e si aggiorna solo la resa visiva attorno ad esso;
- dialog, superfici, bordi e testi devono appoggiarsi a risorse theme-aware condivise del progetto, evitando colori locali che degradano tra tema chiaro e tema scuro.

## 2026-04-18 - Registry centrale di navigazione shell come fonte unica

Problema chiuso:
- la shell Banco aveva gia` un primo `INavigationRegistry`, ma sidebar e shell continuavano a tenere una parte della verita` in strutture locali separate;
- ricerca, pannello contestuale e apertura destinazioni non erano ancora governati fino in fondo dalla stessa fonte unica;
- `ShellViewModel` manteneva ancora switch e mapping hardcoded che decidevano davvero la destinazione finale.

Correzione:
- il contratto `INavigationRegistry` e` stato evoluto per descrivere entry navigabili complete con metadata canonici di categoria, destinazione, visibilita` e ricerca;
- il registry concreto della shell valida ora univocita`, coerenza delle category key, presenza dei group per il pannello contestuale e risolvibilita` delle destination key;
- la ricerca sidebar legge ora solo dal registry centrale e usa `Title`, `Keywords` e `Aliases` delle entry registrate;
- il pannello contestuale viene costruito dal registry filtrando `MacroCategoryKey` e `ShowInContextPanel`, senza gruppi hardcoded nella shell;
- `ShellViewModel` usa ora il `DestinationKey` della tab attiva per sincronizzare la sidebar e non mantiene piu` lo switch inverso `tab -> destinazione`;
- gli override persistiti della sidebar restano compatibili senza cambio schema: il vecchio `TargetKey` nei settings viene usato solo come adapter unidirezionale verso la `DestinationKey` del registry.

Regola viva:
- categorie shell, pannello contestuale, ricerca e destinazione finale devono derivare dallo stesso registry centrale;
- una funzione shell-side non registrata nel registry e` fuori standard e non accettabile in review;
- nessuna nuova funzione navigabile puo` considerarsi completa se manca registry entry, metadata minimi coerenti, esposizione corretta in ricerca/menu/pannello e aggiornamento documentale relativo.

## 2026-04-18 - CashRegisterOptionsDialogWindow con ristampa DGFE e annullo protocollo

Problema chiuso:
- la dialog cassa del Banco gestiva solo giornale giornaliero e chiusura cassa;
- mancavano due operazioni richieste in modo operativo sul Ditron Elsi Retail R1: ristampa scontrino per numero/data e annullo scontrino secondo protocollo.

Correzione:
- `CashRegisterOptionsDialogWindow` espone ora nello stesso pannello operativo i campi `scontrino`, `data`, `Z` e `matricola`, senza aprire dialog secondari;
- la ristampa usa il comando WinEcr `DGFE` filtrato per data e numero scontrino (`NUMSCOI/NUMSCOF`) con stampa diretta;
- l'annullo usa il protocollo `DOCANNULLO` con `RESPRN`, `SETP ABORTFILE=SI`, riferimenti scontrino e ricostruzione delle righe della scheda fiscalizzata corrente, seguita da `CHIUS`.

Regola viva:
- `Annullo scontrino` non e` un comando generico scollegato dal documento: sul Ditron va trattato come operazione fiscale di protocollo e richiede una scheda corrente coerente con righe, numero e data;
- `Ristampa scontrino` va invece instradata tramite `DGFE`, non con una falsa ristampa applicativa costruita a mano dal Banco.

## 2026-04-18 - `ShellWindow` riallineata a workspace ufficiale e pannello categoria dal registry

Problema chiuso:
- `ShellWindow` ospitava ancora la navigazione shell in modo troppo compatto dentro una sidebar unica, con ricerca pannello non ancora limitata al perimetro della macro-categoria attiva;
- il pannello contestuale non spariva davvero quando il contesto non richiedeva sottovoci;
- shell e sidebar usavano ancora diverse superfici hardcoded locali, poco allineate al perimetro UI/UI3 e alla futura compatibilita` light/dark.

Correzione:
- `ShellWindow` resta la shell ufficiale del gestionale, ma ora compone in modo esplicito rail categorie, pannello categoria contestuale e desktop/workspace centrale;
- la rail espone solo macro-categorie del registry, mentre il pannello categoria legge gruppi e search scope dallo stesso registry centrale;
- la ricerca nel pannello filtra solo le entry della macro-categoria attiva e non costruisce un secondo indice o un secondo modello;
- il pannello contestuale si sincronizza anche su navigazione indiretta e su selezione da risultati ricerca;
- l'area centrale e` stata riallineata come host desktop/workspace della shell, predisposta a superfici configurabili future senza introdurre docking o MDI paralleli;
- i token visuali shell piu` specifici sono stati centralizzati nel punto canonico del nuovo stack UI/UI3 e `ShellWindow` / `Banco.Sidebar` usano ora risorse theme-aware invece di colori locali sparsi.

Regola viva:
- la shell resta host di navigazione e workspace, non owner del contenuto di dominio dei moduli;
- rail categorie, pannello contestuale e ricerca pannello devono sempre derivare dal registry centrale;
- gli override persistiti della sidebar possono adattare la UI solo in modo unidirezionale e compatibile, senza diventare una seconda fonte di verita`.

Aggiornamento operativo:
- il pannello categoria non apre piu` per click ma su hover della macro-categoria nella rail;
- il pannello e` ora overlay sopra il workspace e non spinge piu` tab o contenuto centrale;
- la ricerca shell e` stata riportata nella colonna categorie e usa il registry centrale per aprire categoria corretta, overlay corretto e voce evidenziata.
- 2026-04-20: Banco quantità articolo manuale resa editabile senza formattazione da importo; promo premio non più riproposta a ogni nuova riga dopo rifiuto sulla stessa soglia; stampa Banco/POS riallineata al motore `Banco.Punti` per mostrare punti prima/dopo coerenti col documento corrente.
