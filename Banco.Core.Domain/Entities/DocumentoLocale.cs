using Banco.Core.Domain.Enums;

namespace Banco.Core.Domain.Entities;

public sealed class DocumentoLocale
{
    private readonly List<RigaDocumentoLocale> _righe = [];
    private readonly List<PagamentoLocale> _pagamenti = [];
    private decimal _scontoDocumento;

    public Guid Id { get; init; } = Guid.NewGuid();

    public StatoDocumentoLocale Stato { get; private set; } = StatoDocumentoLocale.BozzaLocale;

    public DateTimeOffset DataCreazione { get; init; } = DateTimeOffset.Now;

    public DateTimeOffset DataUltimaModifica { get; private set; } = DateTimeOffset.Now;

    public string Operatore { get; set; } = string.Empty;

    public string Cliente { get; set; } = "Cliente generico";

    public int? ClienteOid { get; private set; }

    public int? ListinoOid { get; private set; }

    public string? ListinoNome { get; private set; }

    public string NoteOperative { get; set; } = string.Empty;

    public ModalitaChiusuraDocumento ModalitaChiusura { get; private set; } = ModalitaChiusuraDocumento.BozzaLocale;

    public CategoriaDocumentoBanco CategoriaDocumentoBanco { get; private set; } = CategoriaDocumentoBanco.Indeterminata;

    public bool HasComponenteSospeso { get; private set; }

    public StatoFiscaleBanco StatoFiscaleBanco { get; private set; } = StatoFiscaleBanco.Nessuno;

    public int? DocumentoGestionaleOid { get; private set; }

    public long? NumeroDocumentoGestionale { get; private set; }

    public int? AnnoDocumentoGestionale { get; private set; }

    public DateTime? DataDocumentoGestionale { get; private set; }

    public DateTimeOffset? DataPagamentoFinale { get; private set; }

    public DateTimeOffset? DataComandoFiscaleFinale { get; private set; }

    public decimal ScontoDocumento => _scontoDocumento;

    public IReadOnlyCollection<RigaDocumentoLocale> Righe => _righe;

    public IReadOnlyCollection<PagamentoLocale> Pagamenti => _pagamenti;

    public decimal TotaleDocumento => _righe.Sum(riga => riga.ImportoRiga);

    public decimal TotaleScontoLocale => ScontoDocumento;

    public decimal TotaleIncassatoLocale => SommaPagamentiPerTipo("contanti", "contante", "carta", "bancomat", "pos", "buoni", "buonipasto", "ticket");

    public decimal TotaleSospesoLocale => SommaPagamentiPerTipo("sospeso");

    public decimal TotaleDaIncassareLocale => Math.Max(0, TotaleDocumento - TotaleScontoLocale);

    public decimal TotalePagatoLocale => TotaleIncassatoLocale;

    public decimal Residuo => Math.Max(0, TotaleDaIncassareLocale - TotaleIncassatoLocale - TotaleSospesoLocale);

    public decimal Resto => Math.Max(0, TotaleIncassatoLocale - TotaleDaIncassareLocale);

