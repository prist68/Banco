using Banco.Vendita.Abstractions;
using Banco.Vendita.Configuration;
using Banco.Vendita.Points;
using Microsoft.Data.Sqlite;

namespace Banco.Core.LocalStore;

public sealed class SqlitePromotionEventRepository : IPromotionEventRepository
{
    private readonly IApplicationConfigurationService _configurationService;

    public SqlitePromotionEventRepository(IApplicationConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    public async Task AddAsync(PromotionEventRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO PromotionEvents (
                Id,
                CampaignOid,
                RuleId,
                CustomerOid,
                LocalDocumentId,
                GestionaleDocumentOid,
                EventType,
                RewardType,
                AvailablePoints,
                RequiredPoints,
                AppliedRowId,
                Title,
                Message,
                CreatedAt
            )
            VALUES (
                $id,
                $campaignOid,
                $ruleId,
                $customerOid,
                $localDocumentId,
                $gestionaleDocumentOid,
                $eventType,
                $rewardType,
                $availablePoints,
                $requiredPoints,
                $appliedRowId,
                $title,
                $message,
                $createdAt
            );
            """;
        command.Parameters.AddWithValue("$id", record.Id.ToString("D"));
        command.Parameters.AddWithValue("$campaignOid", record.CampaignOid);
        command.Parameters.AddWithValue("$ruleId", record.RuleId.HasValue ? record.RuleId.Value.ToString("D") : DBNull.Value);
        command.Parameters.AddWithValue("$customerOid", (object?)record.CustomerOid ?? DBNull.Value);
        command.Parameters.AddWithValue("$localDocumentId", record.LocalDocumentId.HasValue ? record.LocalDocumentId.Value.ToString("D") : DBNull.Value);
        command.Parameters.AddWithValue("$gestionaleDocumentOid", (object?)record.GestionaleDocumentOid ?? DBNull.Value);
        command.Parameters.AddWithValue("$eventType", record.EventType.ToString());
        command.Parameters.AddWithValue("$rewardType", record.RewardType?.ToString() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$availablePoints", record.AvailablePoints);
        command.Parameters.AddWithValue("$requiredPoints", record.RequiredPoints);
        command.Parameters.AddWithValue("$appliedRowId", record.AppliedRowId.HasValue ? record.AppliedRowId.Value.ToString("D") : DBNull.Value);
        command.Parameters.AddWithValue("$title", record.Title);
        command.Parameters.AddWithValue("$message", record.Message);
        command.Parameters.AddWithValue("$createdAt", record.CreatedAt.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task<PromotionEventRecord?> GetLastByCampaignAndCustomerAsync(int campaignOid, int customerOid, CancellationToken cancellationToken = default)
    {
        return GetSingleAsync(
            """
            SELECT Id, CampaignOid, RuleId, CustomerOid, LocalDocumentId, GestionaleDocumentOid, EventType, RewardType, AvailablePoints, RequiredPoints, AppliedRowId, Title, Message, CreatedAt
            FROM PromotionEvents
            WHERE CampaignOid = $campaignOid AND CustomerOid = $customerOid
            ORDER BY CreatedAt DESC
            LIMIT 1;
            """,
            ("$campaignOid", campaignOid),
            ("$customerOid", customerOid),
            cancellationToken);
    }

    public Task<PromotionEventRecord?> GetLastByDocumentAsync(Guid localDocumentId, CancellationToken cancellationToken = default)
    {
        return GetSingleAsync(
            """
            SELECT Id, CampaignOid, RuleId, CustomerOid, LocalDocumentId, GestionaleDocumentOid, EventType, RewardType, AvailablePoints, RequiredPoints, AppliedRowId, Title, Message, CreatedAt
            FROM PromotionEvents
            WHERE LocalDocumentId = $localDocumentId
            ORDER BY CreatedAt DESC
            LIMIT 1;
            """,
            ("$localDocumentId", localDocumentId.ToString("D")),
            cancellationToken);
    }

    private async Task<PromotionEventRecord?> GetSingleAsync(
        string sql,
        (string Name, object Value) firstParameter,
        CancellationToken cancellationToken)
    {
        return await GetSingleAsync(sql, firstParameter, null, cancellationToken);
    }

    private async Task<PromotionEventRecord?> GetSingleAsync(
        string sql,
        (string Name, object Value) firstParameter,
        (string Name, object Value)? secondParameter,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue(firstParameter.Name, firstParameter.Value);
        if (secondParameter.HasValue)
        {
            command.Parameters.AddWithValue(secondParameter.Value.Name, secondParameter.Value.Value);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new PromotionEventRecord
        {
            Id = Guid.Parse(reader.GetString(0)),
            CampaignOid = reader.GetInt32(1),
            RuleId = reader.IsDBNull(2) ? null : Guid.Parse(reader.GetString(2)),
            CustomerOid = reader.IsDBNull(3) ? null : reader.GetInt32(3),
            LocalDocumentId = reader.IsDBNull(4) ? null : Guid.Parse(reader.GetString(4)),
            GestionaleDocumentOid = reader.IsDBNull(5) ? null : reader.GetInt32(5),
            EventType = Enum.TryParse<PromotionEventType>(reader.GetString(6), out var eventType)
                ? eventType
                : PromotionEventType.NotEligible,
            RewardType = reader.IsDBNull(7) ? null : Enum.TryParse<PointsRewardType>(reader.GetString(7), out var rewardType)
                ? rewardType
                : null,
            AvailablePoints = reader.IsDBNull(8) ? 0 : Convert.ToDecimal(reader.GetDouble(8)),
            RequiredPoints = reader.IsDBNull(9) ? 0 : Convert.ToDecimal(reader.GetDouble(9)),
            AppliedRowId = reader.IsDBNull(10) ? null : Guid.Parse(reader.GetString(10)),
            Title = reader.IsDBNull(11) ? string.Empty : reader.GetString(11),
            Message = reader.IsDBNull(12) ? string.Empty : reader.GetString(12),
            CreatedAt = reader.IsDBNull(13) ? DateTimeOffset.Now : DateTimeOffset.Parse(reader.GetString(13))
        };
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var settings = await _configurationService.LoadAsync(cancellationToken);
        Directory.CreateDirectory(settings.LocalStore.BaseDirectory);
        return new SqliteConnection($"Data Source={settings.LocalStore.DatabasePath}");
    }
}
