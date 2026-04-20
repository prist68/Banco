using Banco.Vendita.Abstractions;
using Banco.Vendita.Documents;
using Banco.Vendita.Fiscal;
using MySqlConnector;

namespace Banco.Core.Infrastructure;

public sealed class GestionaleSupplierOrderWriteService : IGestionaleSupplierOrderWriteService
{
    private const int SupplierOrderModelOid = 9;

    private readonly IApplicationConfigurationService _configurationService;
    private readonly IGestionaleDocumentWriter _documentWriter;

    public GestionaleSupplierOrderWriteService(
        IApplicationConfigurationService configurationService,
        IGestionaleDocumentWriter documentWriter)
    {
        _configurationService = configurationService;
        _documentWriter = documentWriter;
    }

    public async Task<FiscalizationResult> CreateSupplierOrderAsync(
        GestionaleSupplierOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.SupplierOid <= 0)
        {
            throw new InvalidOperationException("Il fornitore selezionato non ha un OID legacy valido.");
        }

        if (request.Rows.Count == 0)
        {
            throw new InvalidOperationException("La lista fornitore non contiene righe da scrivere su FM.");
        }

        var nextNumber = await GetNextSupplierOrderNumberAsync(request.DocumentDate, cancellationToken);
        var fiscalRequest = new FiscalizationRequest
        {
            LocalDocumentId = Guid.NewGuid(),
            ModellodocumentoOid = SupplierOrderModelOid,
            Numero = nextNumber.Numero,
            Anno = nextNumber.Anno,
            DataDocumento = request.DocumentDate,
            SoggettoOid = request.SupplierOid,
            Operatore = string.IsNullOrWhiteSpace(request.OperatorName) ? "Banco" : request.OperatorName.Trim(),
            Pagamenti = new FiscalizationPaymentBreakdown(),
            Righe = request.Rows
                .Select(row => new FiscalizationRow
                {
                    OrdineRiga = row.OrdineRiga,
                    ArticoloOid = row.ArticoloOid,
                    CodiceArticolo = row.CodiceArticolo,
                    Descrizione = row.Descrizione,
                    UnitaMisura = string.IsNullOrWhiteSpace(row.UnitaMisura) ? "PZ" : row.UnitaMisura.Trim(),
                    Quantita = row.Quantita <= 0 ? 1 : row.Quantita,
                    ValoreUnitario = row.PrezzoUnitario,
                    ImportoRiga = Math.Round((row.Quantita <= 0 ? 1 : row.Quantita) * row.PrezzoUnitario, 2, MidpointRounding.AwayFromZero),
                    IvaOid = row.IvaOid <= 0 ? 1 : row.IvaOid
                })
                .ToList()
        };

        return await _documentWriter.UpsertFiscalDocumentAsync(fiscalRequest, cancellationToken: cancellationToken);
    }

    private async Task<(int Numero, int Anno)> GetNextSupplierOrderNumberAsync(
        DateTime documentDate,
        CancellationToken cancellationToken)
    {
        var settings = await _configurationService.LoadAsync(cancellationToken);
        await using var connection = await GestionaleConnectionFactory.CreateOpenConnectionAsync(settings, cancellationToken);

        var anno = documentDate.Year;

        await using (var yearCommand = connection.CreateCommand())
        {
            yearCommand.CommandText =
                """
                SELECT COALESCE(MAX(d.Anno), @fallbackYear)
                FROM documento d
                WHERE d.Modellodocumento = @modellodocumento;
                """;
            yearCommand.Parameters.AddWithValue("@fallbackYear", anno);
            yearCommand.Parameters.AddWithValue("@modellodocumento", SupplierOrderModelOid);

            var yearResult = await yearCommand.ExecuteScalarAsync(cancellationToken);
            if (yearResult is not null && yearResult != DBNull.Value)
            {
                anno = Convert.ToInt32(yearResult);
            }
        }

        var numero = 0;

        await using (var numberCommand = connection.CreateCommand())
        {
            numberCommand.CommandText =
                """
                SELECT COALESCE(MAX(d.Numero), 0)
                FROM documento d
                WHERE d.Modellodocumento = @modellodocumento
                  AND d.Anno = @anno;
                """;
            numberCommand.Parameters.AddWithValue("@modellodocumento", SupplierOrderModelOid);
            numberCommand.Parameters.AddWithValue("@anno", anno);

            var numberResult = await numberCommand.ExecuteScalarAsync(cancellationToken);
            if (numberResult is not null && numberResult != DBNull.Value)
            {
                numero = Convert.ToInt32(numberResult);
            }
        }

        return (numero + 1, anno);
    }
}
