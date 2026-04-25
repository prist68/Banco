# Diario Modulo Punti / Promozioni

## Indice

Questo file resta come punto di accesso sintetico al lavoro sul modulo.
Il dettaglio operativo e` diviso nei due diari dedicati:

- `Doc/diario_modulo_punti.md`
- `Doc/diario_modulo_promozioni.md`

## Stato sintetico

- la parte punti/campagne e` gia` attiva e modificabile
- la parte promozioni e` ora integrata nel Banco con regola premio locale
- il cliente generico blocca la promo senza popup
- il popup promo compare solo sulla transizione reale a soglia raggiunta
- lo sconto promo viene gestito come riga tecnica `PremioSconto`
- l'articolo premio viene gestito come riga tecnica `PremioArticolo`
- il LocalStore mantiene configurazione premio, eventi promo e reversal
- la ricerca cliente e` in tempo reale mentre si digita
- il campo base calcolo e` ora testuale e non piu` un controllo ambiguo a selezione
- la maschera punti e` stata riordinata per separare campagna nativa, regola premio e riepilogo cliente

## Regola

Per ogni evoluzione del modulo:

- aggiorna il diario punti se cambia la maschera/campagna
- aggiorna il diario promozioni se cambia il flusso promo o vendita
- aggiorna il diario operativo generale solo quando chiudi un passaggio tecnico rilevante

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
