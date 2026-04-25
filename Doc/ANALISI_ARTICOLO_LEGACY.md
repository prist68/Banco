# ANALISI_ARTICOLO_LEGACY.md

## Scopo

Questo file raccoglie la mappa tecnica minima affidabile per iniziare la gestione `Articolo` in Banco senza introdurre disallineamenti rispetto a `FM`.

Focus del file:
- tabelle legacy reali coinvolte;
- collegamenti confermati su `db_diltech`;
- parti che Banco oggi gestisce gia`;
- parti che non possono essere ignorate se si vuole arrivare a creazione/modifica articolo compatibile con `FM`.

Questo file **non** descrive la UI.

---

## Esito sintetico

Sì: dal dump + dal DB reale c'e` abbastanza materiale per iniziare il lavoro su `Articolo`.

Pero` c'e` una regola forte:

- **non basta la sola tabella `articolo`**;
- per un salvataggio coerente con `FM` bisogna separare almeno:
  - tabella master;
  - lookup FK / combo reali;
  - tabelle figlie editabili;
  - tabelle derivate/cache che non vanno scritte a mano.

Se si ignora anche solo una parte importante:
- `FM` puo` mostrare dati incoerenti;
- alcune combo possono risultare scollegate;
- i prezzi o le varianti possono andare fuori sincronia;
- il record puo` sembrare salvato ma restare logicamente incompleto.

---

## 1. Tabella master reale

La tabella principale e`:

- `articolo`

Colonne confermate e rilevanti trovate sul DB reale:

- chiave:
  - `OID`
- lookup/FK diretti:
  - `Categoria`
  - `Categoriaricarico`
  - `Contoprimanota`
  - `Contoprimanotaricavo`
  - `Disponibilitaonline`
  - `Imballaggio`
  - `Iva`
  - `Produttore`
  - `Unitadimisura`
  - `Unitadimisura2`
  - `Variante1`
  - `Variante2`
  - `Tassa`
  - `Articolodalavorare`
- campi anagrafici/operativi:
  - `Codicearticolo`
  - `Codiciabarre`
  - `Descrizionearticolo`
  - `Notearticolo`
  - `Notearticoloestese`
  - `Codicetipo`
  - `Codicevalore`
  - `Quantitaminimavendita`
  - `Quantitamultiplivendita`
  - `Qtapercollo`
  - `Moltiplicativoum`
  - `Moltiplicativoum2`
  - `Arrotondamentoprezzo`
  - `Garanziamesivendita`
  - `Avvertenze`
  - `Tagecommerce`
  - `Slug`
  - `Ubicazione`
- flag/bit rilevanti:
  - `Escludiinventario`
  - `Escluditotaledocumento`
  - `Escludiscontrino`
  - `Escludiscontosoggetto`
  - `Usavenditaalbancotouch`
  - `Esporta`
  - `Online`
  - `Invetrina`
  - `Marketplace`
  - `Usatassa`
  - `Usamisure`
  - `Usaconfiguratoreprodotto`
  - `Descrizionebreveinaltridatigestionali`
  - `Stampanoteindocumenti`
- codifiche int "enum-like" confermate presenti nei dati:
  - `Condizione`
  - `Tipoarticolo`
  - `Tracciabilita`
  - `Tipocostoarticolo`
  - `Operazionesucartafedelta`
  - `Assegnazionemisure`

Conclusione pratica:

- `articolo` e` il master anagrafico;
- ma non e` sufficiente da solo per coprire prezzi, barcode alternativi, immagini, specifiche e varianti.

---

## 2. FK dirette da `articolo`

FK confermate dal DB reale:

- `articolo.Categoria -> categoria.OID`
- `articolo.Categoriaricarico -> categoriaricarico.OID`
- `articolo.Contoprimanota -> contoprimanota.OID`
- `articolo.Contoprimanotaricavo -> contoprimanota.OID`
- `articolo.Disponibilitaonline -> disponibilitaonline.OID`
- `articolo.Imballaggio -> imballaggio.OID`
- `articolo.Iva -> iva.OID`
- `articolo.Produttore -> soggetto.OID`
- `articolo.Tassa -> tassa.OID`
- `articolo.Unitadimisura -> unitadimisura.OID`
- `articolo.Unitadimisura2 -> unitadimisura.OID`
- `articolo.Variante1 -> variante.OID`
- `articolo.Variante2 -> variante.OID`
- `articolo.Articolodalavorare -> articolo.OID`

Nota importante:

- `Produttore` **non** punta a una tabella `produttore`, ma a `soggetto`;
- quindi la combo `Produttore` va trattata come lookup filtrato su `soggetto`, non come tabella autonoma inventata.

---

## 3. Tabelle figlie che referenziano `articolo`

Tabelle con FK verso `articolo` confermate dal DB reale:

- `articolocategoria`
- `articolocomponente`
- `articoloconfiguratore`
- `articoloconfiguratoreopzione`
- `articolocorrelato`
- `articolodizionario`
- `articolodizionarioscelta`
- `articoloimmagine`
- `articololistino`
- `articolomulticodice`
- `articolomultilistino`
- `articolonegoziostore`
- `articolopersonalizzato`
- `articoloricercaavanzata`
- `articolosituazione`
- `articolosituazionecombinazionevariante`
- `articolospecificatecnica`
- `articolotipotrasporto`
- oltre a moduli a valle che leggono l'articolo:
  - `documentoriga`
  - `archidoc`
  - `carrello`
  - `distintabase`
  - `valorizzazionearticolo`
  - ecc.

Interpretazione utile:

- non tutte vanno scritte nel primo step;
- ma vanno tutte considerate nel perimetro, perche' molte rappresentano parti reali della scheda articolo FM.

---

## 4. Tabelle da trattare come editabili nel dominio articolo

Queste sono le prime tabelle che non possono essere ignorate se si vuole una gestione articolo credibile in Banco.

### 4.1 `articololistino`

Serve per:
- prezzo base;
- prezzi ivati/netti;
- fasce quantita`;
- varianti di prezzo per combinazione variante;
- date inizio/fine;
- riferimento listino.

