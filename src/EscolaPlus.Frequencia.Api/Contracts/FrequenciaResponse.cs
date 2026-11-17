namespace EscolaPlus.Frequencia.Api.Contracts;

public class FrequenciaResponse
{
    public int Id { get; set; }
    public int AlunoId { get; set; }
    public int TurmaId { get; set; }
    public DateOnly Data { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Origem { get; set; } = string.Empty;
    public DateTime CriadoEm { get; set; }
}