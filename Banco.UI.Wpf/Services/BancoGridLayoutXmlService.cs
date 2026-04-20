using System.Globalization;
using System.IO;
using System.Xml.Linq;

namespace Banco.UI.Wpf.Services;

/// <summary>
/// Servizio per la persistenza del layout griglia documento (larghezze colonne + altezza riga)
/// su file XML locale: %LocalAppData%\Banco\grid_layout.xml
/// </summary>
internal sealed class BancoGridLayoutXmlService
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Banco",
        "grid_layout.xml");

    /// <summary>
    /// Salva il layout corrente della griglia nel file XML.
    /// </summary>
    public void Salva(BancoGridLayoutDati dati)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("BancoGridLayout",
                new XElement("RowHeight",
                    dati.AltezzaRiga.ToString("F2", CultureInfo.InvariantCulture)),
                new XElement("Columns",
                    dati.LarghezzeColonne.Select(c =>
                        new XElement("Column",
                            new XAttribute("Key", c.Key),
                            new XAttribute("Width",
                                c.Value.ToString("F2", CultureInfo.InvariantCulture)))))));

        doc.Save(FilePath);
    }

    /// <summary>
    /// Carica il layout dal file XML. Restituisce null se il file non esiste o non è valido.
    /// </summary>
    public BancoGridLayoutDati? Carica()
    {
        if (!File.Exists(FilePath))
        {
            return null;
        }

        try
        {
            var doc = XDocument.Load(FilePath);
            var root = doc.Root;
            if (root is null)
            {
                return null;
            }

            var altezzaTesto = root.Element("RowHeight")?.Value;
            if (!double.TryParse(altezzaTesto, NumberStyles.Any, CultureInfo.InvariantCulture, out var altezza)
                || altezza < 20)
            {
                altezza = 32;
            }

            var colonne = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var col in root.Element("Columns")?.Elements("Column") ?? [])
            {
                var chiave = (string?)col.Attribute("Key");
                var larghezzaTesto = (string?)col.Attribute("Width");
                if (chiave is not null
                    && double.TryParse(larghezzaTesto, NumberStyles.Any, CultureInfo.InvariantCulture, out var larghezza)
                    && larghezza > 0)
                {
                    colonne[chiave] = larghezza;
                }
            }

            return new BancoGridLayoutDati { AltezzaRiga = altezza, LarghezzeColonne = colonne };
        }
        catch
        {
            // File corrotto o formato non riconosciuto: ignora e usa i valori predefiniti.
            return null;
        }
    }
}

/// <summary>
/// Dati del layout griglia da persistere su XML.
/// </summary>
internal sealed class BancoGridLayoutDati
{
    public double AltezzaRiga { get; set; } = 32;

    public Dictionary<string, double> LarghezzeColonne { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}
