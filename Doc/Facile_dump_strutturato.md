# Analisi strutturata del dump `Facile.il`

## Stato del file
- **Percorso analizzato:** `Facile.il`
- **Numero righe:** circa **197.070**
- **Formato:** dump IL / decompilazione .NET
- **Namespace principale individuato:** `Facile`
- **Classi principali rilevate in questo dump:** **13**
- **Dump interrotto:** sì

## Valutazione rapida
Questo file non è un sorgente pulito, ma una **decompilazione IL** di un applicativo .NET piuttosto grande.
Dall'analisi emerge una base applicativa con forte uso di:

- **DevExpress XPO** per la persistenza ORM
- **DevExpress WinForms / XtraEditors / XtraUserControl** per la UI
- **entità business** legate ad articoli, archivio documentale, anagrafiche, allegati e configurazione applicativa

Il dump risulta inoltre **parzialmente offuscato**. I segnali principali sono:

- nomi di membri con caratteri non leggibili
- flussi IL con `switch`, `xor`, salti e rami inutilmente complessi
- membri statici/metodi con nomi non semantici

## Mappa delle classi rilevate

| Classe | Base | Righe approx | Campi rilevati | Ruolo probabile |
|---|---|---:|---:|---|
| `Facile.Agente` | `[DevExpress.Xpo.v18.1]DevExpress.Xpo.XPObject` | 779 | 9 | entità agente / provvigioni / collegamento soggetto-operatore |
| `Facile.Agentescaglione` | `[DevExpress.Xpo.v18.1]DevExpress.Xpo.XPObject` | 622 | 6 | scaglioni percentuali agente per categoria / validità temporale |
| `Facile.Allegati` | `[mscorlib]System.Object` | 60 | 5 | DTO allegato / attachment compresso / descrizione |
| `Facile.AltriDatiGestionali` | `[mscorlib]System.Object` | 59 | 4 | DTO dati gestionali aggiuntivi |
| `Facile.Anagrafica` | `[mscorlib]System.Object` | 60 | 5 | DTO anagrafico base |
| `Facile.Applicazione` | `[mscorlib]System.Object` | 1561 | 39 | configurazione applicativa, path, versione, ambiente |
| `Facile.Archidoc` | `[DevExpress.Xpo.v18.1]DevExpress.Xpo.XPObject` | 4088 | 37 | entità archivio documentale collegata a soggetti/documenti/articoli |
| `Facile.Archidocemailinfo` | `[mscorlib]System.Object` | 67 | 12 | DTO info email / esito SdI / riferimenti file |
| `Facile.Archidocemaillog` | `[DevExpress.Xpo.v18.1]DevExpress.Xpo.XPObject` | 691 | 11 | log email collegate ad Archidoc |
| `Facile.ArchiDocViewControl` | `[DevExpress.Utils.v18.1]DevExpress.XtraEditors.XtraUserControl` | 19431 | 94 | controllo UI DevExpress per consultazione archivio documentale |
| `Facile.Archivio` | `[DevExpress.Xpo.v18.1]DevExpress.Xpo.XPObject` | 4387 | 51 | configurazione archivio / backup / restore / flags operativi |
| `Facile.Arrotondamentoinriepilogo` | `[mscorlib]System.Object` | 59 | 4 | DTO arrotondamenti IVA/riepilogo |
| `Facile.Articolo` | `[DevExpress.Xpo.v18.1]DevExpress.Xpo.XPObject` | 1559 | 112 | entità articolo di magazzino/listino/gestione commerciale |

## Dettaglio sintetico per classe

### Facile.Agente
- **Intervallo righe:** 3 - 781
- **Classe base:** `[DevExpress.Xpo.v18.1]DevExpress.Xpo.XPObject`
- **Implementa:** `[SogesSoftware.Utils]SogesSoftware.Utils.IXpo,              [DevExpress.Data.v18.1]DevExpress.XtraEditors.DXErrorProvider.IDXDataErrorInfo`
- **Campi leggibili rilevati:** 9
- **Campi notevoli:** `DisplayMember`, `FieldnameColumns`, `CaptionColumns`, `Agente`, `Operatore`, `fAlnettosconti`, `fCalcolosu`, `fCodicesegretofacileagente`, `fMaturazioneprovvigione`
- **Lettura funzionale:** gestisce anagrafica agente, operatore associato, segreto/codice e parametri di maturazione provvigioni.

