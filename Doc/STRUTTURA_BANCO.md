# STRUTTURA_BANCO.md

## Scopo

Questo file descrive la struttura reale del progetto `Banco`:
- verità di dominio;
- verità architetturali;
- flussi corretti;
- stati utente;
- regole della schermata `Documenti`;
- regole della schermata `Banco`.

Qui dentro deve esistere una sola linea coerente:
**Banco = documento ufficiale su `db_diltech`**.

---

## 1. Verità di dominio del Banco

### 1.1 Documento ufficiale Banco
Il documento ufficiale Banco:
- vive su `db_diltech`;
- usa `modellodocumento = 27`;
- usa la numerazione ufficiale legacy;
- è l’unico documento funzionale del dominio Banco.

Non esistono, nel dominio utente:
- documenti paralleli;
- numerazioni parallele;
- stati documento paralleli al legacy;
- seconde identità del documento.

### 1.2 Categorie principali reali
Le categorie principali di dominio sono:

- `Cortesia`
- `Scontrino`

Entrambe:
- producono un documento ufficiale Banco;
- usano la stessa numerazione legacy;
- vengono poi distinte tramite stato applicativo e fiscale.

### 1.3 Componente aggiuntiva
Esiste una componente aggiuntiva:

- `Sospeso`

`Sospeso`:
- non è categoria principale;
- non sostituisce `Cortesia` o `Scontrino`;
- si ricostruisce prioritariamente dal legacy tramite `Pagatosospeso`.

### 1.4 Stato utente aggiuntivo di `Salva`
Quando il documento viene chiuso con `Salva`, lo stato utente mostrato è:

- `Pubblicato Banco`

`Pubblicato Banco`:
- è un documento ufficiale Banco;
- non è `Cortesia`;
- non è `Scontrino`;
- non è una bozza;
- non è un concetto separato dal documento ufficiale;
- compare in `Completa`.

---

## 2. Verità architetturali

### 2.1 Database operativo
Il database operativo corretto è:

- `db_diltech`

### 2.2 Writer ufficiale
Il writer ufficiale Banco è l’unico punto di scrittura verso:
- `documento`
- `documentoriga`
- `documentoiva`

Nessun altro flusso deve scrivere in parallelo sul legacy.

### 2.2 bis Regola ferrea sulle tabelle legacy
Quando Banco legge o scrive sul legacy:
- non deve fermarsi alla sola tabella "piu` ovvia" del caso d'uso;
- deve rispettare l'intera struttura reale che `FM` usa per quel concetto;
- devono essere verificate e mantenute coerenti tutte le tabelle collegate necessarie al comportamento reale, incluse combinazioni, lookup, dettagli variante, anagrafiche collegate e metadati che `FM` legge davvero;
- nessun campo o record puo` essere inventato "per analogia" se prima non e` stato verificato sul DB reale;
- se `FM` tratta un'informazione come strutturata su piu` tabelle, Banco deve scriverla e rileggerla con la stessa struttura.

### 2.3 Fiscalizzazione
La fiscalizzazione reale:
- è separata dalla pubblicazione del documento legacy;
- avviene tramite WinEcr;
- non coincide con la creazione del documento Banco;
- non deve essere dedotta in modo primario da `Scontrinonumero` o `Scontrinodata`.

### 2.4 Dati tecnici interni
Possono esistere dati tecnici interni di audit/compatibilità, ma:
- non sono un secondo documento;
- non sono stati utente;
- non sono categorie di dominio;
- non guidano la semantica utente;
- non sostituiscono il documento ufficiale.

Se citati, vanno trattati solo come supporto tecnico interno.

### 2.4 quinqies Modulo tecnico `Stampa`
Il modulo `Stampa`:
- vive come modulo tecnico separato in root progetto;
- gestisce stampanti, layout, profili e motori di rendering senza introdurre un secondo documento Banco;
- puo` mantenere in parallelo il motore stampa corrente e quello nuovo solo per il tempo strettamente necessario alla migrazione;
- deve esporre ai layout solo dati reali del documento e delle tabelle legacy collegate davvero usate dal gestionale;
- non deve inventare campi o semantiche non presenti nel DB reale o nel flusso ufficiale Banco.

Regola architetturale:
- il nuovo runtime `FastReport` va pensato come parte integrante di Banco, non come tool separato scollegato;
- nel modulo devono restare distinti supporto runtime, preview, stampa reale, designer e schema dati esposto ai layout;
- il designer integrato puo` essere attivato solo quando il pacchetto/licenza scelti lo rendono hostabile in modo stabile dentro l'app.

Regola UI:
- il modulo `FastReport` deve avere una schermata propria nella shell Banco per mostrare stato runtime, catalogo layout e schema campi;
- la schermata puo` aprire cartella layout e catalogo tecnico, ma non deve mascherare l'assenza del runtime reale con finti esiti positivi.
- nella stessa schermata deve comparire anche il riferimento legacy `.repx` usato come blueprint di migrazione, con file sorgente, parametri, binding e regole condizionali da replicare nel nuovo layout.
- la stessa schermata deve permettere anche l'associazione `layout -> stampante` e deve esporre comandi operativi distinti per `Anteprima`, `Apri designer` e `Test stampa`, con esito esplicito se il runtime FastReport non e` ancora installato.
- la pagina `FastReport` deve restare interamente raggiungibile anche su viewport basse della shell: sezioni come contratto tecnico, catalogo e stato finale non possono finire tagliate senza scroll verticale.
- il comando `Apri layout selezionato` deve garantire che il `.frx` tecnico esista davvero prima di delegare l'apertura al sistema.

