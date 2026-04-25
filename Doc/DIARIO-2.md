# DIARIO-2.md

## 2026-04-25 - Vendita Banco consuma il barcode senza concatenare letture rapide

Decisione chiusa:
- nella vendita al Banco, il lookup articolo aperto da ricerca manuale espande i risultati padre in varianti reali quando l'articolo ha varianti;
- la conferma dal lookup prende sempre la riga selezionata prima di chiudere il modale e passa dal flusso di inserimento reale;
- se per qualunque motivo nel lookup resta una riga padre con varianti, Banco non chiude il modale e non inserisce il padre: chiede di selezionare una variante reale;
- quando l'input e` riconosciuto come scanner, il testo del campo ricerca viene consumato e pulito subito prima della query, cosi` una seconda lettura rapida non si accoda al codice precedente;
- il filtro anti-doppio evento dello stesso barcode resta solo come protezione strettissima da rimbalzi tecnici, non come blocco operativo di una doppia scansione reale.

Regola viva:
- in vendita Banco non si deve mai scaricare il padre se esistono varianti: lookup, doppio click, pulsante di conferma e scanner devono convergere sull'articolo/variante realmente movimentabile.

## 2026-04-25 - Desktop Banco trasformato in scrivania modulare

Decisione chiusa:
- il desktop non deve essere una dashboard monolitica ne` una pagina unica difficile da sostituire;
- la schermata e` stata divisa in componenti WPF separati: azioni rapide, situazione oggi, segnalazioni operative e attivita` recenti;
- i dati dei blocchi sono modelli semplici e portabili, senza dipendere da controlli WPF, cosi` una futura UI alternativa puo` riusarli;
- la segnalazione `F.E. disponibili` e` gia` rappresentata come item modulare predisposto, non come testo cucito nel layout;
- `DesktopHomeView` resta solo composizione della scrivania e non contiene tutta la logica visuale dei pannelli.

Regola viva:
- nuovi blocchi del desktop vanno aggiunti come sezioni/componenti modulari e dati trasportabili, non accodando XAML e logica dentro un unico file.

## 2026-04-25 - Ricerca comando spostata nell'header della scrivania

Decisione chiusa:
- il campo `Cerca comando` non vive piu` nel rail laterale sinistro, cosi` la sidebar resta piu` stretta e dedicata alla navigazione principale;
- la ricerca e` stata portata nella testata della `Scrivania Banco`, con input compatto da 30px e risultati in menu contestuale ordinato;
- la pipeline resta quella del `SidebarHostViewModel` e del registry centrale di navigazione: non sono state duplicate collezioni UI o destinazioni hardcoded;
- il click su un risultato usa la stessa apertura della sidebar, quindi evidenzia e naviga verso la destination gia` registrata.

Regola viva:
- le ricerche comando della shell devono derivare dal registry centrale e possono cambiare posizione visuale senza creare una seconda logica di navigazione.

## 2026-04-25 - Pannello categorie sidebar adattivo verso l'alto

Decisione chiusa:
- il pannello contestuale della sidebar non viene piu` aperto sempre verso il basso dalla categoria selezionata;
- quando una categoria e` vicina al fondo della finestra, la posizione verticale viene corretta in base all'altezza stimata del contenuto e allo spazio disponibile;
- il pannello mantiene uno scroll interno, cosi` categorie future con molti comandi restano utilizzabili senza uscire dallo schermo.

Regola viva:
- i menu della shell devono rimanere sempre contenuti nell'area visibile, anche quando partono da categorie basse o hanno molte voci.

## 2026-04-25 - Prototipo desktop Avalonia isolato

Decisione chiusa:
- e` stato creato il progetto `Banco.UI.Avalonia.Test` come host sperimentale separato dal Banco WPF operativo;
- il prototipo mostra una prima `Scrivania Banco` Avalonia con rail compatto, testata, ricerca comando da 30px, azioni rapide, situazione oggi, segnalazioni e attivita` recenti;
- il prototipo non accede al DB legacy, non modifica il flusso Banco e non sostituisce la shell WPF;
- serve solo per valutare resa grafica, densita`, tema e possibilita` futura di portare la UI sopra logica non dipendente da WPF.

Regola viva:
- ogni esperimento Avalonia deve restare isolato finche` non esiste una decisione esplicita di migrazione, riusando dati/modelli portabili e senza duplicare dominio o persistenza.

## 2026-04-25 - Prototipo Avalonia reso dimostrativo

