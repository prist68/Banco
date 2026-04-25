# Facile.il — strutturazione completa del dump

## Stato del materiale analizzato

- File sorgente: `Facile.il`
- Dimensione: **11.390.593 caratteri**
- Righe: **197.070**
- Tipo: dump **IL .NET** decompilato
- Namespace principale trovato: `Facile`
- Stato del dump: **incompleto**; il file termina con `Decompilation was cancelled.` durante `Articolo::UpdateEcommerceArticoloSituazione`
- Presenza evidente di **offuscamento/obfuscation**: simboli illeggibili, helper con nomi non stampabili, flussi con `switch/xor/branch` artefatti

## Lettura ad alto livello

Questo dump contiene una porzione consistente di un'applicazione gestionale basata su **.NET + DevExpress/XPO**. La parte disponibile ruota soprattutto intorno a:

- anagrafiche e provvigioni (`Agente`, `Agentescaglione`)
- gestione documentale (`Archidoc`, `Archivio`, `Archidocemaillog`, `ArchiDocViewControl`)
- bootstrap applicativo (`Applicazione`)
- articoli/prodotti (`Articolo`), però la classe è **tagliata** nella parte finale

## Snapshot strutturale

| Classe | Righe dump | Base type | Campi | Metodi | Proprietà rilevate | Ruolo probabile |
|---|---:|---|---:|---:|---:|---|
| `Facile.Agente` | 3-781 | `[DevExpress.Xpo.v18.1]DevExpress.Xpo.XPObject` | 10 | 20 | 13 | entità agente/provvigioni collegata a soggetti, operatori e documenti |
| `Facile.Agentescaglione` | 2303-2924 | `[DevExpress.Xpo.v18.1]DevExpress.Xpo.XPObject` | 7 | 12 | 7 | fasce/scaglioni provvigionali dell'agente |
| `Facile.Allegati` | 3602-3661 | `[mscorlib]System.Object` | 6 | 1 | 0 | DTO/container per allegati |
| `Facile.AltriDatiGestionali` | 3709-3767 | `[mscorlib]System.Object` | 5 | 1 | 0 | classe di supporto o DTO |
| `Facile.Anagrafica` | 3831-3890 | `[mscorlib]System.Object` | 6 | 1 | 0 | DTO/container per dati anagrafici |
| `Facile.Applicazione` | 3938-5498 | `[mscorlib]System.Object` | 40 | 2 | 0 | bootstrap/entry point dell'applicazione Windows |
| `Facile.Archidoc` | 6203-10290 | `[DevExpress.Xpo.v18.1]DevExpress.Xpo.XPObject` | 38 | 67 | 34 | entità documento/archivio documentale |
| `Facile.Archidocemailinfo` | 32618-32684 | `[mscorlib]System.Object` | 13 | 1 | 0 | supporto a metadati/log email collegati ai documenti |
| `Facile.Archidocemaillog` | 32748-33438 | `[DevExpress.Xpo.v18.1]DevExpress.Xpo.XPObject` | 12 | 14 | 6 | supporto a metadati/log email collegati ai documenti |
| `Facile.ArchiDocViewControl` | 34120-53550 | `[DevExpress.Utils.v18.1]DevExpress.XtraEditors.XtraUserControl` | 95 | 153 | 74 | controllo UI DevExpress per gestione e visualizzazione documenti |
| `Facile.Archivio` | 68745-73131 | `[DevExpress.Xpo.v18.1]DevExpress.Xpo.XPObject` | 52 | 98 | 50 | configurazione archivio, backup, remoto e sincronizzazione |
| `Facile.Arrotondamentoinriepilogo` | 78322-78380 | `[mscorlib]System.Object` | 5 | 1 | 0 | DTO/container per arrotondamenti di riepilogo |
| `Facile.Articolo` | 78444-80002 | `[DevExpress.Xpo.v18.1]DevExpress.Xpo.XPObject` | 114 | 37 | 15 | entità articolo/prodotto con categorie, IVA, varianti e metadati ecommerce |

## Dipendenze tecniche osservate

- `[DevExpress.Xpo.v18.1]DevExpress.Xpo.XPObject` — presente in 6 classe/i
- `[mscorlib]System.Object` — presente in 6 classe/i
- `[SogesSoftware.Utils]SogesSoftware.Utils.IXpo` — presente in 4 classe/i
- `[DevExpress.Data.v18.1]DevExpress.XtraEditors.DXErrorProvider.IDXDataErrorInfo` — presente in 4 classe/i
- `[DevExpress.Utils.v18.1]DevExpress.XtraEditors.XtraUserControl` — presente in 1 classe/i
- `Facile.ITabella` — presente in 1 classe/i

## Segnali forti emersi dal dump

- **Persistenza XPO**: diverse entità estendono `DevExpress.Xpo.XPObject` e usano attributi come `Persistent`, `Association`, `Aggregated`, `Size`, `DeferredDeletion`, `OptimisticLocking`.
- **UI desktop DevExpress**: `ArchiDocViewControl` estende `XtraUserControl` e usa `BarManager`, `GridControl`, `LayoutView`, repository item, dialog file e docking bar.
- **Dominio documentale** molto ricco: documenti, email log, stato invio, firma, export/import, file firmati, tag, numero, anno, riferimenti.
- **Area archivio/backup/remoto** molto presente: FTP, cartelle backup/restore, server remoto, credenziali rete, copie archivio/report/layout/immagini.
- **Area articolo**: categorie, varianti, produttore, unità di misura, IVA, disponibilità online, imballaggio, conti di prima nota.

---

## Dettaglio classe per classe

### `Facile.Agente`

- **Range righe**: `3-781`
- **Ruolo probabile**: entità agente/provvigioni collegata a soggetti, operatori e documenti
- **Base type**: `[DevExpress.Xpo.v18.1]DevExpress.Xpo.XPObject`
- **Interfacce**:
  - `[SogesSoftware.Utils]SogesSoftware.Utils.IXpo`
  - `[DevExpress.Data.v18.1]DevExpress.XtraEditors.DXErrorProvider.IDXDataErrorInfo`
- **Attributi custom rilevati**: `DevExpress.Xpo.PersistentAttribute`, `DevExpress.Xpo.DeferredDeletionAttribute`, `DevExpress.Xpo.OptimisticLockingAttribute`, `DevExpress.Xpo.AssociationAttribute`, `DevExpress.Xpo.AssociationAttribute`
- **Campi**: 10
- **Metodi**: 20
- **Proprietà rilevate da getter/setter**: 13

**Proprietà rilevate**

- `Alnettosconti`, `Calcolosu`, `Capoarea`, `Codicesegretofacileagente`, `GetOid`, `GetIdentity`, `Maturazioneprovvigione`, `Ragionesociale`, `Agentecapoarea`, `Clienteagente`, `Agentescaglione`, `Documento`
- `Provvigione`

**Campi dichiarati**

- public:
  - `public class Facile.Soggetto Agente`
  - `public class Facile.Operatore Operatore`
- private:
  - `private bool fAlnettosconti`
  - `private int32 fCalcolosu`
  - `private string fCodicesegretofacileagente`
  - `private int32 fMaturazioneprovvigione`
- assembly:
  - `assembly static string DisplayMember`
  - `assembly static string[] FieldnameColumns`
  - `assembly static string[] CaptionColumns`
  - `assembly static class Facile.Agente '\u0086\u0086\u0086\r\n\u0086\u0086\u0087\u009b\u008d\u009e'`

**Metodi rilevati**

- `.cctor`, `get_Alnettosconti`, `set_Alnettosconti`, `get_Calcolosu`, `set_Calcolosu`, `get_Capoarea`, `set_Capoarea`, `get_Codicesegretofacileagente`, `set_Codicesegretofacileagente`, `get_GetOid`, `get_GetIdentity`, `get_Maturazioneprovvigione`, `set_Maturazioneprovvigione`, `get_Ragionesociale`, `get_Agentecapoarea`, `get_Clienteagente`, `get_Agentescaglione`, `get_Documento`
- `get_Provvigione`, `.ctor`

**Note interpretative**

- entità XPO persistente collegata a `Soggetto` e `Operatore`
- ha raccolte/relazioni verso clienti, documenti, scaglioni e provvigioni
- usa inizializzazione statica con colonne display per UI/lista

---