Regola di migrazione layout:
- per i report Banco ereditati dal modulo stampa legacy non si fa conversione automatica "cieca" a `FastReport`;
- il file `.repx` reale va trattato come blueprint tecnico da leggere e codificare esplicitamente nel nuovo modulo;
- il primo riferimento vivo per il POS 80 mm e` `C:\Facile Manager\DILTECH\Report\Pos.repx`;
- i campi del nuovo schema dati devono coprire i parametri e le binding reali del report legacy prima di costruire il `.frx` pilota.

Regola operativa Banco:
- la `Cortesia` della schermata `Banco` usa ora il layout operativo del modulo `Stampa`, associato al documento `receipt-80-db`;
- l'anteprima Banco della `Cortesia` passa dal runtime `FastReport`;
- la stampa operativa della `Cortesia` non deve piu` dipendere dal modulo stampa storico rimosso dal progetto.

Regola runtime `FastReport Open Source`:
- il motore Open Source integrato in `Banco.Stampa` puo` creare/caricare `.frx`, registrare i dati Banco ed esportare anteprime HTML locali;
- la `Anteprima` e il `Test stampa` del modulo passano quindi da export HTML apribile subito, senza simulare un previewer desktop proprietario che il pacchetto base non espone;
- il designer visuale integrato non fa parte del runtime Open Source base: se presente, Banco puo` aprire solo un Community Designer esterno installato sulla postazione.
- la schermata `FastReport` deve anche esporre una prima mappa tecnica del contratto report, distinguendo campi certi, deduzioni forti e punti ancora da verificare per la famiglia di stampa selezionata.
- se nella root del progetto o della cartella pubblicata e` presente una directory `FastReport` con `Designer.exe`, il modulo deve rilevarla e aprire quel designer direttamente, senza richiedere scorciatoie manuali sul desktop.
- per il layout POS pilota la preview deve privilegiare dati reali del legacy quando disponibili: intestazione negozio da `config` e vendita esempio da documento Banco reale, invece di restare su payload completamente inventati.

### 2.4 bis Vendita Banco e supporto locale
Nella vendita `Banco`:
- interrogazione articoli, prezzi, documento e pubblicazione devono dipendere dal legacy `db_diltech`;
- il local store / SQLite non e` parte del flusso operativo della vendita;
- nessun dato di vendita Banco deve essere reso corretto o distinguibile solo grazie a persistenza locale tecnica;
- eventuali supporti locali restano ammessi solo per moduli separati dal dominio vendita legacy, come funzioni interne non appartenenti a Facile Manager.

### 2.4 ter Modulo locale `Lista riordino`
`Lista riordino`:
- e` un modulo locale separato dal dominio documento Banco;
- raccoglie fabbisogni operativi di riordino durante la vendita, senza diventare un documento ufficiale;
- puo` usare persistenza locale tecnica per righe, stato e fornitore scelto;
- mantiene sia la quantita` raccolta dal Banco sia una `Q.ta da ordinare` modificabile dall'operatore;
- usa il check solo come selezione operativa delle righe su cui agiscono i pulsanti `Conferma selezionati`, `Riapri selezionati` e `Rimuovi`;
- la conferma della riga per il successivo ordine fornitore deve partire solo dai pulsanti o dal menu destro, non dal click diretto sul check;
- la conferma delle righe non deve autochiudere la lista corrente ne` aprirne subito una nuova vuota: la lista resta la stessa finche` non viene processata/chiusa esplicitamente;
- la schermata puo` raggruppare operativamente le righe per fornitore scelto, con fallback sul fornitore suggerito se la riga non e` stata ancora corretta;
- dalle righe confermate la schermata puo` generare automaticamente liste locali per fornitore con data e contatore;
- una lista fornitore locale ancora non registrata su FM puo` essere eliminata interamente dalla schermata, rimuovendo insieme tutte le righe locali che la compongono;
- la riga confermata ma non ancora registrata su FM deve permettere la modifica diretta della `Q.ta da ordinare` dalla griglia, senza passaggi nascosti o ambigui;
- il comando `Crea ordine su FM` della lista fornitore deve creare un vero `Ordine Fornitore` sul legacy, usando `modellodocumento = 9`, e solo dopo aggiornare lo stato locale della lista;
- la raccolta `Lista riordino` non deve scrivere da sola ordini o documenti sul legacy `db_diltech`; la scrittura legacy e` ammessa solo nel passaggio esplicito `Crea ordine su FM` della lista fornitore;
- resta un supporto interno da processare successivamente su `Facile Manager`.

### 2.4 quater Scheda `Articolo magazzino`
La scheda `Articolo magazzino`:
- vive sotto `Magazzino` come supporto locale del modulo riordino;
- usa un modulo root dedicato `Banco.Magazzino` per la logica applicativa della scheda, cosi` la crescita futura di articolo, varianti e barcode non resta accatastata nel solo host WPF;
- legge i dati articolo reali dal legacy e li usa come base ufficiale della scheda, con modifica diretta ammessa sui campi legacy che il modulo gestisce davvero;
- salva su SQLite solo parametri locali di riordino collegati alla stessa chiave articolo/variante reale;
- tratta il padre come sorgente default dei parametri locali: i valori salvati sul padre si ereditano su tutte le varianti senza override locale, comprese le varianti future;
- permette comunque override locale indipendente sulla singola variante, che non deve propagarsi al resto della famiglia;
- non crea una seconda anagrafica articolo e non riscrive dati anagrafici sul legacy.

### 2.4 sexies Registro centrale di navigazione shell
La navigazione shell-side di Banco deve passare da un solo registro centrale.

Regola architetturale stabile:
- ogni entry navigabile esposta in shell deve essere censita nel registry centrale;
- categorie principali, pannello contestuale e ricerca globale devono derivare dallo stesso registry;
- la shell non deve mantenere elenchi hardcoded separati di funzioni navigabili, gruppi contestuali o risultati ricerca;
- l'ordinamento canonico della rail categorie deve vivere nel registry o in metadata strettamente associati al registry, non emergere dall'ordine delle entry figlie;
- la ricerca di navigazione deve leggere solo dal registry centrale e usare almeno `Title`, `Keywords` e `Aliases` delle entry registrate;
- il pannello contestuale deve essere costruito filtrando il registry per `MacroCategoryKey` e `ShowInContextPanel`, senza gruppi locali costruiti a mano;
- la correlazione tra entry, macro-categoria da attivare, voce da evidenziare e destinazione finale da aprire deve restare governata dal registry;
- eventuali adapter verso le preferenze persistite della sidebar sono ammessi solo come compatibilita` unidirezionale e minimale;
- e` vietato trasformare il registry in contenitore di badge dinamici, stato pagina, permessi di dominio o dati business.

Contratto minimo della entry navigabile:
- `Key` stabile e indipendente dal titolo visualizzato;
- `Title` canonico;
- `MacroCategoryKey` obbligatoria;
- `DestinationKey` stabile, non duplicata e risolvibile dalla shell;
- `GroupKey` obbligatoria solo quando `ShowInContextPanel = true`;
- metadata minimi di ricerca e visibilita` shell.

