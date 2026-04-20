# AGENTS.md

## Scopo

Questo workspace contiene il progetto `Banco`, applicazione desktop WPF per la vendita al banco, integrata con il gestionale legacy su `db_diltech`.

Questo file definisce le regole permanenti del progetto.  
Chiunque lavori nel workspace deve leggerlo prima di proporre analisi, prompt o codice.

---

## File di riferimento obbligatori

Prima di procedere leggere sempre, in questo ordine:

1. `Doc/AGENTS.md`
2. `Doc/STRUTTURA_BANCO.md`
3. `Doc/DIARIO.md`

Regola tassativa:
- questi tre file sono la fonte primaria di verità del progetto;
- se emergono contraddizioni tra chat e file, prevalgono i file finché non vengono aggiornati esplicitamente.
- tutti i file `.md` del progetto devono vivere nella cartella `Doc` sotto la root del workspace;
- nei riferimenti interni non vanno usati percorsi assoluti della macchina, ma il formato stabile `Doc/<nomefile>.md`.

---

## Regole di lavoro nel progetto

### 1. Niente architetture parallele

Non creare:
- flussi alternativi scollegati dal Banco reale;
- modelli di documento paralleli;
- categorie inventate fuori dal dominio reale;
- percorsi di persistenza diversi dal writer ufficiale.

### 2. Database legacy intoccabile nella struttura

`db_diltech` è il DB operativo ufficiale.

Non introdurre:
- nuove tabelle;
- nuovi campi;
- codifiche parallele nel DB legacy;
- logiche invasive sul DB legacy.

Prima di modellare un concetto, verificare sempre se esiste già nel DB reale.

