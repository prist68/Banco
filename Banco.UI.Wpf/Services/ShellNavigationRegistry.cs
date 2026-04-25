using Banco.Core.Contracts.Navigation;

namespace Banco.UI.Wpf.Services;

public sealed class ShellNavigationRegistry : INavigationRegistry
{
    private static readonly IReadOnlyList<NavigationMacroCategoryDefinition> MacroCategories =
    [
        new("dashboard", "Dashboard", "IconDiagnostics", 0, "#5B8DEF"),
        new("banco", "Banco", "IconBanco", 1, "#4F86DC"),
        new("magazzino", "Magazzino", "IconFolder", 2, "#4E748F"),
        new("documenti", "Documenti", "IconDocumenti", 3, "#C47F1F"),
        new("anagrafiche", "Anagrafiche", "IconPoints", 4, "#3FA06E"),
        new("contabilita", "Contabilita`", "IconCash", 5, "#B55454"),
        new("impostazioni", "Impostazioni", "IconSettings", 6, "#7059B8")
    ];

    private static readonly IReadOnlyList<NavigationDestinationDefinition> Destinations =
    [
        new("dashboard.home", "Dashboard", "workspace.dashboard"),
        new("banco.vendita", "Vendita Banco", "workspace.banco", SupportsMultipleTabs: true),
        new("documenti.lista", "Documenti", "workspace.documenti"),
        new("magazzino.riordino", "Lista riordino", "workspace.riordino"),
        new("magazzino.gestione-articolo", "Gestione Articolo", "workspace.gestione-articolo"),
        new("magazzino.articolo", "Articolo magazzino", "workspace.magazzino-articolo"),
        new("anagrafiche.punti", "Raccolta punti", "workspace.punti"),
        new("impostazioni.archivio", "Impostazioni archivio", "workspace.impostazioni-archivio"),
        new("impostazioni.fastreport", "FastReport", "workspace.fastreport"),
        new("impostazioni.pos", "Configurazione POS", "workspace.pos"),
        new("impostazioni.fiscale", "Configurazione fiscale", "workspace.fiscale"),
        new("impostazioni.temi", "Gestione temi", "workspace.temi"),
        new("impostazioni.diagnostica", "Diagnostica / Percorsi", "workspace.diagnostica"),
        new("impostazioni.backup", "Importa backup", "workspace.backup")
    ];

