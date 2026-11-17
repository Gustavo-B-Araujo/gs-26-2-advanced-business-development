namespace EscolaPlus.Frequencia.Api.Domain;

public class IdempotencyRecord
{
    public int Id { get; set; }
    public string Chave { get; set; } = string.Empty;
    public string HashCorpo { get; set; } = string.Empty;
    public int StatusHttp { get; set; }
    public string CorpoResposta { get; set; } = string.Empty;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}