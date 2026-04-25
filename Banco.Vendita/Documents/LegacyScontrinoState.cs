namespace Banco.Vendita.Documents;

public static class LegacyScontrinoState
{
    public static bool IsFiscalizzato(int? fatturato)
    {
        return fatturato is 1 or 4;
    }

    public static bool IsNonScontrinato(int? fatturato)
    {
        return !IsFiscalizzato(fatturato);
    }

    public static string ToLabel(int? fatturato)
    {
        return fatturato switch
        {
            1 => "Si",
            4 => "Si!",
            _ => "No"
        };
    }
}
