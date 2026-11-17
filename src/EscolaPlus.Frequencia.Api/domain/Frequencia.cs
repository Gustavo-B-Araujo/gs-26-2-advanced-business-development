namespace EscolaPlus.Frequencia.Api.Domain;

public enum StatusFrequencia 
{ 
    PRESENTE, 
    FALTA, 
    ATRASO 
}

public enum OrigemFrequencia 
{ 
    APP, 
    TOTEM 
}

public class Frequencia
{
    public int Id { get; set; }
    public int AlunoId { get; set; }
    public Aluno Aluno { get; set; } = null!;
    
    public int TurmaId { get; set; }
    public Turma Turma { get; set; } = null!;
    
    public DateOnly Data { get; set; }
    public StatusFrequencia Status { get; set; }
    public string RegistradoPor { get; set; } = string.Empty;
    public OrigemFrequencia Origem { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}