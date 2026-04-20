using System.Collections.ObjectModel;
using Banco.Vendita.Abstractions;
using Banco.Vendita.Documents;

namespace Banco.UI.Wpf.ViewModels;

/// <summary>
/// ViewModel per la finestra modale storico acquisti di un cliente.
/// Creato ad-hoc da BancoViewModel, non registrato in DI.
/// </summary>
public sealed class StoricoAcquistiViewModel : DateFilterViewModelBase
{
    private readonly IGestionaleDocumentReadService _documentReadService;

    private bool _isLoading;
    private string _filtroArticolo = string.Empty;
    private StoricoDocumentoItem? _documentoSelezionato;

    public StoricoAcquistiViewModel(IGestionaleDocumentReadService documentReadService)
    {
        _documentReadService = documentReadService;

        CercaCommand = new RelayCommand(() => _ = CaricaDocumentiAsync());
        ResetFiltriCommand = new RelayCommand(ResetFiltri);

        // Anni disponibili: da 5 anni fa ad oggi
        var annoCorrente = DateTime.Now.Year;
        var oggi = DateTime.Today;
        SetInitialDateFilterState(
            new DateTime(oggi.Year, oggi.Month, 1),
            new DateTime(oggi.Year, oggi.Month, DateTime.DaysInMonth(oggi.Year, oggi.Month)),
            oggi.Month,
            annoCorrente);
    }

    public int SoggettoOid { get; init; }

    public string ClienteNominativo { get; init; } = string.Empty;

    public string TitoloFinestra => $"Storico acquisti — {ClienteNominativo}";

    public ObservableCollection<StoricoDocumentoItem> Documenti { get; } = [];

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string FiltroArticolo
    {
        get => _filtroArticolo;
        set
        {
            if (SetProperty(ref _filtroArticolo, value))
            {
                ScheduleRefresh();
            }
        }
    }

    public StoricoDocumentoItem? DocumentoSelezionato
    {
        get => _documentoSelezionato;
        set
        {
            if (SetProperty(ref _documentoSelezionato, value) && value is not null)
            {
                _ = CaricaDettaglioItemAsync(value);
            }
        }
    }

    public RelayCommand CercaCommand { get; }

    public RelayCommand ResetFiltriCommand { get; }

    public async Task CaricaDocumentiAsync()
    {
        if (SoggettoOid <= 0)
        {
            return;
        }

        IsLoading = true;

        try
        {
            var risultati = await _documentReadService.GetCustomerDocumentsAsync(
                SoggettoOid,
                DataInizio,
                DataFine,
                string.IsNullOrWhiteSpace(FiltroArticolo) ? null : FiltroArticolo);

            Documenti.Clear();
            foreach (var doc in risultati)
            {
                Documenti.Add(new StoricoDocumentoItem(doc));
            }
        }
        catch
        {
            Documenti.Clear();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task CaricaDettaglioItemAsync(StoricoDocumentoItem item)
    {
        if (item.IsDettaglioCaricato || item.IsCaricamentoDettaglio)
        {
            return;
        }

        item.IsCaricamentoDettaglio = true;

        try
        {
            var dettaglio = await _documentReadService.GetDocumentDetailAsync(item.Oid);
            if (dettaglio is not null)
            {
                item.CaricaRighe(dettaglio.Righe);
            }
        }
        catch
        {
            // Errore silenzioso: le righe restano vuote
        }
        finally
        {
            item.IsCaricamentoDettaglio = false;
        }
    }

    private void ResetFiltri()
    {
        var oggi = DateTime.Today;
        SetInitialDateFilterState(
            new DateTime(oggi.Year, oggi.Month, 1),
            new DateTime(oggi.Year, oggi.Month, DateTime.DaysInMonth(oggi.Year, oggi.Month)),
            oggi.Month,
            oggi.Year);
        FiltroArticolo = string.Empty;
        ScheduleRefresh(0);
    }

    protected override async Task RefreshOnFiltersChangedAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        await CaricaDocumentiAsync();
    }
}

/// <summary>
/// Wrapper per un documento nello storico, con caricamento lazy delle righe.
/// </summary>
public sealed class StoricoDocumentoItem : ViewModelBase
{
    private bool _isDettaglioCaricato;
    private bool _isCaricamentoDettaglio;
    private bool _isEspanso;

    public StoricoDocumentoItem(GestionaleDocumentSummary summary)
    {
        Oid = summary.Oid;
        DocumentoLabel = summary.DocumentoLabel;
        Data = summary.Data;
        OraVenditaLabel = summary.OraVenditaLabel;
        TotaleDocumento = summary.TotaleDocumento;
        StatoLabel = summary.StatoLabel;
        IsScontrinato = summary.ScontrinoNumero.HasValue;
        Operatore = summary.Operatore;
    }

    public int Oid { get; }
    public string DocumentoLabel { get; }
    public DateTime Data { get; }
    public string OraVenditaLabel { get; }
    public decimal TotaleDocumento { get; }
    public string StatoLabel { get; }
    public bool IsScontrinato { get; }
    public string Operatore { get; }

    public ObservableCollection<GestionaleDocumentRowDetail> Righe { get; } = [];

    public bool IsDettaglioCaricato
    {
        get => _isDettaglioCaricato;
        set => SetProperty(ref _isDettaglioCaricato, value);
    }

    public bool IsCaricamentoDettaglio
    {
        get => _isCaricamentoDettaglio;
        set => SetProperty(ref _isCaricamentoDettaglio, value);
    }

    public bool IsEspanso
    {
        get => _isEspanso;
        set => SetProperty(ref _isEspanso, value);
    }

    public void CaricaRighe(IReadOnlyList<GestionaleDocumentRowDetail> righe)
    {
        Righe.Clear();
        foreach (var riga in righe)
        {
            Righe.Add(riga);
        }

        IsDettaglioCaricato = true;
    }
}