    public static DocumentoLocale Reidrata(
        Guid id,
        StatoDocumentoLocale stato,
        DateTimeOffset dataCreazione,
        DateTimeOffset dataUltimaModifica,
        string operatore,
        string cliente,
        int? clienteOid,
        int? listinoOid,
        string? listinoNome,
        string noteOperative,
        ModalitaChiusuraDocumento modalitaChiusura,
        CategoriaDocumentoBanco categoriaDocumentoBanco,
        bool hasComponenteSospeso,
        StatoFiscaleBanco statoFiscaleBanco,
        decimal scontoDocumento,
        int? documentoGestionaleOid,
        long? numeroDocumentoGestionale,
        int? annoDocumentoGestionale,
        DateTime? dataDocumentoGestionale,
        DateTimeOffset? dataPagamentoFinale,
        DateTimeOffset? dataComandoFiscaleFinale,
        IEnumerable<RigaDocumentoLocale>? righe,
        IEnumerable<PagamentoLocale>? pagamenti)
    {
        var documento = new DocumentoLocale
        {
            Id = id,
            Stato = stato,
            DataCreazione = dataCreazione,
            DataUltimaModifica = dataUltimaModifica,
            Operatore = operatore,
            Cliente = cliente,
            ClienteOid = clienteOid,
            ListinoOid = listinoOid,
            ListinoNome = listinoNome,
            NoteOperative = noteOperative,
            ModalitaChiusura = modalitaChiusura,
            CategoriaDocumentoBanco = categoriaDocumentoBanco,
            HasComponenteSospeso = hasComponenteSospeso,
            StatoFiscaleBanco = statoFiscaleBanco,
            _scontoDocumento = Math.Max(0, scontoDocumento)
        };

        documento.DocumentoGestionaleOid = documentoGestionaleOid;
        documento.NumeroDocumentoGestionale = numeroDocumentoGestionale;
        documento.AnnoDocumentoGestionale = annoDocumentoGestionale;
        documento.DataDocumentoGestionale = dataDocumentoGestionale;
        documento.DataPagamentoFinale = dataPagamentoFinale;
        documento.DataComandoFiscaleFinale = dataComandoFiscaleFinale;

        if (righe is not null)
        {
            documento._righe.AddRange(righe.OrderBy(riga => riga.OrdineRiga));
        }

        if (pagamenti is not null)
        {
            documento._pagamenti.AddRange(pagamenti.OrderBy(pagamento => pagamento.DataOra));
        }

        return documento;
    }

    public void AggiungiRiga(RigaDocumentoLocale riga)
    {
        if (Stato is StatoDocumentoLocale.Fiscalizzato or StatoDocumentoLocale.Annullato)
        {
            throw new InvalidOperationException("Il documento non e` modificabile.");
        }

        _righe.Add(riga);
        DataUltimaModifica = DateTimeOffset.Now;
        Stato = StatoDocumentoLocale.Aperto;
        ModalitaChiusura = ModalitaChiusuraDocumento.BozzaLocale;
    }

    public void RimuoviRiga(Guid rigaId)
    {
        if (Stato is StatoDocumentoLocale.Fiscalizzato or StatoDocumentoLocale.Annullato)
        {
            throw new InvalidOperationException("Il documento non e` modificabile.");
        }

        var removed = _righe.RemoveAll(riga => riga.Id == rigaId);
        if (removed > 0)
        {
            DataUltimaModifica = DateTimeOffset.Now;
        }
    }

    public void AggiungiPagamento(PagamentoLocale pagamento)
    {
        if (Stato is StatoDocumentoLocale.Fiscalizzato or StatoDocumentoLocale.Annullato)
        {
            throw new InvalidOperationException("Il documento non e` modificabile.");
        }

        _pagamenti.Add(pagamento);
        DataUltimaModifica = DateTimeOffset.Now;
    }

    public void ImpostaScontoDocumento(decimal importo)
    {
        if (Stato is StatoDocumentoLocale.Fiscalizzato or StatoDocumentoLocale.Annullato)
        {
            throw new InvalidOperationException("Il documento non e` modificabile.");
        }

        var nuovoImporto = Math.Max(0, importo);
        if (_scontoDocumento == nuovoImporto)
        {
            return;
        }

        _scontoDocumento = nuovoImporto;
        DataUltimaModifica = DateTimeOffset.Now;
    }

    public void SostituisciPagamenti(IEnumerable<PagamentoLocale>? pagamenti)
    {
        _pagamenti.Clear();

        if (pagamenti is not null)
        {
            _pagamenti.AddRange(pagamenti.OrderBy(pagamento => pagamento.DataOra));
        }

        DataUltimaModifica = DateTimeOffset.Now;
    }

