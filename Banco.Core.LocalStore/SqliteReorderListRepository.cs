using Banco.Riordino;
using Banco.Vendita.Abstractions;
using Microsoft.Data.Sqlite;

namespace Banco.Core.LocalStore;

public sealed class SqliteReorderListRepository : IReorderListRepository
{
    private readonly IApplicationConfigurationService _configurationService;

    public event Action? CurrentListChanged;

    public SqliteReorderListRepository(IApplicationConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    public async Task<ReorderListSnapshot> GetCurrentListAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await connection.OpenAsync(cancellationToken);

        var list = await EnsureCurrentListAsync(connection, cancellationToken);
        var items = await LoadItemsAsync(connection, list.Id, cancellationToken);
        return new ReorderListSnapshot
        {
            List = list,
            Items = items
        };
    }

    public async Task AddOrIncrementItemAsync(ReorderListItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var list = await EnsureCurrentListAsync(connection, cancellationToken);
        var existingItem = await FindCurrentItemAsync(connection, list.Id, item.ArticoloOid, item.CodiceArticolo, item.Descrizione, cancellationToken);
        if (existingItem is not null)
        {
            var updateCommand = connection.CreateCommand();
            updateCommand.Transaction = (SqliteTransaction)transaction;
            updateCommand.CommandText =
                """
                UPDATE ReorderListItems
                SET Quantita = $quantita,
                    QuantitaDaOrdinare = $quantitaDaOrdinare,
                    UnitaMisura = $unitaMisura,
                    FornitoreSuggeritoOid = $fornitoreSuggeritoOid,
                    FornitoreSuggeritoNome = $fornitoreSuggeritoNome,
                    FornitoreSelezionatoOid = CASE
                        WHEN FornitoreSelezionatoOid IS NULL OR FornitoreSelezionatoOid = 0 THEN $fornitoreSelezionatoOid
                        ELSE FornitoreSelezionatoOid
                    END,
                    FornitoreSelezionatoNome = CASE
                        WHEN COALESCE(FornitoreSelezionatoNome, '') = '' THEN $fornitoreSelezionatoNome
                        ELSE FornitoreSelezionatoNome
                    END,
                    PrezzoSuggerito = $prezzoSuggerito,
                    IvaOid = $ivaOid,
                    Motivo = $motivo,
                    Operatore = $operatore,
                    UpdatedAt = $updatedAt
                WHERE Id = $id;
                """;
            updateCommand.Parameters.AddWithValue("$id", existingItem.Id.ToString("D"));
            var updatedQuantity = existingItem.Quantita + item.Quantita;
            var updatedQuantityToOrder = existingItem.QuantitaDaOrdinare + (item.QuantitaDaOrdinare > 0 ? item.QuantitaDaOrdinare : item.Quantita);
            updateCommand.Parameters.AddWithValue("$quantita", Convert.ToDouble(updatedQuantity));
            updateCommand.Parameters.AddWithValue("$quantitaDaOrdinare", Convert.ToDouble(updatedQuantityToOrder));
            updateCommand.Parameters.AddWithValue("$unitaMisura", NormalizeText(item.UnitaMisura, "PZ"));
            updateCommand.Parameters.AddWithValue("$fornitoreSuggeritoOid", item.FornitoreSuggeritoOid.HasValue ? item.FornitoreSuggeritoOid.Value : DBNull.Value);
            updateCommand.Parameters.AddWithValue("$fornitoreSuggeritoNome", NormalizeText(item.FornitoreSuggeritoNome));
            updateCommand.Parameters.AddWithValue("$fornitoreSelezionatoOid", item.FornitoreSelezionatoOid.HasValue ? item.FornitoreSelezionatoOid.Value : DBNull.Value);
            updateCommand.Parameters.AddWithValue("$fornitoreSelezionatoNome", NormalizeText(item.FornitoreSelezionatoNome));
            updateCommand.Parameters.AddWithValue("$prezzoSuggerito", item.PrezzoSuggerito.HasValue ? Convert.ToDouble(item.PrezzoSuggerito.Value) : DBNull.Value);
            updateCommand.Parameters.AddWithValue("$ivaOid", item.IvaOid <= 0 ? 1 : item.IvaOid);
            updateCommand.Parameters.AddWithValue("$motivo", item.Motivo.ToString());
            updateCommand.Parameters.AddWithValue("$operatore", NormalizeText(item.Operatore));
            updateCommand.Parameters.AddWithValue("$updatedAt", DateTimeOffset.Now.ToString("O"));
            await updateCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        else
        {
            var now = DateTimeOffset.Now;
            item.Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id;
            item.ListId = list.Id;
            item.CreatedAt = now;
            item.UpdatedAt = now;
            item.Quantita = item.Quantita <= 0 ? 1 : item.Quantita;
            item.QuantitaDaOrdinare = item.QuantitaDaOrdinare <= 0 ? item.Quantita : item.QuantitaDaOrdinare;
            item.UnitaMisura = NormalizeText(item.UnitaMisura, "PZ");
            item.CodiceArticolo = NormalizeText(item.CodiceArticolo);
            item.Descrizione = NormalizeText(item.Descrizione);
            item.FornitoreSuggeritoNome = NormalizeText(item.FornitoreSuggeritoNome);
            item.FornitoreSelezionatoNome = NormalizeText(item.FornitoreSelezionatoNome);
            item.Operatore = NormalizeText(item.Operatore);
            item.Note = NormalizeText(item.Note);

            var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = (SqliteTransaction)transaction;
            insertCommand.CommandText =
                """
                INSERT INTO ReorderListItems (
                    Id,
                    ListId,
                    ArticoloOid,
                    CodiceArticolo,
                    Descrizione,
                    Quantita,
                    QuantitaDaOrdinare,
                    UnitaMisura,
                    FornitoreSuggeritoOid,
                    FornitoreSuggeritoNome,
                    FornitoreSelezionatoOid,
                    FornitoreSelezionatoNome,
                    PrezzoSuggerito,
                    IvaOid,
                    Motivo,
                    Stato,
                    Operatore,
                    Note,
                    CreatedAt,
                    UpdatedAt)
                VALUES (
                    $id,
                    $listId,
                    $articoloOid,
                    $codiceArticolo,
                    $descrizione,
                    $quantita,
                    $quantitaDaOrdinare,
                    $unitaMisura,
                    $fornitoreSuggeritoOid,
                    $fornitoreSuggeritoNome,
                    $fornitoreSelezionatoOid,
                    $fornitoreSelezionatoNome,
                    $prezzoSuggerito,
                    $ivaOid,
                    $motivo,
                    $stato,
                    $operatore,
                    $note,
                    $createdAt,
                    $updatedAt);
                """;
            insertCommand.Parameters.AddWithValue("$id", item.Id.ToString("D"));
            insertCommand.Parameters.AddWithValue("$listId", item.ListId.ToString("D"));
            insertCommand.Parameters.AddWithValue("$articoloOid", item.ArticoloOid.HasValue ? item.ArticoloOid.Value : DBNull.Value);
            insertCommand.Parameters.AddWithValue("$codiceArticolo", item.CodiceArticolo);
            insertCommand.Parameters.AddWithValue("$descrizione", item.Descrizione);
            insertCommand.Parameters.AddWithValue("$quantita", Convert.ToDouble(item.Quantita));
            insertCommand.Parameters.AddWithValue("$quantitaDaOrdinare", Convert.ToDouble(item.QuantitaDaOrdinare));
            insertCommand.Parameters.AddWithValue("$unitaMisura", item.UnitaMisura);
            insertCommand.Parameters.AddWithValue("$fornitoreSuggeritoOid", item.FornitoreSuggeritoOid.HasValue ? item.FornitoreSuggeritoOid.Value : DBNull.Value);
            insertCommand.Parameters.AddWithValue("$fornitoreSuggeritoNome", item.FornitoreSuggeritoNome);
            insertCommand.Parameters.AddWithValue("$fornitoreSelezionatoOid", item.FornitoreSelezionatoOid.HasValue ? item.FornitoreSelezionatoOid.Value : DBNull.Value);
            insertCommand.Parameters.AddWithValue("$fornitoreSelezionatoNome", item.FornitoreSelezionatoNome);
            insertCommand.Parameters.AddWithValue("$prezzoSuggerito", item.PrezzoSuggerito.HasValue ? Convert.ToDouble(item.PrezzoSuggerito.Value) : DBNull.Value);
            insertCommand.Parameters.AddWithValue("$ivaOid", item.IvaOid <= 0 ? 1 : item.IvaOid);
            insertCommand.Parameters.AddWithValue("$motivo", item.Motivo.ToString());
            insertCommand.Parameters.AddWithValue("$stato", item.Stato.ToString());
            insertCommand.Parameters.AddWithValue("$operatore", item.Operatore);
            insertCommand.Parameters.AddWithValue("$note", item.Note);
            insertCommand.Parameters.AddWithValue("$createdAt", item.CreatedAt.ToString("O"));
            insertCommand.Parameters.AddWithValue("$updatedAt", item.UpdatedAt.ToString("O"));
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await UpdateListStateAsync(connection, list.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        CurrentListChanged?.Invoke();
    }

    public async Task SetItemOrderedAsync(Guid itemId, bool isOrdered, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var updateCommand = connection.CreateCommand();
        updateCommand.Transaction = (SqliteTransaction)transaction;
        updateCommand.CommandText =
            """
            UPDATE ReorderListItems
            SET Stato = $stato,
                UpdatedAt = $updatedAt
            WHERE Id = $id;
            """;
        updateCommand.Parameters.AddWithValue("$id", itemId.ToString("D"));
        updateCommand.Parameters.AddWithValue("$stato", isOrdered ? ReorderItemStatus.Ordinato.ToString() : ReorderItemStatus.DaOrdinare.ToString());
        updateCommand.Parameters.AddWithValue("$updatedAt", DateTimeOffset.Now.ToString("O"));
        await updateCommand.ExecuteNonQueryAsync(cancellationToken);

        var listId = await ResolveListIdAsync(connection, itemId, cancellationToken);
        if (listId.HasValue)
        {
            await UpdateListStateAsync(connection, listId.Value, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        CurrentListChanged?.Invoke();
    }

    public async Task RemoveItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var listId = await ResolveListIdAsync(connection, itemId, cancellationToken);

        var deleteCommand = connection.CreateCommand();
        deleteCommand.Transaction = (SqliteTransaction)transaction;
        deleteCommand.CommandText = "DELETE FROM ReorderListItems WHERE Id = $id;";
        deleteCommand.Parameters.AddWithValue("$id", itemId.ToString("D"));
        await deleteCommand.ExecuteNonQueryAsync(cancellationToken);

        if (listId.HasValue)
        {
            await UpdateListStateAsync(connection, listId.Value, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        CurrentListChanged?.Invoke();
    }

    public async Task RemoveSupplierDraftAsync(
        Guid listId,
        string supplierName,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var normalizedSupplierName = NormalizeText(supplierName, "Senza fornitore");

        var deleteItemsCommand = connection.CreateCommand();
        deleteItemsCommand.Transaction = (SqliteTransaction)transaction;
        deleteItemsCommand.CommandText =
            """
            DELETE FROM ReorderListItems
            WHERE ListId = $listId
              AND COALESCE(NULLIF(TRIM(FornitoreSelezionatoNome), ''), NULLIF(TRIM(FornitoreSuggeritoNome), ''), 'Senza fornitore') = $supplierName;
            """;
        deleteItemsCommand.Parameters.AddWithValue("$listId", listId.ToString("D"));
        deleteItemsCommand.Parameters.AddWithValue("$supplierName", normalizedSupplierName);
        await deleteItemsCommand.ExecuteNonQueryAsync(cancellationToken);

        var deleteDraftCommand = connection.CreateCommand();
        deleteDraftCommand.Transaction = (SqliteTransaction)transaction;
        deleteDraftCommand.CommandText =
            """
            DELETE FROM ReorderSupplierDrafts
            WHERE ListId = $listId
              AND SupplierName = $supplierName;
            """;
        deleteDraftCommand.Parameters.AddWithValue("$listId", listId.ToString("D"));
        deleteDraftCommand.Parameters.AddWithValue("$supplierName", normalizedSupplierName);
        await deleteDraftCommand.ExecuteNonQueryAsync(cancellationToken);

        await UpdateListStateAsync(connection, listId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        CurrentListChanged?.Invoke();
    }

    public async Task UpdateSelectedSupplierAsync(
        Guid itemId,
        int? supplierOid,
        string supplierName,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE ReorderListItems
            SET FornitoreSelezionatoOid = $supplierOid,
                FornitoreSelezionatoNome = $supplierName,
                UpdatedAt = $updatedAt
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", itemId.ToString("D"));
        command.Parameters.AddWithValue("$supplierOid", supplierOid.HasValue ? supplierOid.Value : DBNull.Value);
        command.Parameters.AddWithValue("$supplierName", supplierName ?? string.Empty);
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.Now.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateQuantityToOrderAsync(
        Guid itemId,
        decimal quantityToOrder,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await connection.OpenAsync(cancellationToken);

        var normalizedQuantity = quantityToOrder <= 0 ? 1 : quantityToOrder;

        var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE ReorderListItems
            SET QuantitaDaOrdinare = $quantitaDaOrdinare,
                UpdatedAt = $updatedAt
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", itemId.ToString("D"));
        command.Parameters.AddWithValue("$quantitaDaOrdinare", Convert.ToDouble(normalizedQuantity));
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.Now.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ReorderSupplierDraftState>> GetOrCreateSupplierDraftStatesAsync(
        Guid listId,
        IReadOnlyList<string> supplierNames,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var normalizedSupplierNames = supplierNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => NormalizeText(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingStates = await LoadSupplierDraftStatesAsync(connection, listId, cancellationToken);
        var duplicateStates = existingStates
            .GroupBy(state => state.SupplierName, StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => group
                .OrderByDescending(state => state.UpdatedAt)
                .ThenByDescending(state => state.LocalCounter)
                .Skip(1))
            .ToList();

        foreach (var duplicateState in duplicateStates)
        {
            var deleteDuplicateCommand = connection.CreateCommand();
            deleteDuplicateCommand.Transaction = (SqliteTransaction)transaction;
            deleteDuplicateCommand.CommandText = "DELETE FROM ReorderSupplierDrafts WHERE Id = $id;";
            deleteDuplicateCommand.Parameters.AddWithValue("$id", duplicateState.Id.ToString("D"));
            await deleteDuplicateCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        if (duplicateStates.Count > 0)
        {
            existingStates = existingStates
                .GroupBy(state => state.SupplierName, StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(state => state.UpdatedAt)
                    .ThenByDescending(state => state.LocalCounter)
                    .First())
                .OrderBy(state => state.LocalCounter)
                .ThenBy(state => state.SupplierName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var existingBySupplier = existingStates.ToDictionary(state => state.SupplierName, StringComparer.OrdinalIgnoreCase);

        var obsoleteStates = existingStates
            .Where(state => !normalizedSupplierNames.Contains(state.SupplierName, StringComparer.OrdinalIgnoreCase))
            .ToList();

        foreach (var obsoleteState in obsoleteStates)
        {
            var deleteCommand = connection.CreateCommand();
            deleteCommand.Transaction = (SqliteTransaction)transaction;
            deleteCommand.CommandText = "DELETE FROM ReorderSupplierDrafts WHERE Id = $id;";
            deleteCommand.Parameters.AddWithValue("$id", obsoleteState.Id.ToString("D"));
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        var currentCounter = existingStates.Count == 0 ? 0 : existingStates.Max(state => state.LocalCounter);
        var now = DateTimeOffset.Now;

        foreach (var supplierName in normalizedSupplierNames)
        {
            if (existingBySupplier.ContainsKey(supplierName))
            {
                continue;
            }

            currentCounter++;
            var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = (SqliteTransaction)transaction;
            insertCommand.CommandText =
                """
                INSERT INTO ReorderSupplierDrafts (
                    Id,
                    ListId,
                    SupplierName,
                    LocalCounter,
                    DraftDate,
                    Status,
                    UpdatedAt,
                    OrderedAt,
                    RegisteredOnFmAt,
                    ClosedAt,
                    FmDocumentOid,
                    FmDocumentNumber,
                    FmDocumentYear)
                VALUES (
                    $id,
                    $listId,
                    $supplierName,
                    $localCounter,
                    $draftDate,
                    $status,
                    $updatedAt,
                    $orderedAt,
                    $registeredOnFmAt,
                    $closedAt,
                    $fmDocumentOid,
                    $fmDocumentNumber,
                    $fmDocumentYear);
                """;
            insertCommand.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
            insertCommand.Parameters.AddWithValue("$listId", listId.ToString("D"));
            insertCommand.Parameters.AddWithValue("$supplierName", supplierName);
            insertCommand.Parameters.AddWithValue("$localCounter", currentCounter);
            insertCommand.Parameters.AddWithValue("$draftDate", now.ToString("O"));
            insertCommand.Parameters.AddWithValue("$status", ReorderSupplierDraftStatus.Aperta.ToString());
            insertCommand.Parameters.AddWithValue("$updatedAt", now.ToString("O"));
            insertCommand.Parameters.AddWithValue("$orderedAt", DBNull.Value);
            insertCommand.Parameters.AddWithValue("$registeredOnFmAt", DBNull.Value);
            insertCommand.Parameters.AddWithValue("$closedAt", DBNull.Value);
            insertCommand.Parameters.AddWithValue("$fmDocumentOid", DBNull.Value);
            insertCommand.Parameters.AddWithValue("$fmDocumentNumber", DBNull.Value);
            insertCommand.Parameters.AddWithValue("$fmDocumentYear", DBNull.Value);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return await LoadSupplierDraftStatesAsync(connection, listId, cancellationToken);
    }

    public async Task SetSupplierDraftStatusAsync(
        Guid listId,
        string supplierName,
        ReorderSupplierDraftStatus status,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await connection.OpenAsync(cancellationToken);

        var normalizedSupplierName = NormalizeText(supplierName);
        var now = DateTimeOffset.Now;

        var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE ReorderSupplierDrafts
            SET Status = $status,
                UpdatedAt = $updatedAt,
                OrderedAt = CASE WHEN $status = $ordinata THEN COALESCE(OrderedAt, $orderedAt) ELSE OrderedAt END,
                RegisteredOnFmAt = CASE WHEN $status = $registrataSuFm THEN COALESCE(RegisteredOnFmAt, $registeredOnFmAt) ELSE RegisteredOnFmAt END,
                ClosedAt = CASE WHEN $status = $chiusa THEN COALESCE(ClosedAt, $closedAt) ELSE NULL END
            WHERE ListId = $listId
              AND SupplierName = $supplierName;
            """;
        command.Parameters.AddWithValue("$listId", listId.ToString("D"));
        command.Parameters.AddWithValue("$supplierName", normalizedSupplierName);
        command.Parameters.AddWithValue("$status", status.ToString());
        command.Parameters.AddWithValue("$updatedAt", now.ToString("O"));
        command.Parameters.AddWithValue("$ordinata", ReorderSupplierDraftStatus.Ordinata.ToString());
        command.Parameters.AddWithValue("$registrataSuFm", ReorderSupplierDraftStatus.RegistrataSuFm.ToString());
        command.Parameters.AddWithValue("$chiusa", ReorderSupplierDraftStatus.Chiusa.ToString());
        command.Parameters.AddWithValue("$orderedAt", now.ToString("O"));
        command.Parameters.AddWithValue("$registeredOnFmAt", now.ToString("O"));
        command.Parameters.AddWithValue("$closedAt", now.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SetSupplierDraftFmDocumentAsync(
        Guid listId,
        string supplierName,
        int documentoOid,
        long numeroDocumento,
        int annoDocumento,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await connection.OpenAsync(cancellationToken);

        var normalizedSupplierName = NormalizeText(supplierName);
        var now = DateTimeOffset.Now;

        var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE ReorderSupplierDrafts
            SET Status = $status,
                UpdatedAt = $updatedAt,
                RegisteredOnFmAt = COALESCE(RegisteredOnFmAt, $registeredOnFmAt),
                FmDocumentOid = $fmDocumentOid,
                FmDocumentNumber = $fmDocumentNumber,
                FmDocumentYear = $fmDocumentYear
            WHERE ListId = $listId
              AND SupplierName = $supplierName;
            """;
        command.Parameters.AddWithValue("$listId", listId.ToString("D"));
        command.Parameters.AddWithValue("$supplierName", normalizedSupplierName);
        command.Parameters.AddWithValue("$status", ReorderSupplierDraftStatus.RegistrataSuFm.ToString());
        command.Parameters.AddWithValue("$updatedAt", now.ToString("O"));
        command.Parameters.AddWithValue("$registeredOnFmAt", now.ToString("O"));
        command.Parameters.AddWithValue("$fmDocumentOid", documentoOid);
        command.Parameters.AddWithValue("$fmDocumentNumber", numeroDocumento);
        command.Parameters.AddWithValue("$fmDocumentYear", annoDocumento);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<ReorderList> EnsureCurrentListAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var selectCommand = connection.CreateCommand();
        selectCommand.CommandText =
            """
            SELECT Id, Titolo, Stato, CreatedAt, UpdatedAt, ClosedAt
            FROM ReorderLists
            WHERE Stato = $stato
            ORDER BY CreatedAt DESC
            LIMIT 1;
            """;
        selectCommand.Parameters.AddWithValue("$stato", ReorderListStatus.Aperta.ToString());

        await using var reader = await selectCommand.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            var openList = MapList(reader);
            await reader.DisposeAsync();

            if (!await HasItemsAsync(connection, openList.Id, cancellationToken))
            {
                var orderedList = await TryReopenLatestOrderedListWithItemsAsync(connection, openList.Id, cancellationToken);
                if (orderedList is not null)
                {
                    return orderedList;
                }
            }

            return openList;
        }

        await reader.DisposeAsync();

        var fallbackOrderedList = await TryReopenLatestOrderedListWithItemsAsync(connection, null, cancellationToken);
        if (fallbackOrderedList is not null)
        {
            return fallbackOrderedList;
        }

        var createdAt = DateTimeOffset.Now;
        var list = new ReorderList
        {
            Id = Guid.NewGuid(),
            Titolo = $"Lista riordino {createdAt:dd/MM/yyyy}",
            Stato = ReorderListStatus.Aperta,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };

        var insertCommand = connection.CreateCommand();
        insertCommand.CommandText =
            """
            INSERT INTO ReorderLists (Id, Titolo, Stato, CreatedAt, UpdatedAt, ClosedAt)
            VALUES ($id, $titolo, $stato, $createdAt, $updatedAt, $closedAt);
            """;
        insertCommand.Parameters.AddWithValue("$id", list.Id.ToString("D"));
        insertCommand.Parameters.AddWithValue("$titolo", list.Titolo);
        insertCommand.Parameters.AddWithValue("$stato", list.Stato.ToString());
        insertCommand.Parameters.AddWithValue("$createdAt", list.CreatedAt.ToString("O"));
        insertCommand.Parameters.AddWithValue("$updatedAt", list.UpdatedAt.ToString("O"));
        insertCommand.Parameters.AddWithValue("$closedAt", DBNull.Value);
        await insertCommand.ExecuteNonQueryAsync(cancellationToken);

        return list;
    }

    private static async Task<IReadOnlyList<ReorderListItem>> LoadItemsAsync(SqliteConnection connection, Guid listId, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id,
                   ListId,
                   ArticoloOid,
                   CodiceArticolo,
                   Descrizione,
                   Quantita,
                   QuantitaDaOrdinare,
                   UnitaMisura,
                   FornitoreSuggeritoOid,
                   FornitoreSuggeritoNome,
                   FornitoreSelezionatoOid,
                   FornitoreSelezionatoNome,
                   PrezzoSuggerito,
                   IvaOid,
                   Motivo,
                   Stato,
                   Operatore,
                   Note,
                   CreatedAt,
                   UpdatedAt
            FROM ReorderListItems
            WHERE ListId = $listId
            ORDER BY Stato ASC, Descrizione ASC, CreatedAt ASC;
            """;
        command.Parameters.AddWithValue("$listId", listId.ToString("D"));

        var items = new List<ReorderListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new ReorderListItem
            {
                Id = Guid.Parse(reader.GetString(0)),
                ListId = Guid.Parse(reader.GetString(1)),
                ArticoloOid = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                CodiceArticolo = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                Descrizione = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                Quantita = reader.IsDBNull(5) ? 0m : Convert.ToDecimal(reader.GetDouble(5)),
                QuantitaDaOrdinare = reader.IsDBNull(6) ? (reader.IsDBNull(5) ? 0m : Convert.ToDecimal(reader.GetDouble(5))) : Convert.ToDecimal(reader.GetDouble(6)),
                UnitaMisura = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                FornitoreSuggeritoOid = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                FornitoreSuggeritoNome = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                FornitoreSelezionatoOid = reader.IsDBNull(10) ? null : reader.GetInt32(10),
                FornitoreSelezionatoNome = reader.IsDBNull(11) ? string.Empty : reader.GetString(11),
                PrezzoSuggerito = reader.IsDBNull(12) ? null : Convert.ToDecimal(reader.GetDouble(12)),
                IvaOid = reader.IsDBNull(13) ? 1 : reader.GetInt32(13),
                Motivo = ParseEnum(reader, 14, ReorderReason.Manuale),
                Stato = ParseEnum(reader, 15, ReorderItemStatus.DaOrdinare),
                Operatore = reader.IsDBNull(16) ? string.Empty : reader.GetString(16),
                Note = reader.IsDBNull(17) ? string.Empty : reader.GetString(17),
                CreatedAt = reader.IsDBNull(18) ? DateTimeOffset.Now : DateTimeOffset.Parse(reader.GetString(18)),
                UpdatedAt = reader.IsDBNull(19) ? DateTimeOffset.Now : DateTimeOffset.Parse(reader.GetString(19))
            });
        }

        return items;
    }

    private static async Task<ReorderListItem?> FindCurrentItemAsync(
        SqliteConnection connection,
        Guid listId,
        int? articoloOid,
        string codiceArticolo,
        string descrizione,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id,
                   ListId,
                   ArticoloOid,
                   CodiceArticolo,
                   Descrizione,
                   Quantita,
                   QuantitaDaOrdinare,
                   UnitaMisura,
                   FornitoreSuggeritoOid,
                   FornitoreSuggeritoNome,
                   FornitoreSelezionatoOid,
                   FornitoreSelezionatoNome,
                   PrezzoSuggerito,
                   IvaOid,
                   Motivo,
                   Stato,
                   Operatore,
                   Note,
                   CreatedAt,
                   UpdatedAt
            FROM ReorderListItems
            WHERE ListId = $listId
              AND (
                    (
                        $articoloOid IS NOT NULL
                        AND ArticoloOid = $articoloOid
                        AND COALESCE(CodiceArticolo, '') = $codiceArticolo
                        AND COALESCE(Descrizione, '') = $descrizione
                    )
                 OR (
                        $articoloOid IS NULL
                        AND COALESCE(CodiceArticolo, '') <> ''
                        AND CodiceArticolo = $codiceArticolo
                        AND COALESCE(Descrizione, '') = $descrizione
                    )
              )
            ORDER BY CreatedAt ASC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$listId", listId.ToString("D"));
        command.Parameters.AddWithValue("$articoloOid", articoloOid.HasValue ? articoloOid.Value : DBNull.Value);
        command.Parameters.AddWithValue("$codiceArticolo", NormalizeText(codiceArticolo));
        command.Parameters.AddWithValue("$descrizione", NormalizeText(descrizione));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ReorderListItem
        {
            Id = Guid.Parse(reader.GetString(0)),
            ListId = Guid.Parse(reader.GetString(1)),
            ArticoloOid = reader.IsDBNull(2) ? null : reader.GetInt32(2),
            CodiceArticolo = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
            Descrizione = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
            Quantita = reader.IsDBNull(5) ? 0m : Convert.ToDecimal(reader.GetDouble(5)),
            QuantitaDaOrdinare = reader.IsDBNull(6) ? (reader.IsDBNull(5) ? 0m : Convert.ToDecimal(reader.GetDouble(5))) : Convert.ToDecimal(reader.GetDouble(6)),
            UnitaMisura = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
            FornitoreSuggeritoOid = reader.IsDBNull(8) ? null : reader.GetInt32(8),
            FornitoreSuggeritoNome = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
            FornitoreSelezionatoOid = reader.IsDBNull(10) ? null : reader.GetInt32(10),
            FornitoreSelezionatoNome = reader.IsDBNull(11) ? string.Empty : reader.GetString(11),
            PrezzoSuggerito = reader.IsDBNull(12) ? null : Convert.ToDecimal(reader.GetDouble(12)),
            IvaOid = reader.IsDBNull(13) ? 1 : reader.GetInt32(13),
            Motivo = ParseEnum(reader, 14, ReorderReason.Manuale),
            Stato = ParseEnum(reader, 15, ReorderItemStatus.DaOrdinare),
            Operatore = reader.IsDBNull(16) ? string.Empty : reader.GetString(16),
            Note = reader.IsDBNull(17) ? string.Empty : reader.GetString(17),
            CreatedAt = reader.IsDBNull(18) ? DateTimeOffset.Now : DateTimeOffset.Parse(reader.GetString(18)),
            UpdatedAt = reader.IsDBNull(19) ? DateTimeOffset.Now : DateTimeOffset.Parse(reader.GetString(19))
        };
    }

    private static async Task<Guid?> ResolveListIdAsync(SqliteConnection connection, Guid itemId, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT ListId FROM ReorderListItems WHERE Id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", itemId.ToString("D"));
        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is not string raw || !Guid.TryParse(raw, out var listId))
        {
            return null;
        }

        return listId;
    }

    private static async Task UpdateListStateAsync(SqliteConnection connection, Guid listId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.Now;

        var updateCommand = connection.CreateCommand();
        updateCommand.CommandText =
            """
            UPDATE ReorderLists
            SET Stato = $stato,
                UpdatedAt = $updatedAt,
                ClosedAt = $closedAt
            WHERE Id = $id;
            """;
        updateCommand.Parameters.AddWithValue("$id", listId.ToString("D"));
        updateCommand.Parameters.AddWithValue("$stato", ReorderListStatus.Aperta.ToString());
        updateCommand.Parameters.AddWithValue("$updatedAt", now.ToString("O"));
        updateCommand.Parameters.AddWithValue("$closedAt", DBNull.Value);
        await updateCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> HasItemsAsync(SqliteConnection connection, Guid listId, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ReorderListItems WHERE ListId = $listId;";
        command.Parameters.AddWithValue("$listId", listId.ToString("D"));
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private static async Task<ReorderList?> TryReopenLatestOrderedListWithItemsAsync(
        SqliteConnection connection,
        Guid? emptyOpenListId,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT l.Id, l.Titolo, l.Stato, l.CreatedAt, l.UpdatedAt, l.ClosedAt
            FROM ReorderLists l
            WHERE l.Stato = $stato
              AND EXISTS (
                    SELECT 1
                    FROM ReorderListItems i
                    WHERE i.ListId = l.Id
                )
            ORDER BY l.UpdatedAt DESC, l.CreatedAt DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$stato", ReorderListStatus.Ordinata.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var list = MapList(reader);
        await reader.DisposeAsync();

        var now = DateTimeOffset.Now;

        var reopenCommand = connection.CreateCommand();
        reopenCommand.CommandText =
            """
            UPDATE ReorderLists
            SET Stato = $stato,
                UpdatedAt = $updatedAt,
                ClosedAt = $closedAt
            WHERE Id = $id;
            """;
        reopenCommand.Parameters.AddWithValue("$id", list.Id.ToString("D"));
        reopenCommand.Parameters.AddWithValue("$stato", ReorderListStatus.Aperta.ToString());
        reopenCommand.Parameters.AddWithValue("$updatedAt", now.ToString("O"));
        reopenCommand.Parameters.AddWithValue("$closedAt", DBNull.Value);
        await reopenCommand.ExecuteNonQueryAsync(cancellationToken);

        if (emptyOpenListId.HasValue)
        {
            var deleteEmptyListCommand = connection.CreateCommand();
            deleteEmptyListCommand.CommandText =
                """
                DELETE FROM ReorderLists
                WHERE Id = $id
                  AND NOT EXISTS (
                        SELECT 1
                        FROM ReorderListItems
                        WHERE ListId = $id
                    );
                """;
            deleteEmptyListCommand.Parameters.AddWithValue("$id", emptyOpenListId.Value.ToString("D"));
            await deleteEmptyListCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        list.Stato = ReorderListStatus.Aperta;
        list.UpdatedAt = now;
        list.ClosedAt = null;
        return list;
    }

    private static async Task<IReadOnlyList<ReorderSupplierDraftState>> LoadSupplierDraftStatesAsync(
        SqliteConnection connection,
        Guid listId,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id,
                   ListId,
                   SupplierName,
                   LocalCounter,
                   DraftDate,
                   Status,
                   UpdatedAt,
                   OrderedAt,
                   RegisteredOnFmAt,
                   ClosedAt,
                   FmDocumentOid,
                   FmDocumentNumber,
                   FmDocumentYear
            FROM ReorderSupplierDrafts
            WHERE ListId = $listId
            ORDER BY LocalCounter ASC, SupplierName ASC;
            """;
        command.Parameters.AddWithValue("$listId", listId.ToString("D"));

        var states = new List<ReorderSupplierDraftState>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            states.Add(new ReorderSupplierDraftState
            {
                Id = Guid.Parse(reader.GetString(0)),
                ListId = Guid.Parse(reader.GetString(1)),
                SupplierName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                LocalCounter = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                DraftDate = reader.IsDBNull(4) ? DateTimeOffset.Now : DateTimeOffset.Parse(reader.GetString(4)),
                Status = ParseEnum(reader, 5, ReorderSupplierDraftStatus.Aperta),
                UpdatedAt = reader.IsDBNull(6) ? DateTimeOffset.Now : DateTimeOffset.Parse(reader.GetString(6)),
                OrderedAt = reader.IsDBNull(7) ? null : DateTimeOffset.Parse(reader.GetString(7)),
                RegisteredOnFmAt = reader.IsDBNull(8) ? null : DateTimeOffset.Parse(reader.GetString(8)),
                ClosedAt = reader.IsDBNull(9) ? null : DateTimeOffset.Parse(reader.GetString(9)),
                FmDocumentOid = reader.IsDBNull(10) ? null : reader.GetInt32(10),
                FmDocumentNumber = reader.IsDBNull(11) ? null : reader.GetInt64(11),
                FmDocumentYear = reader.IsDBNull(12) ? null : reader.GetInt32(12)
            });
        }

        return states;
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var settings = await _configurationService.LoadAsync(cancellationToken);
        Directory.CreateDirectory(settings.LocalStore.BaseDirectory);
        return new SqliteConnection($"Data Source={settings.LocalStore.DatabasePath}");
    }

    private static ReorderList MapList(SqliteDataReader reader)
    {
        return new ReorderList
        {
            Id = Guid.Parse(reader.GetString(0)),
            Titolo = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
            Stato = ParseEnum(reader, 2, ReorderListStatus.Aperta),
            CreatedAt = reader.IsDBNull(3) ? DateTimeOffset.Now : DateTimeOffset.Parse(reader.GetString(3)),
            UpdatedAt = reader.IsDBNull(4) ? DateTimeOffset.Now : DateTimeOffset.Parse(reader.GetString(4)),
            ClosedAt = reader.IsDBNull(5) ? null : DateTimeOffset.Parse(reader.GetString(5))
        };
    }

    private static TEnum ParseEnum<TEnum>(SqliteDataReader reader, int ordinal, TEnum fallback)
        where TEnum : struct
    {
        if (reader.IsDBNull(ordinal))
        {
            return fallback;
        }

        return Enum.TryParse<TEnum>(reader.GetString(ordinal), out var parsed)
            ? parsed
            : fallback;
    }

    private static string NormalizeText(string? value, string fallback = "")
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
