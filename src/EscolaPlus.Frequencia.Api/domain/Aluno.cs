namespace EscolaPlus.Frequencia.Api.Domain;

public class Aluno
{
    public int Id { get; set; }
    public string Apelido { get; set; } = string.Empty;
    public int TurmaId { get; set; }
    
    // Relacionamentos
    public Turma Turma { get; set; } = null!;
    public ICollection<Frequencia> Frequencias { get; set; } = new List<Frequencia>();
}