using System.Text;
using System.Text.Json;
using Banco.Core.Domain.Entities;
using Banco.Vendita.Abstractions;
using Banco.Vendita.Points;
using MySqlConnector;

namespace Banco.Core.Infrastructure;

public sealed class GestionaleFidelityHistoryService : IGestionaleFidelityHistoryService
{
    private const int ModelloDocumentoBanco = 27;
    private readonly IApplicationConfigurationService _configurationService;
    private readonly ILocalAuditRepository _localAuditRepository;

    public GestionaleFidelityHistoryService(
        IApplicationConfigurationService configurationService,
        ILocalAuditRepository localAuditRepository)
    {
        _configurationService = configurationService;
        _localAuditRepository = localAuditRepository;
    }

    public async Task<FidelityCustomerHistory?> GetCustomerHistoryAsync(
        int customerOid,
        CancellationToken cancellationToken = default)
    {
        if (customerOid <= 0)
        {
            return null;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await NormalizeBancoDocumentsForFidelityAsync(connection, customerOid, cancellationToken);
        return await LoadCustomerHistoryAsync(connection, customerOid, cancellationToken);
    }

    public async Task<FidelityBalanceRecalculationResult?> RecalculateCustomerBalanceAsync(
        int customerOid,
        bool persistToLegacy = true,
        string operatore = "Sistema",
        CancellationToken cancellationToken = default)
    {
        if (customerOid <= 0)
        {
            return null;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await NormalizeBancoDocumentsForFidelityAsync(connection, customerOid, cancellationToken);
        var history = await LoadCustomerHistoryAsync(connection, customerOid, cancellationToken);
        if (history is null)
        {
            return null;
        }

        var legacyUpdated = false;
        if (persistToLegacy && history.LegacyCurrentPoints != history.ComputedCurrentPoints)
        {
            await UpdateCustomerCurrentPointsAsync(connection, customerOid, history.ComputedCurrentPoints, cancellationToken);
            legacyUpdated = true;
        }

        var result = new FidelityBalanceRecalculationResult
        {
            CustomerOid = history.CustomerOid,
            CardCode = history.CardCode,
            InitialPoints = history.InitialPoints,
            PreviousLegacyCurrentPoints = history.LegacyCurrentPoints,
            ComputedCurrentPoints = history.ComputedCurrentPoints,
            DocumentCount = history.Entries.Count,
            LegacyUpdated = legacyUpdated
        };

        await SaveAuditSafeAsync(
            new EventoAudit
            {
                EntityType = "FidelityBalance",
                EntityId = customerOid.ToString(),
                EventType = "RicalcoloSaldoCliente",
                Operatore = string.IsNullOrWhiteSpace(operatore) ? "Sistema" : operatore.Trim(),
                PayloadSinteticoJson = JsonSerializer.Serialize(new
                {
                    history.CustomerOid,
                    history.CardCode,
                    history.InitialPoints,
                    PreviousLegacyCurrentPoints = history.LegacyCurrentPoints,
                    history.ComputedCurrentPoints,
                    DeltaPoints = history.DeltaPoints,
                    DocumentCount = history.Entries.Count,
                    Persisted = persistToLegacy,
                    legacyUpdated
                }),
                Esito = "Ok"
            },
            cancellationToken);

        return result;
    }

    public async Task<IReadOnlyList<FidelityBalanceRecalculationResult>> RecalculateAllActiveCustomersAsync(
        bool persistToLegacy = true,
        string operatore = "Sistema",
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var customerOids = await LoadActiveFidelityCustomerOidsAsync(connection, cancellationToken);
        var results = new List<FidelityBalanceRecalculationResult>(customerOids.Count);

        foreach (var customerOid in customerOids)
        {
            await NormalizeBancoDocumentsForFidelityAsync(connection, customerOid, cancellationToken);
            var history = await LoadCustomerHistoryAsync(connection, customerOid, cancellationToken);
            if (history is null)
            {
                continue;
            }

            var legacyUpdated = false;
            if (persistToLegacy && history.LegacyCurrentPoints != history.ComputedCurrentPoints)
            {
                await UpdateCustomerCurrentPointsAsync(connection, customerOid, history.ComputedCurrentPoints, cancellationToken);
                legacyUpdated = true;
            }

            results.Add(new FidelityBalanceRecalculationResult
            {
                CustomerOid = history.CustomerOid,
                CardCode = history.CardCode,
                InitialPoints = history.InitialPoints,
                PreviousLegacyCurrentPoints = history.LegacyCurrentPoints,
                ComputedCurrentPoints = history.ComputedCurrentPoints,
                DocumentCount = history.Entries.Count,
                LegacyUpdated = legacyUpdated
            });
        }

        await SaveAuditSafeAsync(
            new EventoAudit
            {
                EntityType = "FidelityBalance",
                EntityId = "ALL",
                EventType = "RicalcoloSaldoGlobale",
                Operatore = string.IsNullOrWhiteSpace(operatore) ? "Sistema" : operatore.Trim(),
                PayloadSinteticoJson = JsonSerializer.Serialize(new
                {
                    CustomersProcessed = results.Count,
                    CustomersUpdated = results.Count(item => item.LegacyUpdated),
                    TotalDelta = results.Sum(item => item.DeltaPoints),
                    Persisted = persistToLegacy
                }),
                Esito = "Ok"
            },
            cancellationToken);

        return results;
    }

    private static async Task<FidelityCustomerHistory?> LoadCustomerHistoryAsync(
        MySqlConnection connection,
        int customerOid,
        CancellationToken cancellationToken)
    {
        var customerInfoResult = await LoadCustomerInfoAsync(connection, customerOid, cancellationToken);
        if (!customerInfoResult.HasValue)
        {
            return null;
        }

        var customerInfo = customerInfoResult.Value;
        var euroPerPunto = await LoadEuroPerPuntoAsync(connection, cancellationToken);
        var entries = await LoadEntriesAsync(connection, customerOid, customerInfo.InitialPoints, euroPerPunto, cancellationToken);

        return new FidelityCustomerHistory
        {
            CustomerOid = customerOid,
            CardCode = customerInfo.CardCode,
            InitialPoints = customerInfo.InitialPoints,
            LegacyCurrentPoints = customerInfo.CurrentPoints,
            ComputedCurrentPoints = entries.Count == 0 ? customerInfo.InitialPoints : entries[^1].ProgressivePoints,
            Entries = entries
        };
    }

    private static async Task<(string CardCode, decimal InitialPoints, decimal CurrentPoints)?> LoadCustomerInfoAsync(
        MySqlConnection connection,
        int customerOid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                COALESCE(NULLIF(TRIM(Codicecartafedelta), ''), '') AS CardCode,
                COALESCE(Punticartafedeltainiziali, 0) AS InitialPoints,
                COALESCE(Punticartafedelta, 0) AS CurrentPoints
            FROM soggetto
            WHERE OID = @customerOid
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@customerOid", customerOid);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return (
            reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
            reader.IsDBNull(1) ? 0m : Convert.ToDecimal(reader.GetValue(1)),
            reader.IsDBNull(2) ? 0m : Convert.ToDecimal(reader.GetValue(2)));
    }

    private static async Task<decimal> LoadEuroPerPuntoAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COALESCE(Europerpunto, 0)
            FROM cartafedelta
            WHERE COALESCE(Attiva, 0) <> 0
            ORDER BY OID
            LIMIT 1;
            """;

        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar is null || scalar == DBNull.Value ? 0m : Convert.ToDecimal(scalar);
    }

    private static async Task<IReadOnlyList<int>> LoadActiveFidelityCustomerOidsAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT OID
            FROM soggetto
            WHERE NULLIF(TRIM(Codicecartafedelta), '') IS NOT NULL
            ORDER BY OID;
            """;

        var result = new List<int>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(reader.GetInt32(0));
        }

