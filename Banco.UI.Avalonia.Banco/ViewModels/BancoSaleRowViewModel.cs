using Banco.Core.Domain.Entities;
using Banco.Core.Domain.Enums;

namespace Banco.UI.Avalonia.Banco.ViewModels;

public sealed class BancoSaleRowViewModel : ViewModelBase
{
    private string _codice = string.Empty;
    private string _descrizione = string.Empty;
    private string _unitaMisura = "Pz";
    private decimal _disponibilita;
    private decimal _aliquotaIva;
    private string _stato = "Normale";
    private decimal _quantita;
    private decimal _prezzo;
    private decimal _sconto;
    private bool _isInReorderList;

    public BancoSaleRowViewModel()
        : this(new RigaDocumentoLocale
        {
            TipoRiga = TipoRigaDocumento.Manuale,
            Quantita = 1,
            UnitaMisura = "PZ",
            Descrizione = "Articolo manuale",
            FlagManuale = true
        })
    {
    }

    public BancoSaleRowViewModel(RigaDocumentoLocale model)
    {
        Model = model;
        _codice = model.CodiceArticolo ?? string.Empty;
        _descrizione = model.Descrizione;
        _unitaMisura = model.UnitaMisura;
        _disponibilita = model.DisponibilitaRiferimento;
        _aliquotaIva = model.AliquotaIva;
        _stato = model.FlagManuale ? "Manuale" : model.DisponibilitaRiferimento <= 0 ? "Disponibilita" : "Normale";
        _quantita = model.Quantita;
        _prezzo = model.PrezzoUnitario;
        _sconto = model.ScontoPercentuale;
    }

    public RigaDocumentoLocale Model { get; }

    public int? ArticoloOid
    {
        get => Model.ArticoloOid;
        set
        {
            if (Model.ArticoloOid == value)
            {
                return;
            }

            Model.ArticoloOid = value;
            OnPropertyChanged();
        }
    }

    public string Codice
    {
        get => _codice;
        set
        {
            if (SetProperty(ref _codice, value))
            {
                Model.CodiceArticolo = string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }
    }

    public string Descrizione
    {
        get => _descrizione;
        set
        {
            if (SetProperty(ref _descrizione, value))
            {
                Model.Descrizione = value;
            }
        }
    }

    public string UnitaMisura
    {
        get => _unitaMisura;
        set
        {
            if (SetProperty(ref _unitaMisura, value))
            {
                Model.UnitaMisura = value;
            }
        }
    }

    public decimal Disponibilita
    {
        get => _disponibilita;
        set
        {
            if (SetProperty(ref _disponibilita, value))
            {
                Model.DisponibilitaRiferimento = value;
            }
        }
    }

    public decimal AliquotaIva
    {
        get => _aliquotaIva;
        set
        {
            if (SetProperty(ref _aliquotaIva, value))
            {
                Model.AliquotaIva = value;
            }
        }
    }

    public string Stato
    {
        get => _stato;
        set => SetProperty(ref _stato, value);
    }

    public decimal Quantita
    {
        get => _quantita;
        set
        {
            if (SetProperty(ref _quantita, value))
            {
                Model.Quantita = value;
                OnPropertyChanged(nameof(Importo));
            }
        }
    }

    public decimal Prezzo
    {
        get => _prezzo;
        set
        {
            if (SetProperty(ref _prezzo, value))
            {
                Model.PrezzoUnitario = value;
                OnPropertyChanged(nameof(Importo));
            }
        }
    }

    public decimal Sconto
    {
        get => _sconto;
        set
        {
            if (SetProperty(ref _sconto, value))
            {
                Model.ScontoPercentuale = value;
                OnPropertyChanged(nameof(Importo));
            }
        }
    }

    public decimal Importo => Math.Round(Quantita * Prezzo * (1 - Sconto / 100), 2);

    public bool IsInReorderList
    {
        get => _isInReorderList;
        set => SetProperty(ref _isInReorderList, value);
    }
}
