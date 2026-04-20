namespace Banco.Stampa;

public interface IPrintModulePathService
{
    string GetRootDirectory();

    string GetLayoutsDirectory();

    string GetDesignDataDirectory();

    string GetProfilesDirectory();

    string GetCatalogFilePath();
}