### `Facile.Agentescaglione`

- **Range righe**: `2303-2924`
- **Ruolo probabile**: fasce/scaglioni provvigionali dell'agente
- **Base type**: `[DevExpress.Xpo.v18.1]DevExpress.Xpo.XPObject`
- **Interfacce**:
  - `[SogesSoftware.Utils]SogesSoftware.Utils.IXpo`
- **Attributi custom rilevati**: `DevExpress.Xpo.PersistentAttribute`, `System.Diagnostics.DebuggerStepThroughAttribute`, `DevExpress.Xpo.DeferredDeletionAttribute`, `DevExpress.Xpo.OptimisticLockingAttribute`, `DevExpress.Xpo.AssociationAttribute`, `DevExpress.Xpo.AssociationAttribute`
- **Campi**: 7
- **Metodi**: 12
- **Proprietà rilevate da getter/setter**: 7

**Proprietà rilevate**

- `GetDisplayMember`, `GetOid`, `GetIdentity`, `Percentualeagente`, `Percentualecapoarea`, `Validoal`, `Validodal`

**Campi dichiarati**

- public:
  - `public class Facile.Agente Agente`
  - `public class Facile.Categoria Categoria`
- private:
  - `private valuetype [mscorlib]System.Decimal fPercentualeagente`
  - `private valuetype [mscorlib]System.Decimal fPercentualecapoarea`
  - `private valuetype [mscorlib]System.DateTime fValidoal`
  - `private valuetype [mscorlib]System.DateTime fValdidodal`
- assembly:
  - `assembly static class Facile.Agentescaglione '\u0086\u0086\u0086\r\n\u0086\u0086\u0088\u0088\u0089\u0091'`

**Metodi rilevati**

- `get_GetDisplayMember`, `get_GetOid`, `get_GetIdentity`, `get_Percentualeagente`, `set_Percentualeagente`, `get_Percentualecapoarea`, `set_Percentualecapoarea`, `get_Validoal`, `set_Validoal`, `get_Validodal`, `set_Validodal`, `.ctor`

**Note interpretative**

- fasce provvigionali con percentuali agente/capoarea e intervallo di validità
- probabile classe figlia/relazione di `Agente`

---

### `Facile.Allegati`

- **Range righe**: `3602-3661`
- **Ruolo probabile**: DTO/container per allegati
- **Base type**: `[mscorlib]System.Object`
- **Interfacce**: nessuna rilevata in intestazione
- **Attributi custom rilevati**: nessuno nei primi metadati
- **Campi**: 6
- **Metodi**: 1
- **Proprietà rilevate da getter/setter**: 0

**Campi dichiarati**

- public:
  - `public string NomeAttachment`
  - `public string AlgoritmoCompressione`
  - `public string FormatoAttachment`
  - `public string DescrizioneAttachment`
  - `public string Attachment`
- assembly:
  - `assembly static class Facile.Allegati '\u0086\u0086\u0086\r\n\u0086\u0086\u0087\u008b\u0097\u008c'`

**Metodi rilevati**

- `.ctor`

**Note interpretative**

- DTO minimale con soli campi; nessuna logica oltre al costruttore

---

### `Facile.AltriDatiGestionali`

- **Range righe**: `3709-3767`
- **Ruolo probabile**: classe di supporto o DTO
- **Base type**: `[mscorlib]System.Object`
- **Interfacce**: nessuna rilevata in intestazione
- **Attributi custom rilevati**: nessuno nei primi metadati
- **Campi**: 5
- **Metodi**: 1
- **Proprietà rilevate da getter/setter**: 0

**Campi dichiarati**

- public:
  - `public string TipoDato`
  - `public string RiferimentoTesto`
  - `public string RiferimentoNumero`
  - `public string RiferimentoData`
- private:
  - `private static class Facile.AltriDatiGestionali '\u0086\u0086\u0086\r\n\u0086\u0086\u0087\u008b\u0097\u0090'`

**Metodi rilevati**

- `.ctor`

**Note interpretative**

- DTO minimale con soli campi; nessuna logica oltre al costruttore

---

### `Facile.Anagrafica`

- **Range righe**: `3831-3890`
- **Ruolo probabile**: DTO/container per dati anagrafici
- **Base type**: `[mscorlib]System.Object`
- **Interfacce**: nessuna rilevata in intestazione
- **Attributi custom rilevati**: nessuno nei primi metadati
- **Campi**: 6
- **Metodi**: 1
- **Proprietà rilevate da getter/setter**: 0

**Campi dichiarati**

- public:
  - `public string Denominazione`
  - `public string Nome`
  - `public string Cognome`
  - `public string Titolo`
  - `public string CodEORI`
- private:
  - `private static class Facile.Anagrafica '\u0086\u0086\u0086\r\n\u0086\u0086\u0087\u008b\u0097\u0095'`

**Metodi rilevati**

- `.ctor`

**Note interpretative**

- DTO minimale con soli campi; nessuna logica oltre al costruttore

---

### `Facile.Applicazione`

- **Range righe**: `3938-5498`
- **Ruolo probabile**: bootstrap/entry point dell'applicazione Windows
- **Base type**: `[mscorlib]System.Object`
- **Interfacce**: nessuna rilevata in intestazione
- **Attributi custom rilevati**: `System.ComponentModel.LicenseProviderAttribute`, `Microsoft.VisualBasic.CompilerServices.StandardModuleAttribute`
- **Campi**: 40
- **Metodi**: 2
- **Proprietà rilevate da getter/setter**: 0

**Campi dichiarati**

- private:
  - `private static bool isDemo`
  - `private static class Facile.Applicazione '\u0086\u0086\u0086\r\n\u0086\u0086\u0086\u008e\u0086\u0098'`
