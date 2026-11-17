using System.ComponentModel.DataAnnotations;

namespace EscolaPlus.Frequencia.Api.Contracts;

public class FrequenciaRequest
{
    [Required(ErrorMessage = "O AlunoId é obrigatório.")]
    public int AlunoId { get; set; }

    [Required(ErrorMessage = "O TurmaId é obrigatório.")]
    public int TurmaId { get; set; }

    [Required(ErrorMessage = "A data é obrigatória.")]
    public DateOnly Data { get; set; }

    [Required(ErrorMessage = "O status é obrigatório (PRESENTE, FALTA, ATRASO).")]
    public string Status { get; set; } = string.Empty; 
    
    public string Origem { get; set; } = "APP";
}