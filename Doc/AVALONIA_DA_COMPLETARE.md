# Avalonia - cosa completare

Documento operativo per tenere traccia del porting Avalonia di Banco, con focus iniziale su `Vendita al banco`.

## Obiettivo

Portare la UI Avalonia vicino alla versione Windows/WPF senza riscrivere la logica gia esistente.

La regola di lavoro e':

- UI e comportamento grafico in Avalonia.
- Logica gestionale, documenti, fiscalita, POS, stampe e legacy nel layer condiviso gia presente.
- Su Mac deve funzionare senza errori anche senza DB legacy.
- Su Windows deve usare DB legacy, POS, registratore e stampe reali quando disponibili.

## Stato generale

### Completato

- Progetto `Banco.UI.Avalonia.Banco` creato e collegato.
- Vendita al banco aperta dentro la scrivania Avalonia Lab, non come finestra separata.
- Cambio tema chiaro/scuro rispettato.
- Layout principale vicino alla versione Windows, con colonna griglia e colonna destra operativa.
- Modello demo sostituito con `DocumentoLocale` e `RigaDocumentoLocale`.
- Workflow documento collegato a `IBancoDocumentWorkflowService`.
- App avviabile su Mac senza DB legacy: in assenza di DB resta operativa in modalita offline/demo.
- Warning `Tmds.DBus.Protocol` risolto.

### Ricerca articoli

- Ricerca articolo superiore collegata.
- Comportamento differenziato tra barcode scanner e scrittura manuale:
  - scanner: tenta inserimento diretto;
  - scrittura manuale: apre modale di ricerca.
- Modale ricerca articolo collegata.
- Inserimento articolo dalla modale corretto, senza forzare sempre `ART-001`.
- Ricerca articolo dentro griglia collegata.
- Se ci sono articoli simili, la griglia puo aprire la modale di scelta.
- Varianti articolo gestite nella modale.
- Quantita minima/multipla/listini gestiti tramite dettaglio prezzo.
- Giacenza negativa gestita con dialog decisionale.
- Possibilita di aggiungere articolo alla lista riordino.

### Griglia vendita

- Riga griglia editabile.
- Colonna azioni con cancellazione riga.
- `Canc` sulla riga svuota solo il codice articolo e lascia il resto.
- `Ins` crea una riga manuale.
- `+` e `-` modificano la quantita della riga selezionata.
- `*` aggiunge la riga alla lista riordino.
- `/` rimuove la riga dalla lista riordino.
- Indicatore riga in lista riordino.
- Font/allineamento codice articolo normalizzato rispetto al resto della griglia.

### Cliente e punti

- Ricerca cliente con combo editabile.
- Ricerca live mentre si scrive.
- Ricerca per nome/cognome non legata rigidamente all'ordine di scrittura.
- Cliente selezionato applicato al documento.
- Visualizzazione punti cliente predisposta.

### Pagamenti

- Colonna destra pagamenti rifatta in stile piu vicino a WPF.
- Pulsanti importo:
  - Contanti;
  - Carta;
  - Sospeso;
  - Buoni.
- Click sul tipo pagamento assegna l'importo residuo.
- Importi sincronizzati nei `PagamentoLocale`.
- Pagamento carta collegato a `IPosPaymentService`.
- Scontrino blocca la pubblicazione se il POS non autorizza o richiede verifica manuale.

### Comandi documento

- Salva collegato al workflow.
- Cortesia collegata al workflow.
- Scontrino collegato al workflow.
- Azzera documento collegato.
- Operatore selezionato collegato al documento.
- Listino selezionato collegato al documento.
- Shortcut collegati:
  - `F4` Salva;
  - `F6` POS80;
  - `F7` Anteprima POS80;
  - `F8` Cortesia;
  - `F9` Scontrino.

### POS80 e stampe

- UI POS80 aggiunta.
- Comandi POS80 e Anteprima POS80 aggiunti.
- Contratto Avalonia `IBancoSalePrintService` creato.
- Fallback non distruttivo su Mac: se la stampa non e' disponibile mostra messaggio, senza crash.
- Cortesia ora segue il flusso corretto: pubblica documento e poi tenta stampa POS80.

## Da completare

### 1. Adapter Windows per POS80

Da completare per rendere reale la stampa POS80 su Windows.

Situazione attuale:

- `Banco.Stampa` e' `net10.0-windows`.
- Usa WindowsForms/FastReport.
- Non puo essere referenziato direttamente dalla build Avalonia cross-platform usata su Mac.

Lavoro da fare:

- Creare un adapter Windows che implementa `IBancoSalePrintService`.
- L'adapter deve chiamare:
  - `IBancoPosPrintService.PrintCortesiaAsync`;
  - `IBancoPosPrintService.PreviewCortesiaAsync`.
- Registrare l'adapter solo nella build/profilo Windows.
- Lasciare il fallback `UnsupportedBancoSalePrintService` su Mac/Linux.
- Verificare stampa reale POS80 e anteprima su Windows.