### Facile.Agentescaglione
- **Intervallo righe:** 2303 - 2924
- **Classe base:** `[DevExpress.Xpo.v18.1]DevExpress.Xpo.XPObject`
- **Implementa:** `[SogesSoftware.Utils]SogesSoftware.Utils.IXpo`
- **Campi leggibili rilevati:** 6
- **Campi notevoli:** `Agente`, `Categoria`, `fPercentualeagente`, `fPercentualecapoarea`, `fValidoal`, `fValdidodal`
- **Lettura funzionale:** sembra definire percentuali/scaglioni provvigionali per categoria con date di validità.

### Facile.Allegati
- **Intervallo righe:** 3602 - 3661
- **Classe base:** `[mscorlib]System.Object`
- **Campi leggibili rilevati:** 5
- **Campi notevoli:** `NomeAttachment`, `AlgoritmoCompressione`, `FormatoAttachment`, `DescrizioneAttachment`, `Attachment`
- **Lettura funzionale:** contenitore semplice di allegati, con nome, formato, descrizione, algoritmo di compressione e contenuto binario.

### Facile.AltriDatiGestionali
- **Intervallo righe:** 3709 - 3767
- **Classe base:** `[mscorlib]System.Object`
- **Campi leggibili rilevati:** 4
- **Campi notevoli:** `TipoDato`, `RiferimentoTesto`, `RiferimentoNumero`, `RiferimentoData`
- **Lettura funzionale:** struttura accessoria con riferimento testuale, numerico e data, probabilmente usata in serializzazione XML/fatture.

### Facile.Anagrafica
- **Intervallo righe:** 3831 - 3890
- **Classe base:** `[mscorlib]System.Object`
- **Campi leggibili rilevati:** 5
- **Campi notevoli:** `Denominazione`, `Nome`, `Cognome`, `Titolo`, `CodEORI`
- **Lettura funzionale:** struttura anagrafica minimale con denominazione, nome, cognome, titolo e codice EORI.

### Facile.Applicazione
- **Intervallo righe:** 3938 - 5498
- **Classe base:** `[mscorlib]System.Object`
- **Campi leggibili rilevati:** 39
- **Campi notevoli:** `isDemo`, `stopEcommerce`, `isDemoBackup`, `MainThreadId`, `productNameBase`, `ProductVersion`, `ProductSign`, `ComputerName`, `SuffissoLayout`, `PathArchidoc`, `PathImmagini`, `PathLayout`, `PathReport`, `PathStartup`, `PathValues_Import`, `PathValues_Assets`, `PathValues_Icone`, `PathConfig`
- **Lettura funzionale:** classe globale/configurativa con informazioni su demo mode, stop ecommerce, versione prodotto, nome computer e vari path applicativi.

### Facile.Archidoc
- **Intervallo righe:** 6203 - 10290
- **Classe base:** `[DevExpress.Xpo.v18.1]DevExpress.Xpo.XPObject`
- **Implementa:** `[SogesSoftware.Utils]SogesSoftware.Utils.IXpo,              [DevExpress.Data.v18.1]DevExpress.XtraEditors.DXErrorProvider.IDXDataErrorInfo`
- **Campi leggibili rilevati:** 37
- **Campi notevoli:** `deleteFromEcommerceSync`, `tabellaFields`, `forceToUpdate`, `Articolo`, `Automezzo`, `Categoria`, `Contratto`, `Documento`, `Liquidazioneiva`, `Operatore`, `Primanota`, `Soggetto`, `Soggettoaggregated`, `Spedizione`, `TipoArchidoc`, `fAlfaArchidoc`, `fAnno`, `fAutoGenerato`
- **Lettura funzionale:** è uno dei nodi centrali del dump. Collega documenti, soggetti, articoli e altri oggetti del dominio; contiene anche flag di sincronizzazione/aggiornamento.