- assembly:
  - `assembly static bool stopEcommerce`
  - `assembly static bool isDemoBackup`
  - `assembly static int32 MainThreadId`
  - `assembly static string productNameBase`
  - `assembly static initonly class [mscorlib]System.Version ProductVersion`
  - `assembly static initonly string ProductSign`
  - `assembly static initonly string ComputerName`
  - `assembly static string SuffissoLayout`
  - `assembly static string PathArchidoc`
  - `assembly static string PathImmagini`
  - `assembly static string PathLayout`
  - `assembly static string PathReport`
  - `assembly static string PathStartup`
  - `assembly static string PathValues_Import`
  - `assembly static string PathValues_Assets`
  - `assembly static string PathValues_Icone`
  - `assembly static string PathConfig`
  - `assembly static string FileConfig`
  - `assembly static string FileLicense`
  - `assembly static class Facile.DatabaseXpo FacileConnessioneCorrente`
  - `assembly static class Facile.Utente FacileUtenteCorrente`
  - `assembly static class Facile.DatabaseXpo ArchivioConnessioneCorrente`
  - `assembly static class [mscorlib]System.Collections.Generic.Dictionary`2<int32, object[]> dictArchivioCorrenteConnessioneThread`
  - `assembly static class Facile.Archivio ArchivioCorrente`
  - `assembly static class Facile.Operatore ArchivioOperatoreCorrente`
  - `assembly static class Facile.Rivenditore Rivenditore`
  - `assembly static string adminLiveUpdate`
  - `assembly static string crypterKey`
  - `assembly static string computerId`
  - `assembly static class Facile.Configurazione configurazione`
  - `assembly static valuetype [SogesSoftware.Utils]SogesSoftware.Utils.Enums/EnumUscita enumUscita`
  - `assembly static bool primoAvvio`
  - `assembly static class Facile.ModuliLicenziati moduliAttivati`
  - `assembly static class Facile.Licenzer licenzer`
  - `assembly static class [DevExpress.Utils.v18.1]DevExpress.Utils.ImageCollection imageCollectionApplication`
  - `assembly static class [DevExpress.Utils.v18.1]DevExpress.XtraEditors.XtraForm parentMenu`
  - `assembly static bool serverOnDemandStarted`
  - `assembly static int32 intRitardoLog`

**Metodi rilevati**

- `.cctor`, `Main`

**Note interpretative**

- entry point dell'applicazione
- presenza di numerosi campi statici legati a startup, mutex, settings, parametri o servizi condivisi
- classe fortemente offuscata: molti helper e nomi illeggibili

---

### `Facile.Archidoc`

- **Range righe**: `6203-10290`
- **Ruolo probabile**: entità documento/archivio documentale
- **Base type**: `[DevExpress.Xpo.v18.1]DevExpress.Xpo.XPObject`
- **Interfacce**:
  - `[SogesSoftware.Utils]SogesSoftware.Utils.IXpo`
  - `[DevExpress.Data.v18.1]DevExpress.XtraEditors.DXErrorProvider.IDXDataErrorInfo`
- **Attributi custom rilevati**: `DevExpress.Xpo.DeferredDeletionAttribute`, `DevExpress.Xpo.IndicesAttribute`, `DevExpress.Xpo.PersistentAttribute`, `DevExpress.Xpo.OptimisticLockingAttribute`, `DevExpress.Xpo.AssociationAttribute`
- **Campi**: 38
- **Metodi**: 67
- **Proprietà rilevate da getter/setter**: 34

**Proprietà rilevate**

- `AlfaArchidoc`, `Alfaoggetto`, `Alfadocumento`, `Anno`, `AutoGenerato`, `Constipoarchidocoid`, `Data`, `DataFirma`, `DataInviato`, `Dataoggetto`, `Datastatofatturaelettronica`, `File`
- `FileFirmato`, `Firmato`, `GetOid`, `GetDisplaymember`, `GetIdentity`, `Idesterno`, `Inviataalcommercialista`, `Inviato`, `Nascondievidenza`, `Note`, `Numerooggetto`, `NumeroArchidoc`
- `Numero`, `Numeroalfaesterno`, `Oggetto`, `Progressivoesterno`, `Ragionesocialereloaded`, `Riferimentooggetto`, `Statofatturaelettronica`, `Tag`, `Versione`, `Archidocemaillogcollection`

**Campi dichiarati**

- public:
  - `public class Facile.Articolo Articolo`
  - `public class Facile.Automezzo Automezzo`
  - `public class Facile.Categoria Categoria`
  - `public class Facile.Contratto Contratto`
  - `public class Facile.Documento Documento`
  - `public class Facile.Liquidazioneiva Liquidazioneiva`
  - `public class Facile.Operatore Operatore`
  - `public class Facile.Primanota Primanota`
  - `public class Facile.Soggetto Soggetto`
  - `public class Facile.Soggetto Soggettoaggregated`
  - `public class Facile.Spedizione Spedizione`
  - `public class Facile.TipoArchidoc TipoArchidoc`
- private:
  - `private bool deleteFromEcommerceSync`
  - `private object[] tabellaFields`
  - `private string fAlfaArchidoc`
  - `private int32 fAnno`
  - `private bool fAutoGenerato`
  - `private int32 fConstipoarchidocoid`
  - `private valuetype [mscorlib]System.DateTime fData`
  - `private valuetype [mscorlib]System.DateTime fDataFirma`
  - `private valuetype [mscorlib]System.DateTime fDataInviato`
  - `private valuetype [mscorlib]System.DateTime fDatastatofatturaelettronica`
  - `private string fFile`
  - `private bool fFirmato`
  - `private string fIdesterno`
  - `private bool fInviataalcommercialista`
  - `private bool fInviato`
  - `private bool fNascondievidenza`
  - `private string fNote`
  - `private int32 fNumeroArchidoc`
  - `private string fNumeroalfaesterno`
  - `private string fOggetto`
  - `private string fProgressivoesterno`
  - `private int32 fStatofatturaelettronica`
  - `private string fTag`
  - `private string fVersione`
- assembly:
  - `assembly bool forceToUpdate`
  - `assembly static class Facile.Archidoc '\u0086\u0086\u0086\r\n\u0086\u0086\u0086\u008e\u008a\u0091'`

**Metodi rilevati**

- `get_AlfaArchidoc`, `set_AlfaArchidoc`, `get_Alfaoggetto`, `get_Alfadocumento`, `get_Anno`, `set_Anno`, `get_AutoGenerato`, `set_AutoGenerato`, `get_Constipoarchidocoid`, `set_Constipoarchidocoid`, `get_Data`, `set_Data`, `get_DataFirma`, `set_DataFirma`, `get_DataInviato`, `set_DataInviato`, `get_Dataoggetto`, `get_Datastatofatturaelettronica`
- `set_Datastatofatturaelettronica`, `get_File`, `set_File`, `get_FileFirmato`, `get_Firmato`, `set_Firmato`, `get_GetOid`, `get_GetDisplaymember`, `get_GetIdentity`, `get_Idesterno`, `set_Idesterno`, `get_Inviataalcommercialista`, `set_Inviataalcommercialista`, `get_Inviato`, `set_Inviato`, `get_Nascondievidenza`, `set_Nascondievidenza`, `get_Note`
- `set_Note`, `get_Numerooggetto`, `get_NumeroArchidoc`, `set_NumeroArchidoc`, `get_Numero`, `get_Numeroalfaesterno`, `set_Numeroalfaesterno`, `get_Oggetto`, `set_Oggetto`, `get_Progressivoesterno`, `set_Progressivoesterno`, `get_Ragionesocialereloaded`, `get_Riferimentooggetto`, `get_Statofatturaelettronica`, `set_Statofatturaelettronica`, `get_Tag`, `set_Tag`, `get_Versione`
- `set_Versione`, `get_Archidocemaillogcollection`, `.ctor`, `.ctor`, `AfterConstruction`, `GetPropertyError`, `GetError`, `SetErrorInfo`, `OnSaving`, `OnDeleting`, `OnSaved`, `OnSavedCall`, `addArchidocEmailLog`

**Note interpretative**

- entità documentale centrale
- gestisce metadati di documento, numero, data, oggetto, file, stato invio e firma
- espone raccolta `Archidocemaillogcollection`, quindi ha relazione uno-a-molti con i log email

---

### `Facile.Archidocemailinfo`

- **Range righe**: `32618-32684`
- **Ruolo probabile**: supporto a metadati/log email collegati ai documenti
- **Base type**: `[mscorlib]System.Object`
- **Interfacce**: nessuna rilevata in intestazione
- **Attributi custom rilevati**: nessuno nei primi metadati
- **Campi**: 13
- **Metodi**: 1
- **Proprietà rilevate da getter/setter**: 0

**Campi dichiarati**

- assembly:
  - `assembly string EmailBody`
  - `assembly valuetype [mscorlib]System.DateTime Data`
  - `assembly string Descrizione`
  - `assembly string Emailid`
  - `assembly valuetype Facile.Enums/EnumFatturaElettronicaEsito EnumEsito`
  - `assembly string EsitoCodice`
  - `assembly string IdentificativoSdI`
  - `assembly string NomeFileNotifica`
  - `assembly string NomeFileFattura`
  - `assembly string PathFileNofifica`
  - `assembly string PathFilefattura`
  - `assembly string RiferimentoFileInvio`
  - `assembly static class Facile.Archidocemailinfo '\u0086\u0086\u0086\r\n\u0086\u0086\u0086\u008e\u0090\u0086'`

**Metodi rilevati**

- `.ctor`

**Note interpretative**

- DTO di supporto per informazioni email/archidoc

---

### `Facile.Archidocemaillog`

- **Range righe**: `32748-33438`
- **Ruolo probabile**: supporto a metadati/log email collegati ai documenti
- **Base type**: `[DevExpress.Xpo.v18.1]DevExpress.Xpo.XPObject`
- **Interfacce**: nessuna rilevata in intestazione
- **Attributi custom rilevati**: `DevExpress.Xpo.DeferredDeletionAttribute`, `DevExpress.Xpo.PersistentAttribute`, `DevExpress.Xpo.OptimisticLockingAttribute`, `DevExpress.Xpo.AssociationAttribute`
- **Campi**: 12
- **Metodi**: 14
- **Proprietà rilevate da getter/setter**: 6

**Proprietà rilevate**

- `Data`, `Emailid`, `fFilenotifica`, `Filenotifica`, `Identificativosdi`, `Oggetto`

**Campi dichiarati**

- public:
  - `public class Facile.Archidoc Archidoc`
- private:
  - `private valuetype [mscorlib]System.DateTime fData`
  - `private string fEmailid`
  - `private string _fFilenotifica`
  - `private string fIdentificativosdi`
  - `private string fOggetto`
- assembly:
  - `assembly bool deleteFromEcommerceSync`
  - `assembly static string DisplayMember`
  - `assembly static string[] FieldnameColumns`
  - `assembly static string[] CaptionColumns`
  - `assembly static int32[] WidthColumns`
  - `assembly static class Facile.Archidocemaillog '\u0086\u0086\u0086\r\n\u0086\u0086\u0086\u008e\u0089\u009d'`

**Metodi rilevati**

- `.cctor`, `get_Data`, `set_Data`, `get_Emailid`, `set_Emailid`, `get_fFilenotifica`, `set_fFilenotifica`, `get_Filenotifica`, `set_Filenotifica`, `get_Identificativosdi`, `set_Identificativosdi`, `get_Oggetto`, `set_Oggetto`, `.ctor`

**Note interpretative**

- log email collegato ai documenti con data, oggetto, identificativi SDI e file notifica
- entità XPO persistente

---

### `Facile.ArchiDocViewControl`

- **Range righe**: `34120-53550`
- **Ruolo probabile**: controllo UI DevExpress per gestione e visualizzazione documenti
- **Base type**: `[DevExpress.Utils.v18.1]DevExpress.XtraEditors.XtraUserControl`
- **Interfacce**: nessuna rilevata in intestazione
- **Attributi custom rilevati**: `Microsoft.VisualBasic.CompilerServices.DesignerGeneratedAttribute`, `System.Runtime.CompilerServices.AccessedThroughPropertyAttribute`, `System.Runtime.CompilerServices.CompilerGeneratedAttribute`, `System.Runtime.CompilerServices.AccessedThroughPropertyAttribute`, `System.Runtime.CompilerServices.CompilerGeneratedAttribute`
- **Campi**: 95
- **Metodi**: 153
- **Proprietà rilevate da getter/setter**: 74

**Proprietà rilevate**

- `BarManagerArchiDocView`, `barDockControlTop`, `barDockControlBottom`, `barDockControlLeft`, `barDockControlRight`, `XpCollectionArchidoc`, `GridControlArchiDoc`, `LayoutViewArchiDoc`, `colNumeroOggetto`, `colAlfaOggetto`, `colDataArchidoc`, `colOggetto`
- `colNote`, `StandaloneBarDockControlArchiDoc`, `BarDockControl3`, `BarDockControl4`, `BarDockControl2`, `BarDockControl1`, `btnNuovo`, `btnElimina`, `btnModifica`, `BarArchiDocView`, `SelezionaFileDialog`, `BarStaticItem3`
- `ColSoggetto`, `ColCategoria`, `ColTipologia`, `ColNumeroArchidoc`, `ColAlfaArchidoc`, `BarSubItemEsporta`, `btnEsportaDocumento`, `btnEsportaDocumentoFirmato`, `BarStaticItem1`, `ColDataoggetto`, `ColRiferimentooggetto`, `BarStatus`
- `beiNumeroAllegati`, `beiCartella`, `beiFirmato`, `beiInviato`, `colFile`, `beiCreaLottoZip`, `btnImportaDocumentoFirmato`, `bbiAiuto`, `btnImportaDocumento`, `ColDocumento`, `ColVediNote`, `ColPdf`
- `layoutViewField_Colfile`, `layoutViewField_LayoutViewColumn1`, `layoutViewField_ColDocumento`, `layoutViewField_Colpdf`, `layoutViewField_colNumero`, `layoutViewField_LayoutViewColumn1_2`, `layoutViewField_colAlfa`, `layoutViewField_LayoutViewColumn1_3`, `layoutViewField_ColDataoggetto`, `layoutViewField_colData`, `layoutViewField_colOggetto`, `layoutViewField_colNote`
- `layoutViewField_LayoutViewSoggetto`, `layoutViewField_LayoutViewColumn2`, `layoutViewField_LayoutViewColumn1_1`, `layoutViewField_Colriferimento`, `LayoutViewCard1`, `Group1`, `BarSubItemImporta`, `BarButtonItem1`, `repApriFile`, `repFirmato`, `repInviato`, `repDocumento`
- `repButtonVediNote`, `repPdf`

**Campi dichiarati**

- private:
  - `private class [System]System.ComponentModel.IContainer components`
  - `private class [DevExpress.XtraBars.v18.1]DevExpress.XtraBars.BarManager _BarManagerArchiDocView`
  - `private class [DevExpress.XtraBars.v18.1]DevExpress.XtraBars.BarDockControl _barDockControlTop`
  - `private class [DevExpress.XtraBars.v18.1]DevExpress.XtraBars.BarDockControl _barDockControlBottom`
  - `private class [DevExpress.XtraBars.v18.1]DevExpress.XtraBars.BarDockControl _barDockControlLeft`
  - `private class [DevExpress.XtraBars.v18.1]DevExpress.XtraBars.BarDockControl _barDockControlRight`
  - `private class [DevExpress.Xpo.v18.1]DevExpress.Xpo.XPCollection _XpCollectionArchidoc`
  - `private class [DevExpress.XtraGrid.v18.1]DevExpress.XtraGrid.GridControl _GridControlArchiDoc`
  - `private class [DevExpress.XtraGrid.v18.1]DevExpress.XtraGrid.Views.Layout.LayoutView _LayoutViewArchiDoc`
  - `private class [DevExpress.XtraGrid.v18.1]DevExpress.XtraGrid.Columns.LayoutViewColumn _colNumeroOggetto`
  - `private class [DevExpress.XtraGrid.v18.1]DevExpress.XtraGrid.Columns.LayoutViewColumn _colAlfaOggetto`
  - `private class [DevExpress.XtraGrid.v18.1]DevExpress.XtraGrid.Columns.LayoutViewColumn _colDataArchidoc`
  - `private class [DevExpress.XtraGrid.v18.1]DevExpress.XtraGrid.Columns.LayoutViewColumn _colOggetto`
  - `private class [DevExpress.XtraGrid.v18.1]DevExpress.XtraGrid.Columns.LayoutViewColumn _colNote`
  - `private class [DevExpress.XtraBars.v18.1]DevExpress.XtraBars.StandaloneBarDockControl _StandaloneBarDockControlArchiDoc`
  - `private class [DevExpress.XtraBars.v18.1]DevExpress.XtraBars.BarDockControl _BarDockControl3`
  - `private class [DevExpress.XtraBars.v18.1]DevExpress.XtraBars.BarDockControl _BarDockControl4`
  - `private class [DevExpress.XtraBars.v18.1]DevExpress.XtraBars.BarDockControl _BarDockControl2`
  - `private class [DevExpress.XtraBars.v18.1]DevExpress.XtraBars.BarDockControl _BarDockControl1`
  - `private class [DevExpress.XtraBars.v18.1]DevExpress.XtraBars.BarButtonItem _btnNuovo`
  - `private class [DevExpress.XtraBars.v18.1]DevExpress.XtraBars.BarButtonItem _btnElimina`
  - `private class [DevExpress.XtraBars.v18.1]DevExpress.XtraBars.BarButtonItem _btnModifica`
  - `private class [DevExpress.XtraBars.v18.1]DevExpress.XtraBars.Bar _BarArchiDocView`
  - `private class [System.Windows.Forms]System.Windows.Forms.OpenFileDialog _SelezionaFileDialog`
  - `private class [DevExpress.XtraBars.v18.1]DevExpress.XtraBars.BarStaticItem _BarStaticItem3`
  - `private class [DevExpress.XtraGrid.v18.1]DevExpress.XtraGrid.Columns.LayoutViewColumn _ColSoggetto`
  - `private class [DevExpress.XtraGrid.v18.1]DevExpress.XtraGrid.Columns.LayoutViewColumn _ColCategoria`
  - `private class [DevExpress.XtraGrid.v18.1]DevExpress.XtraGrid.Columns.LayoutViewColumn _ColTipologia`
  - `private class [DevExpress.XtraGrid.v18.1]DevExpress.XtraGrid.Columns.LayoutViewColumn _ColNumeroArchidoc`
  - `private class [DevExpress.XtraGrid.v18.1]DevExpress.XtraGrid.Columns.LayoutViewColumn _ColAlfaArchidoc`
  - `private class [DevExpress.XtraBars.v18.1]DevExpress.XtraBars.BarSubItem _BarSubItemEsporta`
  - `private class [DevExpress.XtraBars.v18.1]DevExpress.XtraBars.BarButtonItem _btnEsportaDocumento`
  - `private class [DevExpress.XtraBars.v18.1]DevExpress.XtraBars.BarButtonItem _btnEsportaDocumentoFirmato`
  - `private class [DevExpress.XtraBars.v18.1]DevExpress.XtraBars.BarStaticItem _BarStaticItem1`
  - `private class [DevExpress.XtraGrid.v18.1]DevExpress.XtraGrid.Columns.LayoutViewColumn _ColDataoggetto`
  - `private class [DevExpress.XtraGrid.v18.1]DevExpress.XtraGrid.Columns.LayoutViewColumn _ColRiferimentooggetto`
  - `private class [DevExpress.XtraBars.v18.1]DevExpress.XtraBars.Bar _BarStatus`
  - `private class [DevExpress.XtraBars.v18.1]DevExpress.XtraBars.BarEditItem _beiNumeroAllegati`
  - `private class [DevExpress.XtraBars.v18.1]DevExpress.XtraBars.BarEditItem _beiCartella`
  - `private class [DevExpress.XtraBars.v18.1]DevExpress.XtraBars.BarEditItem _beiFirmato`
  - `private class [DevExpress.XtraBars.v18.1]DevExpress.XtraBars.BarEditItem _beiInviato`
  - `private class [DevExpress.XtraGrid.v18.1]DevExpress.XtraGrid.Columns.LayoutViewColumn _colFile`
  - `private class [DevExpress.XtraBars.v18.1]DevExpress.XtraBars.BarButtonItem _beiCreaLottoZip`
  - `private class [DevExpress.XtraBars.v18.1]DevExpress.XtraBars.BarButtonItem _btnImportaDocumentoFirmato`
  - `private class [DevExpress.XtraBars.v18.1]DevExpress.XtraBars.BarButtonItem _bbiAiuto`
  - `private class [DevExpress.XtraBars.v18.1]DevExpress.XtraBars.BarButtonItem _btnImportaDocumento`
  - `private class [DevExpress.XtraGrid.v18.1]DevExpress.XtraGrid.Columns.LayoutViewColumn _ColDocumento`
  - `private class [DevExpress.XtraGrid.v18.1]DevExpress.XtraGrid.Columns.LayoutViewColumn _ColVediNote`
  - `private class [DevExpress.XtraGrid.v18.1]DevExpress.XtraGrid.Columns.LayoutViewColumn _ColPdf`
  - `private class [DevExpress.XtraGrid.v18.1]DevExpress.XtraGrid.Views.Layout.LayoutViewField _layoutViewField_Colfile`
  - `private class [DevExpress.XtraGrid.v18.1]DevExpress.XtraGrid.Views.Layout.LayoutViewField _layoutViewField_LayoutViewColumn1`
  - `private class [DevExpress.XtraGrid.v18.1]DevExpress.XtraGrid.Views.Layout.LayoutViewField _layoutViewField_ColDocumento`
  - `private class [DevExpress.XtraGrid.v18.1]DevExpress.XtraGrid.Views.Layout.LayoutViewField _layoutViewField_Colpdf`
  - `private class [DevExpress.XtraGrid.v18.1]DevExpress.XtraGrid.Views.Layout.LayoutViewField _layoutViewField_colNumero`
  - `private class [DevExpress.XtraGrid.v18.1]DevExpress.XtraGrid.Views.Layout.LayoutViewField _layoutViewField_LayoutViewColumn1_2`
  - `private class [DevExpress.XtraGrid.v18.1]DevExpress.XtraGrid.Views.Layout.LayoutViewField _layoutViewField_colAlfa`
  - `private class [DevExpress.XtraGrid.v18.1]DevExpress.XtraGrid.Views.Layout.LayoutViewField _layoutViewField_LayoutViewColumn1_3`
  - `private class [DevExpress.XtraGrid.v18.1]DevExpress.XtraGrid.Views.Layout.LayoutViewField _layoutViewField_ColDataoggetto`
  - `private class [DevExpress.XtraGrid.v18.1]DevExpress.XtraGrid.Views.Layout.LayoutViewField _layoutViewField_colData`
  - `private class [DevExpress.XtraGrid.v18.1]DevExpress.XtraGrid.Views.Layout.LayoutViewField _layoutViewField_colOggetto`
  - `private class [DevExpress.XtraGrid.v18.1]DevExpress.XtraGrid.Views.Layout.LayoutViewField _layoutViewField_colNote`
  - `private class [DevExpress.XtraGrid.v18.1]DevExpress.XtraGrid.Views.Layout.LayoutViewField _layoutViewField_LayoutViewSoggetto`
  - `private class [DevExpress.XtraGrid.v18.1]DevExpress.XtraGrid.Views.Layout.LayoutViewField _layoutViewField_LayoutViewColumn2`
  - `private class [DevExpress.XtraGrid.v18.1]DevExpress.XtraGrid.Views.Layout.LayoutViewField _layoutViewField_LayoutViewColumn1_1`
  - `private class [DevExpress.XtraGrid.v18.1]DevExpress.XtraGrid.Views.Layout.LayoutViewField _layoutViewField_Colriferimento`
  - `private class [DevExpress.XtraGrid.v18.1]DevExpress.XtraGrid.Views.Layout.LayoutViewCard _LayoutViewCard1`
  - `private class [DevExpress.XtraLayout.v18.1]DevExpress.XtraLayout.LayoutControlGroup _Group1`
  - `private class [DevExpress.XtraBars.v18.1]DevExpress.XtraBars.BarSubItem _BarSubItemImporta`
  - `private class [DevExpress.XtraBars.v18.1]DevExpress.XtraBars.BarButtonItem _BarButtonItem1`
  - `private valuetype Facile.Enums/EnumProvenienzaArchidoc enumPprovenienza`
  - `private int32 intOidOggettoCorrente`
  - `private int32 intOidSoggettocorrente`
  - `private int32 intRecordPresenti`
  - `private class [mscorlib]System.Collections.Generic.List`1<string> listFolderToDelete`
  - `private class [mscorlib]System.Collections.Generic.List`1<int32> listOidsAutoGenerati`
  - `private string strObjectTypeStrCorrente`
  - `private bool swAllowSort`
  - `private bool swAllowFilter`
  - `private bool swAutoGeneratoFirmato`
  - `private bool swAutoGeneratoInviato`
- assembly:
  - `assembly static class Facile.ArchiDocViewControl '\u0086\u0086\u0086\r\n\u0086\u0086\u0086\u008e\u0090\u008b'`

**Metodi rilevati**

- `.ctor`, `Dispose`, `InitializeComponent`, `get_BarManagerArchiDocView`, `set_BarManagerArchiDocView`, `get_barDockControlTop`, `set_barDockControlTop`, `get_barDockControlBottom`, `set_barDockControlBottom`, `get_barDockControlLeft`, `set_barDockControlLeft`, `get_barDockControlRight`, `set_barDockControlRight`, `get_XpCollectionArchidoc`, `set_XpCollectionArchidoc`, `get_GridControlArchiDoc`, `set_GridControlArchiDoc`, `get_LayoutViewArchiDoc`
- `set_LayoutViewArchiDoc`, `get_colNumeroOggetto`, `set_colNumeroOggetto`, `get_colAlfaOggetto`, `set_colAlfaOggetto`, `get_colDataArchidoc`, `set_colDataArchidoc`, `get_colOggetto`, `set_colOggetto`, `get_colNote`, `set_colNote`, `get_StandaloneBarDockControlArchiDoc`, `set_StandaloneBarDockControlArchiDoc`, `get_BarDockControl3`, `set_BarDockControl3`, `get_BarDockControl4`, `set_BarDockControl4`, `get_BarDockControl2`
- `set_BarDockControl2`, `get_BarDockControl1`, `set_BarDockControl1`, `get_btnNuovo`, `set_btnNuovo`, `get_btnElimina`, `set_btnElimina`, `get_btnModifica`, `set_btnModifica`, `get_BarArchiDocView`, `set_BarArchiDocView`, `get_SelezionaFileDialog`, `set_SelezionaFileDialog`, `get_BarStaticItem3`, `set_BarStaticItem3`, `get_ColSoggetto`, `set_ColSoggetto`, `get_ColCategoria`
- `set_ColCategoria`, `get_ColTipologia`, `set_ColTipologia`, `get_ColNumeroArchidoc`, `set_ColNumeroArchidoc`, `get_ColAlfaArchidoc`, `set_ColAlfaArchidoc`, `get_BarSubItemEsporta`, `set_BarSubItemEsporta`, `get_btnEsportaDocumento`, `set_btnEsportaDocumento`, `get_btnEsportaDocumentoFirmato`, `set_btnEsportaDocumentoFirmato`, `get_BarStaticItem1`, `set_BarStaticItem1`, `get_ColDataoggetto`, `set_ColDataoggetto`, `get_ColRiferimentooggetto`
- `set_ColRiferimentooggetto`, `get_BarStatus`, `set_BarStatus`, `get_beiNumeroAllegati`, `set_beiNumeroAllegati`, `get_beiCartella`, `set_beiCartella`, `get_beiFirmato`, `set_beiFirmato`, `get_beiInviato`, `set_beiInviato`, `get_colFile`, `set_colFile`, `get_beiCreaLottoZip`, `set_beiCreaLottoZip`, `get_btnImportaDocumentoFirmato`, `set_btnImportaDocumentoFirmato`, `get_bbiAiuto`
- `set_bbiAiuto`, `get_btnImportaDocumento`, `set_btnImportaDocumento`, `get_ColDocumento`, `set_ColDocumento`, `get_ColVediNote`, `set_ColVediNote`, `get_ColPdf`, `set_ColPdf`, `get_layoutViewField_Colfile`, `set_layoutViewField_Colfile`, `get_layoutViewField_LayoutViewColumn1`, `set_layoutViewField_LayoutViewColumn1`, `get_layoutViewField_ColDocumento`, `set_layoutViewField_ColDocumento`, `get_layoutViewField_Colpdf`, `set_layoutViewField_Colpdf`, `get_layoutViewField_colNumero`
- `set_layoutViewField_colNumero`, `get_layoutViewField_LayoutViewColumn1_2`, `set_layoutViewField_LayoutViewColumn1_2`, `get_layoutViewField_colAlfa`, `set_layoutViewField_colAlfa`, `get_layoutViewField_LayoutViewColumn1_3`, `set_layoutViewField_LayoutViewColumn1_3`, `get_layoutViewField_ColDataoggetto`, `set_layoutViewField_ColDataoggetto`, `get_layoutViewField_colData`, `set_layoutViewField_colData`, `get_layoutViewField_colOggetto`, `set_layoutViewField_colOggetto`, `get_layoutViewField_colNote`, `set_layoutViewField_colNote`, `get_layoutViewField_LayoutViewSoggetto`, `set_layoutViewField_LayoutViewSoggetto`, `get_layoutViewField_LayoutViewColumn2`
- `set_layoutViewField_LayoutViewColumn2`, `get_layoutViewField_LayoutViewColumn1_1`, `set_layoutViewField_LayoutViewColumn1_1`, `get_layoutViewField_Colriferimento`, `set_layoutViewField_Colriferimento`, `get_LayoutViewCard1`, `set_LayoutViewCard1`, `get_Group1`, `set_Group1`, `get_BarSubItemImporta`, `set_BarSubItemImporta`, `get_BarButtonItem1`, `set_BarButtonItem1`, `get_repApriFile`, `set_repApriFile`, `get_repFirmato`, `set_repFirmato`, `get_repInviato`
- `set_repInviato`, `get_repDocumento`, `set_repDocumento`, `get_repButtonVediNote`, `set_repButtonVediNote`, `get_repPdf`, `set_repPdf`, `Inizializza`, `SelectArchiDocObject`

**Note interpretative**

- grossa view WinForms/DevExpress per gestione documenti
- contiene toolbar, griglia, card/layout view, colonne, repository item e dialog di selezione file
- la quantità di controlli e metodi indica che qui c'è molta logica UI oltre al designer

---

### `Facile.Archivio`

- **Range righe**: `68745-73131`
- **Ruolo probabile**: configurazione archivio, backup, remoto e sincronizzazione
- **Base type**: `[DevExpress.Xpo.v18.1]DevExpress.Xpo.XPObject`
- **Interfacce**:
  - `[DevExpress.Data.v18.1]DevExpress.XtraEditors.DXErrorProvider.IDXDataErrorInfo`
- **Attributi custom rilevati**: `DevExpress.Xpo.DeferredDeletionAttribute`, `DevExpress.Xpo.OptimisticLockingAttribute`, `DevExpress.Xpo.PersistentAttribute`
- **Campi**: 52
- **Metodi**: 98
- **Proprietà rilevate da getter/setter**: 50

**Proprietà rilevate**

- `Archivio`, `Archivioremoto`, `Attivo`, `Backupgiorni`, `Cartellabackup`, `Cartellarestore`, `Disattivamagazzino`, `Disattivacontabilita`, `Disattivatrasporti`, `Disattivacontratti`, `Disattivaecommerce`, `Disattivaproduzione`
- `GetCartellacondivisaserver`, `Cartellacondivisaserver`, `Cartellacondivisaserverremoto`, `Computerbackup`, `Copiaarchivio`, `Copiaarchidoc`, `Copiaimmagini`, `Copialayout`, `Copiareport`, `Copiatimestamp`, `Dataultimouso`, `Descrizione`
- `Disattivacopia`, `Ricordaimpostazioni`, `Indirizzoftp`, `Nascosto`, `Networkuser`, `Networkpassword`, `Networkuserremoto`, `Networkpasswordremoto`, `Nomedatabase_Formattato`, `Nomedatabase_Remoto_Formattato`, `Passwordftp`, `Passwordremoto`
- `Ripristinabackupdaftp`, `Ripristinoultimorestore`, `Tipoarchivio`, `TipoArchivioReloaded`, `Ultimadatacopia`, `Ultimadatacopiaremota`, `Ultimaswitchcopiaremota`, `Serverremoto`, `Usaserverremotoondemand`, `Usaarchivioremoto`, `Usacartellacondivisaperfiletemporanei`, `Utenteftp`
- `Utenteremoto`, `Backupadminlimitato`

**Campi dichiarati**

- private:
  - `private static initonly object lockDict`
  - `private string fArchivio`
  - `private string fArchivioremoto`
  - `private bool fAttivo`
  - `private int32 fBackupgiorni`
  - `private string fCartellabackup`
  - `private string fCartellarestore`
  - `private bool fDisattivamagazzino`
  - `private bool fDisattivacontabilita`
  - `private bool fDisattivatrasporti`
  - `private bool fDisattivacontratti`
  - `private bool fDisattivaecommerce`
  - `private bool fDisattivaproduzione`
  - `private string FCartellacondivisaserver`
  - `private string FCartellacondivisaserverremoto`
  - `private string fComputerbackup`
  - `private bool fCopiaarchivio`
  - `private bool fCopiaarchidoc`
  - `private bool fCopiaimmagini`
  - `private bool fCopialayout`
  - `private bool fCopiareport`
  - `private bool fCopiatimestamp`
  - `private valuetype [mscorlib]System.DateTime fDataultimouso`
  - `private string fDescrizione`
  - `private bool fDisattivacopia`
  - `private bool fRicordaimpostazioni`
  - `private string fIndirizzoftp`
  - `private bool fNascosto`
  - `private string fNetworkuser`
  - `private string fNetworkpassword`
  - `private string fNetworkuserremoto`
  - `private string fNetworkpasswordremoto`
  - `private string fPasswordftp`
  - `private string fPasswordremoto`
  - `private bool fRipristinabackupdaftp`
  - `private bool fRipristinoultimorestore`
  - `private int32 fTipoarchivio`
  - `private valuetype [mscorlib]System.DateTime fUltimadatacopia`
  - `private valuetype [mscorlib]System.DateTime fUltimadatacopiaremota`
  - `private int32 fUltimaswitchcopiaremota`
  - `private string fServerremoto`
  - `private bool fUsaserverremotoondemand`
  - `private bool fUsaarchivioremoto`
  - `private bool fUsacartellacondivisaperfiletemporanei`
  - `private string fUtenteftp`
  - `private string fUtenteremoto`
  - `private bool fBackupadminlimitato`
- assembly:
  - `assembly static string DisplayMember`
  - `assembly static string[] FieldnameColumns`
  - `assembly static string[] CaptionColumns`
  - `assembly static string[] EnumsTipoArchivio`
  - `assembly static class Facile.Archivio '\u0086\u0086\u0086\r\n\u0086\u0086\u0088\u0093\u009c\u009c'`

**Metodi rilevati**

- `.cctor`, `get_Archivio`, `set_Archivio`, `get_Archivioremoto`, `set_Archivioremoto`, `get_Attivo`, `set_Attivo`, `get_Backupgiorni`, `set_Backupgiorni`, `get_Cartellabackup`, `set_Cartellabackup`, `get_Cartellarestore`, `set_Cartellarestore`, `get_Disattivamagazzino`, `set_Disattivamagazzino`, `get_Disattivacontabilita`, `set_Disattivacontabilita`, `get_Disattivatrasporti`
- `set_Disattivatrasporti`, `get_Disattivacontratti`, `set_Disattivacontratti`, `get_Disattivaecommerce`, `set_Disattivaecommerce`, `get_Disattivaproduzione`, `set_Disattivaproduzione`, `get_GetCartellacondivisaserver`, `get_Cartellacondivisaserver`, `set_Cartellacondivisaserver`, `get_Cartellacondivisaserverremoto`, `set_Cartellacondivisaserverremoto`, `get_Computerbackup`, `set_Computerbackup`, `get_Copiaarchivio`, `set_Copiaarchivio`, `get_Copiaarchidoc`, `set_Copiaarchidoc`
- `get_Copiaimmagini`, `set_Copiaimmagini`, `get_Copialayout`, `set_Copialayout`, `get_Copiareport`, `set_Copiareport`, `get_Copiatimestamp`, `set_Copiatimestamp`, `get_Dataultimouso`, `set_Dataultimouso`, `get_Descrizione`, `set_Descrizione`, `get_Disattivacopia`, `set_Disattivacopia`, `get_Ricordaimpostazioni`, `set_Ricordaimpostazioni`, `get_Indirizzoftp`, `set_Indirizzoftp`
- `get_Nascosto`, `set_Nascosto`, `get_Networkuser`, `set_Networkuser`, `get_Networkpassword`, `set_Networkpassword`, `get_Networkuserremoto`, `set_Networkuserremoto`, `get_Networkpasswordremoto`, `set_Networkpasswordremoto`, `get_Nomedatabase_Formattato`, `get_Nomedatabase_Remoto_Formattato`, `get_Passwordftp`, `set_Passwordftp`, `get_Passwordremoto`, `set_Passwordremoto`, `get_Ripristinabackupdaftp`, `set_Ripristinabackupdaftp`
- `get_Ripristinoultimorestore`, `set_Ripristinoultimorestore`, `get_Tipoarchivio`, `set_Tipoarchivio`, `get_TipoArchivioReloaded`, `get_Ultimadatacopia`, `set_Ultimadatacopia`, `get_Ultimadatacopiaremota`, `set_Ultimadatacopiaremota`, `get_Ultimaswitchcopiaremota`, `set_Ultimaswitchcopiaremota`, `get_Serverremoto`, `set_Serverremoto`, `get_Usaserverremotoondemand`, `set_Usaserverremotoondemand`, `get_Usaarchivioremoto`, `set_Usaarchivioremoto`, `get_Usacartellacondivisaperfiletemporanei`
- `set_Usacartellacondivisaperfiletemporanei`, `get_Utenteftp`, `set_Utenteftp`, `get_Utenteremoto`, `set_Utenteremoto`, `get_Backupadminlimitato`, `set_Backupadminlimitato`, `.ctor`

**Note interpretative**

- configurazione archivio locale/remoto
- forte presenza di backup, restore, FTP, copie risorse, cartelle condivise e server remoto
- probabile nodo centrale di configurazione infrastrutturale dell'app

---

### `Facile.Arrotondamentoinriepilogo`

- **Range righe**: `78322-78380`
- **Ruolo probabile**: DTO/container per arrotondamenti di riepilogo
- **Base type**: `[mscorlib]System.Object`
- **Interfacce**: nessuna rilevata in intestazione
- **Attributi custom rilevati**: nessuno nei primi metadati
- **Campi**: 5
- **Metodi**: 1
- **Proprietà rilevate da getter/setter**: 0

**Campi dichiarati**

- assembly:
  - `assembly valuetype [mscorlib]System.Decimal Percentualeiva`
  - `assembly valuetype [mscorlib]System.Decimal Arrotondamento`
  - `assembly valuetype Facile.Enums/EnumNaturaIva NaturaIva`
  - `assembly string Riferimentonormativo`
  - `assembly static class Facile.Arrotondamentoinriepilogo '\u0086\u0086\u0086\r\n\u0086\u0086\u0087\u0092\u0089\u009a'`

**Metodi rilevati**

- `.ctor`

**Note interpretative**

- DTO minimale per gestione arrotondamento in riepilogo

---

### `Facile.Articolo`

- **Range righe**: `78444-80002`
- **Ruolo probabile**: entità articolo/prodotto con categorie, IVA, varianti e metadati ecommerce
- **Base type**: `[DevExpress.Xpo.v18.1]DevExpress.Xpo.XPObject`
- **Interfacce**:
  - `[SogesSoftware.Utils]SogesSoftware.Utils.IXpo`
  - `[DevExpress.Data.v18.1]DevExpress.XtraEditors.DXErrorProvider.IDXDataErrorInfo`
  - `Facile.ITabella`
- **Attributi custom rilevati**: `DevExpress.Xpo.DeferredDeletionAttribute`, `DevExpress.Xpo.OptimisticLockingAttribute`, `DevExpress.Xpo.PersistentAttribute`, `System.Runtime.CompilerServices.CompilerGeneratedAttribute`
- **Campi**: 114
- **Metodi**: 37
- **Proprietà rilevate da getter/setter**: 15

**Proprietà rilevate**

- `Articolodalavorare`, `Contoprimanota`, `Contoprimanotaricavo`, `Categoria`, `Categoriaricarico`, `Disponibilitaonline`, `Imballaggio`, `Iva`, `Produttore`, `Tassa`, `Unitadimisura`, `Unitadimisura2`
- `Variante1`, `Variante2`, `Alfa`

**Campi dichiarati**

- public:
  - `public string $VB$Local_codicearticolo`
  - `public static class [DevExpress.Xpo.v18.1]DevExpress.Xpo.UnitOfWork unitOfWorkNonPersistent`
- private:
  - `private class Facile.Soggetto produttoreOnLoaded`
  - `private class Facile.Categoria categoriaOnLoaded`
  - `private string descrizioneOnloaded`
  - `private bool deleteFromEcommerceSync`
  - `private int32 Idesternotemporaneo`
  - `private class [System.Core]System.Collections.Generic.HashSet`1<string> stringsChanged`
  - `private class [mscorlib]System.Collections.Generic.Dictionary`2<string, class Facile.Articolosituazionecombinazionevariante> _cacheSituazioniVarianti`
  - `private class [mscorlib]System.Collections.Generic.Dictionary`2<int32, class Facile.Articolosituazione> _cacheSituazioni`
  - `private bool _cacheCostruita`
  - `private class Facile.Articolo _Articolodalavorare`
  - `private class Facile.Contoprimanota _Contoprimanota`
  - `private class Facile.Contoprimanota _Contoprimanotaricavo`
  - `private class Facile.Categoria _Categoria`
  - `private class Facile.Categoriaricarico _Categoriaricarico`
  - `private class Facile.Disponibilitaonline _Disponibilitaonline`
  - `private class Facile.Imballaggio _Imballaggio`
  - `private class Facile.Iva _Iva`
  - `private class Facile.Soggetto _Produttore`
  - `private class Facile.Tassa _Tassa`
  - `private class Facile.Unitadimisura _Unitadimisura`
  - `private class Facile.Unitadimisura _Unitadimisura2`
  - `private class Facile.Variante _Variante1`
  - `private class Facile.Variante _Variante2`
  - `private valuetype [mscorlib]System.Decimal fAltezza`
  - `private int32 fArrotondamentoprezzo`
  - `private int32 fAssegnazionemisure`
  - `private string fAvvertenze`
  - `private string fCodicearticolo`
  - `private string fCodicedoganale`
  - `private string fCodiciabarre`
  - `private string fCodicetipo`
  - `private string fCodicevalore`
  - `private int32 fCondizione`
  - `private valuetype [mscorlib]System.DateTime fDatastatofitosanitario`
  - `private valuetype [mscorlib]System.Decimal fDaziopercentuale`
  - `private int32 fDecimaliqta`
  - `private string fDescrizionearticolo`
  - `private bool fDescrizionebreveinaltridatigestionali`
  - `private bool fEscludiinventario`
  - `private bool fEscluditotaledocumento`
  - `private bool fEscludiscontrino`
  - `private bool fEscludiscontosoggetto`
  - `private valuetype [mscorlib]System.Decimal fEuroperunpunto`
  - `private bool fEsporta`
  - `private string fFonte`
  - `private int32 fGaranziamesivendita`
  - `private bool fInofferta`
  - `private bool fInvetrina`
  - `private int32 fIdesterno`
  - `private bool fIsdescrizionebloccata`
  - `private bool fIsobsolete`
  - `private bool fIsdualtax`
  - `private bool fIspesoparziale`
  - `private valuetype [mscorlib]System.Decimal fLarghezza`
  - `private bool fMarketplace`
  - `private valuetype [mscorlib]System.Decimal fMisure1`
  - `private valuetype [mscorlib]System.Decimal fMisure2`
  - `private valuetype [mscorlib]System.Decimal fMisure3`
  - `private valuetype [mscorlib]System.Decimal fMisureminimofatturabile`
  - `private valuetype [mscorlib]System.Decimal fMoltiplicativoum2`
  - `private valuetype [mscorlib]System.Decimal fMoltiplicatoretassa`
  - `private string fNotearticolo`
  - `private string fNotearticoloestese`
  - `private bool fOnline`
  - `private int32 fOperazionesucartafedelta`
  - `private int32 fPuntidasottrarrecartafedeleta`
  - `private valuetype [mscorlib]System.Decimal fPesolordo`
  - `private valuetype [mscorlib]System.Decimal fPesonetto`
  - `private int32 fPosizione`
  - `private valuetype [mscorlib]System.Decimal fProfondita`
  - `private valuetype [mscorlib]System.Decimal fQtapercollo`
  - `private valuetype [mscorlib]System.Decimal fQuantitaminimavendita`
  - `private valuetype [mscorlib]System.Decimal fQuantitamultiplivendita`
  - `private string fSlug`
  - `private bool fStampaarticolocomponentiindocumenti`
  - `private bool fStampanoteindocumenti`
  - `private string fStatofitosanitario`
  - `private bool fStatofitosanitariochanged`
  - `private string fTabtext1`
  - `private string fTabtext2`