Regola finale:
- una funzione shell-side non registrata nel registry e` fuori standard e non accettabile in review;
- nessuna nuova funzione navigabile puo` considerarsi completa se non aggiorna anche il registry e la documentazione attiva correlata.

### 2.5 Ripristino del DB locale
Il ripristino del `db_diltech` locale:
- non cambia lo schema legacy;
- non introduce tabelle o campi nuovi;
- importa un dump del gestionale sul DB gia` configurato nella postazione;
- serve per riallineare la base dati locale a un backup reale di Facile Manager.

La UI amministrativa del restore:
- deve mostrare avanzamento reale e leggibile;
- deve esporre fase corrente, percentuale e messaggi operativi chiari;
- deve supportare i dump Facile Manager che contengono anche istruzioni `DELIMITER`.

---

## 3. Flussi corretti del Banco

## 3.1 `Salva`
`Salva`:
- pubblica o aggiorna il documento ufficiale Banco su `db_diltech`;
- non conclude su una bozza;
- non crea un documento di dominio separato;
- usa lo stesso writer ufficiale del Banco;
- produce stato utente `Pubblicato Banco`.

Esito corretto:
- documento ufficiale legacy creato o aggiornato;
- UI allineata ai riferimenti reali del legacy;
- lista documenti aggiornata coerentemente.

## 3.2 `Cortesia`
`Cortesia`:
- pubblica il documento ufficiale Banco;
- non invia WinEcr;
- resta recuperabile sullo stesso `DocumentoGestionaleOid`;
- può essere fiscalizzata successivamente.

## 3.3 `Scontrino`
`Scontrino`:
- pubblica il documento legacy se necessario;
- poi avvia il passo fiscale WinEcr;
- deve distinguere:
  - publish legacy riuscito + WinEcr riuscito;
  - publish legacy riuscito + WinEcr non completato;
  - publish legacy fallito.

## 3.4 Recupero successivo
Un documento ufficiale non fiscalizzato:
- si recupera sullo stesso `DocumentoGestionaleOid`;
- ricarica righe, pagamenti e stato applicativo;
- viene aggiornato sullo stesso documento legacy;
- non genera un secondo documento ufficiale.

---

## 4. Stati operativi veri

### 4.1 Documento Banco operativo
Documento in lavorazione nella schermata Banco, non ancora chiuso fiscalmente.

### 4.2 Documento ufficiale non fiscalizzato recuperabile
Documento ufficiale già pubblicato su legacy ma ancora recuperabile e aggiornabile.

Questo vale per:
- `Pubblicato Banco`
- `Cortesia` non fiscalizzata

### 4.3 Documento fiscalizzato / scontrinato
Documento ufficiale fiscalizzato o trattato come fiscalmente chiuso.

Regola:
- non è liberamente modificabile;
- non va trattato come scheda operativa libera.

### 4.4 Documento in consultazione bloccata
Documento ufficiale aperto solo per consultazione, senza possibilità di modifica operativa.

Questo vale:
- per fiscalizzati/scontrinati;
- per casi prudenziali in cui i segnali non bastano a garantire apertura libera sicura.

---

## 5. Apertura e recupero documenti

## 5.1 Entry point
L’entry point reale resta:

- `Apri nel Banco`

### 5.2 Modalità di apertura
L’apertura deve passare da un unico resolver della modalità scheda Banco.

Esiti possibili:
- documento Banco operativo;
- documento ufficiale non fiscalizzato recuperabile;
- documento ufficiale in consultazione bloccata.

### 5.3 Gerarchia delle fonti
Il resolver decide la modalità usando questo ordine:

1. stato applicativo coerente già disponibile sul documento collegato;
2. segnali legacy affidabili disponibili;
3. fallback prudenziale conservativo.

Regola:
- se i segnali non bastano, prevale la consultazione bloccata.

### 5.4 Regola sui fiscalizzati
Un documento fiscalizzato/scontrinato:
- non è liberamente riapribile come scheda normale;
- può essere consultato;
- resta base per futuri flussi controllati dedicati, ma non per editing libero.

---

## 6. Eliminabilità del documento

## 6.1 Regola corretta
Il pulsante `Del` non dipende da “locale/non locale”.

Il criterio corretto è:
- documento **non scontrinato**
- documento **non fiscalizzato**
- documento **realmente eliminabile secondo il dominio**
- assenza di blocchi funzionali o fiscali reali

## 6.2 Regola negativa
`Del`:
- non deve mai comparire sui fiscalizzati/scontrinati;
- non deve mai comparire sui documenti bloccati dal dominio;
- non deve dipendere da una distinzione tecnica parallela.

## 6.3 Effetto in UI
Quando `Del` è ammesso:
- deve essere visibile in modo sobrio e chiaro;
- deve aggiornare subito griglia, selezione, dettaglio laterale e footer;
- non deve lasciare stati fantasma.

---

## 7. Schermata `Documenti`

## 7.1 Funzione
La schermata `Documenti` serve a:
- consultare documenti Banco;
- filtrare documenti Banco;
- aprire/recuperare documenti Banco;
- leggere riepiloghi coerenti;
- mostrare azioni ammesse dal dominio.

## 7.2 Filtri
Filtri ufficiali:
- `Completa`
- `Solo scontrinati`
- `Solo cortesia`

Questi bucket non vanno alterati da semantiche tecniche parallele.

## 7.3 Regole lista
La lista documenti:
- usa il documento ufficiale legacy come base primaria;
- integra solo le informazioni che il legacy non espone abbastanza;
- non deve produrre documenti ufficiali invisibili;
- non deve produrre doppie righe concorrenti;
- non deve mostrare bucket artificiali paralleli.

## 7.4 Regole footer e totali
I riepiloghi economici:
- restano sempre visibili;
- devono essere coerenti col filtro attivo;
- usano i documenti ufficiali legacy coerenti col filtro;
- non devono lasciare stati stantii o incoerenti rispetto alla griglia;
- non devono scambiare supporti tecnici per vendite chiuse.

## 7.5 Regole click destro
Il click destro:
- deve usare la stessa pipeline di selezione/colori della selezione normale;
- non deve avere una seconda logica di evidenziazione;
- non deve produrre selezioni incoerenti.

## 7.5 bis Contratto menu contestuale della griglia condivisa
Per le griglie condivise Banco:
- il menu contestuale e` una capacita` strutturale del modulo griglia, non una pipeline locale della singola view;
- la pipeline del tasto destro deve vivere in un controller condiviso del modulo, responsabile di contesto `header` / `body` / `empty`, selezione coerente e apertura menu;
- le singole schermate devono dichiarare solo le azioni contestuali da esporre, senza ricostruire click destro, apertura menu o sincronizzazione selezione;
- il menu colonne/layout deve essere prodotto dal modulo griglia e non dalla view;
- le voci condivise di appearance/layout devono provenire da un provider condiviso e non da append locali riscritti in ogni schermata;
- il click destro su una riga gia` inclusa nella multi-selezione deve preservare la selezione corrente;
- il click destro su una riga esterna alla selezione deve riallineare la selezione alla riga cliccata;
- il click destro su area vuota o chrome della griglia non deve aprire menu ne` interferire con scrollbar o comportamento standard.

## 7.6 Regole visuali
Colori consolidati:
- `Cortesia` = verde chiaro leggibile
- `Pubblicato Banco` = verde chiaro più neutro e distinto da `Cortesia`
- `Sospeso` = rosso chiaro con priorità visiva
- `Scontrino` = resa coerente con lo stato fiscale reale

## 7.7 Regole UX
La schermata `Documenti` deve essere:
- compatta;
- leggibile;
- sobria;
- da gestionale professionale;
- senza helper testuali inutili;
- senza pulsanti superflui.

---

## 8. Schermata `Banco`

## 8.1 Funzione
La schermata `Banco` serve a:
- costruire il documento operativo;
- pubblicare il documento ufficiale;
- aggiornare un documento ufficiale recuperabile;
- eseguire la fiscalizzazione quando prevista.

## 8.2 Gating comandi
I comandi devono dipendere dallo stato operativo reale del documento, non da scorciatoie tecniche.

### `Anteprima`
Abilitata quando il documento è operativamente modificabile.

### `Cortesia`
Abilitata quando il documento è operativamente modificabile e il dominio lo consente.

### `Scontrino`
Abilitata secondo il gating fiscale reale già previsto.

## 8.3 Pagamenti
Regola chiarita:
- se l’operatore non specifica nulla, il default desiderato è `Contanti`;
- il pagamento deve comunque restare:
  - modificabile;
  - splittabile;
  - sostituibile completamente prima della chiusura finale.

## 8.4 Ricerca articoli
La ricerca articoli della scheda `Banco`:
- parte dal campo ricerca, non da un vecchio pulsante `Cerca` come meccanica principale;
- deve funzionare in modo live e multi-termine;
- deve cercare prodotti che combinano tutte le parole significative inserite dall'operatore;
- deve tollerare separatori, spazi e token tipici del catalogo reale come ml, gusti, marchio e formato.

Regola di ordinamento:
- il risultato finale deve unire match esatti, varianti e catalogo generale;
- poi deve deduplicare e ordinare per rilevanza, favorendo codice/barcode esatto e descrizioni che contengono davvero la combinazione cercata.

## 8.5 Cliente, punti e segnalazioni
Nel riquadro cliente della scheda `Banco`:
- il cliente predefinito resta `Cliente generico`;
- il listino predefinito della scheda `Banco` e` `Web`, ma se il cliente selezionato ha un `Clientelistino` sul legacy quel listino va selezionato automaticamente;
- la scelta listino e` una scelta operativa esplicita della vendita Banco e deve restare visibile nella testata shell accanto all'operatore, mostrando solo il nome listino e non codici tecnici;
- tutta la vendita deve restare coerente col listino scelto: ricerca articoli, prezzo righe, riapertura documento e publish sul legacy devono usare lo stesso `documento.Listino`;
- entrando nel textbox cliente la scrittura deve partire pulita, senza accodarsi al nome precedente;
- uscendo dal box senza testo valido il cliente corrente deve restare invariato;
- carta fedelta`, punti, promo e sospeso non devono comparire in card che fanno cambiare altezza alla pagina;
- il riepilogo operativo del cliente deve stare su una sola riga compatta e stabile.