Colonne chiave confermate:

- `Articolo`
- `Listino`
- `Variantedettaglio1`
- `Variantedettaglio2`
- `Quantitaminima`
- `Valore`
- `Valoreivato`
- `Datainizio`
- `Datafine`
- `Iva`
- `Offerta`

Questa tabella e` **centrale**.

Se Banco vuole modificare prezzi o promo quantita`, deve passare di qui.

### 4.2 `articolomulticodice`

Serve per:
- codici alternativi;
- barcode multipli;
- PLU;
- prezzo predefinito calcolato per alcuni codici;
- eventuale esportazione codici.

Colonne chiave:

- `Articolo`
- `Variantedettaglio1`
- `Variantedettaglio2`
- `Codicealternativo`
- `Plu`
- `Tipocodiceabarre`
- `Qtaalternativa`

Se Banco vuole una vera gestione barcode/codici, questa tabella non e` opzionale.

### 4.3 `articoloimmagine`

Serve per:
- immagini articolo;
- immagini variante;
- ordinamento immagini;
- immagine predefinita.

Colonne chiave:

- `Articolo`
- `Variantedettaglio`
- `Fonteimmagine`
- `Posizione`
- `Predefinita`

### 4.4 `articolospecificatecnica`

Serve per:
- specifiche tecniche;
- tag/spec ecommerce;
- legame tra articolo, caratteristica e valore caratteristica.

Colonne chiave:

- `Articolo`
- `Caratteristica`
- `Caratteristicavalore`
- `Specificatecnica`
- `Posizione`

### 4.5 Tabelle opzionali ma funzionalmente reali

Da considerare in step successivi, non da ignorare nel disegno:

- `articolocorrelato`
- `articolocomponente`
- `articoloconfiguratore`
- `articoloconfiguratoreopzione`
- `articolocategoria`
- `articolotipotrasporto`
- `articolodizionario`
- `articolodizionarioscelta`

---

## 5. Tabelle derivate / cache da non trattare come master di input

Queste tabelle esistono davvero, ma non vanno considerate la fonte primaria da editare a mano:

- `articolomultilistino`
- `articolosituazione`
- `articolosituazionecombinazionevariante`
- `valorizzazionearticolo`

Perche' non vanno trattate come master:

- aggregano o proiettano prezzi / esistenze / disponibilita`;
- rappresentano situazione di magazzino o snapshot derivati;
- sono lette da Banco/FM, ma non sono il punto corretto da usare come anagrafica articolo.

