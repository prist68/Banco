namespace Banco.Riordino;

public sealed class ReorderList
{
    public Guid Id { get; set; }

    public string Titolo { get; set; } = string.Empty;

    public ReorderListStatus Stato { get; set; } = ReorderListStatus.Aperta;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset? ClosedAt { get; set; }
}
