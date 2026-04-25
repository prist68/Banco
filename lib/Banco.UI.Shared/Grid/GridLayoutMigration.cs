using Banco.Vendita.Configuration;

namespace Banco.UI.Shared.Grid;

public static class GridLayoutMigration
{
    public const string BancoGridId = "banco-documento-righe";
    public const string DocumentListGridId = "documenti-lista-unica";
    public const string DocumentDetailGridId = "documenti-dettaglio-righe";
    public const string ReorderListGridId = "documenti-lista-riordino";
    public const string PurchaseHistoryGridId = "storico-acquisti-ricerca";

    public static GridLayoutSettings GetOrCreateBancoLayout(AppSettings settings, IEnumerable<GridColumnDefinition> definitions)
    {
        if (settings.GridLayouts.TryGetValue(BancoGridId, out var layout))
        {
            EnsureDefaults(layout, definitions);
            return layout;
        }

        layout = new GridLayoutSettings();
        foreach (var definition in definitions)
        {
            layout.Columns[definition.Key] = new GridColumnLayoutState
            {
                Width = GetBancoWidth(settings.BancoDocumentGridLayout, definition.Key, definition.DefaultWidth),
                DisplayIndex = GetBancoDisplayIndex(settings.BancoDocumentGridLayout, definition.Key, definition.DefaultDisplayIndex),
                IsVisible = GetBancoVisibility(settings.BancoDocumentGridLayout, definition.Key, definition.IsVisibleByDefault),
                ContentAlignment = definition.TextAlignment
            };
        }

        settings.GridLayouts[BancoGridId] = layout;
        return layout;
    }

    public static GridLayoutSettings GetOrCreateDocumentListLayout(AppSettings settings, IEnumerable<GridColumnDefinition> definitions)
    {
        if (settings.GridLayouts.TryGetValue(DocumentListGridId, out var layout))
        {
            EnsureDefaults(layout, definitions);
            return layout;
        }

        layout = new GridLayoutSettings();
        foreach (var definition in definitions)
        {
            layout.Columns[definition.Key] = new GridColumnLayoutState
            {
                Width = GetDocumentWidth(settings.DocumentListLayout, definition.Key, definition.DefaultWidth),
                DisplayIndex = definition.DefaultDisplayIndex,
                IsVisible = GetDocumentVisibility(settings.DocumentListLayout, definition.Key, definition.IsVisibleByDefault),
                ContentAlignment = definition.TextAlignment
            };
        }

        layout.Flags["includeLocalDocuments"] = settings.DocumentListIncludeLocalDocuments;
        layout.Flags["unscontrinatiExpandedMode"] = settings.DocumentListUnscontrinatiExpandedMode;
        settings.GridLayouts[DocumentListGridId] = layout;
        return layout;
    }

    public static GridLayoutSettings GetOrCreateDocumentDetailLayout(AppSettings settings, IEnumerable<GridColumnDefinition> definitions)
    {
        if (settings.GridLayouts.TryGetValue(DocumentDetailGridId, out var layout))
        {
            EnsureDefaults(layout, definitions);
            return layout;
        }

        layout = new GridLayoutSettings();
        foreach (var definition in definitions)
        {
            layout.Columns[definition.Key] = new GridColumnLayoutState
            {
                Width = definition.DefaultWidth,
                DisplayIndex = definition.DefaultDisplayIndex,
                IsVisible = definition.IsVisibleByDefault,
                ContentAlignment = definition.TextAlignment
            };
        }

        settings.GridLayouts[DocumentDetailGridId] = layout;
        return layout;
    }

    public static GridLayoutSettings GetOrCreateReorderListLayout(AppSettings settings, IEnumerable<GridColumnDefinition> definitions)
    {
        if (settings.GridLayouts.TryGetValue(ReorderListGridId, out var layout))
        {
            EnsureDefaults(layout, definitions);
            return layout;
        }

        layout = new GridLayoutSettings();
        foreach (var definition in definitions)
        {
            layout.Columns[definition.Key] = new GridColumnLayoutState
            {
                Width = definition.DefaultWidth,
                DisplayIndex = definition.DefaultDisplayIndex,
                IsVisible = definition.IsVisibleByDefault,
                ContentAlignment = definition.TextAlignment
            };
        }

        settings.GridLayouts[ReorderListGridId] = layout;
        return layout;
    }