Regola pratica:

- si possono leggere per mostrare disponibilita`, ultimo costo, listino migliore, ecc.;
- ma non devono diventare il write model principale della scheda `Articolo`.

---

## 6. Lookup reali per le combo

Lookup confermati dal DB reale.

### 6.1 Combo con tabella certa

- `Produttore` -> `soggetto`
- `Iva` -> `iva`
- `Unita` / `2° Um` -> `unitadimisura`
- `Categoria` -> `categoria`
- `Categoria ricarico` -> `categoriaricarico`
- `Conto costo` / `Conto ricavo` -> `contoprimanota`
- `Variante 1` / `Variante 2` -> `variante`
- `Disponibilita` ecommerce -> `disponibilitaonline`
- `Imballaggio` -> `imballaggio`
- `Tassa / accise` -> `tassa`
- `Listino` prezzi -> `listino`

### 6.2 Colonne utili trovate nelle lookup

#### `soggetto`

Rilevante per `Produttore`:
- `OID`
- `Ragionesociale1`
- `Sigla`
- `Tiposoggetto`

Serve un filtro applicativo corretto per non mostrare soggetti non coerenti.

#### `iva`

Rilevante per combo IVA:
- `OID`
- `Iva`
- `Percentualeiva`
- `Predefinitoarticolo`
- `Repartorc`
- `Repartodescrizione`

#### `unitadimisura`

- `OID`
- `Unitadimisura`
- `Descrizione`
- `Predefinito`

#### `contoprimanota`

- `OID`
- `Conto`
- `Gruppo`
- `Tipoconto`
- `Tipocontoprimanota`

#### `categoriaricarico`

- `OID`
- `Categoriaricarico`
- `Percentualericarico`
- `Predefinito`

#### `variante`

- `OID`
- `Variante`
- `Sigla`
- `Tipo`

---

## 7. Codifiche int "enum-like" ancora da trattare con cautela

Su queste colonne il DB reale non ha mostrato una FK diretta nelle verifiche fatte.

Quindi oggi vanno considerate:
- **campi reali e usati**;
- ma con dominio valori ancora da verificare meglio prima di esporre una scrittura completa.

Campi:

- `Condizione`
- `Tipoarticolo`
- `Tracciabilita`
- `Tipocostoarticolo`
- `Operazionesucartafedelta`
- `Assegnazionemisure`

Valori osservati sul DB reale:

- `Condizione`: solo `0`
- `Tipoarticolo`: `0`, `5`, `7`
- `Tracciabilita`: solo `0`
- `Tipocostoarticolo`: solo `0`
- `Operazionesucartafedelta`: `0`, `1`
- `Assegnazionemisure`: solo `0`

Conclusione:

- non vanno inventate combo o descrizioni finali solo per analogia;
- prima della scrittura completa serve capire se `FM` le tratta come enum hardcoded, tabelle non FK, o codifiche legacy sparse.

---

## 8. Cosa Banco gestisce gia` oggi

Dal codice corrente di Banco:

### Lettura (`GestionaleArticleReadService`)