Regola ferrea aggiuntiva:
- quando si lavora sul legacy, non basta conoscere la tabella principale coinvolta;
- vanno controllate e rispettate tutte le tabelle collegate dal flusso reale;
- vanno verificati vincoli, foreign key, lookup, codifiche, campi usati da `FM` e tabelle accessorie lette o scritte dal gestionale;
- non si devono dedurre significati o valori "probabili" senza verifica sul DB reale;
- se il legacy salva un concetto tramite piu` tabelle coordinate, Banco deve replicare quella struttura reale e non una versione semplificata.

### 3. Unica linea funzionale del Banco

Nel Banco esiste una sola linea funzionale corretta:

- documento ufficiale su `db_diltech`
- numerazione ufficiale legacy
- scrittura ufficiale tramite writer Banco
- fiscalizzazione separata tramite WinEcr
- nessuna persistenza vendita su SQLite o local store come appoggio del flusso Banco operativo

Non devono esistere nei documenti di progetto concetti funzionali paralleli al documento ufficiale.

### 4. Distinguere sempre dominio e dettaglio tecnico

Qualsiasi supporto tecnico interno:
- non è un secondo documento;
- non è uno stato utente;
- non è una categoria di dominio;
- non deve essere descritto come alternativa al documento ufficiale Banco.

Se un dettaglio tecnico va citato, deve essere dichiarato solo come supporto tecnico interno, mai come concetto funzionale.

### 5. Commenti nel codice

I commenti nel codice devono essere:
- in italiano;
- tecnici;
- sintetici;
- utili.

Evitare commenti banali o ridondanti.

---

## Verità architetturali non negoziabili

### Gestionale master
Il gestionale legacy resta il sistema master per i documenti ufficiali compatibili.

### Database corretto
Il database corretto è:

- `db_diltech`

### Modello documento Banco
Il modello documento ufficiale Banco è:

- `modellodocumento = 27`

### Writer ufficiale unico
Il writer ufficiale è l’unico punto di scrittura verso:
- `documento`
- `documentoriga`
- `documentoiva`

Nessun altro flusso deve scrivere in parallelo sul legacy.

### Fiscalizzazione
La fiscalizzazione reale:
- è separata dalla pubblicazione del documento legacy;
- passa da WinEcr;
- non va confusa con la scrittura del documento legacy.

---

## Regole funzionali permanenti

### 1. `Cortesia`
`Cortesia`:
- è un documento ufficiale Banco;
- usa numerazione ufficiale legacy;
- può essere recuperata sullo stesso `DocumentoGestionaleOid`;
- può essere fiscalizzata successivamente.

### 2. `Scontrino`
`Scontrino`:
- è un documento ufficiale Banco;
- usa numerazione ufficiale legacy;
- pubblica prima il documento legacy;
- poi esegue la fiscalizzazione tramite WinEcr.

### 3. `Salva`
`Salva`:
- non è una bozza finale;
- non conclude fuori dal dominio ufficiale;
- pubblica o aggiorna il documento ufficiale Banco sul legacy;
- ha stato utente `Pubblicato Banco`.

### 4. `Sospeso`
`Sospeso`:
- non è categoria principale;
- è una componente aggiuntiva del documento;
- si ricostruisce prioritariamente dal legacy tramite `Pagatosospeso`.

### 5. Documenti fiscalizzati
Un documento fiscalizzato:
- non è liberamente modificabile;
- può essere sbloccato solo con azione esplicita di abilitazione modifica dalla scheda Banco;
- una volta sbloccato resta correggibile in modo controllato e si richiude con `Salva` / `F4` senza nuova stampa;
- non va trattato come scheda operativa libera di default;
- non va confuso con un documento recuperabile.

### 6. Documenti non fiscalizzati
Un documento ufficiale Banco non fiscalizzato:
- può essere recuperato;
- può essere aggiornato sullo stesso `DocumentoGestionaleOid`;
- non deve essere trattato come chiuso in modo totale.

---

## Regole UI permanenti

### 1. UI professionale
La UI deve essere:
- moderna;
- pulita;
- coerente col programma;
- professionale;
- non piatta;
- non stancante;
- senza gigantismi;
- senza estetica anni '90;
- senza elementi decorativi inutili.

### 2. Niente ambiguità utente
La UI non deve mai:
- mostrare concetti tecnici interni come stati utente;
- confondere `Cortesia`, `Scontrino`, `Pubblicato Banco`;
- mostrare numerazioni tecniche come numeri documento;
- trattare supporti tecnici come documenti di dominio.

### 3. Liste, griglie, footer
Per qualsiasi griglia o lista:
- rendering, selezione, dettaglio laterale e footer devono restare coerenti;
- non devono esistere stati fantasma;
- il click destro deve usare la stessa pipeline visiva della selezione normale;
- i riepiloghi devono riflettere il set dati reale che la schermata sta usando.

### 4. Stabilita` visiva Banco
Nella schermata `Banco`:
- la pagina non deve "ballare" durante l'uso;
- banner, notifiche operative, punti, promo e sospeso devono stare in aree stabili;
- i messaggi variabili non devono cambiare l'altezza complessiva dei riquadri principali;
- i contenuti dinamici devono preferire fasce fisse con testo troncato o riepiloghi compatti.

### 5. Ricerca operativa
Le ricerche operative del Banco devono essere:
- coerenti con il comportamento atteso di un catalogo reale;
- multi-termine;
- tolerant su spazi e separatori;
- ordinate per rilevanza reale del risultato.

Quando si parla di ricerca articoli:
- la semantica corretta e` il campo ricerca live;
- non va documentato un "tasto Cerca" come meccanica principale se il flusso reale e` live da textbox.

### 6. Fix UI
Un fix UI non è chiuso se il risultato non è:
- visibile davvero;
- riproducibile davvero;
- verificato in uno scenario reale.

Il codice “teoricamente corretto” non basta.

### 7. Governance UI e componenti condivisi
Per tutte le schermate UI del progetto:
- non usare popup o dialog nativi Windows improvvisati se esiste un componente applicativo standard;
- non creare griglie, toolbar, card, input o tab locali se esiste già una variante condivisa;
- se manca uno standard globale, crearlo centralmente prima di applicarlo alla view;
- le schermate operative dense devono usare layout compatti da gestionale;
- sono vietati spacing e padding eccessivi, controlli oversize e spazio morto non motivato;
- i controlli devono usare taglie ufficiali centralizzate del design system;
- non introdurre linguaggi visivi diversi tra Banco, Documenti, Configurazioni, FastReport, Import e moduli affini.
---

