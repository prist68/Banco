# NOTA TECNICA FM - STAMPA E ACCOPPIAMENTO REPORT

## Scopo

Questa nota raccoglie cio` che si riesce a dedurre dal dump IL di `Facile Manager 2026.3.1330.17` sulla meccanica di stampa/report.

Obiettivo pratico:
- capire **come FM aggancia il tipo di stampa ai dati**;
- capire **quali parametri decide il contratto di stampa**;
- preparare una base utile per costruire i report `FastReport` in `Banco.Stampa` senza inventare campi.

Questa nota **non** e` documentazione ufficiale Soges.

---

## Fonti usate

- dump decompilato:
  - `C:\Users\dileo\Desktop\Facile-Manager--2026.3.1330.17--.NETFramework--v4.0-.il`
- verifica runtime gia` emersa nel progetto:
  - FM usa `DevExpress.XtraReports.v18.1`
- blueprint POS gia` fissato nel progetto:
  - `C:\Facile Manager\DILTECH\Report\Pos.repx`

---

## Regola chiave emersa

In FM il report **non** sembra essere deciso da un solo file `.repx` e basta.

La meccanica reale e` piu` strutturata:

1. il form o il modulo costruisce un **dataset sorgente** (`XPCollection`);
2. passa anche il **contesto tabellare** (`Tabella.EnumTabella`);
3. puo` passare un **report personalizzato** (`Reportpersonalizzato`);
4. puo` passare filtri/periodo/soggetto/parametri extra;
5. il motore di stampa decide quale layout applicare e con quali dati.

Quindi il contratto di stampa in FM e` una combinazione di:
- **tipo oggetto/tabella**
- **recordset sorgente**
- **report personalizzato scelto**
- **parametri di contesto**

Non e` una semplice lista globale di campi identica per tutti i report.

---

## Motore di report

### Dato certo

Dal dump risultano riferimenti a:
- `DevExpress.XtraReports.v18.1`
- `DevExpress.XtraReports.v18.1.Extensions`

Quindi il motore report classico di FM e` basato su **DevExpress XtraReports**.

### Conseguenza pratica

Per migrare verso `FastReport` non serve copiare il motore FM.

Serve invece ricostruire:
- il **dataset di testata**
- il **dataset di righe**
- i **parametri**
- la **tabella dominio**
- l’eventuale **report personalizzato** agganciato al contesto

---

## Due famiglie principali di stampa emerse

### 1. `Reportpersonalizzato.StampaScheda(...)`

Firma rilevata:

```text
Facile.Reportpersonalizzato::StampaScheda(
    XPCollection,
    int32,
    string[],
    string[])
```

### Lettura tecnica

Questa famiglia sembra usata per la stampa di **schede singole** o contesti semplici, dove:
- c’e` una `XPCollection` del dominio;
- si passa un `int32` che molto probabilmente identifica il report/layout o il contesto specifico;
- ci sono due array stringa che sembrano servire a colonne/campi visibili o metadati di stampa.

### Esempi trovati

- `FormTipoArchidoc`
- stampa scheda banca

### Meccanica di accoppiamento

Per questa famiglia il report sembra dipendere da:
- **collezione oggetti**
- **id/contesto report**
- **liste di campi/colonne**

Questa e` una meccanica piu` vicina a:
- schede
- anagrafiche
- output relativamente semplici

---

### 2. `Reportpersonalizzato.StampaReportPredefinito(...)`

Firma rilevata:

```text
Facile.Reportpersonalizzato::StampaReportPredefinito(
    XPCollection,
    string,
    Enums.EnumTiporeport,
    Reportpersonalizzato,
    Tabella.EnumTabella,
    Soggetto,
    DateTime,
    DateTime,
    string,
    string[],
    object[],
    string,
    string,
    string,
    bool,
    Enums.EnumMostraMessaggioEsitoEmail,
    object,
    bool)
```

### Lettura tecnica

Questa e` la pipeline piu` importante.

FM qui non passa solo il layout, ma un pacchetto completo di contesto:
- `XPCollection`: recordset reale da stampare
- `EnumTiporeport`: modalita` output
  - es. `Anteprima`
  - es. `Pdf`
- `Reportpersonalizzato`: report scelto o `null`
- `Tabella.EnumTabella`: dominio principale del report
  - es. `Archidoc`
  - es. `Bilancio`
  - es. `Generale`
- `Soggetto`: contesto cliente/fornitore quando serve
- `DateTime da / a`: range periodo
- `string[]` + `object[]`: parametri extra dinamici
- path/file output e flag vari
- modalita` messaggi esito

### Conseguenza pratica

