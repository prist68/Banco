namespace Banco.Magazzino.ViewModels;

public sealed class LegacyOfferRowViewModel : ViewModelBase
{
    private string _quantitaMinimaText = "1";
    private string _prezzoNettoText = string.Empty;
    private string _prezzoIvatoText = string.Empty;
    private string _dataFineText = string.Empty;

    public LegacyOfferRowViewModel(
        string listinoLabel,
        string varianteLabel)
    {
        ListinoLabel = listinoLabel;
        VarianteLabel = varianteLabel;
    }

    public string ListinoLabel { get; }

    public string TipoRigaLabel { get; init; } = string.Empty;

    public string VarianteLabel { get; }

    public string ScopeLabel { get; init; } = string.Empty;

    public decimal UltimoCostoLegacy { get; init; }

    public bool IsBasePriceRow { get; init; }

    public bool CanEditTierValues { get; init; }

    public int? VarianteDettaglioOid1 { get; init; }

    public int? VarianteDettaglioOid2 { get; init; }

    public bool MatchesCurrentVariantScope { get; init; }

    public string QuantitaMinimaText
    {
        get => _quantitaMinimaText;
        set
        {
            if (SetProperty(ref _quantitaMinimaText, value))
            {
                RaiseComputedPropertiesChanged();
            }
        }
    }

    public string PrezzoNettoText
    {
        get => _prezzoNettoText;
        set
        {
            if (SetProperty(ref _prezzoNettoText, value))
            {
                RaiseComputedPropertiesChanged();
            }
        }
    }

    public string PrezzoIvatoText
    {
        get => _prezzoIvatoText;
        set
        {
            if (SetProperty(ref _prezzoIvatoText, value))
            {
                RaiseComputedPropertiesChanged();
            }
        }
    }

    public string DataFineText
    {
        get => _dataFineText;
        set => SetProperty(ref _dataFineText, value);
    }

    public decimal QuantitaMinima => ParseDecimal(QuantitaMinimaText, 1m);

    public decimal PrezzoNetto => ParseDecimal(PrezzoNettoText, 0m);

    public decimal PrezzoIvato => ParseDecimal(PrezzoIvatoText, 0m);

    public decimal MarginePercentuale =>
        PrezzoNetto <= 0 || UltimoCostoLegacy <= 0
            ? 0
            : decimal.Round(((PrezzoNetto - UltimoCostoLegacy) / UltimoCostoLegacy) * 100m, 2, MidpointRounding.AwayFromZero);

    public decimal RicaricoPercentuale =>
        UltimoCostoLegacy <= 0 || PrezzoNetto <= 0
            ? 0
            : decimal.Round(((PrezzoNetto - UltimoCostoLegacy) / PrezzoNetto) * 100m, 2, MidpointRounding.AwayFromZero);

    public decimal TotaleIvato => QuantitaMinima <= 0 || PrezzoIvato <= 0
        ? 0
        : decimal.Round(QuantitaMinima * PrezzoIvato, 2, MidpointRounding.AwayFromZero);

    public bool IsValid => QuantitaMinima > 1 && PrezzoNetto > 0 && PrezzoIvato > 0;

    public bool CanEditQuantitaMinima => CanEditTierValues && !IsBasePriceRow;

    public bool CanEditPrezzoNetto => CanEditTierValues && !IsBasePriceRow;

    public bool CanEditPrezzoIvato => CanEditTierValues && !IsBasePriceRow;

    public bool CanEditDataFine => CanEditTierValues && !IsBasePriceRow;

    public string TotaleIvatoLabel => TotaleIvato <= 0 ? "-" : TotaleIvato.ToString("0.00");

    public string PrezzoNettoLabel => PrezzoNetto <= 0 ? "-" : PrezzoNetto.ToString("0.0000");

    public string UltimoCostoLabel => UltimoCostoLegacy <= 0 ? "-" : UltimoCostoLegacy.ToString("0.0000");

    public string MargineLabel => $"{MarginePercentuale:0.##}%";

    public string RicaricoLabel => $"{RicaricoPercentuale:0.##}%";

    public string VarianteDisplayLabel => string.IsNullOrWhiteSpace(VarianteLabel) ? "Articolo base" : VarianteLabel;

    public DateTime? DataFine =>
        DateTime.TryParseExact(
            DataFineText,
            "dd/MM/yyyy",
            System.Globalization.CultureInfo.GetCultureInfo("it-IT"),
            System.Globalization.DateTimeStyles.None,
            out var parsed)
            ? parsed
            : null;

    public string DataFineLabel => string.IsNullOrWhiteSpace(DataFineText) ? "-" : DataFineText;

    public void NotifyComputedValuesChanged() => RaiseComputedPropertiesChanged();

    private void RaiseComputedPropertiesChanged()
    {
        NotifyPropertyChanged(nameof(QuantitaMinima));
        NotifyPropertyChanged(nameof(PrezzoNetto));
        NotifyPropertyChanged(nameof(PrezzoIvato));
        NotifyPropertyChanged(nameof(MarginePercentuale));
        NotifyPropertyChanged(nameof(RicaricoPercentuale));
        NotifyPropertyChanged(nameof(TotaleIvato));
        NotifyPropertyChanged(nameof(TotaleIvatoLabel));
        NotifyPropertyChanged(nameof(PrezzoNettoLabel));
        NotifyPropertyChanged(nameof(UltimoCostoLabel));
        NotifyPropertyChanged(nameof(MargineLabel));
        NotifyPropertyChanged(nameof(RicaricoLabel));
        NotifyPropertyChanged(nameof(IsValid));
        NotifyPropertyChanged(nameof(CanEditQuantitaMinima));
        NotifyPropertyChanged(nameof(CanEditPrezzoNetto));
        NotifyPropertyChanged(nameof(CanEditPrezzoIvato));
        NotifyPropertyChanged(nameof(CanEditDataFine));
    }

    private static decimal ParseDecimal(string? value, decimal fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalized = value.Trim().Replace(',', '.');
        return decimal.TryParse(normalized, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }
}
