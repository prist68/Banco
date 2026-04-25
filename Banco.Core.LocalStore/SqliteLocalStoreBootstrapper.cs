using Banco.Vendita.Abstractions;
using Banco.Vendita.Configuration;
using Microsoft.Data.Sqlite;

namespace Banco.Core.LocalStore;

public sealed class SqliteLocalStoreBootstrapper : ILocalStoreBootstrapper
{
    public async Task InitializeAsync(LocalStoreSettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(settings.BaseDirectory);

        await using var connection = new SqliteConnection($"Data Source={settings.DatabasePath}");
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS AppSettings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS LocalDocuments (
                Id TEXT PRIMARY KEY,
                NumeroLocale TEXT NOT NULL,
                Stato TEXT NOT NULL,
                ModalitaChiusura TEXT NOT NULL DEFAULT 'BozzaLocale',
                CategoriaDocumentoBanco TEXT NOT NULL DEFAULT 'Indeterminata',
                HasComponenteSospeso INTEGER NOT NULL DEFAULT 0,
                StatoFiscaleBanco TEXT NOT NULL DEFAULT 'Nessuno',
                Operatore TEXT NOT NULL,
                Cliente TEXT NOT NULL,
                ClienteOid INTEGER NULL,
                ListinoOid INTEGER NULL,
                ListinoNome TEXT NULL,
                ScontoDocumento REAL NOT NULL DEFAULT 0,
                TotaleDocumento REAL NOT NULL DEFAULT 0,
                DocumentoGestionaleOid INTEGER NULL,
                NumeroDocumentoGestionale INTEGER NULL,
                AnnoDocumentoGestionale INTEGER NULL,
                DataDocumentoGestionale TEXT NULL,
                DataPagamentoFinale TEXT NULL,
                DataComandoFiscaleFinale TEXT NULL,
                DataCreazione TEXT NOT NULL,
                DataUltimaModifica TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS LocalDocumentRows (
                Id TEXT PRIMARY KEY,
                DocumentId TEXT NOT NULL,
                OrdineRiga INTEGER NOT NULL,
                TipoRiga TEXT NOT NULL,
                ArticoloOid INTEGER NULL,
                CodiceArticolo TEXT NULL,
                Descrizione TEXT NOT NULL,
                Quantita REAL NOT NULL,
                DisponibilitaRiferimento REAL NOT NULL DEFAULT 0,
                PrezzoUnitario REAL NOT NULL,
                ScontoPercentuale REAL NOT NULL DEFAULT 0,
                Sconto1 REAL NOT NULL DEFAULT 0,
                Sconto2 REAL NOT NULL DEFAULT 0,
                Sconto3 REAL NOT NULL DEFAULT 0,
                Sconto4 REAL NOT NULL DEFAULT 0,
                IvaOid INTEGER NOT NULL,
                AliquotaIva REAL NOT NULL,
                FlagManuale INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS LocalPayments (
                Id TEXT PRIMARY KEY,
                DocumentId TEXT NOT NULL,
                TipoPagamento TEXT NOT NULL,
                Importo REAL NOT NULL,
                StatoPagamentoLocale TEXT NOT NULL,
                DataOra TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS AuditEvents (
                Id TEXT PRIMARY KEY,
                DocumentId TEXT NULL,
                EntityType TEXT NOT NULL,
                EntityId TEXT NOT NULL,
                EventType TEXT NOT NULL,
                Operatore TEXT NOT NULL,
                PayloadSinteticoJson TEXT NOT NULL,
                Esito TEXT NOT NULL,
                Timestamp TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS CampaignRewardRules (
                RuleId TEXT PRIMARY KEY,
                CampaignOid INTEGER NOT NULL,
                RuleName TEXT NOT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1,
                RequiredPoints REAL NULL,
                RewardType TEXT NOT NULL,
                DiscountAmount REAL NULL,
                DiscountPercent REAL NULL,
                RewardArticleOid INTEGER NULL,
                RewardArticleCode TEXT NULL,
                RewardArticleDescription TEXT NULL,
                RewardArticleIvaOid INTEGER NULL,
                RewardArticleAliquotaIva REAL NULL,
                RewardArticleTipoArticoloOid INTEGER NULL,
                RewardArticlePrezzoVendita REAL NULL,
                RewardQuantity REAL NOT NULL DEFAULT 1,
                EnableSaleCheck INTEGER NOT NULL DEFAULT 0,
                Notes TEXT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS PromotionEvents (
                Id TEXT PRIMARY KEY,
                CampaignOid INTEGER NOT NULL,
                RuleId TEXT NULL,
                CustomerOid INTEGER NULL,
                LocalDocumentId TEXT NULL,
                GestionaleDocumentOid INTEGER NULL,
                EventType TEXT NOT NULL,
                RewardType TEXT NULL,
                AvailablePoints REAL NOT NULL DEFAULT 0,
                RequiredPoints REAL NOT NULL DEFAULT 0,
                AppliedRowId TEXT NULL,
                Title TEXT NOT NULL,
                Message TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ReorderLists (
                Id TEXT PRIMARY KEY,
                Titolo TEXT NOT NULL,
                Stato TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                ClosedAt TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS ReorderListItems (
                Id TEXT PRIMARY KEY,
                ListId TEXT NOT NULL,
                ArticoloOid INTEGER NULL,
                CodiceArticolo TEXT NOT NULL,
                Descrizione TEXT NOT NULL,
                Quantita REAL NOT NULL,
                QuantitaDaOrdinare REAL NOT NULL DEFAULT 0,
                UnitaMisura TEXT NOT NULL DEFAULT '',
                FornitoreSuggeritoOid INTEGER NULL,
                FornitoreSuggeritoNome TEXT NOT NULL DEFAULT '',
                FornitoreSelezionatoOid INTEGER NULL,
                FornitoreSelezionatoNome TEXT NOT NULL DEFAULT '',
                PrezzoSuggerito REAL NULL,
                IvaOid INTEGER NOT NULL DEFAULT 1,
                Motivo TEXT NOT NULL,
                Stato TEXT NOT NULL,
                Operatore TEXT NOT NULL DEFAULT '',
                Note TEXT NOT NULL DEFAULT '',
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ReorderSupplierDrafts (
                Id TEXT PRIMARY KEY,
                ListId TEXT NOT NULL,
                SupplierName TEXT NOT NULL,
                LocalCounter INTEGER NOT NULL,
                DraftDate TEXT NOT NULL,
                Status TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                OrderedAt TEXT NULL,
                RegisteredOnFmAt TEXT NULL,
                ClosedAt TEXT NULL,
                FmDocumentOid INTEGER NULL,
                FmDocumentNumber INTEGER NULL,
                FmDocumentYear INTEGER NULL
            );

            CREATE TABLE IF NOT EXISTS ReorderArticleSettingsEntries (
                SettingsKey TEXT PRIMARY KEY,
                ArticoloOid INTEGER NOT NULL,
                CodiceArticolo TEXT NOT NULL DEFAULT '',
                DescrizioneArticolo TEXT NOT NULL DEFAULT '',
                BarcodeAlternativo TEXT NULL,
                VarianteDettaglioOid1 INTEGER NULL,
                VarianteDettaglioOid2 INTEGER NULL,
                VarianteLabel TEXT NOT NULL DEFAULT '',
                AcquistoAConfezione INTEGER NOT NULL DEFAULT 0,
                VenditaAPezzoSingolo INTEGER NOT NULL DEFAULT 0,
                PezziPerConfezione REAL NULL,
                MultiploOrdine REAL NULL,
                LottoMinimoOrdine REAL NULL,
                GiorniCopertura INTEGER NULL,
                PrezzoConfezione REAL NULL,
                PrezzoSingolo REAL NULL,
                PrezzoVenditaRiferimento REAL NULL,
                QuantitaPromo REAL NULL,
                PrezzoPromo REAL NULL,
                Note TEXT NOT NULL DEFAULT '',
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS LocalArticleTags (
                ArticoloOid INTEGER PRIMARY KEY,
                TagsText TEXT NOT NULL DEFAULT '',
                UpdatedAt TEXT NOT NULL
            );
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);

        var alterCommand = connection.CreateCommand();
        alterCommand.CommandText =
            """
            ALTER TABLE LocalDocumentRows ADD COLUMN ScontoPercentuale REAL NOT NULL DEFAULT 0;
            """;

        try
        {
            await alterCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException)
        {
            // La colonna esiste gia`: non serve alcuna azione.
        }

        await EnsureRowDiscountColumnAsync(connection, "Sconto1", cancellationToken);
        await EnsureRowDiscountColumnAsync(connection, "Sconto2", cancellationToken);
        await EnsureRowDiscountColumnAsync(connection, "Sconto3", cancellationToken);
        await EnsureRowDiscountColumnAsync(connection, "Sconto4", cancellationToken);
        await EnsureLocalDocumentsColumnAsync(connection, "ListinoOid", "INTEGER NULL", cancellationToken);
        await EnsureLocalDocumentsColumnAsync(connection, "ListinoNome", "TEXT NULL", cancellationToken);

        var alterDisponibilitaCommand = connection.CreateCommand();
        alterDisponibilitaCommand.CommandText =
            """
            ALTER TABLE LocalDocumentRows ADD COLUMN DisponibilitaRiferimento REAL NOT NULL DEFAULT 0;
            """;

        try
        {
            await alterDisponibilitaCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException)
        {
            // La colonna esiste gia`: non serve alcuna azione.
        }

        var alterScontoDocumentoCommand = connection.CreateCommand();
        alterScontoDocumentoCommand.CommandText =
            """
            ALTER TABLE LocalDocuments ADD COLUMN ScontoDocumento REAL NOT NULL DEFAULT 0;
            """;

        try
        {
            await alterScontoDocumentoCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException)
        {
            // La colonna esiste gia`: non serve alcuna azione.
        }

        await EnsureLocalDocumentRowColumnAsync(connection, "PromoCampaignOid", "INTEGER NULL", cancellationToken);
        await EnsureLocalDocumentRowColumnAsync(connection, "PromoRuleId", "TEXT NULL", cancellationToken);
        await EnsureLocalDocumentRowColumnAsync(connection, "PromoEventId", "TEXT NULL", cancellationToken);
        await EnsureLocalDocumentColumnAsync(connection, "ModalitaChiusura", "TEXT NOT NULL DEFAULT 'BozzaLocale'", cancellationToken);
        await EnsureLocalDocumentColumnAsync(connection, "CategoriaDocumentoBanco", "TEXT NOT NULL DEFAULT 'Indeterminata'", cancellationToken);
        await EnsureLocalDocumentColumnAsync(connection, "HasComponenteSospeso", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await EnsureLocalDocumentColumnAsync(connection, "StatoFiscaleBanco", "TEXT NOT NULL DEFAULT 'Nessuno'", cancellationToken);
        await EnsureLocalDocumentColumnAsync(connection, "ClienteOid", "INTEGER NULL", cancellationToken);
        await EnsureLocalDocumentColumnAsync(connection, "DocumentoGestionaleOid", "INTEGER NULL", cancellationToken);
        await EnsureLocalDocumentColumnAsync(connection, "NumeroDocumentoGestionale", "INTEGER NULL", cancellationToken);
        await EnsureLocalDocumentColumnAsync(connection, "AnnoDocumentoGestionale", "INTEGER NULL", cancellationToken);
        await EnsureLocalDocumentColumnAsync(connection, "DataDocumentoGestionale", "TEXT NULL", cancellationToken);
        await EnsureLocalDocumentColumnAsync(connection, "DataPagamentoFinale", "TEXT NULL", cancellationToken);
        await EnsureLocalDocumentColumnAsync(connection, "DataComandoFiscaleFinale", "TEXT NULL", cancellationToken);
        await EnsurePromotionEventColumnAsync(connection, "RuleId", "TEXT NULL", cancellationToken);
        await EnsureReorderListItemColumnAsync(connection, "UnitaMisura", "TEXT NOT NULL DEFAULT ''", cancellationToken);
        await EnsureReorderListItemColumnAsync(connection, "QuantitaDaOrdinare", "REAL NOT NULL DEFAULT 0", cancellationToken);
        await EnsureReorderListItemColumnAsync(connection, "FornitoreSuggeritoOid", "INTEGER NULL", cancellationToken);
        await EnsureReorderListItemColumnAsync(connection, "FornitoreSuggeritoNome", "TEXT NOT NULL DEFAULT ''", cancellationToken);
        await EnsureReorderListItemColumnAsync(connection, "FornitoreSelezionatoOid", "INTEGER NULL", cancellationToken);
        await EnsureReorderListItemColumnAsync(connection, "FornitoreSelezionatoNome", "TEXT NOT NULL DEFAULT ''", cancellationToken);
        await EnsureReorderListItemColumnAsync(connection, "PrezzoSuggerito", "REAL NULL", cancellationToken);
        await EnsureReorderListItemColumnAsync(connection, "IvaOid", "INTEGER NOT NULL DEFAULT 1", cancellationToken);
        await EnsureReorderListItemColumnAsync(connection, "Operatore", "TEXT NOT NULL DEFAULT ''", cancellationToken);
        await EnsureReorderListItemColumnAsync(connection, "Note", "TEXT NOT NULL DEFAULT ''", cancellationToken);
        await EnsureReorderSupplierDraftColumnAsync(connection, "FmDocumentOid", "INTEGER NULL", cancellationToken);
        await EnsureReorderSupplierDraftColumnAsync(connection, "FmDocumentNumber", "INTEGER NULL", cancellationToken);
        await EnsureReorderSupplierDraftColumnAsync(connection, "FmDocumentYear", "INTEGER NULL", cancellationToken);
        await EnsureReorderArticleSettingsColumnAsync(connection, "PrezzoConfezione", "REAL NULL", cancellationToken);
        await EnsureReorderArticleSettingsColumnAsync(connection, "PrezzoSingolo", "REAL NULL", cancellationToken);
        await EnsureReorderArticleSettingsColumnAsync(connection, "PrezzoVenditaRiferimento", "REAL NULL", cancellationToken);
        await EnsureReorderArticleSettingsColumnAsync(connection, "QuantitaPromo", "REAL NULL", cancellationToken);
        await EnsureReorderArticleSettingsColumnAsync(connection, "PrezzoPromo", "REAL NULL", cancellationToken);
        await EnsureCampaignRewardRuleColumnAsync(connection, "RewardArticleTipoArticoloOid", "INTEGER NULL", cancellationToken);
        await EnsureCampaignRewardRuleColumnAsync(connection, "RewardArticlePrezzoVendita", "REAL NULL", cancellationToken);
        await EnsureLegacyPointsRewardRuleColumnsAsync(connection, cancellationToken);
        await MigrateLegacyRewardRulesAsync(connection, cancellationToken);
    }

    private static async Task EnsureReorderArticleSettingsColumnAsync(
        SqliteConnection connection,
        string columnName,
        string sqlType,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"ALTER TABLE ReorderArticleSettingsEntries ADD COLUMN {columnName} {sqlType};";

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException)
        {
            // La colonna esiste gia`: non serve alcuna azione.
        }
    }

    private static async Task EnsureReorderListItemColumnAsync(
        SqliteConnection connection,
        string columnName,
        string sqlType,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"ALTER TABLE ReorderListItems ADD COLUMN {columnName} {sqlType};";

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException)
        {
            // La colonna esiste gia`: non serve alcuna azione.
        }
    }

    private static async Task EnsureReorderSupplierDraftColumnAsync(
        SqliteConnection connection,
        string columnName,
        string sqlType,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"ALTER TABLE ReorderSupplierDrafts ADD COLUMN {columnName} {sqlType};";

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException)
        {
            // La colonna esiste gia`: non serve alcuna azione.
        }
    }

    private static async Task EnsureLocalDocumentColumnAsync(
        SqliteConnection connection,
        string columnName,
        string sqlType,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"ALTER TABLE LocalDocuments ADD COLUMN {columnName} {sqlType};";

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException)
        {
            // La colonna esiste gia`: non serve alcuna azione.
        }
    }

    private static async Task EnsureRowDiscountColumnAsync(
        SqliteConnection connection,
        string columnName,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"ALTER TABLE LocalDocumentRows ADD COLUMN {columnName} REAL NOT NULL DEFAULT 0;";

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException)
        {
            // La colonna esiste gia`: non serve alcuna azione.
        }
    }

    private static async Task EnsureLocalDocumentsColumnAsync(
        SqliteConnection connection,
        string columnName,
        string columnType,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"ALTER TABLE LocalDocuments ADD COLUMN {columnName} {columnType};";

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException)
        {
            // La colonna esiste gia`: non serve alcuna azione.
        }
    }

    private static async Task EnsureLocalDocumentRowColumnAsync(
        SqliteConnection connection,
        string columnName,
        string sqlType,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"ALTER TABLE LocalDocumentRows ADD COLUMN {columnName} {sqlType};";

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException)
        {
            // La colonna esiste gia`: non serve alcuna azione.
        }
    }

    private static async Task EnsureCampaignRewardRuleColumnAsync(
        SqliteConnection connection,
        string columnName,
        string sqlType,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"ALTER TABLE CampaignRewardRules ADD COLUMN {columnName} {sqlType};";

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException)
        {
            // La colonna esiste gia`: non serve alcuna azione.
        }
    }

    private static async Task EnsureLegacyPointsRewardRuleColumnsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await EnsureLegacyPointsRewardRuleColumnAsync(connection, "RewardArticleIvaOid", "INTEGER NULL", cancellationToken);
        await EnsureLegacyPointsRewardRuleColumnAsync(connection, "RewardArticleAliquotaIva", "REAL NULL", cancellationToken);
        await EnsureLegacyPointsRewardRuleColumnAsync(connection, "RewardArticleTipoArticoloOid", "INTEGER NULL", cancellationToken);
        await EnsureLegacyPointsRewardRuleColumnAsync(connection, "RewardArticlePrezzoVendita", "REAL NULL", cancellationToken);
        await EnsureLegacyPointsRewardRuleColumnAsync(connection, "DiscountPercent", "REAL NULL", cancellationToken);
    }