Il layout non puo` essere progettato bene se non si conosce **anche**:
- la tabella dominio;
- il tipo recordset;
- il soggetto;
- il periodo;
- gli eventuali parametri extra.

Questa e` la ragione per cui:
- i campi di una `Fattura` non coincidono con quelli di una `Lista`;
- i campi di una `Lista` non coincidono con quelli di una `Etichetta`;
- i campi disponibili non possono essere dedotti dal solo nome del file report.

---

## Cosa decide davvero i campi disponibili

## 1. `Tabella.EnumTabella`

Dal dump emerge che il motore riceve esplicitamente la tabella dominio.

Esempi trovati:
- `Tabella.EnumTabella.Archidoc`
- `Tabella.EnumTabella.Bilancio`
- `Tabella.EnumTabella.Generale`

### Impatto

Questo e` un discriminante forte:
- un report `Archidoc` ragiona su documenti;
- un report `Bilancio` ragiona su dati contabili/sintetici;
- un report `Generale` e` piu` trasversale o di servizio.

Quindi il **set campi base** cambia gia` per tabella.

---

## 2. `XPCollection`

La sorgente vera dei dati e` una `XPCollection`.

### Impatto

Questo significa che:
- il report puo` ricevere una collezione di `Archidoc`;
- oppure una collezione di righe/oggetti di altro dominio;
- oppure una collezione sintetica costruita dal form.

Quindi il **corpo** del report dipende dal tipo effettivo di record contenuti nella collection.

---

## 3. `Reportpersonalizzato`

FM usa una entita` `Reportpersonalizzato`.

Dal dump si vedono chiaramente:
- `SearchLookUpReportPersonalizzato`
- `cmbElencoReportPersonalizzato`
- `Reportpersonalizzato.DisplayMember`
- `Reportpersonalizzato.FieldnameColumns`
- `Reportpersonalizzato.CaptionColumns`

### Lettura tecnica

FM non ragiona solo con layout fissi su disco.

Esiste un livello di selezione report che passa da:
- anagrafica `Reportpersonalizzato`
- lookup UI
- filtro per tipo report/contesto

### Impatto

Per i futuri report Banco conviene prevedere anche noi:
- catalogo layout
- dominio/tabella associata
- famiglia report
- parametri previsti

Non basta avere una cartella con file `.frx`.

---

## 4. `EnumTiporeport`

Dal dump risultano almeno:
- `Anteprima`
- `Pdf`

### Impatto

La stessa pipeline puo` essere invocata in modalita` diverse:
- preview
- export pdf
- probabilmente stampa diretta o altre varianti in altri rami

Quindi alcune variabili di output non sono campi di dominio, ma **campi/modalita` di esecuzione**.

---

## Mappa pratica per famiglie di report

## Fattura / documenti (`Archidoc`)

### Dati certi

In `FormFattureAlCommercialista` il dump mostra:
- `SearchLookUpReportPersonalizzato`
- `reportPersonalizzato`
- chiamata a:
  - `Reportpersonalizzato.StampaReportPredefinito(...)`
- con:
  - `XPCollection`
  - `EnumTiporeport.Pdf`
  - `Tabella.EnumTabella.Archidoc`
  - range data iniziale/finale

### Deduzione forte

Per la famiglia fatture/documenti il report usa almeno:
- **testata**
  - dati documento `Archidoc`
  - dati cliente/fornitore collegati
  - parametri periodo
- **corpo**
  - o recordset di documenti
  - o dettaglio costruito a partire dal dominio `Archidoc`
- **piede**
  - aggregati / esito export / percorso output / note extra

### Regola pratica per Banco

Se costruiamo un report `Fattura` o `Cortesia documento`, dobbiamo modellare almeno:
- documento testata
- soggetto
- righe documento
- iva/totali
- periodo o riferimenti output se il report e` da elenco/export

---

## Schede singole

### Dati certi

Esistono chiamate a:
- `Reportpersonalizzato.StampaScheda(...)`

Esempi trovati:
- tipo documento
- banca

### Deduzione forte

Questa famiglia e` adatta a:
- anagrafiche
- schede sintetiche
- stampe su un singolo oggetto o collezione semplice

### Meccanica

Il campo disponibile qui dipende da:
- oggetto della scheda
- eventuale id report
- liste di campi/colonne passate al metodo

Quindi la scheda **non** usa la stessa struttura di un report documento `Archidoc`.

---

## Liste / elenchi / bilanci

### Dati certi

Il dump mostra chiamate a `StampaReportPredefinito(...)` anche da `FormBilancio`, con:
- `Tabella.EnumTabella.Generale`
- `Tabella.EnumTabella.Bilancio`
- `EnumTiporeport.Anteprima`
- `EnumTiporeport.Pdf`

