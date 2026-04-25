using Banco.Core.Domain.Entities;
using Banco.Core.Domain.Enums;

namespace Banco.Vendita.Fiscal;

public enum BancoSaveExecutionKind
{
    SaveAsCortesiaWithoutPrint = 0,
    SaveExistingScontrinoWithoutPrint = 1,
    ReplaceNonFiscalizedWithNewScontrino = 2
}

public sealed class BancoSavePlan
{
    public required BancoSaveExecutionKind ExecutionKind { get; init; }

    public required CategoriaDocumentoBanco CategoriaDocumentoBanco { get; init; }

    public required BancoPublishOptions PublishOptions { get; init; }
}

public static class BancoSavePlanResolver
{
    public static BancoSavePlan Resolve(DocumentoLocale documento)
    {
        ArgumentNullException.ThrowIfNull(documento);

        var pagamenti = NormalizePagamenti(documento);
        var hasCardLikePayment = pagamenti.Carta > 0 || pagamenti.Web > 0;
        var hasLegacyReference = documento.DocumentoGestionaleOid.HasValue;
        var isExistingScontrino = documento.CategoriaDocumentoBanco == CategoriaDocumentoBanco.Scontrino ||
                                  documento.ModalitaChiusura == ModalitaChiusuraDocumento.Scontrino ||
                                  documento.StatoFiscaleBanco is StatoFiscaleBanco.FiscalizzazioneWinEcrCompletata
                                      or StatoFiscaleBanco.FiscalizzazioneWinEcrRichiesta
                                      or StatoFiscaleBanco.FiscalizzazioneWinEcrFallita;

        if (isExistingScontrino)
        {
            return new BancoSavePlan
            {
                ExecutionKind = BancoSaveExecutionKind.SaveExistingScontrinoWithoutPrint,
                CategoriaDocumentoBanco = CategoriaDocumentoBanco.Scontrino,
                PublishOptions = new BancoPublishOptions
                {
                    SkipWinEcr = true
                }
            };
        }

        if (documento.HasComponenteSospeso && hasCardLikePayment && hasLegacyReference)
        {
            return new BancoSavePlan
            {
                ExecutionKind = BancoSaveExecutionKind.ReplaceNonFiscalizedWithNewScontrino,
                CategoriaDocumentoBanco = CategoriaDocumentoBanco.Scontrino,
                PublishOptions = new BancoPublishOptions
                {
                    ForceNewLegacyDocument = true,
                    DeleteExistingNonFiscalizedLegacyDocument = true
                }
            };
        }

        return new BancoSavePlan
        {
            ExecutionKind = BancoSaveExecutionKind.SaveAsCortesiaWithoutPrint,
            CategoriaDocumentoBanco = CategoriaDocumentoBanco.Cortesia,
            PublishOptions = BancoPublishOptions.Default
        };
    }

    private static (decimal Contanti, decimal Carta, decimal Web, decimal Buoni, decimal Sospeso) NormalizePagamenti(DocumentoLocale documento)
    {
        decimal contanti = 0;
        decimal carta = 0;
        decimal web = 0;
        decimal buoni = 0;
        decimal sospeso = 0;

        foreach (var pagamento in documento.Pagamenti)
        {
            var tipo = pagamento.TipoPagamento?.Trim().ToLowerInvariant() ?? string.Empty;
            switch (tipo)
            {
                case "contanti":
                case "contante":
                    contanti += pagamento.Importo;
                    break;
                case "carta":
                case "bancomat":
                case "pos":
                    carta += pagamento.Importo;
                    break;
                case "web":
                case "online":
                    web += pagamento.Importo;
                    break;
                case "buoni":
                case "ticket":
                case "buonipasto":
                    buoni += pagamento.Importo;
                    break;
                case "sospeso":
                    sospeso += pagamento.Importo;
                    break;
            }
        }

        return (contanti, carta, web, buoni, sospeso);
    }
}
