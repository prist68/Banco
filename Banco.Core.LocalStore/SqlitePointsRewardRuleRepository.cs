using Banco.Vendita.Abstractions;
using Banco.Vendita.Configuration;
using Banco.Vendita.Points;
using Microsoft.Data.Sqlite;

namespace Banco.Core.LocalStore;

public sealed class SqlitePointsRewardRuleRepository : IPointsRewardRuleRepository
{
    private readonly IApplicationConfigurationService _configurationService;

    public SqlitePointsRewardRuleRepository(IApplicationConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    public async Task<IReadOnlyList<PointsRewardRule>> GetByCampaignOidAsync(int campaignOid, CancellationToken cancellationToken = default)
    {
        if (campaignOid <= 0)
        {
            return [];
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT RuleId, CampaignOid, RuleName, IsActive, RequiredPoints, RewardType, DiscountAmount, DiscountPercent, RewardArticleOid, RewardArticleCode, RewardArticleDescription, RewardArticleIvaOid, RewardArticleAliquotaIva, RewardArticleTipoArticoloOid, RewardArticlePrezzoVendita, RewardQuantity, EnableSaleCheck, Notes, UpdatedAt
            FROM CampaignRewardRules
            WHERE CampaignOid = $campaignOid
            ORDER BY IsActive DESC, RequiredPoints ASC, RuleName ASC;
            """;
        command.Parameters.AddWithValue("$campaignOid", campaignOid);

        var results = new List<PointsRewardRule>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new PointsRewardRule
            {
                Id = Guid.Parse(reader.GetString(0)),
                CampaignOid = reader.GetInt32(1),
                RuleName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                IsActive = !reader.IsDBNull(3) && reader.GetInt32(3) == 1,
                RequiredPoints = reader.IsDBNull(4) ? null : Convert.ToDecimal(reader.GetDouble(4)),
                RewardType = Enum.TryParse<PointsRewardType>(reader.GetString(5), out var rewardType)
                    ? rewardType
                    : PointsRewardType.ScontoFisso,
                DiscountAmount = reader.IsDBNull(6) ? null : Convert.ToDecimal(reader.GetDouble(6)),
                DiscountPercent = reader.IsDBNull(7) ? null : Convert.ToDecimal(reader.GetDouble(7)),
                RewardArticleOid = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                RewardArticleCode = reader.IsDBNull(9) ? null : reader.GetString(9),
                RewardArticleDescription = reader.IsDBNull(10) ? null : reader.GetString(10),
                RewardArticleIvaOid = reader.IsDBNull(11) ? null : reader.GetInt32(11),
                RewardArticleAliquotaIva = reader.IsDBNull(12) ? null : Convert.ToDecimal(reader.GetDouble(12)),
                RewardArticleTipoArticoloOid = reader.IsDBNull(13) ? null : reader.GetInt32(13),
                RewardArticlePrezzoVendita = reader.IsDBNull(14) ? null : Convert.ToDecimal(reader.GetDouble(14)),
                RewardQuantity = reader.IsDBNull(15) ? 1m : Convert.ToDecimal(reader.GetDouble(15)),
                EnableSaleCheck = !reader.IsDBNull(16) && reader.GetInt32(16) == 1,
                Notes = reader.IsDBNull(17) ? null : reader.GetString(17),
                UpdatedAt = reader.IsDBNull(18) ? DateTimeOffset.Now : DateTimeOffset.Parse(reader.GetString(18))
            });
        }

        return results;
    }

    public async Task SaveRangeAsync(int campaignOid, IReadOnlyList<PointsRewardRule> rules, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = (SqliteTransaction)transaction;
            deleteCommand.CommandText = "DELETE FROM CampaignRewardRules WHERE CampaignOid = $campaignOid;";
            deleteCommand.Parameters.AddWithValue("$campaignOid", campaignOid);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var rule in rules)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText =
                """
                INSERT INTO CampaignRewardRules (
                    RuleId,
                    CampaignOid,
                    RuleName,
                    IsActive,
                    RequiredPoints,
                    RewardType,
                    DiscountAmount,
                    DiscountPercent,
                    RewardArticleOid,
                    RewardArticleCode,
                    RewardArticleDescription,
                    RewardArticleIvaOid,
                    RewardArticleAliquotaIva,
                    RewardArticleTipoArticoloOid,
                    RewardArticlePrezzoVendita,
                    RewardQuantity,
                    EnableSaleCheck,
                    Notes,
                    UpdatedAt
                )
                VALUES (
                    $ruleId,
                    $campaignOid,
                    $ruleName,
                    $isActive,
                    $requiredPoints,
                    $rewardType,
                    $discountAmount,
                    $discountPercent,
                    $rewardArticleOid,
                    $rewardArticleCode,
                    $rewardArticleDescription,
                    $rewardArticleIvaOid,
                    $rewardArticleAliquotaIva,
                    $rewardArticleTipoArticoloOid,
                    $rewardArticlePrezzoVendita,
                    $rewardQuantity,
                    $enableSaleCheck,
                    $notes,
                    $updatedAt
                );
                """;
            command.Parameters.AddWithValue("$ruleId", rule.Id.ToString("D"));
            command.Parameters.AddWithValue("$campaignOid", campaignOid);
            command.Parameters.AddWithValue("$ruleName", string.IsNullOrWhiteSpace(rule.RuleName) ? "Regola premio" : rule.RuleName);
            command.Parameters.AddWithValue("$isActive", rule.IsActive ? 1 : 0);
            command.Parameters.AddWithValue("$requiredPoints", (object?)rule.RequiredPoints ?? DBNull.Value);
            command.Parameters.AddWithValue("$rewardType", rule.RewardType.ToString());
            command.Parameters.AddWithValue("$discountAmount", (object?)rule.DiscountAmount ?? DBNull.Value);
            command.Parameters.AddWithValue("$discountPercent", (object?)rule.DiscountPercent ?? DBNull.Value);
            command.Parameters.AddWithValue("$rewardArticleOid", (object?)rule.RewardArticleOid ?? DBNull.Value);
            command.Parameters.AddWithValue("$rewardArticleCode", (object?)rule.RewardArticleCode ?? DBNull.Value);
            command.Parameters.AddWithValue("$rewardArticleDescription", (object?)rule.RewardArticleDescription ?? DBNull.Value);
            command.Parameters.AddWithValue("$rewardArticleIvaOid", (object?)rule.RewardArticleIvaOid ?? DBNull.Value);
            command.Parameters.AddWithValue("$rewardArticleAliquotaIva", (object?)rule.RewardArticleAliquotaIva ?? DBNull.Value);
            command.Parameters.AddWithValue("$rewardArticleTipoArticoloOid", (object?)rule.RewardArticleTipoArticoloOid ?? DBNull.Value);
            command.Parameters.AddWithValue("$rewardArticlePrezzoVendita", (object?)rule.RewardArticlePrezzoVendita ?? DBNull.Value);
            command.Parameters.AddWithValue("$rewardQuantity", rule.RewardQuantity);
            command.Parameters.AddWithValue("$enableSaleCheck", rule.EnableSaleCheck ? 1 : 0);
            command.Parameters.AddWithValue("$notes", (object?)rule.Notes ?? DBNull.Value);
            command.Parameters.AddWithValue("$updatedAt", rule.UpdatedAt.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var settings = await _configurationService.LoadAsync(cancellationToken);
        Directory.CreateDirectory(settings.LocalStore.BaseDirectory);
        return new SqliteConnection($"Data Source={settings.LocalStore.DatabasePath}");
    }
}