    private static readonly IReadOnlyList<NavigationEntryDefinition> Entries =
    [
        new("dashboard.home", "Dashboard", "dashboard", "dashboard-main", "dashboard.home", NavigationEntryAvailability.Available, 0, true, true, ["dashboard", "home", "riepilogo"], ["cruscotto", "panoramica"], true, "#5B8DEF", false, false, true, true, "Panoramica"),
        new("dashboard.vendita", "Apri Banco", "dashboard", "dashboard-quick", "banco.vendita", NavigationEntryAvailability.Available, 1, true, true, ["banco", "vendita"], ["apri banco"], true, "#4F86DC", true, true, true, true, "Azioni rapide"),
        new("dashboard.documenti", "Documenti", "dashboard", "dashboard-quick", "documenti.lista", NavigationEntryAvailability.Available, 2, true, true, ["documenti", "storico"], ["lista documenti"], true, "#C47F1F", true, true, true, true, "Azioni rapide"),
        new("dashboard.riordino", "Lista riordino", "dashboard", "dashboard-quick", "magazzino.riordino", NavigationEntryAvailability.Available, 3, true, true, ["riordino", "ordini"], ["lista ordini"], true, "#4E748F", true, true, true, true, "Azioni rapide"),

        new("banco.vendita", "Vendita Banco", "banco", "banco-main", "banco.vendita", NavigationEntryAvailability.Available, 0, true, true, ["banco", "nuova vendita", "vendita banco"], ["cassa", "vendita al banco"], false, "#4F86DC", false, false, true, false, "Banco"),
        new("banco.nuovo-documento", "Vendita Banco", "banco", "banco-operazioni", "banco.vendita", NavigationEntryAvailability.Available, 1, true, false, ["vendita banco"], ["nuovo documento", "nuova vendita"], true, "#4F86DC", false, false, true, false, "Operazioni"),
        new("banco.lista-documenti", "Documenti", "banco", "banco-operazioni", "documenti.lista", NavigationEntryAvailability.Available, 2, true, true, ["documenti", "storico", "lista documenti"], ["elenco documenti", "vendite"], true, "#4F86DC", true, true, true, true, "Operazioni"),
        new("banco.lista-riordino", "Lista riordino", "banco", "banco-operazioni", "magazzino.riordino", NavigationEntryAvailability.Available, 3, true, true, ["riordino", "ordini", "lista riordino"], ["articoli da ordinare", "riordino locale"], true, "#4F86DC", true, true, true, true, "Operazioni"),
        new("banco.cerca-cliente", "Cerca cliente", "banco", "banco-clienti", null, NavigationEntryAvailability.DisabledPhase, 4, true, false, [], [], true, "#3FA06E", false, false, false, false, "Clienti"),
        new("banco.gestione-clienti", "Gestione clienti", "banco", "banco-clienti", null, NavigationEntryAvailability.DisabledPhase, 5, true, false, [], [], true, "#3FA06E", false, false, false, false, "Clienti"),
        new("banco.raccolta-punti", "Raccolta punti", "banco", "banco-fidelity", "anagrafiche.punti", NavigationEntryAvailability.Available, 6, true, true, ["fidelity", "punti", "raccolta punti"], ["clienti fidelity", "storico punti"], true, "#3FA06E", true, true, true, true, "Fidelity"),
        new("banco.storico-fidelity", "Storico fidelity", "banco", "banco-fidelity", "anagrafiche.punti", NavigationEntryAvailability.Available, 7, true, true, ["storico fidelity", "movimenti fidelity"], ["saldo punti", "movimenti punti"], true, "#3FA06E", true, true, true, true, "Fidelity"),

        new("magazzino.lista-riordino", "Lista riordino", "magazzino", "magazzino-main", "magazzino.riordino", NavigationEntryAvailability.Available, 0, true, true, ["riordino", "ordini"], ["lista riordino", "articoli da ordinare"], true, "#4E748F", true, true, true, true, "Magazzino"),
        new("magazzino.gestione-articolo", "Gestione Articolo", "magazzino", "magazzino-main", "magazzino.gestione-articolo", NavigationEntryAvailability.Available, 1, true, true, ["articolo", "catalogo", "gestione articolo"], ["scheda articolo", "anagrafica articolo", "consultazione articolo"], true, "#4E748F", true, true, true, true, "Magazzino"),
        new("magazzino.articolo", "Articolo magazzino", "magazzino", "magazzino-main", "magazzino.articolo", NavigationEntryAvailability.Available, 2, true, true, ["articolo", "magazzino"], ["parametri articolo", "confezione", "riordino articolo"], true, "#4E748F", true, true, true, true, "Magazzino"),

        new("documenti.lista-native", "Documenti", "documenti", null, "documenti.lista", NavigationEntryAvailability.Available, 0, true, true, ["documenti", "storico"], ["elenco documenti", "lista documenti", "vendite"], false, "#C47F1F", false, false, true, false),

        new("anagrafiche.punti-native", "Raccolta punti", "anagrafiche", null, "anagrafiche.punti", NavigationEntryAvailability.Available, 0, true, true, ["punti", "fidelity"], ["raccolta punti", "clienti fidelity"], false, "#3FA06E", false, false, true, false),

        new("contabilita.info", "Modulo in ampliamento", "contabilita", "contabilita-info", null, NavigationEntryAvailability.Informational, 0, true, false, [], [], true, "#B55454", false, false, false, false, "Contabilita`", "Le funzioni contabili verranno agganciate quando esistera` il modulo reale."),

        new("impostazioni.archivio", "Impostazioni archivio", "impostazioni", "impostazioni-core", "impostazioni.archivio", NavigationEntryAvailability.Available, 1, true, true, ["archivio", "configurazione archivio", "configurazioni"], ["impostazioni archivio"], true, "#7059B8", true, true, true, true, "Configurazioni"),
        new("impostazioni.fastreport", "FastReport", "impostazioni", "impostazioni-core", "impostazioni.fastreport", NavigationEntryAvailability.Available, 2, true, true, ["fastreport", "layout"], ["designer stampa", "runtime fastreport", "studio fastreport"], true, "#7059B8", true, true, true, true, "Configurazioni"),
        new("impostazioni.pos", "Configurazione POS", "impostazioni", "impostazioni-core", "impostazioni.pos", NavigationEntryAvailability.Available, 3, true, true, ["pos", "nexi"], ["smartpos", "registratore"], true, "#7059B8", true, true, true, true, "Configurazioni"),
        new("impostazioni.fiscale", "Configurazione fiscale", "impostazioni", "impostazioni-core", "impostazioni.fiscale", NavigationEntryAvailability.Available, 4, true, true, ["fiscale", "winecr"], ["registratore", "fiscalizzazione"], true, "#7059B8", true, true, true, true, "Configurazioni"),
        new("impostazioni.temi", "Gestione temi", "impostazioni", "impostazioni-layout", "impostazioni.temi", NavigationEntryAvailability.Available, 5, true, true, ["temi", "colori"], ["aspetto", "palette"], true, "#7059B8", true, true, true, true, "Aspetto"),
        new("impostazioni.diagnostica", "Diagnostica / Percorsi", "impostazioni", "impostazioni-tools", "impostazioni.diagnostica", NavigationEntryAvailability.Available, 6, true, true, ["diagnostica", "percorsi"], ["percorsi file", "log", "cartelle"], true, "#7059B8", true, true, true, true, "Strumenti"),
        new("impostazioni.backup", "Importa backup", "impostazioni", "impostazioni-tools", "impostazioni.backup", NavigationEntryAvailability.Available, 7, true, true, ["backup", "ripristino"], ["import backup"], true, "#7059B8", true, true, true, true, "Strumenti")
    ];