## Regole su prompt e coding agent

### Quando si prepara un prompt
Ogni prompt per Codex o Claude Code deve:
- avere un solo obiettivo per volta;
- indicare file target;
- indicare cosa non toccare;
- definire il comportamento atteso finale;
- includere test obbligatori;
- includere clausola `il lavoro non è chiuso se...`;
- imporre aggiornamento dei file `.md` interessati.

### Quando il task è UI
Per task UI bisogna imporre sempre:
- scenario reale di test;
- risultato visibile atteso;
- divieto di chiudere il task solo su base teorica;
- verifica finale osservabile in schermata.

### Quando il task è lungo
Se il task è lungo o ambiguo:
- procedere step by step;
- uno step per volta;
- chiudere uno step prima di aprire il successivo.

---

## Regole documentali

### 1. Documentazione attiva del progetto
La documentazione attiva del progetto è concentrata solo in:
- `Doc/AGENTS.md`
- `Doc/STRUTTURA_BANCO.md`
- `Doc/DIARIO.md`

Altri file storici possono esistere solo in archivio, non come fonte attiva.

### 2. Cosa va aggiornato nei documenti
Vanno registrati nei documenti:
- grossi bug chiusi che cambiano il comportamento reale;
- routine rilevanti introdotte o modificate;
- moduli nuovi o modificati in modo significativo;
- cambiamenti architetturali;
- cambiamenti di dominio;
- step chiusi;
- decisioni operative che devono restare vive nelle prossime chat.

Non vanno registrati nei documenti:
- micro-fix senza impatto reale;
- ritocchi minori di layout;
- pulizie locali di codice;
- refusi o rinomine irrilevanti;
- sperimentazioni non consolidate.

### 3. Nessuna semantica sporca nei `.md`
Nei file attivi non devono comparire:
- semantiche parallele al documento ufficiale;
- stati utente tecnici;
- descrizioni che confondono supporto tecnico e dominio;
- terminologia che riapre concetti già eliminati dal progetto.

### 4. Checklist obbligatoria per task sulla navigazione shell
Per ogni task futuro che tocca shell, sidebar, pannello contestuale o ricerca navigazionale:
- verificare prima il registry centrale di navigazione;
- non aggiungere funzioni navigabili con wiring locale sparso nella sola UI;
- registrare la nuova entry nel registry prima di esporla in shell;
- compilare i metadati minimi obbligatori della entry;
- far derivare ricerca e pannello contestuale dallo stesso registry, non da collezioni UI duplicate;
- evitare mapping hardcoded permanenti in `ShellViewModel` che decidano davvero la destinazione finale;
- aggiornare `Doc/STRUTTURA_BANCO.md` solo se cambia la regola architetturale stabile;
- aggiornare `Doc/DIARIO.md` per tracciare la decisione o la migrazione reale chiusa.

---

## Metodo richiesto nelle nuove chat

Quando si apre una nuova chat su questo progetto:
- chiedere prima i file aggiornati se sono cambiati;
- leggere i tre file principali;
- non proporre subito codice se la richiesta è ancora di analisi o pianificazione;
- mantenere continuità con le decisioni già chiuse.

Formula desiderata all’inizio della nuova chat:

> Prima di partire, mandami i file aggiornati del progetto se sono cambiati dall’ultima volta, soprattutto quelli architetturali e il diario operativo.

---

## Obiettivo attuale del progetto

L’obiettivo corrente è consolidare il Banco reale su:
- documento ufficiale Banco su `db_diltech`
- numerazione legacy ufficiale
- fiscalizzazione WinEcr separata
- recupero sullo stesso `DocumentoGestionaleOid`
- lista documenti coerente
- UI professionale e non ambigua
- documentazione pulita e definitiva

---

## Regola finale

Se una proposta:
- reintroduce semantiche parallele,
- sporca il dominio,
- usa scorciatoie tecniche come verità utente,
- o contraddice il documento ufficiale Banco su `db_diltech`,

allora va considerata sbagliata e non va implementata.
