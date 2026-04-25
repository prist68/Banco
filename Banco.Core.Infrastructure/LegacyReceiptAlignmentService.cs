using Banco.Core.Domain.Enums;
using Banco.Vendita.Abstractions;
using System.Text.RegularExpressions;

namespace Banco.Core.Infrastructure;

public sealed class LegacyReceiptAlignmentService : ILegacyReceiptAlignmentService
{
    private static readonly Regex PublishedReceiptOidRegex =
        new(@"Esito publish Scontrino: OID=(\d+), Outcome=LegacyPublished", RegexOptions.Compiled);

    private readonly ILocalDocumentRepository _localDocumentRepository;
    private readonly IGestionaleDocumentWriter _documentWriter;
    private readonly IPosProcessLogService _logService;

    public LegacyReceiptAlignmentService(
        ILocalDocumentRepository localDocumentRepository,
        IGestionaleDocumentWriter documentWriter,
        IPosProcessLogService logService)
    {
        _localDocumentRepository = localDocumentRepository;
        _documentWriter = documentWriter;
        _logService = logService;
    }

    public async Task<int> AlignHistoricalReceiptsAsync(CancellationToken cancellationToken = default)
    {
        var localDocuments = await _localDocumentRepository.GetAllAsync(cancellationToken);

        var targetOids = localDocuments
            .Where(document =>
                document.DocumentoGestionaleOid.HasValue &&
                document.DocumentoGestionaleOid.Value > 0 &&
                document.ModalitaChiusura == ModalitaChiusuraDocumento.Scontrino &&
                document.StatoFiscaleBanco == StatoFiscaleBanco.FiscalizzazioneWinEcrCompletata)
            .GroupBy(document => document.DocumentoGestionaleOid!.Value)
            .Select(group => group
                .OrderByDescending(document => document.DataUltimaModifica)
                .First()
                .DocumentoGestionaleOid!.Value)
            .ToHashSet();

        foreach (var oidFromLog in LoadReceiptOidsFromBancoLog())
        {
            targetOids.Add(oidFromLog);
        }

        if (targetOids.Count == 0)
        {
            _logService.Info(nameof(LegacyReceiptAlignmentService), "Nessun documento storico Banco da riallineare a Scontrino=Si su db_diltech.");
            return 0;
        }

        var aligned = 0;
        foreach (var documentoGestionaleOid in targetOids.OrderBy(oid => oid))
        {
            await _documentWriter.MarkLegacyReceiptCompletedAsync(documentoGestionaleOid, cancellationToken);
            aligned++;
        }

        _logService.Info(
            nameof(LegacyReceiptAlignmentService),
            $"Riallineati {aligned} documenti storici Banco su db_diltech con Fatturato=1 per stato Scontrino FM.");

        return aligned;
    }

    private IEnumerable<int> LoadReceiptOidsFromBancoLog()
    {
        var bancoLogPath = Path.Combine(AppContext.BaseDirectory, "Log", "Banco.log");
        if (!File.Exists(bancoLogPath))
        {
            yield break;
        }

        string logContent;
        try
        {
            logContent = File.ReadAllText(bancoLogPath);
        }
        catch (Exception ex)
        {
            _logService.Warning(nameof(LegacyReceiptAlignmentService), $"Lettura Banco.log non disponibile per il riallineamento storico: {ex.Message}");
            yield break;
        }

        foreach (Match match in PublishedReceiptOidRegex.Matches(logContent))
        {
            if (int.TryParse(match.Groups[1].Value, out var documentoOid) && documentoOid > 0)
            {
                yield return documentoOid;
            }
        }
    }
}
