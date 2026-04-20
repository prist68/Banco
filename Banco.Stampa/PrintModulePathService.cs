namespace Banco.Stampa;

public sealed class PrintModulePathService : IPrintModulePathService
{
    private static readonly string RootDirectory =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Stampa"));

    public string GetRootDirectory()
    {
        Directory.CreateDirectory(RootDirectory);
        return RootDirectory;
    }

    public string GetLayoutsDirectory()
    {
        var path = Path.Combine(GetRootDirectory(), "Layouts");
        Directory.CreateDirectory(path);
        return path;
    }

    public string GetDesignDataDirectory()
    {
        var path = Path.Combine(GetRootDirectory(), "DesignData");
        Directory.CreateDirectory(path);
        return path;
    }

    public string GetProfilesDirectory()
    {
        var path = Path.Combine(GetRootDirectory(), "Profili");
        Directory.CreateDirectory(path);
        return path;
    }

    public string GetCatalogFilePath()
    {
        return Path.Combine(GetRootDirectory(), "layouts.catalog.json");
    }
}
