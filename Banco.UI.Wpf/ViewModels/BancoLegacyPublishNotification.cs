using Banco.Core.Domain.Enums;
using Banco.Vendita.Fiscal;

namespace Banco.UI.Wpf.ViewModels;

public sealed class BancoLegacyPublishNotification
{
    public int DocumentoGestionaleOid { get; init; }

    public CategoriaDocumentoBanco CategoriaDocumentoBanco { get; init; } = CategoriaDocumentoBanco.Indeterminata;

    public LegacyPublishOutcomeKind OutcomeKind { get; init; } = LegacyPublishOutcomeKind.LegacyPublished;
}