- assembly:
  - `assembly static class Facile.Articolo/'_Closure$__705-0' '\u0086\u0086\u0086\r\n\u0086\u0086\u0089\u0086\u008a\u009e'`
  - `assembly static string DisplayMember`
  - `assembly static initonly string[] FieldnameColumns`
  - `assembly static initonly string[] CaptionColumns`
  - `assembly static string DisplayMember2`
  - `assembly static initonly string[] FieldnameColumns2`
  - `assembly static initonly string[] CaptionColumns2`
  - `assembly static int32[] WidthColumns2`
  - `assembly static int32 OidMagazzinoPredefinito`
  - `assembly class [mscorlib]System.Collections.Generic.List`1<class Facile.Articoloconfiguratoreopzione> ArticoloconfiguratoreSelectedList`
  - `assembly class [System.Core]System.Collections.Generic.HashSet`1<string> ListCodiciABarreImportatiToUseInSync`
  - `assembly valuetype [mscorlib]System.Decimal[] costoAcquistoProposto`
  - `assembly bool stopUpdateEcommerce`
  - `assembly bool forceResyncEcommerce`
  - `assembly static class Facile.Magazzino magazzinoPredefinito`
  - `assembly static class Facile.Articolo '\u0086\u0086\u0086\r\n\u0086\u0086\u0086\u0097\u0096\u0097'`