## 8.6 Notifiche e stabilita` della schermata
Le notifiche operative della scheda `Banco`:
- devono confluire nella fascia alta centrale della testata;
- non devono essere duplicate in piu` riquadri con altezze variabili;
- devono usare una fascia stabile a riga singola con troncamento, non box che espandono la pagina.

La schermata Banco:
- non deve cambiare altezza mentre l'operatore lavora;
- non deve "ballare" quando cambiano stato documento, cliente, promo o messaggi operativi.

## 8.7 Griglia documento e scorciatoie
La griglia documento Banco:
- deve mantenere una intestazione visivamente completa con bordi coerenti superiori e inferiori;
- deve rispettare gli angoli arrotondati del contenitore senza effetti sporchi sui radius;
- deve mostrare scorciatoie coerenti con il comportamento reale del codice.

Regole scorciatoie vive:
- `INS` inserisce una riga manuale;
- `Canc` sulla riga selezionata converte la riga in manuale e rimuove il codice articolo, lasciando il campo codice vuoto;
- la documentazione non deve descrivere `Canc` come eliminazione riga se il comportamento reale e` diverso.

---

## 9. Guardia uscita e stati di perdita lavoro

## 9.1 Distinzioni corrette
La guardia uscita deve distinguere almeno:
- documento Banco operativo;
- documento ufficiale recuperabile con modifiche correnti;
- documento fiscalizzato in consultazione.

