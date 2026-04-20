# Diario Modulo Punti

## Scopo

Diario operativo della parte punti/campagne del modulo `Banco.Punti`.
Questo file va aggiornato ogni volta che cambia la maschera punti o il calcolo dei punti soggetto.

## Stato attuale

- modulo WPF gia` creato
- collegamento nella shell sotto `Clienti > Punti`
- campagna nativa letta da `cartafedelta`
- punti cliente calcolati come punti iniziali + punti assegnati
- nessuna lista completa di articoli collegati mostrata nella UI
- campagna modificabile con nuovo/salva/annulla
- la maschera distingue in modo esplicito tra campagna nativa e regola premio locale
- ricerca cliente in tempo reale mentre si digita
- il campo `Base calcolo` e` testuale e non usa piu` un controllo ambiguo a selezione
- la UI e` stata riorganizzata in due aree nette: sinistra per campagne e simulazione cliente, destra per dettaglio campagna e regole premio
- il blocco `Regole premio` gestisce piu` regole locali per campagna, non una sola regola statica
- l'editor regola mostra campi dinamici per `Sconto fisso (EUR)`, `Sconto percentuale (%)` e `Articolo premio`
- l'articolo premio usa un lookup reale sui servizi articoli esistenti, con ricerca live e rimozione dell'articolo selezionato
- presente un riepilogo cliente con punti storici, maturati, totale disponibile, soglia, distanza dalla soglia, regola letta e premio configurato
- i blocchi sinistro e destro sono stati compattati per ridurre spazio sprecato e migliorare leggibilita`
- `Base calcolo` usa ora una lista guidata con opzioni operative del gestionale
- il blocco `Regole premio` espone un pulsante esplicito `Salva premi` per rendere chiaro il salvataggio della configurazione locale

## UI reale

- intestazione con titolo `Raccolta Punti`
- pulsante `Aggiorna`
- colonna sinistra con blocco `Campagne punti` e azioni `Nuova`, `Salva`, `Annulla`, `Aggiorna`
- colonna sinistra con blocco `Cliente e simulazione`
- area ricerca soggetto compatta e live
- lista soggetti trovati
- riepilogo saldo e stato premio del soggetto selezionato
- area destra con dettaglio campagna nativa e soli campi gestionali reali
- area destra con blocco forte `Regole premio`
- elenco regole premio con nome, tipo, punti richiesti, riepilogo premio, stato e controllo banco
- editor regola premio separato con campi dinamici e lookup articolo
- `Base calcolo` a selezione guidata
- barra stato

## Regole fissate

- non introdurre nuove tabelle o nuovi campi nel `db_diltech`
- usare solo campi gia` presenti nello schema reale per la campagna nativa
- non elencare gli articoli collegati nella maschera come lista gestionale completa
- il calcolo punti soggetto deve includere punti iniziali e punti assegnati
- il cliente e` considerato agganciato alla raccolta punti solo se in `soggetto.Codicecartafedelta` esiste un codice valorizzato
- la campagna deve avere data inizio, data fine, attiva, euro per punto, base calcolo, importo minimo e calcolo su valore ivato
- la regola premio non vive in `cartafedelta`: vive nel LocalStore del nuovo gestionale
- l'articolo premio, se usato, viene scelto da lookup mirato e non da lista massiva di articoli agganciati
- il tipo premio deve distinguere chiaramente `Sconto fisso (EUR)`, `Sconto percentuale (%)` e `Articolo premio`
- la campagna nativa e le regole premio locali devono restare separate anche visivamente nella maschera
- la persistenza multi-regola vive solo nel LocalStore del nuovo gestionale
- il campo base calcolo deve essere guidato da lista e salvare il testo reale della voce selezionata
- la ricerca cliente deve aggiornarsi mentre l'utente digita
- l'impaginazione deve evitare elementi che nascondono altri campi o risultano ridondanti
- il simulatore cliente mostra il codice carta fedeltà solo quando il cliente è realmente agganciato alla raccolta punti

## Checklist

- [x] modulo creato
- [x] shell collegata
- [x] campagna nativa letta
- [x] punti cliente calcolati
- [x] UI senza lista articoli completa
- [x] campagna editabile
- [x] ricerca cliente in tempo reale
- [x] campo base calcolo testuale
- [x] distinzione tra campagna nativa e regola premio locale
- [x] lookup articolo premio dedicato
- [x] gestione multi-regola per campagna
- [x] editor dinamico per tre tipi premio
- [x] riepilogo cliente riordinato e leggibile
- [x] base calcolo a lista guidata
- [ ] storico punti interno completo
- [ ] test completi

## Prossimi passi

- mantenere stabile la lettura/salvataggio campagna
- non reintrodurre controlli che trasformano il valore di base calcolo in codici o indici numerici
- non spostare in questo diario la parte promozioni in vendita

## Nota allineamento banca/cortesia

Nessuna modifica diretta al modulo Punti in questo ciclo: la chiusura Banco fiscale/cortesia e stata consolidata nel core/app del modulo Banco.

## Aggiornamento operativo - 2026-04-12 (rifiniture Banco griglia/tastiera/tab)
- Corretto il comportamento tastiera della DataGrid Banco ai bordi (Up/Down) evitando stati incoerenti su CurrentCell/SelectedItem.
- Rafforzata la sincronizzazione bidirezionale tra riga selezionata e cella corrente anche su click mouse, focus e reingresso in griglia.
- Stabilizzato il RowHeader come fascia esterna sempre visibile con marker mostrato solo sulla riga attiva/selezionata.
- Migliorata la continuita visiva della riga documento riducendo l'effetto a box separati e mantenendo separatori di griglia reali.
- Rifinito il template tab workspace lato destro (spaziature/bordo/chiusura) in modo valido per tutte le tab.
- Nessuna modifica a schema db_diltech; nessun cambio architetturale; perimetro limitato a BancoView.xaml, BancoView.xaml.cs, ShellWindow.xaml.


## Aggiornamento operativo - 2026-04-12 (abilitazione Anteprima/Cortesia)
- Corretta la regola UI di abilitazione Anteprima e Cortesia nel Banco: non dipende piu da Residuo > 0 o da buoni gia presenti.
- Nuova condizione can-execute: documento modificabile, almeno una riga documento, nessuna operazione pagamento/stampa concorrente.
- Nessuna modifica a runtime di persistenza, nessuna variazione del flusso ufficiale verso db_diltech, nessun cambio ai riferimenti legacy visibili in Facile Manager.
- Obiettivo operativo garantito: l'operatore puo scegliere il comando anche con residuo zero, mantenendo invariati i controlli economici nei metodi runtime esistenti.


### Aggiornamento 2026-04-12 - Guardia uscita workspace

- Introdotta una guardia unica di uscita workspace per chiusura tab, cambio tab e chiusura programma.
- Il popup di conferma e unico (ConfirmationDialogWindow) con testo contestuale al caso.
- Per Banco, la perdita lavoro locale usa il flusso canonico CancellaSchedaAsync() e non duplica regole in shell.
- Documenti ufficiali/fiscalizzati o con riferimenti legacy non vengono trattati come bozza annullabile.
- Aggiunti flag anti-reentrancy per evitare loop, doppi prompt e stati incoerenti durante switch/close.