### 2. Anteprima POS80 completa

Situazione attuale:

- Il comando e' presente.
- Il servizio fallback risponde correttamente se non disponibile.

Lavoro da fare:

- Su Windows aprire davvero il file di anteprima prodotto da FastReport.
- Replicare il comportamento WPF di `Pos80PreviewRequested`.
- Gestire messaggi di errore e file mancante.

### 3. Registratore di cassa / WinEcr

Situazione attuale:

- Lo scontrino passa dal workflow condiviso.
- La UI dedicata Avalonia non e' ancora completa.

Lavoro da fare:

- Inventariare comportamento WPF completo per:
  - emissione scontrino;
  - errori fiscalizzatore;
  - ristampa;
  - stato registratore;
  - messaggi WinEcr;
  - eventuale conferma manuale.
- Portare solo UI/comportamenti, mantenendo logica nel layer condiviso.
- Verificare su Windows con registratore reale o ambiente configurato.

### 4. POS Nexi avanzato

Situazione attuale:

- Pagamento carta chiama `IPosPaymentService`.
- In caso di errore lo scontrino non viene emesso.

Lavoro da fare:

- Allineare popup/avvisi al comportamento WPF.
- Gestire casi:
  - pagamento autorizzato;
  - pagamento negato;
  - timeout;
  - verifica manuale;
  - operazione annullata;
  - differenza tra pagamento richiesto e pagamento confermato.
- Valutare se serve una modale dedicata invece del solo `StatusMessage`.

### 5. Documenti vendita

Situazione attuale:

- Salva/Cortesia/Scontrino sono collegati.
- Non e' ancora completa la maschera documenti in Avalonia.

Lavoro da fare:

- Portare lista documenti da WPF.
- Caricare documento esistente.
- Aprire/consultare documenti pubblicati.
- Gestire filtri, ricerca, stato documento, categoria, data, cliente.
- Collegare eventuali stampe documento.
- Aprire tutto dentro la scrivania Avalonia.

### 6. Popup operativi e conferme

Situazione attuale:

- Alcune modali esistono:
  - ricerca articolo;
  - scelta quantita;
  - giacenza negativa.
- Molti popup WPF sono ancora da portare.

Lavoro da fare:

- Inventariare popup WPF usati in vendita.
- Creare componenti standard Avalonia per:
  - conferma;
  - warning;
  - errore;
  - operazione in corso;
  - risultato operazione.
- Usare i componenti nella vendita al banco.
- Evitare finestre esterne quando la maschera e' dentro la scrivania.

### 7. Griglia standard Banco

Situazione attuale:

- Esiste la base `Banco.UI.Grid.Core`.
- La griglia vendita ha gia alcune funzioni operative.

Lavoro da fare:

- Consolidare una griglia standard riusabile.
- Funzioni desiderate:
  - colonne visibili/nascoste;
  - ordinamento;
  - menu tasto destro;
  - gestione colore righe;
  - riga espandibile/gruppi se tecnicamente utile;
  - persistenza layout;
  - colonna azioni standard;
  - editor coerenti 30px.
- Applicare poi a vendita, documenti, magazzino, anagrafiche.

### 8. Componenti UI standard Banco

Situazione attuale:

- Esiste `lib/Banco.UI.Avalonia.Controls`.
- Alcuni stili sono gia stati introdotti.

Lavoro da fare:

- Consolidare componenti:
  - textbox 30px;
  - combobox 30px;
  - numeric box/importi;
  - modal standard;
  - conferma;
  - dialog box;
  - card/panel operativo;
  - toolbar;
  - status bar;
  - badge stato;
  - pulsanti primari/secondari/pericolo.
- Garantire tema chiaro/scuro.
- Garantire uso coerente in tutti i moduli Banco.

### 9. Storico cliente e fidelity

Situazione attuale:

- Cliente e punti sono agganciati in modo base.

Lavoro da fare:

- Portare comportamento WPF completo per:
  - punti disponibili;
  - punti usati;
  - saldo dopo documento;
  - eventuali premi/promozioni;
  - storico acquisti se presente.
- Verificare con DB legacy su Windows.

### 10. Moduli collegati alla vendita

Da portare progressivamente, sempre dentro la scrivania Avalonia:

- Documenti.
- Lista riordino.
- Gestione articolo.
- Clienti e punti.
- Stampe.
- POS.
- Registratore di cassa.
- Impostazioni operative.

## Strategia consigliata

Ordine consigliato dei prossimi lavori:

1. Adapter Windows POS80.
2. Popup operativi standard.
3. Comportamento completo scontrino/WinEcr.
4. Maschera Documenti.
5. Griglia standard riusabile.
6. Componenti UI standard.
7. Rifinitura grafica finale confrontata con WPF.

## Verifica corrente

Ultima verifica eseguita:

```bash
dotnet build /Users/rino/Desktop/Banco/Banco.UI.Avalonia.Lab/Banco.UI.Avalonia.Lab.csproj
```

Risultato:

- Avvisi: 0
- Errori: 0

