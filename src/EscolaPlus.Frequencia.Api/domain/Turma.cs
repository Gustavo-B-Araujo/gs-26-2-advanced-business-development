namespace EscolaPlus.Frequencia.Api.Domain;

public class Turma
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int ProfessorId { get; set; }
    
    // Relacionamento: Uma turma tem muitos alunos
    public ICollection<Aluno> Alunos { get; set; } = new List<Aluno>();
}