### Deduzione forte

Le liste e i bilanci usano:
- collection di record filtrati
- tabella dominio piu` generica o contabile
- periodo
- parametri di export/output

### Impatto

Per una `Lista` non possiamo aspettarci i campi di testata fattura.

Serve una mappa separata per:
- filtro
- collezione sorgente
- colonne elenco
- eventuali riepiloghi finali

---

## Etichette

### Dato debole ma utile

Nel dump risultano almeno risorse e riferimenti nominali tipo:
- `Etichetta_label`

### Stato attuale della deduzione

Non ho ancora estratto una pipeline completa equivalente a `FormFattureAlCommercialista`, quindi per le etichette il quadro e` ancora parziale.

### Deduzione forte

Le etichette quasi certamente **non** condividono lo stesso set campi pieno di `Archidoc`.

Di solito la meccanica attesa e`:
- sorgente articoli / varianti / barcode
- layout fisico etichetta
- campi sintetici:
  - descrizione
  - prezzo
  - barcode
  - lotto/scadenza se previsti

### Regola per il progetto

Le etichette vanno documentate a parte, non aggregate sotto la stessa mappa fatture.

---

## Ultimo scontrino / cortesia / POS

### Dati certi

Nel dump esiste la form:
- `FormUltimoScontrino`

Nel progetto Banco il blueprint reale gia` fissato e`:
- `C:\Facile Manager\DILTECH\Report\Pos.repx`

### Lettura prudente

Per il mondo scontrino bisogna distinguere tre casi:

1. **scontrino fiscale vero**
   - non e` un normale report XtraReport
   - passa dal registratore / driver fiscale

2. **cortesia / ultimo scontrino / copia POS**
   - qui un report puo` esistere davvero
   - il blueprint `Pos.repx` e` il riferimento piu` forte che abbiamo oggi

3. **stampa POS tecnica**
   - puo` avere campi dedicati diversi da fattura e lista

### Regola pratica per Banco

Per questa famiglia non bisogna riusare il modello `Fattura`.

Serve una mappa dedicata con:
- testata vendita
- cliente compatto
- righe vendita
- pagamenti
- totali
- footer POS / note / cortesia

---

## Meccanica di accoppiamento da replicare in `FastReport`

Per ogni nuovo report Banco conviene descrivere sempre questi punti:

1. **Famiglia report**
- fattura
- cortesia
- pos
- etichetta
- lista
- scheda

2. **Tabella dominio**
- es. `Archidoc`
- es. `Generale`
- es. `Bilancio`
- oppure equivalente Banco se il report e` locale

3. **Sorgente dati**
- collection principale
- oggetto testata
- collezione righe

4. **Campi testata**
- dati documento / soggetto / periodo / riferimenti

5. **Campi corpo**
- righe documento / articoli / record elenco / scheda

6. **Campi piede**
- totali
- aggregati
- note
- parametri stampa

7. **Parametri runtime**
- preview / pdf / stampa
- file output
- stampante
- filtri temporali
- soggetto

Questa e` la struttura minima che ci evita di creare template scollegati dal flusso reale.

---

## Cosa sappiamo con alta confidenza

- FM usa `DevExpress XtraReports` come motore report classico.
- Il motore passa da `Reportpersonalizzato`.
- La scelta campi dipende almeno da:
  - `XPCollection`
  - `Tabella.EnumTabella`
  - `Reportpersonalizzato`
  - `EnumTiporeport`
  - soggetto / date / parametri extra
- `Fattura` e `Lista` non condividono automaticamente lo stesso set campi.
- `StampaScheda` e `StampaReportPredefinito` sono due famiglie diverse.

---

## Cosa resta da estrarre meglio

- pipeline completa di `FormUltimoScontrino`
- pipeline completa di `FormRicercaAvanzataStampa`
- pipeline etichette
- parametri semantici precisi di:
  - `string[]`
  - `object[]`
  - stringhe output/path/note
- mapping preciso dei campi del `Pos.repx` rispetto al runtime FM

---

## Uso consigliato di questa nota

Questa nota va usata come base per compilare, per ogni layout `FastReport`, una scheda tecnica con:
- nome report
- famiglia report
- tabella dominio
- dataset testata
- dataset righe
- totali/piede
- parametri runtime
- blueprint legacy di riferimento

La nota non va usata per dedurre a intuito i campi mancanti.

Quando un report e` critico:
- va letto il blueprint legacy reale (`.repx`);
- vanno controllate le tabelle legacy collegate;
- va confermato il dataset reale che FM usa per quella famiglia.
