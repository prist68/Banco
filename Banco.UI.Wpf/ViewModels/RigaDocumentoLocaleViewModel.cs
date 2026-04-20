using Banco.Core.Domain.Entities;

namespace Banco.UI.Wpf.ViewModels;

public sealed class RigaDocumentoLocaleViewModel : ViewModelBase
{
    private readonly Action<RigaDocumentoLocaleViewModel, string>? _onValueChanged;
    private bool _isInReorderList;

    public RigaDocumentoLocaleViewModel(
        RigaDocumentoLocale model,
        Action<RigaDocumentoLocaleViewModel, string>? onValueChanged = null)
    {
        Model = model;
        _onValueChanged = onValueChanged;
    }

    public RigaDocumentoLocale Model { get; }

    public Guid Id => Model.Id;

    public int OrdineRiga => Model.OrdineRiga;

    public string CodiceArticolo => string.IsNullOrWhiteSpace(Model.CodiceArticolo) ? string.Empty : Model.CodiceArticolo!;

    public string UnitaMisura
    {
        get => string.IsNullOrWhiteSpace(Model.UnitaMisura) ? "PZ" : Model.UnitaMisura;
        set
        {
            if (Model.IsPromoRow)
            {
                return;
            }

            var normalizedValue = string.IsNullOrWhiteSpace(value) ? "PZ" : value.Trim().ToUpperInvariant();
            if (string.Equals(UnitaMisura, normalizedValue, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Model.UnitaMisura = normalizedValue;
            NotifyPropertyChanged();
            _onValueChanged?.Invoke(this, nameof(UnitaMisura));
        }
    }

    public string Descrizione
    {
        get => Model.Descrizione;
        set
        {
            if (Model.IsPromoRow)
            {
                return;
            }

            var normalizedValue = value?.Trim() ?? string.Empty;
            if (Model.Descrizione == normalizedValue)
            {
                return;
            }

            Model.Descrizione = normalizedValue;
            NotifyPropertyChanged();
            _onValueChanged?.Invoke(this, nameof(Descrizione));
        }
    }

    public decimal Quantita
    {
        get => Model.Quantita;
        set
        {
            if (Model.IsPromoRow)
            {
                return;
            }

            var normalizedValue = value <= 0 ? 1 : value;
            if (Model.Quantita == normalizedValue)
            {
                return;
            }

            Model.Quantita = normalizedValue;
            NotifyPropertyChanged();
            NotifyPropertyChanged(nameof(ImportoRiga));
            NotifyPropertyChanged(nameof(DisponibilitaResidua));
            _onValueChanged?.Invoke(this, nameof(Quantita));
        }
    }

    public decimal DisponibilitaRiferimento => Model.DisponibilitaRiferimento;

    public decimal DisponibilitaResidua => Model.DisponibilitaRiferimento;

    public decimal PrezzoUnitario
    {
        get => Model.PrezzoUnitario;
        set
        {
            if (Model.IsPromoRow)
            {
                return;
            }

            var normalizedValue = value < 0 ? 0 : value;
            if (Model.PrezzoUnitario == normalizedValue)
            {
                return;
            }

            Model.PrezzoUnitario = normalizedValue;
            NotifyPropertyChanged();
            NotifyPropertyChanged(nameof(ImportoRiga));
            _onValueChanged?.Invoke(this, nameof(PrezzoUnitario));
        }
    }

    public decimal ScontoPercentuale
    {
        get => Model.ScontoPercentuale;
        set
        {
            if (Model.IsPromoRow)
            {
                return;
            }

            var normalizedValue = value < 0 ? 0 : value;
            if (Model.ScontoPercentuale == normalizedValue)
            {
                return;
            }

            Model.ScontoPercentuale = normalizedValue;
            NotifyPropertyChanged();
            NotifyPropertyChanged(nameof(ImportoRiga));
            _onValueChanged?.Invoke(this, nameof(ScontoPercentuale));
        }
    }

    public decimal ImportoRiga => Model.ImportoRiga;

    public int IvaOid => Model.IvaOid;

    public string IvaLabel => Model.IvaOid > 0 ? $"Iva {Model.IvaOid}" : "-";

    public decimal Sconto1 => Model.Sconto1;

    public string TipoRigaLabel => Model.TipoRiga switch
    {
        Banco.Core.Domain.Enums.TipoRigaDocumento.Manuale => "Manuale",
        Banco.Core.Domain.Enums.TipoRigaDocumento.PremioSconto => "Premio sconto",
        Banco.Core.Domain.Enums.TipoRigaDocumento.PremioArticolo => "Premio articolo",
        _ => "Articolo"
    };

    public bool IsInReorderList
    {
        get => _isInReorderList;
        set => SetProperty(ref _isInReorderList, value);
    }

    public void NotifyMetadataChanged()
    {
        NotifyPropertyChanged(nameof(CodiceArticolo));
        NotifyPropertyChanged(nameof(Descrizione));
        NotifyPropertyChanged(nameof(UnitaMisura));
        NotifyPropertyChanged(nameof(TipoRigaLabel));
        _onValueChanged?.Invoke(this, nameof(Descrizione));
    }

    public void NotifyPricingChanged()
    {
        NotifyPropertyChanged(nameof(UnitaMisura));
        NotifyPropertyChanged(nameof(Quantita));
        NotifyPropertyChanged(nameof(PrezzoUnitario));
        NotifyPropertyChanged(nameof(ImportoRiga));
        NotifyPropertyChanged(nameof(DisponibilitaResidua));
    }
}
