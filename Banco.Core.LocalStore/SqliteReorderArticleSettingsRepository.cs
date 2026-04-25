using Banco.Riordino;
using Banco.Vendita.Abstractions;
using Microsoft.Data.Sqlite;

namespace Banco.Core.LocalStore;

public sealed class SqliteReorderArticleSettingsRepository : IReorderArticleSettingsRepository
{
    private readonly IApplicationConfigurationService _configurationService;

    public SqliteReorderArticleSettingsRepository(IApplicationConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    public async Task<ReorderArticleSettings?> GetByArticleAsync(
        int articoloOid,
        int? varianteDettaglioOid1,
        int? varianteDettaglioOid2,
        string? barcodeAlternativo,
        CancellationToken cancellationToken = default)
    {
        if (articoloOid <= 0)
        {
            return null;
        }

        var settingsKey = ReorderArticleSettings.BuildSettingsKey(articoloOid, varianteDettaglioOid1, varianteDettaglioOid2, barcodeAlternativo);
        var parentSettingsKey = ReorderArticleSettings.BuildSettingsKey(articoloOid, null, null, null);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await connection.OpenAsync(cancellationToken);

        var exact = await LoadBySettingsKeyAsync(connection, settingsKey, cancellationToken);
        if (exact is not null)
        {
            return exact;
        }

        if (!HasVariantIdentity(varianteDettaglioOid1, varianteDettaglioOid2, barcodeAlternativo))
        {
            return null;
        }

        var inherited = await LoadBySettingsKeyAsync(connection, parentSettingsKey, cancellationToken);
        if (inherited is null)
        {
            return null;
        }

        inherited.InheritedFromParent = true;
        inherited.InheritedFromSettingsKey = inherited.SettingsKey;
        return inherited;
    }

    public async Task SaveAsync(ReorderArticleSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.ArticoloOid <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(settings.ArticoloOid));
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await connection.OpenAsync(cancellationToken);

        settings.SettingsKey = string.IsNullOrWhiteSpace(settings.SettingsKey)
            ? ReorderArticleSettings.BuildSettingsKey(settings.ArticoloOid, settings.VarianteDettaglioOid1, settings.VarianteDettaglioOid2, settings.BarcodeAlternativo)
            : settings.SettingsKey;

        var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO ReorderArticleSettingsEntries (
                SettingsKey, ArticoloOid, CodiceArticolo, DescrizioneArticolo, BarcodeAlternativo,
                VarianteDettaglioOid1, VarianteDettaglioOid2, VarianteLabel,
                AcquistoAConfezione, VenditaAPezzoSingolo, PezziPerConfezione, MultiploOrdine,
                LottoMinimoOrdine, GiorniCopertura, PrezzoConfezione, PrezzoSingolo, PrezzoVenditaRiferimento,
                QuantitaPromo, PrezzoPromo, Note, UpdatedAt)
            VALUES (
                $settingsKey, $articoloOid, $codiceArticolo, $descrizioneArticolo, $barcodeAlternativo,
                $varianteDettaglioOid1, $varianteDettaglioOid2, $varianteLabel,
                $acquistoAConfezione, $venditaAPezzoSingolo, $pezziPerConfezione, $multiploOrdine,
                $lottoMinimoOrdine, $giorniCopertura, $prezzoConfezione, $prezzoSingolo, $prezzoVenditaRiferimento,
                $quantitaPromo, $prezzoPromo, $note, $updatedAt)
            ON CONFLICT(SettingsKey) DO UPDATE SET
                CodiceArticolo = excluded.CodiceArticolo,
                DescrizioneArticolo = excluded.DescrizioneArticolo,
                BarcodeAlternativo = excluded.BarcodeAlternativo,
                VarianteDettaglioOid1 = excluded.VarianteDettaglioOid1,
                VarianteDettaglioOid2 = excluded.VarianteDettaglioOid2,
                VarianteLabel = excluded.VarianteLabel,
                AcquistoAConfezione = excluded.AcquistoAConfezione,
                VenditaAPezzoSingolo = excluded.VenditaAPezzoSingolo,
                PezziPerConfezione = excluded.PezziPerConfezione,
                MultiploOrdine = excluded.MultiploOrdine,
                LottoMinimoOrdine = excluded.LottoMinimoOrdine,
                GiorniCopertura = excluded.GiorniCopertura,
                PrezzoConfezione = excluded.PrezzoConfezione,
                PrezzoSingolo = excluded.PrezzoSingolo,
                PrezzoVenditaRiferimento = excluded.PrezzoVenditaRiferimento,
                QuantitaPromo = excluded.QuantitaPromo,
                PrezzoPromo = excluded.PrezzoPromo,
                Note = excluded.Note,
                UpdatedAt = excluded.UpdatedAt;
            """;
        command.Parameters.AddWithValue("$settingsKey", settings.SettingsKey);
        command.Parameters.AddWithValue("$articoloOid", settings.ArticoloOid);
        command.Parameters.AddWithValue("$codiceArticolo", NormalizeText(settings.CodiceArticolo));
        command.Parameters.AddWithValue("$descrizioneArticolo", NormalizeText(settings.DescrizioneArticolo));
        command.Parameters.AddWithValue("$barcodeAlternativo", string.IsNullOrWhiteSpace(settings.BarcodeAlternativo) ? DBNull.Value : settings.BarcodeAlternativo.Trim());
        command.Parameters.AddWithValue("$varianteDettaglioOid1", settings.VarianteDettaglioOid1.HasValue ? settings.VarianteDettaglioOid1.Value : DBNull.Value);
        command.Parameters.AddWithValue("$varianteDettaglioOid2", settings.VarianteDettaglioOid2.HasValue ? settings.VarianteDettaglioOid2.Value : DBNull.Value);
        command.Parameters.AddWithValue("$varianteLabel", NormalizeText(settings.VarianteLabel));
        command.Parameters.AddWithValue("$acquistoAConfezione", settings.AcquistoAConfezione ? 1 : 0);
        command.Parameters.AddWithValue("$venditaAPezzoSingolo", settings.VenditaAPezzoSingolo ? 1 : 0);
        command.Parameters.AddWithValue("$pezziPerConfezione", settings.PezziPerConfezione.HasValue ? Convert.ToDouble(settings.PezziPerConfezione.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$multiploOrdine", settings.MultiploOrdine.HasValue ? Convert.ToDouble(settings.MultiploOrdine.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$lottoMinimoOrdine", settings.LottoMinimoOrdine.HasValue ? Convert.ToDouble(settings.LottoMinimoOrdine.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$giorniCopertura", settings.GiorniCopertura.HasValue ? settings.GiorniCopertura.Value : DBNull.Value);
        command.Parameters.AddWithValue("$prezzoConfezione", settings.PrezzoConfezione.HasValue ? Convert.ToDouble(settings.PrezzoConfezione.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$prezzoSingolo", settings.PrezzoSingolo.HasValue ? Convert.ToDouble(settings.PrezzoSingolo.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$prezzoVenditaRiferimento", settings.PrezzoVenditaRiferimento.HasValue ? Convert.ToDouble(settings.PrezzoVenditaRiferimento.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$quantitaPromo", settings.QuantitaPromo.HasValue ? Convert.ToDouble(settings.QuantitaPromo.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$prezzoPromo", settings.PrezzoPromo.HasValue ? Convert.ToDouble(settings.PrezzoPromo.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$note", NormalizeText(settings.Note));
        command.Parameters.AddWithValue("$updatedAt", settings.UpdatedAt.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var settings = await _configurationService.LoadAsync();
        return new SqliteConnection($"Data Source={settings.LocalStore.DatabasePath}");
    }

    private static bool HasVariantIdentity(int? varianteDettaglioOid1, int? varianteDettaglioOid2, string? barcodeAlternativo) =>
        varianteDettaglioOid1.HasValue || varianteDettaglioOid2.HasValue || !string.IsNullOrWhiteSpace(barcodeAlternativo);

    private static async Task<ReorderArticleSettings?> LoadBySettingsKeyAsync(
        SqliteConnection connection,
        string settingsKey,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT SettingsKey, ArticoloOid, CodiceArticolo, DescrizioneArticolo, BarcodeAlternativo,
                   VarianteDettaglioOid1, VarianteDettaglioOid2, VarianteLabel,
                   AcquistoAConfezione, VenditaAPezzoSingolo, PezziPerConfezione, MultiploOrdine,
                   LottoMinimoOrdine, GiorniCopertura, PrezzoConfezione, PrezzoSingolo, PrezzoVenditaRiferimento,
                   QuantitaPromo, PrezzoPromo, Note, UpdatedAt
            FROM ReorderArticleSettingsEntries
            WHERE SettingsKey = $settingsKey
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$settingsKey", settingsKey);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ReorderArticleSettings
        {
            SettingsKey = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
            ArticoloOid = reader.GetInt32(1),
            CodiceArticolo = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            DescrizioneArticolo = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
            BarcodeAlternativo = reader.IsDBNull(4) ? null : reader.GetString(4),
            VarianteDettaglioOid1 = reader.IsDBNull(5) ? null : reader.GetInt32(5),
            VarianteDettaglioOid2 = reader.IsDBNull(6) ? null : reader.GetInt32(6),
            VarianteLabel = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
            AcquistoAConfezione = !reader.IsDBNull(8) && reader.GetInt32(8) == 1,
            VenditaAPezzoSingolo = !reader.IsDBNull(9) && reader.GetInt32(9) == 1,
            PezziPerConfezione = reader.IsDBNull(10) ? null : Convert.ToDecimal(reader.GetDouble(10)),
            MultiploOrdine = reader.IsDBNull(11) ? null : Convert.ToDecimal(reader.GetDouble(11)),
            LottoMinimoOrdine = reader.IsDBNull(12) ? null : Convert.ToDecimal(reader.GetDouble(12)),
            GiorniCopertura = reader.IsDBNull(13) ? null : reader.GetInt32(13),
            PrezzoConfezione = reader.IsDBNull(14) ? null : Convert.ToDecimal(reader.GetDouble(14)),
            PrezzoSingolo = reader.IsDBNull(15) ? null : Convert.ToDecimal(reader.GetDouble(15)),
            PrezzoVenditaRiferimento = reader.IsDBNull(16) ? null : Convert.ToDecimal(reader.GetDouble(16)),
            QuantitaPromo = reader.IsDBNull(17) ? null : Convert.ToDecimal(reader.GetDouble(17)),
            PrezzoPromo = reader.IsDBNull(18) ? null : Convert.ToDecimal(reader.GetDouble(18)),
            Note = reader.IsDBNull(19) ? string.Empty : reader.GetString(19),
            UpdatedAt = reader.IsDBNull(20) ? DateTimeOffset.MinValue : DateTimeOffset.Parse(reader.GetString(20))
        };
    }

    private static string NormalizeText(string? value) => value?.Trim() ?? string.Empty;
}