    private readonly Dictionary<string, NavigationMacroCategoryDefinition> _macroCategories;
    private readonly Dictionary<string, NavigationDestinationDefinition> _destinations;
    private readonly Dictionary<string, NavigationEntryDefinition> _entries;

    public ShellNavigationRegistry()
    {
        _macroCategories = MacroCategories.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);
        _destinations = Destinations.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);
        _entries = Entries.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);
        ValidateRegistry();
    }

    public IReadOnlyList<NavigationMacroCategoryDefinition> GetMacroCategories()
    {
        var referencedKeys = _entries.Values
            .Where(entry => entry.IsVisibleInShell)
            .Select(entry => entry.MacroCategoryKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return _macroCategories.Values
            .Where(category => referencedKeys.Contains(category.Key))
            .OrderBy(category => category.RailOrder)
            .ToList();
    }

    public IReadOnlyList<NavigationDestinationDefinition> GetDestinations() =>
        _destinations.Values
            .OrderBy(item => item.Title)
            .ToList();

    public IReadOnlyList<NavigationEntryDefinition> GetEntries() =>
        _entries.Values
            .OrderBy(item => GetMacroCategoryOrder(item.MacroCategoryKey))
            .ThenBy(item => item.GroupTitle ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ContextOrder)
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public IReadOnlyList<NavigationEntryDefinition> SearchEntries(string query, int maxResults = 12)
    {
        return SearchEntriesInternal(query, macroCategoryKey: null, maxResults);
    }

    public IReadOnlyList<NavigationEntryDefinition> SearchEntries(string query, string macroCategoryKey, int maxResults = 12)
    {
        return SearchEntriesInternal(query, macroCategoryKey, maxResults);
    }

    public NavigationMacroCategoryDefinition? GetMacroCategory(string macroCategoryKey) =>
        _macroCategories.TryGetValue(macroCategoryKey, out var category)
            ? category
            : null;

    public NavigationDestinationDefinition? GetDestination(string destinationKey) =>
        _destinations.TryGetValue(destinationKey, out var destination)
            ? destination
            : null;

    public NavigationEntryDefinition? GetEntry(string entryKey) =>
        _entries.TryGetValue(entryKey, out var entry)
            ? entry
            : null;

    private void ValidateRegistry()
    {
        foreach (var entry in _entries.Values)
        {
            if (string.IsNullOrWhiteSpace(entry.Key))
            {
                throw new InvalidOperationException("Ogni entry di navigazione deve avere una Key stabile.");
            }

            if (string.IsNullOrWhiteSpace(entry.Title))
            {
                throw new InvalidOperationException($"L'entry {entry.Key} deve avere un Title canonico.");
            }

            if (string.IsNullOrWhiteSpace(entry.MacroCategoryKey) || !_macroCategories.ContainsKey(entry.MacroCategoryKey))
            {
                throw new InvalidOperationException($"L'entry {entry.Key} deve appartenere a una macro-categoria valida.");
            }

            if (entry.IsVisibleInShell && string.IsNullOrWhiteSpace(entry.MacroCategoryKey))
            {
                throw new InvalidOperationException($"L'entry {entry.Key} visibile in shell deve avere una MacroCategoryKey.");
            }

            if (entry.ShowInContextPanel && string.IsNullOrWhiteSpace(entry.GroupKey))
            {
                throw new InvalidOperationException($"L'entry {entry.Key} mostrata nel pannello contestuale deve avere una GroupKey.");
            }

            if (entry.ShowInContextPanel && string.IsNullOrWhiteSpace(entry.GroupTitle))
            {
                throw new InvalidOperationException($"L'entry {entry.Key} mostrata nel pannello contestuale deve avere una GroupTitle coerente.");
            }

            if (!string.IsNullOrWhiteSpace(entry.DestinationKey) && !_destinations.ContainsKey(entry.DestinationKey))
            {
                throw new InvalidOperationException($"L'entry {entry.Key} usa una DestinationKey non risolvibile: {entry.DestinationKey}.");
            }

            if (entry.IsSearchable && string.IsNullOrWhiteSpace(entry.Title) && entry.Keywords.Count == 0 && entry.Aliases.Count == 0)
            {
                throw new InvalidOperationException($"L'entry {entry.Key} ricercabile non ha metadata di ricerca minimi.");
            }

            if (entry.Availability == NavigationEntryAvailability.Available && string.IsNullOrWhiteSpace(entry.DestinationKey))
            {
                throw new InvalidOperationException($"L'entry {entry.Key} disponibile deve avere una DestinationKey.");
            }
        }

        var duplicateDestinationKeys = _destinations.Values
            .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (duplicateDestinationKeys.Count > 0)
        {
            throw new InvalidOperationException($"DestinationKey duplicate nel registro: {string.Join(", ", duplicateDestinationKeys)}.");
        }

        var duplicateWorkspaceKeys = _destinations.Values
            .GroupBy(item => item.WorkspaceKey, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1 && group.Any(item => !item.SupportsMultipleTabs))
            .Select(group => group.Key)
            .ToList();
        if (duplicateWorkspaceKeys.Count > 0)
        {
            throw new InvalidOperationException($"WorkspaceKey duplicate senza supporto multi-tab: {string.Join(", ", duplicateWorkspaceKeys)}.");
        }
    }

    private int GetMacroCategoryOrder(string macroCategoryKey) =>
        _macroCategories.TryGetValue(macroCategoryKey, out var category)
            ? category.RailOrder
            : int.MaxValue;

    private IReadOnlyList<NavigationEntryDefinition> SearchEntriesInternal(string query, string? macroCategoryKey, int maxResults)
    {
        var normalizedQuery = query?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return [];
        }

        return _entries.Values
            .Where(entry => entry.IsSearchable && entry.IsVisibleInShell)
            .Where(entry => string.IsNullOrWhiteSpace(macroCategoryKey) || string.Equals(entry.MacroCategoryKey, macroCategoryKey, StringComparison.OrdinalIgnoreCase))
            .Where(entry => HasSearchMatch(entry, normalizedQuery))
            .Where(entry => string.IsNullOrWhiteSpace(entry.DestinationKey) || _destinations.TryGetValue(entry.DestinationKey, out var destination) && destination.IsAvailable)
            .OrderByDescending(entry => CalculateSearchScore(entry, normalizedQuery))
            .ThenBy(entry => GetMacroCategoryOrder(entry.MacroCategoryKey))
            .ThenBy(entry => entry.ContextOrder)
            .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(1, maxResults))
            .ToList();
    }

    private static bool HasSearchMatch(NavigationEntryDefinition entry, string normalizedQuery)
    {
        return GetSearchTerms(entry)
            .Any(term => term.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase));
    }

    private static int CalculateSearchScore(NavigationEntryDefinition entry, string normalizedQuery)
    {
        var score = 0;
        if (entry.Title.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
        {
            score += 100;
        }

        if (entry.Aliases.Any(alias => alias.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)))
        {
            score += 50;
        }

        if (entry.Keywords.Any(keyword => keyword.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)))
        {
            score += 25;
        }

        return score;
    }

    private static IEnumerable<string> GetSearchTerms(NavigationEntryDefinition entry)
    {
        yield return entry.Title;

        foreach (var keyword in entry.Keywords)
        {
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                yield return keyword;
            }
        }

        foreach (var alias in entry.Aliases)
        {
            if (!string.IsNullOrWhiteSpace(alias))
            {
                yield return alias;
            }
        }
    }
}