**Metodi rilevati**

- `.ctor`, `_Lambda$__0`, `'\u0086\u0086\u0086\r\n\u0086\u0086\u0089\u0086\u008b\u0088'`, `'\u0086\u0086\u0086\r\n\u0086\u0086\u0089\u0086\u008b\u0089'`, `'\u0086\u0086\u0086\r\n\u0086\u0086\u0089\u0086\u008b\u0086'`, `'\u0086\u0086\u0086\r\n\u0086\u0086\u0089\u0086\u008b\u0087'`, `.cctor`, `get_Articolodalavorare`, `set_Articolodalavorare`, `get_Contoprimanota`, `set_Contoprimanota`, `get_Contoprimanotaricavo`, `set_Contoprimanotaricavo`, `get_Categoria`, `set_Categoria`, `get_Categoriaricarico`, `set_Categoriaricarico`, `get_Disponibilitaonline`
- `set_Disponibilitaonline`, `get_Imballaggio`, `set_Imballaggio`, `get_Iva`, `set_Iva`, `get_Produttore`, `set_Produttore`, `get_Tassa`, `set_Tassa`, `get_Unitadimisura`, `set_Unitadimisura`, `get_Unitadimisura2`, `set_Unitadimisura2`, `get_Variante1`, `set_Variante1`, `get_Variante2`, `set_Variante2`, `get_Alfa`
- `set_Alfa`