### Facile.Archidocemailinfo
- **Intervallo righe:** 32618 - 32684
- **Classe base:** `[mscorlib]System.Object`
- **Campi leggibili rilevati:** 12
- **Campi notevoli:** `EmailBody`, `Data`, `Descrizione`, `Emailid`, `EnumEsito`, `EsitoCodice`, `IdentificativoSdI`, `NomeFileNotifica`, `NomeFileFattura`, `PathFileNofifica`, `PathFilefattura`, `RiferimentoFileInvio`
- **Lettura funzionale:** DTO collegato a notifiche/email documentali, con identificativo SdI, esiti e path file.

### Facile.Archidocemaillog
- **Intervallo righe:** 32748 - 33438
- **Classe base:** `[DevExpress.Xpo.v18.1]DevExpress.Xpo.XPObject`
- **Campi leggibili rilevati:** 11
- **Campi notevoli:** `deleteFromEcommerceSync`, `DisplayMember`, `FieldnameColumns`, `CaptionColumns`, `WidthColumns`, `Archidoc`, `fData`, `fEmailid`, `_fFilenotifica`, `fIdentificativosdi`, `fOggetto`
- **Lettura funzionale:** persistenza del log email associato a `Archidoc`, con oggetto, email id, identificativo SdI e file notifica.

### Facile.ArchiDocViewControl
- **Intervallo righe:** 34120 - 53550
- **Classe base:** `[DevExpress.Utils.v18.1]DevExpress.XtraEditors.XtraUserControl`
- **Campi leggibili rilevati:** 94
- **Campi notevoli:** `components`, `_BarManagerArchiDocView`, `_barDockControlTop`, `_barDockControlBottom`, `_barDockControlLeft`, `_barDockControlRight`, `_XpCollectionArchidoc`, `_GridControlArchiDoc`, `_LayoutViewArchiDoc`, `_colNumeroOggetto`, `_colAlfaOggetto`, `_colDataArchidoc`, `_colOggetto`, `_colNote`, `_StandaloneBarDockControlArchiDoc`, `_BarDockControl3`, `_BarDockControl4`, `_BarDockControl2`
- **Lettura funzionale:** enorme controllo UI DevExpress per la visualizzazione/gestione archivio documentale. Presenza di grid, layout view, bar manager e colonne dedicate.

### Facile.Archivio
- **Intervallo righe:** 68745 - 73131
- **Classe base:** `[DevExpress.Xpo.v18.1]DevExpress.Xpo.XPObject`
- **Implementa:** `[DevExpress.Data.v18.1]DevExpress.XtraEditors.DXErrorProvider.IDXDataErrorInfo`
- **Campi leggibili rilevati:** 51
- **Campi notevoli:** `DisplayMember`, `FieldnameColumns`, `CaptionColumns`, `EnumsTipoArchivio`, `lockDict`, `fArchivio`, `fArchivioremoto`, `fAttivo`, `fBackupgiorni`, `fCartellabackup`, `fCartellarestore`, `fDisattivamagazzino`, `fDisattivacontabilita`, `fDisattivatrasporti`, `fDisattivacontratti`, `fDisattivaecommerce`, `fDisattivaproduzione`, `FCartellacondivisaserver`
- **Lettura funzionale:** classe di configurazione archivio con cartelle backup/restore, attivazione archivio remoto e opzioni varie.

### Facile.Arrotondamentoinriepilogo
- **Intervallo righe:** 78322 - 78380
- **Classe base:** `[mscorlib]System.Object`
- **Campi leggibili rilevati:** 4
- **Campi notevoli:** `Percentualeiva`, `Arrotondamento`, `NaturaIva`, `Riferimentonormativo`
- **Lettura funzionale:** struttura tecnica di riepilogo IVA/arrotondamento con natura IVA e riferimento normativo.