## 9.2 Regole
- un documento ufficiale recuperabile non va trattato come bozza annullabile;
- un fiscalizzato in consultazione non deve generare prompt impropri di perdita lavoro;
- una scheda davvero operativa può richiedere conferma coerente.

---

## 10. Verifiche minime di progetto

Ogni modifica rilevante deve rispettare queste verifiche:

### 10.1 Publish
- `Salva`, `Cortesia`, `Scontrino` devono riflettere il vero esito legacy;
- nessun successo fantasma;
- nessun riferimento legacy mostrato se il publish non è reale.

### 10.2 Lista
- documento ufficiale visibile correttamente;
- nessun duplicato;
- nessun bucket spurio;
- footer coerente col set reale.

### 10.3 Apertura
- non fiscalizzato recuperabile = operativo;
- fiscalizzato = consultazione bloccata;
- nessuna apertura ambigua.

### 10.4 UI
- nessun testo tecnico come stato utente;
- nessuna estetica invasiva;
- nessun risultato considerato “chiuso” se non è visibile o riproducibile davvero.

---

## 11. Cose che non devono più ricomparire

Non devono più rientrare nel progetto attivo, nei file o nelle proposte:
- numerazioni tecniche visibili;
- bozza finale come esito normale di `Salva`;
- documento parallelo al legacy;
- categorie utente tecniche;
- semantiche che separano il documento dal suo stato ufficiale su `db_diltech`;
- scorciatoie che trasformano supporto tecnico interno in concetto di dominio.

---

## 12. Formula sintetica finale

La formula corretta del progetto Banco è questa:

**Banco = documento ufficiale su `db_diltech`**  
**WinEcr = fiscalizzazione separata**  
**Recupero = stesso `DocumentoGestionaleOid`**  
**UI = stati utente chiari, nessuna semantica tecnica esposta**
# STRUTTURA_BANCO.md

## Schermata `Documenti`

### Fonte dati
- la schermata `Documenti` legge i documenti ufficiali Banco dal legacy `db_diltech`;
- eventuali metadati locali servono solo come supporto tecnico interno quando esistono, senza creare un secondo documento.

### Classificazione runtime in lista
- la classificazione mostrata in lista deve descrivere il comportamento reale del gestionale;
- un documento legacy con segnale fiscale reale resta `Scontrino`;
- un documento legacy non scontrinato può essere riconosciuto runtime anche dal pagamento `Buoni` / `PagatoBuoni`, usato nel gestionale reale come segnale pratico dei non scontrinati;
- un documento legacy con incasso `Contanti`, `Carta` o `Web`, in assenza del segnale `Buoni`, deve restare nel bucket runtime degli scontrinati;
- questa regola vale per la lettura della schermata `Documenti`, per i bucket filtro collegati, per la colorazione riga e per i riepiloghi economici.

### Riepiloghi economici
- i totali della schermata `Documenti` devono essere calcolati sul set filtrato corrente della lista;
- il footer e il pannello riepiloghi devono riflettere i documenti ufficiali legacy realmente visibili;
- i documenti non scontrinati riconosciuti tramite `Buoni` non devono finire fuori dai totali o nel bucket sbagliato per effetto di semantiche storiche superate.
## Aggiornamento 2026-04-13

### Non scontrinati DB
- nella schermata `Documenti`, `Scontrinonumero` non e` la verita` primaria della classificazione runtime;
- `Contanti`, `Carta` e `Web` identificano i documenti scontrinati Banco;
- tutte le altre forme pagamento legacy del DB identificano il bucket operativo dei non scontrinati Banco;
- quel bucket copre `Salva`, `Cortesia`, `Sospeso`, la vecchia vendita Banco e i casi recuperabili non fiscalizzati;
- il bucket operativo dei non scontrinati deve pilotare insieme colore riga, filtro `Solo cortesia`, pulsante di cancellazione e riepiloghi.

### Cancellazione
- un documento non scontrinato letto dal DB deve poter essere cancellato dal legacy, con conferma esplicita, sia dalla lista `Documenti` sia dalla scheda `Vendita Banco`;
- la cancellazione reale deve eliminare il documento da `db_diltech` e rimuovere anche l'eventuale supporto tecnico locale collegato.

## Aggiornamento 2026-04-13 - Chiusura e progressivo Banco

### Persistenza della vendita
- la chiusura `Salva`, `Cortesia` e `Scontrino` non deve salvare il documento Banco nel local store;
- la vendita ufficiale vive solo nel `db_diltech`;
- eventuali supporti locali futuri possono esistere solo come appoggio tecnico separato, mai come seconda persistenza della vendita chiusa.

### Progressivo mostrato in `Vendita Banco`
- su una scheda nuova il numero mostrato e` solo una anteprima del prossimo progressivo Banco letta dal DB;
- l'anteprima non riserva alcun numero lato client;
- al publish il workflow deve rileggere il progressivo reale dal legacy, in modo coerente anche con due PC aperti in parallelo.

## Aggiornamento 2026-04-13 - Accesso operativo dei documenti

### Distinzione tra lista e scheda Banco
- `Contanti`, `Carta` e `Web` classificano i documenti come scontrinati per bucket, colore e riepiloghi;
- questa classificazione economica non deve bloccare da sola l'apertura della scheda Banco;
- la lista `Documenti` puo` restare prudenziale e non mostrare il `Del` per gli scontrinati;
- la scheda `Vendita Banco` resta invece il punto operativo da cui recuperare, modificare e cancellare i documenti ufficiali non chiusi da uno stato fiscale concluso.