Decisione chiusa:
- il prototipo Avalonia non deve essere una copia 1:1 del WPF: serve a mostrare potenzialita` grafiche, personalizzazioni, icone e cambio tema;
- la schermata e` stata ridisegnata con rail piu` sottile, superfici custom, icone vettoriali da path e palette piu` marcata;
- e` stato aggiunto il cambio tema chiaro/scuro tramite risorse dinamiche applicate alla finestra;
- i dati restano mock e isolati, senza agganci al DB o al flusso operativo Banco reale.

Regola viva:
- i prototipi UI alternativi devono esplorare una direzione visiva propria, ma senza introdurre logica di dominio duplicata.

## 2026-04-25 - Avalonia Lab allineamento rail e dock alto

Decisione chiusa:
- nel prototipo Avalonia il rail laterale usa ora misure fisse per pulsanti, icone e testo, cosi` le icone restano allineate verticalmente e centrate;
- `Dock rapido` e` stato spostato sopra i pannelli di destra, prima delle segnalazioni, per renderlo piu` accessibile;
- la modifica resta confinata al progetto sperimentale `Banco.UI.Avalonia.Test`.

Regola viva:
- nel laboratorio Avalonia le aree di accesso rapido devono stare in alto o in zone operative immediate, non in fondo alla schermata.

## 2026-04-25 - Actipro Avalonia free referenziato nel laboratorio

Decisione chiusa:
- il progetto `Banco.UI.Avalonia.Test` resta cross-platform con target `net10.0`, quindi puo` essere aperto e compilato anche su macOS con .NET SDK compatibile;
- e` stato aggiunto solo il pacchetto NuGet free `ActiproSoftware.Controls.Avalonia` versione `25.2.3`;
- non e` stato aggiunto il pacchetto `ActiproSoftware.Controls.Avalonia.Pro`, indicato come commerciale e soggetto a licenza;
- la build del laboratorio con il pacchetto free passa senza errori e senza avvisi.

Regola viva:
- nel laboratorio Avalonia si possono valutare librerie esterne solo separando chiaramente pacchetti free da pacchetti commerciali/licenziati.

## 2026-04-25 - Avalonia Lab promosso e design system separato

Decisione chiusa:
- il progetto sperimentale e` stato rinominato da `Banco.UI.Avalonia.Test` a `Banco.UI.Avalonia.Lab`;
- la solution punta ora a `Banco.UI.Avalonia.Lab/Banco.UI.Avalonia.Lab.csproj`;
- assembly, namespace e comando di avvio sono stati aggiornati su `Banco.AvaloniaLab`;
- il tema chiaro/scuro non vive piu` nel code-behind della finestra: e` centralizzato in `DesignSystem/BancoAvaloniaTheme.cs`;
- gli stili base del laboratorio sono stati separati in `DesignSystem/ControlStyles.axaml` e inclusi da `App.axaml`.

Regola viva:
- il laboratorio Avalonia deve crescere con design system e componenti separati, non accumulando tutta la UI dentro `MainWindow.axaml`.

## 2026-04-25 - Tab `Confezione` compatta a baseline 30px

Decisione chiusa:
- la tab `Confezione` della scheda `Gestione Articolo` usa controlli compatti da 30px per textbox, picker e pulsanti operativi;
- i dati legacy articolo, i listini e i parametri locali sono stati riordinati in blocchi piu` densi, con meno spazio morto verticale;
- il pannello dei parametri locali mantiene SQLite solo per i dati interni di riordino, senza cambiare la semantica dei campi legacy.

Regola viva:
- le tab operative dentro `Gestione Articolo` devono restare da gestionale compatto: controlli allineati, altezza 30px e nessun vuoto strutturale non motivato.

## 2026-04-25 - `Gestione Articolo` legge da FM Condizione e Carta fedelta`

Decisione chiusa:
- il campo `Condizione` della sezione `E-commerce e codifiche` non resta piu` una textbox con codice grezzo: e` un picker compatto che legge i valori legacy da `config` tramite `EnumCondizione*` e mostra il valore salvato in `articolo.Condizione`;
- nel pannello centrale `Opzioni` viene esposto il campo `Carta fedelta` / tipo operazione, letto da `articolo.Operazionesucartafedelta` e risolto tramite `EnumOperazioneSuCartaFedelta*`;
- non e` stata introdotta alcuna tabella o colonna nel DB legacy: Banco usa solo campi e lookup gia` presenti in `db_diltech`.

Regola viva:
- i campi articolo visibili anche in FM devono essere letti e salvati sugli stessi campi legacy reali; SQLite resta escluso da queste informazioni ufficiali.

## 2026-04-25 - `Gestione Articolo` avvia il lookup padre anche senza Enter scanner

Decisione chiusa:
- il campo `Codice / barcode / ricerca articolo` della scheda `Gestione Articolo` ora avvia il controllo codice/barcode direttamente quando cambia il valore;
- questo copre gli scanner che scrivono il barcode nel campo senza inviare un Enter riconosciuto;
- se il barcode e` in `articolomulticodice.Codicealternativo`, Banco risale comunque alla scheda padre dell'articolo;
- il modale di ricerca automatica non si apre piu` per stringhe che sembrano codici/barcode diretti, cosi` non interferisce con il caricamento della scheda padre.

Regola viva:
- in `Gestione Articolo`, lo scanner deve aprire direttamente la scheda padre quando il barcode e` univoco; la finestra lookup resta per ricerche descrittive o ambigue.