### Facile.Articolo
- **Intervallo righe:** 78444 - 80002
- **Classe base:** `[DevExpress.Xpo.v18.1]DevExpress.Xpo.XPObject`
- **Implementa:** `[SogesSoftware.Utils]SogesSoftware.Utils.IXpo,              [DevExpress.Data.v18.1]DevExpress.XtraEditors.DXErrorProvider.IDXDataErrorInfo,              Facile.ITabella`
- **Campi leggibili rilevati:** 112
- **Campi notevoli:** `$VB$Local_codicearticolo`, `DisplayMember`, `FieldnameColumns`, `CaptionColumns`, `unitOfWorkNonPersistent`, `DisplayMember2`, `FieldnameColumns2`, `CaptionColumns2`, `WidthColumns2`, `OidMagazzinoPredefinito`, `ArticoloconfiguratoreSelectedList`, `ListCodiciABarreImportatiToUseInSync`, `costoAcquistoProposto`, `stopUpdateEcommerce`, `forceResyncEcommerce`, `magazzinoPredefinito`, `produttoreOnLoaded`, `categoriaOnLoaded`
- **Lettura funzionale:** altra entità molto importante. Dal numero di campi e dai nomi emerge una classe molto estesa, probabilmente cuore della gestione articoli/listini/magazzino.

## Classi più importanti da approfondire per prime

### 1. `Facile.Articolo`
Perché è importante:
- ha un numero molto alto di campi leggibili
- implementa anche `Facile.ITabella`
- ha forte probabilità di contenere logiche commerciali, magazzino, listino, codifiche e riferimenti operativi

### 2. `Facile.Archidoc`
Perché è importante:
- è una delle classi più grandi del dump
- collega molte altre entità del dominio
- sembra stare al centro della parte documentale/archivistica

### 3. `Facile.ArchiDocViewControl`
Perché è importante:
- è il blocco UI più grande trovato
- può aiutare a ricostruire schermate, griglie, pulsanti, colonne e workflow utente

### 4. `Facile.Applicazione`
Perché è importante:
- può rivelare path, configurazioni globali, modalità demo, cartelle e dipendenze ambientali

## Segnali di offuscamento / rumore

Esempi trovati nel dump:

- `.field assembly static class Facile.Agente '\u0086\u0086\u0086\r\n\u0086\u0086\u0087\u009b\u008d\u009e'`
- `// 			Facile.Agente.DisplayMember = (string)Facile.Agente.\u0086\u0086\u0086\u000d\u000a\u0086\u0086\u0087\u009b\u008e\u008a(--236615665 ^ 0xE1E103B);`
- `IL_0040: call object Facile.Agente::'\u0086\u0086\u0086\r\n\u0086\u0086\u0087\u009b\u008e\u008a'(int32)`
- `// 			Facile.Agente.\u0086\u0086\u0086\u000d\u000a\u0086\u0086\u0087\u009b\u008e\u0089();`
- `IL_0064: call void Facile.Agente::'\u0086\u0086\u0086\r\n\u0086\u0086\u0087\u009b\u008e\u0089'()`

Impatto pratico:

- alcune parti **non sono ricostruibili perfettamente** solo da questo dump
- i nomi reali di certi metodi/proprietà potrebbero essere persi
- però la **struttura del dominio** e molte responsabilità delle classi restano leggibili

## Conclusione operativa

Il dump `Facile.il` è abbastanza ricco da consentire una **ricostruzione strutturale seria** del progetto, soprattutto lato:

- modello dati XPO
- configurazione applicativa
- archivio documentale
- gestione articoli
- componenti UI DevExpress

La strada più utile, da qui in avanti, è lavorare a blocchi:

1. approfondire `Articolo`
2. approfondire `Archidoc`
3. estrarre la struttura UI da `ArchiDocViewControl`
4. mappare eventuali classi correlate in dump successivi o più completi

## Nota
Questo file `.md` è una **prima strutturazione tecnica** del dump. Non è ancora una ricostruzione completa del codice sorgente né una traduzione integrale in C#.