### Cancellazione da scheda
- `Cancella scheda` in `Vendita Banco` non deve essere bloccato solo per la classificazione scontrinato della lista;
- il blocco di cancellazione resta solo quando il documento e` chiuso da uno stato fiscale concluso;
- il testo di conferma deve riflettere la cancellazione reale del documento da `db_diltech`.

### Delete legacy
- il servizio di cancellazione legacy non deve bloccare i documenti Banco solo per la presenza di pagamenti `Contanti`, `Carta` o `Web`;
- per la cancellazione contano il modello Banco e lo stato fiscale concluso, non la sola classificazione economica della lista;
- i documenti di prova e le vendite non ancora chiuse fiscalmente restano cancellabili dalla scheda Banco anche se la lista li mostra come scontrinati.

### Refresh live
- la cancellazione eseguita dalla scheda Banco deve rinfrescare subito la tab `Documenti`;
- la lista non deve restare con la riga fantasma fino a un refresh manuale dell'utente.

### Documento inesistente
- se un documento aperto dalla lista non esiste piu` nel legacy, la scheda Banco non deve aprirsi vuota;
- il comportamento corretto e` mostrare il popup `Documento inesistente` e interrompere il caricamento.

### Routine griglia condivisa
- il menu colori righe delle griglie Banco e Documenti e` centralizzato in una routine condivisa;
- le view restano specifiche per contenuto e layout, ma il menu comune si modifica da un solo punto.

### Colore intestazione per griglia
- il colore della parte superiore della griglia e` indipendente tra Banco e Documenti;
- il menu della griglia consente palette acquerello e scelta colore libera;
- la preferenza viene salvata localmente per singola griglia e ripristinata all'avvio;
- le griglie principali usano angoli superiori arrotondati per una resa piu` coerente con la UI.

### Cortesia Banco
- il pulsante `Cortesia` deve pubblicare o aggiornare il documento ufficiale Banco sul legacy;
- dopo il publish deve stampare la ricevuta di cortesia sulla `POS-80`;
- a cortesia completata la scheda `Banco` corrente deve essere riciclata in-place come `Nuovo documento`, senza aprire una nuova tab;
- la vendita appena pubblicata non deve restare come scheda separata nel flusso operativo standard;
- se la stampa termica non parte, il documento pubblicato sul legacy resta valido e la scheda mostra un warning senza perdere il contesto operativo.

### Chiusura operativa e riciclo della tab Banco
- quando una vendita viene conclusa con `Salva`, `Cortesia` o `Scontrino`, la tab `Banco` corrente deve essere riciclata in-place come `Nuovo documento`;
- la chiusura standard non deve aprire automaticamente una nuova scheda Banco;
- la vendita appena chiusa non deve restare come scheda separata di riferimento nel flusso operativo standard;
- il reset deve riusare la pipeline reale di nuovo documento, cosi` titolo, progressivo anteprima, cliente generico, listino e focus tornano coerenti nella stessa tab.

### Pagamento predefinito in chiusura
- `Salva`, `Cortesia` e `Scontrino` condividono la stessa regola di completamento pagamenti;
- se non e` stato inserito alcun importo, il Banco propone di valorizzare automaticamente il totale su `Contanti`;
- la conferma deve passare da popup con `Si` come azione predefinita anche via `Invio`;
- la regola deve valere sia dai pulsanti sia dalle scorciatoie tastiera;
- l'autocompilazione non rende il documento rigido: la scheda resta recuperabile e modificabile secondo le regole del suo stato fiscale.

### Scheda Banco dopo publish riuscito
- dopo un publish riuscito la scheda Banco deve essere considerata allineata al documento ufficiale legacy;
- `Salva`, `Cortesia` e `Scontrino` devono azzerare il flag delle modifiche correnti al termine del publish;
- il popup di uscita o cambio pagina non deve comparire subito dopo un publish riuscito, salvo nuove modifiche effettuate successivamente.

### Cliente e raccolta punti in vendita
- il campo cliente della scheda Banco deve mostrare il nominativo richiamato, non il valore tecnico del cliente generico;
- il saldo punti mostrato in vendita deve riflettere i punti attuali disponibili del cliente;
- i punti maturati dal documento corrente vanno calcolati in tempo reale e mostrati separatamente dal saldo cliente;
- il richiamo di un cliente da documento legacy non deve marcare la scheda come modificata se l'operatore non ha cambiato nulla.
- il badge `MATURA` e` visibile solo su schede operative con modifiche correnti, non quando si riapre una vendita gia` conclusa senza ulteriori variazioni;
- il riquadro cliente mostra punti e strumenti operativi, senza ripetere inutilmente il nominativo gia` presente nel campo ricerca;
- lo storico acquisti del cliente si apre dal box punti dedicato.

### Configurazione Banco su app pubblicata
- per l'app pubblicata il file configurazione primario vive nella cartella `Config` accanto all'eseguibile Banco;
- Banco crea automaticamente la cartella `Config` e il relativo `appsettings.user.json` se mancano;
- al primo avvio il file configurazione server puo` essere inizializzato copiando il vecchio `appsettings.user.json` dal profilo utente;
- il supporto tecnico SQLite viene tenuto nella cartella sorella `LocalStore` accanto al programma e non dentro `Config`;
- se una configurazione legacy punta ancora a `%LOCALAPPDATA%` o a `Config`, Banco riallinea automaticamente `LocalStore` alla cartella corretta del programma;
- il profilo utente `%LOCALAPPDATA%\Banco\appsettings.user.json` resta solo sorgente legacy/fallback di migrazione, non il punto principale per l'app pubblicata;
- la diagnostica deve distinguere chiaramente tra configurazione attiva, connessione `db_diltech` e supporto tecnico SQLite locale.