**Note interpretative**

- classe articolo/prodotto ampia ma dump interrotto
- si vedono relazioni verso categoria, IVA, produttore, varianti, imballaggio e unità di misura
- presente logica ecommerce (`UpdateEcommerceArticolopersonalizzato`, `UpdateEcommerceArticoloSituazione`), ma la seconda è tronca

---

## Focus trasversale: pattern riconoscibili

### 1. Entità persistenti XPO

- `Facile.Agente`
- `Facile.Agentescaglione`
- `Facile.Archidoc`
- `Facile.Archidocemaillog`
- `Facile.Archivio`
- `Facile.Articolo`

Queste classi sono verosimilmente mappate a tabelle o oggetti persistenti del gestionale.

### 2. DTO / container senza logica

- `Facile.Allegati`
- `Facile.AltriDatiGestionali`
- `Facile.Anagrafica`
- `Facile.Archidocemailinfo`
- `Facile.Arrotondamentoinriepilogo`

### 3. UI documentale

- `Facile.ArchiDocViewControl` è il componente più pesante lato interfaccia.
- Dalla nomenclatura dei controlli emergono: creazione, modifica, eliminazione, import/export documento, import documento firmato, creazione lotto zip, colonne soggetto/categoria/tipologia, visualizzazione PDF/note/file.

