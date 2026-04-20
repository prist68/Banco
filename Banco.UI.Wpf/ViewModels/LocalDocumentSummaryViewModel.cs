using Banco.Core.Domain.Entities;

namespace Banco.UI.Wpf.ViewModels;

public sealed class LocalDocumentSummaryViewModel
{
    public required Guid Id { get; init; }

    public required string Cliente { get; init; }

    public required string Operatore { get; init; }

    public required string Stato { get; init; }

    public required DateTimeOffset DataUltimaModifica { get; init; }

    public required decimal TotaleDocumento { get; init; }

    public string DocumentoLabel => "Scheda Banco";

    public string DataUltimaModificaLabel => DataUltimaModifica.ToString("dd/MM/yyyy HH:mm");

    public static LocalDocumentSummaryViewModel FromDocument(DocumentoLocale documento)
    {
        return new LocalDocumentSummaryViewModel
        {
            Id = documento.Id,
            Cliente = documento.Cliente,
            Operatore = documento.Operatore,
            Stato = documento.Stato.ToString(),
            DataUltimaModifica = documento.DataUltimaModifica,
            TotaleDocumento = documento.TotaleDocumento
        };
    }
}
