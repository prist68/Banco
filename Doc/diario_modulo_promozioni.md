# Diario Modulo Promozioni

## Scopo

Diario operativo della parte promozioni del modulo `Banco.Punti`.
Questo file va aggiornato ogni volta che cambia il flusso promo in vendita o la logica di applicazione.

## Stato attuale

- la promo usa una regola premio locale separata dalla campagna `cartafedelta`
- la configurazione locale supporta piu` regole premio per la stessa campagna
- il tipo premio distingue `Sconto fisso`, `Sconto percentuale` e `Articolo premio`
- il cliente generico viene bloccato senza popup
- il cliente senza `Codicecartafedelta` non è agganciato ai punti e non deve mostrare stato premio
- il Banco mostra uno stato promo chiaro legato al diritto al premio
- il popup compare solo sulla transizione reale `NotEligible -> Eligible`
- lo sconto promo viene applicato come riga tecnica `PremioSconto`
- l'articolo premio viene applicato come riga tecnica `PremioArticolo`
- esiste storico eventi promo locale nel LocalStore

## Regole fissate

- la promo non si applica se il cliente e` generico o non identificato
- la promo non si applica se il cliente non ha un codice carta fedeltà valorizzato nel legacy
- la promo deve essere valutata sul cliente corrente, sulla campagna attiva e sull'insieme delle regole premio locali attive
- l'applicazione deve essere reversibile
- lo storico promo deve vivere nel nuovo gestionale / LocalStore
- non usare un articolo fittizio come logica di business centrale
- non introdurre nuove tabelle o nuovi campi nel `db_diltech`
- nessun popup promo senza configurazione completa e soglia realmente raggiunta
- nessun popup al solo cambio cliente
- il popup deve proporre la regola piu` alta realmente raggiunta dal cliente sul documento corrente
- se l'operatore rifiuta il premio, il popup non deve ripetersi finche' non cambia lo stato di eleggibilita`
- se il documento torna sotto soglia dopo applicazione, va eseguito reversal coerente

## Integrazione Banco

- il Banco rivaluta la promo quando cambiano cliente, righe o totale documento
- se il cliente e` generico va mostrato solo lo stato bloccato
- il Banco usa gli stati evento: `NotEligible`, `Eligible`, `Applied`, `Rejected`, `Reversed`
- il popup e` una conferma di business separata dai popup tecnici di lavorazione
- la valutazione promo deve restare allineata con campagna attiva, regole premio locali e stato del documento
- la riga premio tecnica conserva il riferimento alla regola locale che l'ha generata, cosi` da poter evitare duplicazioni e gestire il reversal
- il blocco punti/premio nel Banco compare solo dopo conferma cliente e solo se il cliente ha assegnazione reale alla raccolta punti
- i clienti senza assegnazione alla raccolta punti non mostrano il riquadro promo, cosi` da non anticipare l'applicazione del premio
- il pulsante `Storico` del Banco apre la finestra modale storico acquisti gia` esistente, con lista documenti, preview e filtro data

## Checklist

- [x] cliente generico bloccato
- [x] popup solo su soglia raggiunta
- [x] riga sconto tecnica applicata
- [x] riga articolo premio applicata
- [x] sconto percentuale disponibile come tipo premio locale
- [x] card promo visibile nella scheda cliente
- [x] popup conferma promo in vendita
- [x] annullo/reversal promo
- [x] ledger storico promo
- [x] gestione promo multiple lato configurazione locale
- [x] blocco promo visibile solo dopo conferma cliente
- [ ] test flusso vendita

## Prossimi passi

- rifinire eventuali dettagli UI del riquadro promo Banco
- collegare in futuro l'eventuale documento gestionale ufficiale agli eventi promo locali dopo fiscalizzazione
- valutare la gestione di promo multiple solo dopo consolidamento del flusso singolo

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
