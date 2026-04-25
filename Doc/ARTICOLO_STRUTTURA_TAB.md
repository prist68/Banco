# ARTICOLO_STRUTTURA_TAB.md

## Scopo

Questo file fissa una prima proposta strutturale delle tab della futura gestione `Articolo` in Banco.

Focus:
- contenuti logici per tab;
- priorita` di dominio;
- campi che non vanno rimandati;
- distinzione tra minimo corretto e contenuti avanzati.

Questo file non descrive il layout UI finale.

---

## Regola base

La scheda `Articolo` in Banco non va pensata come una pagina unica piatta.

La suddivisione corretta e`:
- per contenuto reale;
- per responsabilita` funzionale;
- evitando di mescolare nella prima tab prezzi avanzati, immagini, ecommerce e specifiche tecniche.

Pero`:

- la prima tab deve gia` contenere tutto cio` che rende l'articolo **anagraficamente, operativamente e fiscalmente corretto**;
- per il settore Banco attuale, **`Tassa / accise` non e` contenuto secondario** e non va spostata in una tab avanzata.

---

## Tab 1 proposta: `Anagrafica e fiscale`

Questa deve essere la tab principale.

Obiettivo:
- creare/modificare un articolo in modo subito coerente con il legacy;
- rendere l'articolo identificabile, vendibile e fiscalmente corretto;
- coprire il minimo reale senza obbligare l'operatore ad aprire cinque tab per una modifica base.

### Campi da includere

#### Identita` articolo

- `Codice articolo`
- `Descrizione articolo`
- `Produttore`
- `Categoria`

#### Fiscalita`

- `IVA`
- `Tassa / accise`
- `Usa tassa`
- `Moltiplicatore tassa`

Nota:
- per il settore attuale `Tassa / accise` deve stare nella prima tab, vicina a `IVA`;
- non e` un dettaglio avanzato.

#### Unita` e quantita`

- `Unita` di misura principale
- `2° unita` di misura
- `Moltiplicatore unita` secondaria
- `Quantita minima vendita`
- `Quantita multipla vendita`
- `Quantita per collo`

#### Classificazione operativa

- `Tipo articolo`
- `Tracciabilita`
- `Categoria ricarico`
- `Conto costo`
- `Conto ricavo`
- `Variante 1`
- `Variante 2`
- `Garanzia mesi`
- `Ubicazione`

#### Flag operativi principali

- `Usa per vendita al banco touch`
- `Escludi inventario`
- `Escludi totale documento`
- `Escludi scontrino`
- `Escludi sconto soggetto`
- `Esporta`
- `Obsoleto`

---

## Perche' questi campi devono stare nella prima tab

Perche' rappresentano il minimo corretto per:

- riconoscere l'articolo;
- classificarlo;
- venderlo correttamente;
- fiscalizzarlo correttamente;
- non disallinearlo rispetto a `FM`.

Se si spostano fuori dalla prima tab campi come:
- `IVA`
- `Tassa / accise`
- `Unita di misura`
- `Tipo articolo`
- `Tracciabilita`
- `Escludi scontrino`

si rischia di avere:
- articoli creati in modo incompleto;
- salvataggi apparentemente validi ma funzionalmente deboli;
- forte dipendenza da correzioni successive dentro `FM`.

---

## Nota esplicita su unita` di misura e tab `Magazzino`

I campi:

- `Unita` di misura principale
- `2° unita` di misura
- `Moltiplicatore unita` secondaria

**devono restare nella prima tab**.

Motivo:

- non sono dati di stock o situazione magazzino;
- sono parte dell'anagrafica operativa di base dell'articolo;
- incidono direttamente su vendita, quantita`, riga documento e comportamento articolo nel flusso Banco/FM.

Quindi la distinzione corretta e`:

- `unita`, `2° unita`, `moltiplicatore`, `quantita minima`, `quantita multipla` -> **`Anagrafica e fiscale`**
- disponibilita`, esistenza, ultimo costo, costo medio, scorte, riordino, valorizzazioni -> **`Magazzino`**

Questo consente anche di riusare meglio il lavoro gia` presente nel modulo `Articoli magazzino`:

- la parte gia` pronta su stock/costi/riordino non va duplicata;
- va inglobata come tab specializzata della futura gestione `Articolo`;
- la prima tab continua invece a coprire l'anagrafica base che oggi non appartiene al dominio puro di magazzino.

---

## Cosa NON mettere nella prima tab

Nella prima tab non conviene mettere:

- fasce prezzo avanzate;
- promo quantita`;
- listini multipli complessi;
- immagini;
- specifiche tecniche;
- tag ecommerce;
- barcode alternativi multipli;
- correlati / componenti / configuratore.

Queste aree sono reali e importanti, ma non devono sporcare il nucleo iniziale della scheda.

---

## Proposta di tab successive

### Tab 2: `Prezzi e listini`

Contenuti:
- prezzo base;
- listino di riferimento;
- `articololistino`;
- fasce quantita`;
- eventuali promo e date validita`.

### Tab 3: `Codici e barcode`

Contenuti:
- codice principale;
- barcode alternativi;
- PLU;
- `articolomulticodice`.

### Tab 4: `E-commerce e specifiche`

Contenuti:
- note estese;
- avvertenze;
- tag ecommerce;
- specifiche tecniche;
- `articolospecificatecnica`;
- collegamenti con `caratteristica` e `caratteristicavalore`.

### Tab 5: `Immagini`

Contenuti:
- immagini articolo;
- immagini variante;
- ordinamento;
- predefinita;
- `articoloimmagine`.

### Tab 6: `Avanzate`

Contenuti possibili:
- correlati;
- componenti;
- configuratore;
- dati meno frequenti o piu` specialistici.

---

## Regola viva

La prima tab della futura gestione `Articolo` in Banco deve essere:

- **`Anagrafica e fiscale`**

e deve contenere anche:

- `IVA`
- `Tassa / accise`
- `Usa tassa`
- `Moltiplicatore tassa`

Per il dominio Banco attuale, `Tassa / accise` e` parte del minimo corretto e non va trattata come contenuto opzionale o avanzato.

---

## Nota finale

Questa struttura tab non sostituisce ancora la mappa tecnica delle tabelle legacy.

Va letta insieme a:

- `Doc/ANALISI_ARTICOLO_LEGACY.md`

che resta la base tecnica per capire:
- quali tabelle sono coinvolte;
- quali collegamenti sono certi;
- quali parti non possono essere ignorate al salvataggio.