    public void AzzeraContenuto(string cliente = "Cliente generico", string? noteOperative = null)
    {
        if (Stato is StatoDocumentoLocale.Fiscalizzato or StatoDocumentoLocale.Annullato)
        {
            throw new InvalidOperationException("Il documento non e` modificabile.");
        }

        _righe.Clear();
        _pagamenti.Clear();
        _scontoDocumento = 0;
        Cliente = cliente;
        ClienteOid = null;
        ListinoOid = null;
        ListinoNome = null;
        NoteOperative = noteOperative ?? string.Empty;
        ModalitaChiusura = ModalitaChiusuraDocumento.BozzaLocale;
        CategoriaDocumentoBanco = CategoriaDocumentoBanco.Indeterminata;
        HasComponenteSospeso = false;
        StatoFiscaleBanco = StatoFiscaleBanco.Nessuno;
        DocumentoGestionaleOid = null;
        NumeroDocumentoGestionale = null;
        AnnoDocumentoGestionale = null;
        DataDocumentoGestionale = null;
        DataPagamentoFinale = null;
        DataComandoFiscaleFinale = null;
        Stato = StatoDocumentoLocale.BozzaLocale;
        DataUltimaModifica = DateTimeOffset.Now;
    }

    public void SegnaInChiusura()
    {
        if (Stato is StatoDocumentoLocale.Fiscalizzato or StatoDocumentoLocale.Annullato)
        {
            throw new InvalidOperationException("Il documento non e` modificabile.");
        }

        Stato = StatoDocumentoLocale.InChiusura;
        DataUltimaModifica = DateTimeOffset.Now;
    }

    public void Riapri()
    {
        if (Stato is StatoDocumentoLocale.Fiscalizzato or StatoDocumentoLocale.Annullato)
        {
            throw new InvalidOperationException("Il documento non e` modificabile.");
        }

        Stato = _righe.Count == 0 ? StatoDocumentoLocale.BozzaLocale : StatoDocumentoLocale.Aperto;
        DataUltimaModifica = DateTimeOffset.Now;
    }

    public void ImpostaCliente(int? clienteOid, string clienteLabel)
    {
        if (Stato is StatoDocumentoLocale.Fiscalizzato or StatoDocumentoLocale.Annullato)
        {
            throw new InvalidOperationException("Il documento non e` modificabile.");
        }

        ClienteOid = clienteOid;
        Cliente = clienteLabel;
        DataUltimaModifica = DateTimeOffset.Now;
    }

    public void ImpostaListino(int? listinoOid, string? listinoNome)
    {
        if (Stato is StatoDocumentoLocale.Fiscalizzato or StatoDocumentoLocale.Annullato)
        {
            throw new InvalidOperationException("Il documento non e` modificabile.");
        }

        ListinoOid = listinoOid;
        ListinoNome = string.IsNullOrWhiteSpace(listinoNome) ? null : listinoNome.Trim();
        DataUltimaModifica = DateTimeOffset.Now;
    }

    public void AggiornaMetadatiBanco(
        CategoriaDocumentoBanco categoriaDocumentoBanco,
        bool hasComponenteSospeso,
        DateTimeOffset? dataPagamentoFinale)
    {
        if (Stato == StatoDocumentoLocale.Annullato)
        {
            throw new InvalidOperationException("Il documento non e` modificabile.");
        }

        CategoriaDocumentoBanco = categoriaDocumentoBanco;
        HasComponenteSospeso = hasComponenteSospeso;
        DataPagamentoFinale = dataPagamentoFinale;
        ModalitaChiusura = categoriaDocumentoBanco switch
        {
            CategoriaDocumentoBanco.Cortesia => ModalitaChiusuraDocumento.Cortesia,
            CategoriaDocumentoBanco.Scontrino => ModalitaChiusuraDocumento.Scontrino,
            _ => ModalitaChiusura
        };
        DataUltimaModifica = DateTimeOffset.Now;
    }