    public static GridLayoutSettings GetOrCreatePurchaseHistoryLayout(AppSettings settings, IEnumerable<GridColumnDefinition> definitions)
    {
        if (settings.GridLayouts.TryGetValue(PurchaseHistoryGridId, out var layout))
        {
            EnsureDefaults(layout, definitions);
            return layout;
        }

        layout = new GridLayoutSettings();
        foreach (var definition in definitions)
        {
            layout.Columns[definition.Key] = new GridColumnLayoutState
            {
                Width = definition.DefaultWidth,
                DisplayIndex = definition.DefaultDisplayIndex,
                IsVisible = definition.IsVisibleByDefault,
                ContentAlignment = definition.TextAlignment
            };
        }

        settings.GridLayouts[PurchaseHistoryGridId] = layout;
        return layout;
    }

    private static void EnsureDefaults(GridLayoutSettings layout, IEnumerable<GridColumnDefinition> definitions)
    {
        foreach (var definition in definitions)
        {
            if (layout.Columns.ContainsKey(definition.Key))
            {
                continue;
            }

            layout.Columns[definition.Key] = new GridColumnLayoutState
            {
                Width = definition.DefaultWidth,
                DisplayIndex = definition.DefaultDisplayIndex,
                IsVisible = definition.IsVisibleByDefault,
                ContentAlignment = definition.TextAlignment
            };
        }
    }

    private static double GetBancoWidth(BancoDocumentGridLayoutSettings layout, string key, double fallback)
    {
        return key switch
        {
            "Riga" => layout.RigaWidth,
            "Codice" => layout.CodiceWidth,
            "Descrizione" => layout.DescrizioneWidth,
            "Quantita" => layout.QuantitaWidth,
            "Prezzo" => layout.PrezzoWidth,
            "Iva" => layout.IvaWidth,
            "Tipo" => layout.TipoWidth,
            "Sconto" => layout.ScontoWidth,
            "Importo" => layout.ImportoWidth,
            "Azioni" => layout.AzioniWidth,
            _ => fallback
        };
    }

    private static int GetBancoDisplayIndex(BancoDocumentGridLayoutSettings layout, string key, int fallback)
    {
        return key switch
        {
            "Riga" => layout.RigaDisplayIndex,
            "Codice" => layout.CodiceDisplayIndex,
            "Descrizione" => layout.DescrizioneDisplayIndex,
            "Quantita" => layout.QuantitaDisplayIndex,
            "Prezzo" => layout.PrezzoDisplayIndex,
            "Iva" => layout.IvaDisplayIndex,
            "Tipo" => layout.TipoDisplayIndex,
            "Sconto" => layout.ScontoDisplayIndex,
            "Importo" => layout.ImportoDisplayIndex,
            "Azioni" => layout.AzioniDisplayIndex,
            _ => fallback
        };
    }

    private static bool GetBancoVisibility(BancoDocumentGridLayoutSettings layout, string key, bool fallback)
    {
        return key switch
        {
            "Riga" => layout.ShowRiga,
            "Codice" => layout.ShowCodice,
            "Descrizione" => layout.ShowDescrizione,
            "Quantita" => layout.ShowQuantita,
            "Prezzo" => layout.ShowPrezzo,
            "Iva" => layout.ShowIva,
            "Tipo" => layout.ShowTipo,
            "Sconto" => layout.ShowSconto,
            "Importo" => layout.ShowImporto,
            "Azioni" => layout.ShowAzioni,
            _ => fallback
        };
    }

    private static double GetDocumentWidth(DocumentListLayoutSettings layout, string key, double fallback)
    {
        return key switch
        {
            "Status" => layout.StatusWidth,
            "Oid" => layout.OidWidth,
            "Documento" => layout.DocumentoWidth,
            "Data" => layout.DataWidth,
            "Ora" => layout.OraWidth,
            "Nominativo" => layout.NominativoWidth,
            "Totale" => layout.TotaleWidth,
            "Stato" => layout.StatoWidth,
            "Operatore" => layout.OperatoreWidth,
            "Imponibile" => layout.ImponibileWidth,
            "Iva" => layout.IvaWidth,
            "Scontrino" => layout.ScontrinoWidth,
            _ => fallback
        };
    }

    private static bool GetDocumentVisibility(DocumentListLayoutSettings layout, string key, bool fallback)
    {
        return key switch
        {
            "Status" => layout.ShowStatus,
            "Oid" => layout.ShowOid,
            "Documento" => layout.ShowDocumento,
            "Data" => layout.ShowData,
            "Ora" => layout.ShowOra,
            "Nominativo" => layout.ShowNominativo,
            "Totale" => layout.ShowTotale,
            "Stato" => layout.ShowStato,
            "Operatore" => layout.ShowOperatore,
            "Imponibile" => layout.ShowImponibile,
            "Iva" => layout.ShowIva,
            "Scontrino" => layout.ShowScontrino,
            _ => fallback
        };
    }
}
