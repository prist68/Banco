namespace Banco.UI.Wpf.ViewModels;

public abstract class DateFilterViewModelBase : ViewModelBase
{
    private CancellationTokenSource? _refreshCts;
    private bool _isInitializingFilters;
    private bool _isSyncingDateRange;
    private DateTime? _dataInizio;
    private DateTime? _dataFine;
    private int? _meseSelezionato;
    private int? _annoSelezionato;

    protected DateFilterViewModelBase()
    {
        var annoCorrente = DateTime.Today.Year;
        AnniDisponibili = Enumerable.Range(annoCorrente - 5, 7).Reverse().ToArray();
    }

    public int[] AnniDisponibili { get; }

    public DateTime? DataInizio
    {
        get => _dataInizio;
        set
        {
            if (!SetProperty(ref _dataInizio, value))
            {
                return;
            }

            if (_isSyncingDateRange || _isInitializingFilters)
            {
                return;
            }

            AllineaMeseAnnoDalRange();
            ScheduleRefresh();
        }
    }

    public DateTime? DataFine
    {
        get => _dataFine;
        set
        {
            if (!SetProperty(ref _dataFine, value))
            {
                return;
            }

            if (_isSyncingDateRange || _isInitializingFilters)
            {
                return;
            }

            AllineaMeseAnnoDalRange();
            ScheduleRefresh();
        }
    }

    public int? MeseSelezionato
    {
        get => _meseSelezionato;
        set
        {
            if (!SetProperty(ref _meseSelezionato, value))
            {
                return;
            }

            if (_isInitializingFilters)
            {
                return;
            }

            AggiornaRangeDaMeseAnno();
            ScheduleRefresh();
        }
    }

    public int? AnnoSelezionato
    {
        get => _annoSelezionato;
        set
        {
            if (!SetProperty(ref _annoSelezionato, value))
            {
                return;
            }

            if (_isInitializingFilters)
            {
                return;
            }

            AggiornaRangeDaMeseAnno();
            ScheduleRefresh();
        }
    }

    public bool HasIntervalloValido => !DataInizio.HasValue || !DataFine.HasValue || DataInizio.Value.Date <= DataFine.Value.Date;

    protected void SetInitialDateFilterState(DateTime? dataInizio, DateTime? dataFine, int? mese, int? anno)
    {
        _isInitializingFilters = true;
        try
        {
            _dataInizio = dataInizio;
            _dataFine = dataFine;
            _meseSelezionato = mese;
            _annoSelezionato = anno;
        }
        finally
        {
            _isInitializingFilters = false;
        }

        NotifyPropertyChanged(nameof(DataInizio));
        NotifyPropertyChanged(nameof(DataFine));
        NotifyPropertyChanged(nameof(MeseSelezionato));
        NotifyPropertyChanged(nameof(AnnoSelezionato));
        NotifyPropertyChanged(nameof(HasIntervalloValido));
    }

    protected void ScheduleRefresh(int delayMilliseconds = 250)
    {
        NotifyPropertyChanged(nameof(HasIntervalloValido));

        _refreshCts?.Cancel();
        _refreshCts?.Dispose();

        var cts = new CancellationTokenSource();
        _refreshCts = cts;
        _ = ScheduleRefreshCoreAsync(delayMilliseconds, cts.Token);
    }

    protected void AggiornaRangeDaMeseAnno()
    {
        _isSyncingDateRange = true;
        try
        {
            if (_annoSelezionato.HasValue && _meseSelezionato.HasValue)
            {
                _dataInizio = new DateTime(_annoSelezionato.Value, _meseSelezionato.Value, 1);
                _dataFine = _dataInizio.Value.AddMonths(1).AddDays(-1);
            }
            else if (_annoSelezionato.HasValue && !_meseSelezionato.HasValue)
            {
                _dataInizio = new DateTime(_annoSelezionato.Value, 1, 1);
                _dataFine = new DateTime(_annoSelezionato.Value, 12, 31);
            }
            else
            {
                _dataInizio = null;
                _dataFine = null;
            }
        }
        finally
        {
            _isSyncingDateRange = false;
        }

        NotifyPropertyChanged(nameof(DataInizio));
        NotifyPropertyChanged(nameof(DataFine));
        NotifyPropertyChanged(nameof(HasIntervalloValido));
    }

    private void AllineaMeseAnnoDalRange()
    {
        _isSyncingDateRange = true;
        try
        {
            if (!_dataInizio.HasValue || !_dataFine.HasValue)
            {
                _meseSelezionato = null;
                _annoSelezionato = _dataInizio?.Year ?? _dataFine?.Year;
                return;
            }

            var inizio = _dataInizio.Value.Date;
            var fine = _dataFine.Value.Date;
            if (inizio > fine)
            {
                _meseSelezionato = null;
                _annoSelezionato = null;
                return;
            }

            var primoGiornoMese = new DateTime(inizio.Year, inizio.Month, 1);
            var ultimoGiornoMese = primoGiornoMese.AddMonths(1).AddDays(-1);
            if (inizio == primoGiornoMese && fine == ultimoGiornoMese)
            {
                _annoSelezionato = inizio.Year;
                _meseSelezionato = inizio.Month;
                return;
            }

            if (inizio == new DateTime(inizio.Year, 1, 1) && fine == new DateTime(inizio.Year, 12, 31))
            {
                _annoSelezionato = inizio.Year;
                _meseSelezionato = null;
                return;
            }

            _annoSelezionato = null;
            _meseSelezionato = null;
        }
        finally
        {
            _isSyncingDateRange = false;
        }

        NotifyPropertyChanged(nameof(MeseSelezionato));
        NotifyPropertyChanged(nameof(AnnoSelezionato));
        NotifyPropertyChanged(nameof(HasIntervalloValido));
    }

    private async Task ScheduleRefreshCoreAsync(int delayMilliseconds, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delayMilliseconds, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await RefreshOnFiltersChangedAsync(cancellationToken);
        }
        catch (TaskCanceledException)
        {
            // Il filtro e` cambiato di nuovo: la richiesta precedente non serve piu`.
        }
    }

    protected abstract Task RefreshOnFiltersChangedAsync(CancellationToken cancellationToken);
}