    public void SegnaPubblicatoLegacy(
        int documentoGestionaleOid,
        long numeroDocumentoGestionale,
        int annoDocumentoGestionale,
        DateTime dataDocumentoGestionale,
        CategoriaDocumentoBanco categoriaDocumentoBanco,
        bool hasComponenteSospeso,
        DateTimeOffset? dataPagamentoFinale)
    {
        if (Stato == StatoDocumentoLocale.Annullato)
        {
            throw new InvalidOperationException("Il documento non e` modificabile.");
        }

        DocumentoGestionaleOid = documentoGestionaleOid;
        NumeroDocumentoGestionale = numeroDocumentoGestionale;
        AnnoDocumentoGestionale = annoDocumentoGestionale;
        DataDocumentoGestionale = dataDocumentoGestionale;
        CategoriaDocumentoBanco = categoriaDocumentoBanco;
        HasComponenteSospeso = hasComponenteSospeso;
        DataPagamentoFinale = dataPagamentoFinale;
        ModalitaChiusura = categoriaDocumentoBanco switch
        {
            CategoriaDocumentoBanco.Cortesia => ModalitaChiusuraDocumento.Cortesia,
            CategoriaDocumentoBanco.Scontrino => ModalitaChiusuraDocumento.Scontrino,
            _ => ModalitaChiusuraDocumento.PubblicazioneUfficiale
        };
        StatoFiscaleBanco = categoriaDocumentoBanco switch
        {
            CategoriaDocumentoBanco.Scontrino => StatoFiscaleBanco.FiscalizzazioneWinEcrRichiesta,
            _ => StatoFiscaleBanco.PubblicatoLegacyNonFiscalizzato
        };
        Stato = categoriaDocumentoBanco == CategoriaDocumentoBanco.Scontrino
            ? StatoDocumentoLocale.InChiusura
            : StatoDocumentoLocale.ParzialmenteFiscalizzato;
        DataUltimaModifica = DateTimeOffset.Now;
    }

    public void SegnaFiscalizzazioneWinEcrCompletata(DateTimeOffset dataComandoFiscaleFinale)
    {
        if (Stato == StatoDocumentoLocale.Annullato)
        {
            throw new InvalidOperationException("Il documento non e` modificabile.");
        }

        CategoriaDocumentoBanco = CategoriaDocumentoBanco.Scontrino;
        ModalitaChiusura = ModalitaChiusuraDocumento.Scontrino;
        StatoFiscaleBanco = StatoFiscaleBanco.FiscalizzazioneWinEcrCompletata;
        DataComandoFiscaleFinale = dataComandoFiscaleFinale;
        Stato = StatoDocumentoLocale.Fiscalizzato;
        DataUltimaModifica = DateTimeOffset.Now;
    }

    public void SegnaFiscalizzazioneWinEcrFallita(DateTimeOffset dataComandoFiscaleFinale)
    {
        if (Stato == StatoDocumentoLocale.Annullato)
        {
            throw new InvalidOperationException("Il documento non e` modificabile.");
        }

        CategoriaDocumentoBanco = CategoriaDocumentoBanco.Scontrino;
        ModalitaChiusura = ModalitaChiusuraDocumento.Scontrino;
        StatoFiscaleBanco = StatoFiscaleBanco.FiscalizzazioneWinEcrFallita;
        DataComandoFiscaleFinale = dataComandoFiscaleFinale;
        Stato = StatoDocumentoLocale.ParzialmenteFiscalizzato;
        DataUltimaModifica = DateTimeOffset.Now;
    }

    private decimal SommaPagamentiPerTipo(params string[] tipi)
    {
        return _pagamenti
            .Where(pagamento => IsTipoPagamento(pagamento.TipoPagamento, tipi))
            .Sum(pagamento => pagamento.Importo);
    }

    private static bool IsTipoPagamento(string? tipoPagamento, params string[] tipi)
    {
        if (string.IsNullOrWhiteSpace(tipoPagamento))
        {
            return false;
        }

        return tipi.Any(tipo =>
            tipoPagamento.Trim().Equals(tipo, StringComparison.OrdinalIgnoreCase));
    }
}