### Installer Banco
- il progetto usa uno script Inno Setup `Banco.iss` come base installer/aggiornamento del gestionale;
- la sorgente dell'installer e` il publish `publish\\Banco`;
- l'installazione standard e` `C:\Banco`;
- `Config` e `LocalStore` devono sopravvivere agli aggiornamenti e non vanno trattati come file usa-e-getta del pacchetto.
- il setup usa l'icona ufficiale `Banco.UI.Wpf\\Immagini\\Banco.ico`;
- la shell WPF usa lo stesso asset grafico Banco anche nella testata iniziale dell'applicazione.

### Runtime applicativo
- Banco usa `.NET 10` come base tecnica del progetto;
- `Banco.UI.Wpf` e `Banco.Punti` usano `net10.0-windows`;
- il modulo UI di navigazione `Banco.Sidebar` usa `net10.0-windows` ed e` separato dalla shell principale;
- i moduli di libreria Banco usano `net10.0`;
- se il publish resta `framework-dependent`, server e postazioni devono avere il runtime `.NET 10` coerente con la build pubblicata.
- in `Debug` Banco usa un manifest `asInvoker` per consentire il lavoro in Visual Studio senza elevare l'IDE;
- in `Release` e nel publish `Banco.exe` richiede privilegi amministrativi tramite manifest applicativo, cosi` l'avvio corretto non dipende da collegamenti o avvii manuali "come amministratore".
- il post-install dell'installer Banco deve usare `shellexec` quando avvia automaticamente l'app, per restare compatibile con il manifest elevato di `Release`.

### Publish applicativo
- il profilo `FolderProfile` pubblica Banco in `publish\\Banco`;
- il publish usa `single-file` framework-dependent per mantenere una root piu` compatta senza introdurre cartelle artificiali per le dll;
- i simboli debug `.pdb` non devono finire nel publish distribuito;
- l'installer prende come sorgente questa cartella publish gia` ripulita.

### Log e file tecnici
- i log applicativi Banco vengono salvati nella cartella `Log` accanto all'eseguibile del programma;
- la cartella `Log` viene creata automaticamente al primo avvio utile;
- i log non devono piu` confluire in un solo file unico: il servizio di logging deve separare almeno avvio, Banco, Documenti, amministrazione, stampa, POS e fiscale in file dedicati nella stessa cartella `Log`;
- i log transazione POS devono stare in `Log\\Pos\\Transazioni` sotto la cartella del programma, non in percorsi tecnici esterni;
- i percorsi tecnici WinEcr e XML fiscale continuano invece a usare `C:\tmp`, separati dai log applicativi Banco.

### Default configurazione operativa
- una configurazione nuova o parziale deve nascere gia` coerente con i default Banco reali;
- il DB legacy di default e` `127.0.0.1:3306 / db_diltech` con utente `root` e password di progetto;
- se l'utente lascia vuoto il campo host nella schermata `Configurazione DB`, Banco salva comunque `127.0.0.1`; sulle postazioni client il campo host va invece valorizzato con l'IP del server, ad esempio `192.168.1.111`;
- la configurazione POS default usa Nexi SmartPOS `192.168.1.233:8081` e registratore `192.168.1.231:1470`;
- la configurazione fiscale default usa i percorsi tecnici `C:\tmp` e il seriale Ditron del progetto;
- al primo setup l'installer deposita un `Config\\appsettings.user.json` gia` popolato con questi valori, senza sovrascrivere eventuali configurazioni esistenti;
- se la configurazione esistente e` parziale o contiene campi vuoti, Banco la normalizza e la riscrive automaticamente al primo avvio;
- se la configurazione esistente e` corrotta o non e` JSON valido, Banco la archivia automaticamente con suffisso `corrupted-YYYYMMDD-HHMMSS` e ne rigenera una valida con i default di progetto;
- un cambio configurazione DB salvato dall'utente deve essere visto subito dai servizi di lettura Banco, senza richiedere cache stale o restart impliciti.
- la configurazione applicativa deve prevedere anche una sezione `FmContent` per i percorsi legacy del contenuto esterno;
- il default di `FmContent.RootDirectory` e` `C:\Facile Manager\DILTECH`;
- il default di `FmContent.ArticleImagesDirectory` deve puntare alla cartella immagini articoli ricavata dalla root FM e restare modificabile dall'utente se i contenuti vengono spostati su un altro disco o percorso.