Banco oggi legge gia` parti reali del dominio articolo da:

- `articolo`
- `articololistino`
- `articolomultilistino`
- `articolosituazione`
- `valorizzazionearticolo`
- `articoloimmagine`
- `articolospecificatecnica`
- `categoria`
- `tassa`
- `listino`

Quindi Banco possiede gia` una base concreta per:
- ricerca articolo;
- lookup articolo;
- prezzo di vendita;
- giacenza / disponibilita`;
- dettaglio descrittivo;
- immagini;
- specifiche tecniche;
- listini e promo quantita`.

### Scrittura (`GestionaleArticleWriteService`)

Banco oggi scrive solo una parte ridotta ma reale:

- su `articolo`:
  - `Descrizionearticolo`
  - `Unitadimisura`
  - `Unitadimisura2`
  - `Moltiplicativoum`
  - `Moltiplicativoum2`
  - `Quantitaminimavendita`
  - `Quantitamultiplivendita`
- su `articololistino`:
  - prezzo base preferito
  - fasce quantita`

Questo significa:

- il modulo attuale **non** copre ancora la scheda articolo FM completa;
- ma non parte da zero.

---

## 9. Perimetro minimo corretto per una futura modifica articolo completa

Se il target e` "creare/modificare articolo in modo compatibile con FM", il perimetro minimo corretto e` questo.

### 9.1 Master anagrafico

Scrittura diretta su `articolo` per:

- descrizione e note;
- codici base;
- unita` di misura;
- produttore;
- IVA;
- categoria;
- conti;
- varianti principali;
- flag operativi (`Escludiscontrino`, `Esporta`, `Usavenditaalbancotouch`, ecc.);
- ecommerce base;
- tassa/accise;
- quantita` minima / multipli / collo;
- garanzia / arrotondamento / ubicazione.

### 9.2 Prezzi e promo

Scrittura su `articololistino` per:

- prezzo base;
- prezzo variante;
- fasce quantita`;
- date validita`;
- eventuale promo/offerta.

### 9.3 Barcode multipli

Scrittura su `articolomulticodice`.

### 9.4 Immagini

Scrittura su `articoloimmagine`.

### 9.5 Specifiche tecniche / ecommerce

Scrittura su `articolospecificatecnica` + lookup:

- `caratteristica`
- `caratteristicavalore`

---

## 10. Cose che non vanno fatte

Per la gestione articolo in Banco non va fatto:

- scrivere solo `articolo` ignorando `articololistino`;
- trattare `articolomultilistino` come tabella master dei prezzi;
- trattare `articolosituazione` o `valorizzazionearticolo` come input anagrafico;
- inventare lookup locali per `Produttore`, `IVA`, `Variante`, `Conti`, `Categoria`;
- dedurre le combo enum-like senza ulteriore verifica;
- esporre una creazione articolo "semplificata" che poi `FM` completa o corregge in modo implicito.

---

## 11. Conclusione operativa

La risposta alla domanda iniziale e`:

- **sì**, ci sono gia` abbastanza riferimenti per partire seriamente;
- **no**, non basta il dump da solo;
- **sì**, per evitare disallineamenti bisogna collegare anche le tabelle collegate reali e non ignorarle.

Ordine corretto del lavoro futuro:

1. chiudere la mappa completa dei campi `articolo` da esporre;
2. separare lookup certi, enum-like e tabelle figlie editabili;
3. definire il contratto di salvataggio reale;
4. solo dopo costruire i servizi completi di create/update;
5. la UI viene per ultima.

---

## 12. Punto aperto principale

Il punto ancora da verificare meglio prima di una gestione completa e` il dominio reale di alcune combo/codifiche non coperte da FK esplicite:

- `Tipoarticolo`
- `Tracciabilita`
- `Tipocostoarticolo`
- `Condizione`
- eventuali opzioni loyalty/ecommerce non ancora mappate al 100%

Queste non bloccano l'analisi iniziale, ma bloccano una scrittura "completa e definitiva" se non vengono prima chiarite.
