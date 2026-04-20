using Banco.Riordino;
using System.Collections.ObjectModel;

namespace Banco.UI.Wpf.ViewModels;

public sealed class ReorderGridRowViewModel : ViewModelBase
{
    private bool _isOrdered;
    private bool _isActionSelected;
    private ReorderSupplierOptionViewModel? _selectedSupplier;
    private decimal _quantitaDaOrdinare;
    private string _fornitoreSuggerito = string.Empty;
    private string _fornitoreSelezionato = string.Empty;
    private decimal? _prezzoSuggerito;

    public Guid Id { get; init; }

    public string CodiceArticolo { get; init; } = string.Empty;

    public string Descrizione { get; init; } = string.Empty;

    public decimal Quantita { get; init; }

    public decimal QuantitaDaOrdinare
    {
        get => _quantitaDaOrdinare;
        set => SetProperty(ref _quantitaDaOrdinare, value);
    }

    public string UnitaMisura { get; init; } = string.Empty;

    public string FornitoreSuggerito
    {
        get => _fornitoreSuggerito;
        set
        {
            if (SetProperty(ref _fornitoreSuggerito, value))
            {
                NotifyPropertyChanged(nameof(FornitoreGruppoLabel));
            }
        }
    }

    public string FornitoreSelezionato
    {
        get => _fornitoreSelezionato;
        set
        {
            if (SetProperty(ref _fornitoreSelezionato, value))
            {
                NotifyPropertyChanged(nameof(FornitoreSelezionatoLabel));
                NotifyPropertyChanged(nameof(FornitoreGruppoLabel));
            }
        }
    }

    public int? FornitoreSuggeritoOid { get; init; }

    public int? FornitoreSelezionatoOid { get; init; }

    public decimal? PrezzoSuggerito
    {
        get => _prezzoSuggerito;
        set => SetProperty(ref _prezzoSuggerito, value);
    }

    public int IvaOid { get; init; }

    public string Motivo { get; init; } = string.Empty;

    public string Operatore { get; init; } = string.Empty;

    public string Note { get; init; } = string.Empty;

    public DateTime DataInserimento { get; init; }

    public bool IsOrdered
    {
        get => _isOrdered;
        set => SetProperty(ref _isOrdered, value);
    }

    public bool IsActionSelected
    {
        get => _isActionSelected;
        set => SetProperty(ref _isActionSelected, value);
    }

    public string StatoLabel => IsOrdered ? "Confermato" : "Da confermare";

    public int? ArticoloOid { get; init; }

    public ObservableCollection<ReorderSupplierOptionViewModel> SupplierOptions { get; } = [];

    public bool CanChangeSupplier => SupplierOptions.Count > 0;

    public ReorderSupplierOptionViewModel? SelectedSupplier
    {
        get => _selectedSupplier;
        set
        {
            if (SetProperty(ref _selectedSupplier, value))
            {
                NotifyPropertyChanged(nameof(FornitoreSelezionatoLabel));
                NotifyPropertyChanged(nameof(FornitoreGruppoLabel));
            }
        }
    }

    public string FornitoreSelezionatoLabel => SelectedSupplier?.Nome
        ?? FornitoreSelezionato;

    public string FornitoreGruppoLabel =>
        !string.IsNullOrWhiteSpace(FornitoreSelezionatoLabel)
            ? FornitoreSelezionatoLabel
            : (!string.IsNullOrWhiteSpace(FornitoreSuggerito) ? FornitoreSuggerito : "Senza fornitore");

    public int? FornitoreGruppoOid => SelectedSupplier?.Oid
        ?? FornitoreSelezionatoOid
        ?? FornitoreSuggeritoOid;

    public void SetSupplierOptions(IEnumerable<ReorderSupplierOptionViewModel> options)
    {
        SupplierOptions.Clear();
        foreach (var option in options)
        {
            SupplierOptions.Add(option);
        }

        SelectedSupplier = SupplierOptions.FirstOrDefault(option =>
                               !string.IsNullOrWhiteSpace(FornitoreSelezionato) &&
                               string.Equals(option.Nome, FornitoreSelezionato, StringComparison.OrdinalIgnoreCase))
                           ?? SupplierOptions.FirstOrDefault(option =>
                               !string.IsNullOrWhiteSpace(FornitoreSuggerito) &&
                               string.Equals(option.Nome, FornitoreSuggerito, StringComparison.OrdinalIgnoreCase))
                           ?? SupplierOptions.FirstOrDefault();

        if (SelectedSupplier is not null)
        {
            // Se la riga locale nasce senza fornitore/prezzo, usiamo subito il legacy.
            if (string.IsNullOrWhiteSpace(FornitoreSuggerito))
            {
                FornitoreSuggerito = SelectedSupplier.Nome;
            }

            if (string.IsNullOrWhiteSpace(FornitoreSelezionato))
            {
                FornitoreSelezionato = SelectedSupplier.Nome;
            }

            if ((!PrezzoSuggerito.HasValue || PrezzoSuggerito.Value <= 0) &&
                SelectedSupplier.PrezzoRiferimento > 0)
            {
                PrezzoSuggerito = SelectedSupplier.PrezzoRiferimento;
            }
        }

        NotifyPropertyChanged(nameof(CanChangeSupplier));
        NotifyPropertyChanged(nameof(FornitoreSelezionatoLabel));
        NotifyPropertyChanged(nameof(FornitoreGruppoLabel));
    }

    public static ReorderGridRowViewModel FromModel(ReorderListItem item)
    {
        return new ReorderGridRowViewModel
        {
            Id = item.Id,
            ArticoloOid = item.ArticoloOid,
            CodiceArticolo = item.CodiceArticolo,
            Descrizione = item.Descrizione,
            Quantita = item.Quantita,
            QuantitaDaOrdinare = item.QuantitaDaOrdinare <= 0 ? item.Quantita : item.QuantitaDaOrdinare,
            UnitaMisura = item.UnitaMisura,
            FornitoreSuggerito = item.FornitoreSuggeritoNome,
            FornitoreSuggeritoOid = item.FornitoreSuggeritoOid,
            FornitoreSelezionato = string.IsNullOrWhiteSpace(item.FornitoreSelezionatoNome)
                ? item.FornitoreSuggeritoNome
                : item.FornitoreSelezionatoNome,
            FornitoreSelezionatoOid = item.FornitoreSelezionatoOid,
            PrezzoSuggerito = item.PrezzoSuggerito,
            IvaOid = item.IvaOid,
            Motivo = item.Motivo == ReorderReason.GiacenzaZero ? "Giacenza zero" : "Manuale",
            Operatore = item.Operatore,
            Note = item.Note,
            DataInserimento = item.CreatedAt.LocalDateTime,
            IsOrdered = item.Stato == ReorderItemStatus.Ordinato
        };
    }
}