    private static async Task EnsureLegacyPointsRewardRuleColumnAsync(
        SqliteConnection connection,
        string columnName,
        string sqlType,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"ALTER TABLE PointsRewardRules ADD COLUMN {columnName} {sqlType};";

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException)
        {
            // La colonna esiste gia`: non serve alcuna azione.
        }
    }

    private static async Task MigrateLegacyRewardRulesAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM CampaignRewardRules;";
        var existingCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));
        if (existingCount > 0)
        {
            return;
        }

        var legacyCheckCommand = connection.CreateCommand();
        legacyCheckCommand.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'PointsRewardRules';";
        var hasLegacyTable = await legacyCheckCommand.ExecuteScalarAsync(cancellationToken) is not null;
        if (!hasLegacyTable)
        {
            return;
        }

        var legacyReadCommand = connection.CreateCommand();
        legacyReadCommand.CommandText =
            """
            SELECT CampaignOid, RequiredPoints, RewardType, DiscountAmount, DiscountPercent, RewardArticleOid, RewardArticleCode, RewardArticleDescription, RewardArticleIvaOid, RewardArticleAliquotaIva, RewardQuantity, EnableSaleCheck, UpdatedAt
            FROM PointsRewardRules;
            """;

        var migratedRules = new List<(int CampaignOid, Guid RuleId, string RuleName, int IsActive, double? RequiredPoints, string RewardType, double? DiscountAmount, double? DiscountPercent, int? RewardArticleOid, string? RewardArticleCode, string? RewardArticleDescription, int? RewardArticleIvaOid, double? RewardArticleAliquotaIva, int? RewardArticleTipoArticoloOid, double? RewardArticlePrezzoVendita, double RewardQuantity, int EnableSaleCheck, string? Notes, string UpdatedAt)>();
        await using (var reader = await legacyReadCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                migratedRules.Add((
                    reader.GetInt32(0),
                    Guid.NewGuid(),
                    "Regola migrata",
                    1,
                    reader.IsDBNull(1) ? null : reader.GetDouble(1),
                    reader.IsDBNull(2) ? "ScontoFisso" : reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetDouble(3),
                    reader.IsDBNull(4) ? null : reader.GetDouble(4),
                    reader.IsDBNull(5) ? null : reader.GetInt32(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.IsDBNull(7) ? null : reader.GetString(7),
                    reader.IsDBNull(8) ? null : reader.GetInt32(8),
                    reader.IsDBNull(9) ? null : reader.GetDouble(9),
                    null,
                    null,
                    reader.IsDBNull(10) ? 1 : reader.GetDouble(10),
                    reader.IsDBNull(11) ? 0 : reader.GetInt32(11),
                    null,
                    reader.IsDBNull(12) ? DateTimeOffset.Now.ToString("O") : reader.GetString(12)));
            }
        }

        foreach (var migratedRule in migratedRules)
        {
            var insertCommand = connection.CreateCommand();
            insertCommand.CommandText =
                """
                INSERT INTO CampaignRewardRules (
                    RuleId, CampaignOid, RuleName, IsActive, RequiredPoints, RewardType, DiscountAmount, DiscountPercent, RewardArticleOid, RewardArticleCode, RewardArticleDescription, RewardArticleIvaOid, RewardArticleAliquotaIva, RewardArticleTipoArticoloOid, RewardArticlePrezzoVendita, RewardQuantity, EnableSaleCheck, Notes, UpdatedAt
                )
                VALUES (
                    $ruleId, $campaignOid, $ruleName, $isActive, $requiredPoints, $rewardType, $discountAmount, $discountPercent, $rewardArticleOid, $rewardArticleCode, $rewardArticleDescription, $rewardArticleIvaOid, $rewardArticleAliquotaIva, $rewardArticleTipoArticoloOid, $rewardArticlePrezzoVendita, $rewardQuantity, $enableSaleCheck, $notes, $updatedAt
                );
                """;
            insertCommand.Parameters.AddWithValue("$ruleId", migratedRule.RuleId.ToString("D"));
            insertCommand.Parameters.AddWithValue("$campaignOid", migratedRule.CampaignOid);
            insertCommand.Parameters.AddWithValue("$ruleName", migratedRule.RuleName);
            insertCommand.Parameters.AddWithValue("$isActive", migratedRule.IsActive);
            insertCommand.Parameters.AddWithValue("$requiredPoints", (object?)migratedRule.RequiredPoints ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("$rewardType", migratedRule.RewardType);
            insertCommand.Parameters.AddWithValue("$discountAmount", (object?)migratedRule.DiscountAmount ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("$discountPercent", (object?)migratedRule.DiscountPercent ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("$rewardArticleOid", (object?)migratedRule.RewardArticleOid ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("$rewardArticleCode", (object?)migratedRule.RewardArticleCode ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("$rewardArticleDescription", (object?)migratedRule.RewardArticleDescription ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("$rewardArticleIvaOid", (object?)migratedRule.RewardArticleIvaOid ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("$rewardArticleAliquotaIva", (object?)migratedRule.RewardArticleAliquotaIva ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("$rewardArticleTipoArticoloOid", (object?)migratedRule.RewardArticleTipoArticoloOid ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("$rewardArticlePrezzoVendita", (object?)migratedRule.RewardArticlePrezzoVendita ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("$rewardQuantity", migratedRule.RewardQuantity);
            insertCommand.Parameters.AddWithValue("$enableSaleCheck", migratedRule.EnableSaleCheck);
            insertCommand.Parameters.AddWithValue("$notes", (object?)migratedRule.Notes ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("$updatedAt", migratedRule.UpdatedAt);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task EnsurePromotionEventColumnAsync(
        SqliteConnection connection,
        string columnName,
        string sqlType,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"ALTER TABLE PromotionEvents ADD COLUMN {columnName} {sqlType};";

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException)
        {
            // La colonna esiste gia`: non serve alcuna azione.
        }
    }
}