### 4. Area archivio e backup

- `Facile.Archivio` mostra che l'app non gestisce solo dati business, ma anche infrastruttura operativa: backup, restore, FTP, copie file e ambienti remoti.

### 5. Articoli ed ecommerce

- `Facile.Articolo` contiene segnali espliciti di sincronizzazione/stato ecommerce.
- Il dump però si interrompe nel mezzo, quindi questa parte non è ricostruibile al 100% da questo file da solo.

## Priorità di analisi consigliata

Se l'obiettivo è capire davvero il software, l'ordine migliore è:

1. `Facile.Articolo` — per logica prodotti, categorie, varianti ed ecommerce
2. `Facile.Archidoc` — per il cuore documentale
3. `Facile.ArchiDocViewControl` — per capire i flussi operativi della UI
4. `Facile.Archivio` — per backup/remoto/infrastruttura
5. `Facile.Agente` / `Facile.Agentescaglione` — per provvigioni e rete vendita

## Limiti reali di questo documento

- La strutturazione è **completa rispetto al dump disponibile**, non rispetto all'intera applicazione originale.
- Dove i nomi sono offuscati, il significato preciso non è recuperabile senza un dump migliore o direttamente l'assembly.
- La classe `Articolo` è solo parziale perché la decompilazione è stata interrotta.
- Alcune classi DTO sono troppo minimali per inferire il significato business completo dai soli campi.

## Conclusione

Questo dump rappresenta già una base utile per mappare il perimetro del gestionale `Facile`: persistenza XPO, UI DevExpress, area documentale molto forte, configurazione archivio/remoto e una parte articolo/ecommerce non completata. Per un lavoro davvero profondo, il passo successivo ideale è estrarre e ricostruire **classe per classe** partendo da `Articolo`, `Archidoc`, `ArchiDocViewControl` e `Archivio`.