### Writer legacy e righe documento
- quando Banco aggiorna un documento legacy esistente deve cancellare prima i figli riga presenti in `documentorigacombinazionevarianti` e solo dopo le righe di `documentoriga`;
- questa sequenza evita errori FK durante gli aggiornamenti `Cortesia` o altri update sullo stesso `DocumentoGestionaleOid`.
- la scrittura ufficiale delle righe Banco deve valorizzare anche `documentoriga.Unitadimisura` e, per gli articoli variante, la relativa `documentorigacombinazionevarianti`;
- la riapertura di un documento legacy deve ricaricare da `documentoriga` e `documentorigacombinazionevarianti` la U.M., il barcode e gli OID variante, cosi` un salvataggio successivo non perde questi dati.

### Navigazione shell
- la shell resta il punto unico che apre le viste reali in tab;
- la mappa di navigazione condivisa vive nel contratto `INavigationRegistry` e non dentro i controlli UI;
- `Banco.Sidebar` costruisce rail, pannello contestuale, ricerca e personalizzazione leggendo il registro condiviso;
- la ricerca della sidebar usa titolo, alias e keyword delle destinazioni registrate e non deve diventare ricerca contenuti pagina;
- la shell ufficiale resta `ShellWindow`: non sono ammessi workspace paralleli, dashboard parallele o una seconda shell;
- la rail sinistra espone solo macro-categorie del registry;
- il pannello contestuale della categoria si apre su hover della macro-categoria nella rail, resta overlay sopra il workspace e deve sparire quando il contesto non lo richiede;
- il pannello contestuale non deve spingere, ridimensionare o riorganizzare il workspace centrale della shell;
- la ricerca shell visibile nella colonna categorie continua a usare il registry centrale e, quando porta a una entry, deve attivare la macro-categoria corretta, mostrare il pannello overlay e focalizzare la voce giusta;
- la ricerca del pannello categoria non deve esistere come seconda casella locale dentro il pannello overlay;
- la sincronizzazione del pannello contestuale deve avvenire anche quando la categoria attiva cambia per navigazione indiretta o selezione da ricerca globale, non solo per click diretto sulla rail;
- gli override persistiti della sidebar restano solo adapter UI unidirezionali e di compatibilita`, non rami paralleli di composizione menu o pannello;
- `ShellViewModel` resta orchestratore di apertura workspace usando metadata e risoluzione governati dal registry centrale, senza diventare una fonte autonoma di verita` navigazionale;
- l'area centrale di `ShellWindow` e` la scrivania ufficiale del gestionale: deve restare host workspace/navigation e non prendere ownership del contenuto di dominio dei moduli;
- la predisposizione del desktop centrale puo` introdurre solo struttura shell/UI per future superfici configurabili, non un framework docking/MDI parallelo se non gia` previsto dal progetto;
- scorciatoie duplicate come `Lista documenti` o `Raccolta punti` sono ammesse solo come ingressi operativi verso la stessa destinazione reale, non come percorsi paralleli.
- la destinazione reale `documenti.lista` deve aprire la UI ufficiale `Documenti`; il vecchio modulo legacy della lista documenti non deve piu` restare esposto nella sidebar, nella shell o come percorso utente alternativo.
- gli aggiornamenti del programma devono ripulire in modo controllato i binari applicativi obsoleti nella cartella `{app}`, preservando solo i percorsi dati persistenti (`Config`, `LocalStore`, `Log`, `Stampa`, `FastReport`) e i file di uninstall.
- `Lista riordino` non deve essere ospitata dentro `Documenti`: la sua collocazione corretta e` sotto `Magazzino`, come modulo autonomo apribile in una scheda propria;
- l'ingresso rapido da `Banco` verso `Lista riordino` e` ammesso come scorciatoia operativa verso la stessa destinazione reale sotto `Magazzino`;
- la schermata `Documenti` deve restare dedicata ai documenti e non diventare contenitore di moduli tecnici estranei al dominio documento ufficiale.
- la scheda `Configurazioni generali` e` il punto corretto dove far vivere le impostazioni strutturali del programma, incluse connessione DB, supporto tecnico e percorsi FM persistenti;
- la scheda `Configurazioni generali` deve essere aperta dal brand header dell'applicazione (`logo + Banco Operativo`) e non vivere come voce primaria della sidebar;
- la sidebar puo` continuare a offrire scorciatoie tecniche rapide, ma non deve diventare il luogo principale delle configurazioni amministrative rare.
- nelle `Configurazioni generali` i campi di input devono mantenere una scala leggibile da gestionale desktop: niente testo minuto o controlli che sembrano tecnici/provvisori.

### Correzione controllata documenti fiscalizzati
- un documento fiscalizzato/scontrinato si apre ancora inizialmente in consultazione;
- nella scheda `Banco` puÃ² comparire un pulsante esplicito `Abilita modifica` per sbloccare una correzione manuale controllata;
- dopo lo sblocco, `Salva` / `F4` aggiorna il documento legacy senza rilanciare la stampa fiscale;
- il documento fiscalizzato sbloccato non torna per questo a essere cancellabile liberamente o rifiscalizzabile dalla stessa scheda.

### Modulo `Banco.Stampa` e designer FastReport
- la pagina `FastReport` resta il punto tecnico unico per diagnosi runtime, layout, preview, prova stampa e apertura designer del nuovo modulo stampa;
- il layout pilota `Pos.frx` non deve piu` basarsi su un `TableDataSource` isolato registrato solo a runtime, perche` il Community Designer esterno lo considera non connesso in fase di salvataggio;
- per i layout POS pilotati dal nuovo modulo, il datasource design-time deve essere persistito nel `.frx` come connessione reale `CsvDataConnection`, alimentata da un file locale generato da Banco nella cartella `Stampa\\DesignData`;
- il designer FastReport va aperto dal programma Banco, che prima aggiorna il file dati locale e riallinea la connessione del report; l'apertura manuale del `.frx` fuori da Banco non e` il flusso affidabile di lavoro.
- `Banco.Stampa` non va modellato solo sul caso POS: deve esporre un catalogo di famiglie report riusabili, ciascuna con gruppi campi e schema dati propri;
- la base corrente deve gia` prevedere almeno le famiglie `POS / Vendita banco`, `Elenco clienti` e `Lista articoli`, cosi` nuovi report futuri nascono come nuove definizioni e non come moduli separati.
- per il layout POS nel designer i campi non-riga devono comparire come gruppi annidati coerenti col dominio tecnico del report, almeno `Negozio`, `Testata`, `Totali`, `Cliente`, `Punti` e `Footer`, e non come lista piatta di parametri scollegati.
- nella scheda `Banco`, il riquadro azioni in basso a destra ospita scorciatoie operative rapide e non configurazioni duplicate nella sidebar;
- `Impostazioni` da quel riquadro apre la configurazione Banco nella shell, `Registratore di cassa` apre la sezione fiscale/registratore e `Stampa (F10)` lancia la stampa POS80 diretta del documento corrente;
- `Cortesia` e `Stampa (F10)` devono restare flussi di stampa diretta POS80, senza anteprima intermedia.
- quando una scheda `Banco` chiude correttamente un flusso `Salva`, `Cortesia` o `Scontrino`, la stessa scheda deve essere resettata in-place come nuova vendita, anche se l'operazione parte dalla scheda Banco principale iniziale;
- l'azione `Anteprima` del POS80 non deve piu` aprire il browser esterno: deve essere mostrata in una finestra modale interna a Banco;
- la stampa POS80 non deve piu` aprire la preview di stampa del browser: il runtime deve preparare il file di stampa e la UI WPF di Banco deve inviarlo alla stampante assegnata tramite helper interno, senza browser esterni.
