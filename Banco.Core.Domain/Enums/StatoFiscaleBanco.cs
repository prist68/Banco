namespace Banco.Core.Domain.Enums;

public enum StatoFiscaleBanco
{
    Nessuno = 0,
    PubblicatoLegacyNonFiscalizzato = 1,
    FiscalizzazioneWinEcrRichiesta = 2,
    FiscalizzazioneWinEcrCompletata = 3,
    FiscalizzazioneWinEcrFallita = 4
}