        return result;
    }

    private static async Task UpdateCustomerCurrentPointsAsync(
        MySqlConnection connection,
        int customerOid,
        decimal computedCurrentPoints,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE soggetto
            SET Punticartafedelta = @computedCurrentPoints
            WHERE OID = @customerOid;
            """;
        command.Parameters.AddWithValue("@computedCurrentPoints", computedCurrentPoints);
        command.Parameters.AddWithValue("@customerOid", customerOid);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task NormalizeBancoDocumentsForFidelityAsync(
        MySqlConnection connection,
        int customerOid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE documento
            SET
                Dataevasione = COALESCE(Dataevasione, Data, DATE(Dataaggiornamento), DATE(Datacreazione), CURDATE()),
                Isdifferitoreloaded = COALESCE(Isdifferitoreloaded, 0)
            WHERE Soggetto = @customerOid
              AND Modellodocumento = @modelloDocumentoBanco
              AND (Dataevasione IS NULL OR Isdifferitoreloaded IS NULL);
            """;
        command.Parameters.AddWithValue("@customerOid", customerOid);
        command.Parameters.AddWithValue("@modelloDocumentoBanco", ModelloDocumentoBanco);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<FidelityHistoryEntry>> LoadEntriesAsync(
        MySqlConnection connection,
        int customerOid,
        decimal initialPoints,
        decimal euroPerPunto,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                d.OID AS DocumentoOid,
                d.Numero,
                d.Anno,
                d.Data,
                COALESCE(d.Totaledocumento, 0) AS TotaleDocumento,
                dr.Ordineriga,
                COALESCE(dr.Codicearticolo, '') AS CodiceArticolo,
                COALESCE(dr.Descrizione, '') AS Descrizione,
                COALESCE(dr.Quantita, 0) AS Quantita,
                COALESCE(dr.Importoriga, 0) AS ImportoRiga,
                COALESCE(a.Operazionesucartafedelta, 0) AS OperazioneSuCartaFedelta,
                COALESCE(a.Puntidasottrarrecartafedeleta, 0) AS PuntiDaSottrarre
            FROM documento d
            INNER JOIN documentoriga dr ON dr.Documento = d.OID
            LEFT JOIN articolo a ON a.OID = dr.Articolo
            WHERE d.Soggetto = @customerOid
              AND d.Modellodocumento = @modelloDocumentoBanco
            ORDER BY d.Data, d.OID, dr.Ordineriga;
            """;
        command.Parameters.AddWithValue("@customerOid", customerOid);
        command.Parameters.AddWithValue("@modelloDocumentoBanco", ModelloDocumentoBanco);

        var grouped = new Dictionary<int, FidelityEntryBuilder>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var documentoOid = reader.GetInt32(0);
            if (!grouped.TryGetValue(documentoOid, out var builder))
            {
                builder = new FidelityEntryBuilder(
                    documentoOid,
                    reader.IsDBNull(1) ? 0L : Convert.ToInt64(reader.GetValue(1)),
                    reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2)),
                    reader.IsDBNull(3) ? DateTime.MinValue : Convert.ToDateTime(reader.GetValue(3)),
                    reader.IsDBNull(4) ? 0m : Convert.ToDecimal(reader.GetValue(4)));
                grouped.Add(documentoOid, builder);
            }

            var codice = reader.IsDBNull(6) ? string.Empty : reader.GetString(6);
            var descrizione = reader.IsDBNull(7) ? string.Empty : reader.GetString(7);
            var importoRiga = reader.IsDBNull(9) ? 0m : Convert.ToDecimal(reader.GetValue(9));
            var operazioneValue = reader.IsDBNull(10) ? 0 : Convert.ToInt32(reader.GetValue(10));
            var puntiDaSottrarre = reader.IsDBNull(11) ? 0m : Convert.ToDecimal(reader.GetValue(11));

            builder.AddLine(codice, descrizione, importoRiga, operazioneValue, puntiDaSottrarre);
        }

        var entries = new List<FidelityHistoryEntry>(grouped.Count);
        var progressive = initialPoints;
        foreach (var builder in grouped.Values.OrderBy(item => item.DataDocumento).ThenBy(item => item.DocumentoOid))
        {
            var earned = euroPerPunto > 0 && builder.PointsEligibleAmount > 0
                ? Math.Floor(builder.PointsEligibleAmount / euroPerPunto)
                : 0m;
            var spent = builder.SpentPoints;
            progressive = progressive + earned - spent;

            entries.Add(new FidelityHistoryEntry
            {
                DocumentoOid = builder.DocumentoOid,
                NumeroDocumento = builder.NumeroDocumento,
                AnnoDocumento = builder.AnnoDocumento,
                DataDocumento = builder.DataDocumento,
                TotaleDocumento = builder.TotaleDocumento,
                EarnedPoints = earned,
                SpentPoints = spent,
                ProgressivePoints = progressive,
                DetailLines = builder.BuildDetails(earned, spent, progressive)
            });
        }

        return entries;
    }

    private async Task<MySqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var settings = await _configurationService.LoadAsync(cancellationToken);
        return await GestionaleConnectionFactory.CreateOpenConnectionAsync(settings, cancellationToken);
    }

    private async Task SaveAuditSafeAsync(EventoAudit evento, CancellationToken cancellationToken)
    {
        try
        {
            await _localAuditRepository.SaveAsync(evento, cancellationToken);
        }
        catch
        {
        }
    }

    private sealed class FidelityEntryBuilder
    {
        private readonly List<string> _lines = [];

        public FidelityEntryBuilder(int documentoOid, long numeroDocumento, int annoDocumento, DateTime dataDocumento, decimal totaleDocumento)
        {
            DocumentoOid = documentoOid;
            NumeroDocumento = numeroDocumento;
            AnnoDocumento = annoDocumento;
            DataDocumento = dataDocumento;
            TotaleDocumento = totaleDocumento;
        }

        public int DocumentoOid { get; }

        public long NumeroDocumento { get; }

        public int AnnoDocumento { get; }

        public DateTime DataDocumento { get; }

        public decimal TotaleDocumento { get; }

        public decimal PointsEligibleAmount { get; private set; }

        public decimal SpentPoints { get; private set; }

        public void AddLine(string codice, string descrizione, decimal importoRiga, int operazioneSuCartaFedelta, decimal puntiDaSottrarre)
        {
            if (!string.IsNullOrWhiteSpace(codice) || importoRiga != 0)
            {
                _lines.Add($"- {codice} {descrizione}".TrimEnd() + $" | {importoRiga:N2} EUR");
            }

            if (operazioneSuCartaFedelta == 0 && puntiDaSottrarre <= 0 && importoRiga > 0)
            {
                PointsEligibleAmount += importoRiga;
            }

            if (operazioneSuCartaFedelta != 0 || puntiDaSottrarre > 0)
            {
                SpentPoints += puntiDaSottrarre;
            }
        }

        public string BuildDetails(decimal earnedPoints, decimal spentPoints, decimal progressivePoints)
        {
            var builder = new StringBuilder();
            builder.Append($"Banco {NumeroDocumento}/{AnnoDocumento} del {DataDocumento:dd/MM/yyyy}");
            builder.Append($" | Punti: {earnedPoints:N0}");
            if (spentPoints > 0)
            {
                builder.Append($" | Spesi: {spentPoints:N0}");
            }

            builder.Append($" | Progressivo: {progressivePoints:N0}");

            if (_lines.Count > 0)
            {
                builder.AppendLine();
                builder.Append(string.Join(Environment.NewLine, _lines));
            }

            return builder.ToString();
        }
    }
}
