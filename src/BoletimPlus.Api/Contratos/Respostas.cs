using System.Text.Json.Serialization;

namespace BoletimPlus.Api.Contratos;

// Corpos de saída: os mesmos tipos de src/data/repository.ts e schema.ts do app.

public record EnumeradorResponse(
    int Cod,
    string Enumerador,
    [property: JsonPropertyName("data_criacao")] DateTime DataCriacao);

public record CodResponse(int Cod);

public record ProfessorCriadoResponse(int ProfessorCod);

public record AlunoCriadoResponse(int AlunoCod);

public record TokenResponse(string Token, int UsuarioCod, DateTime ExpiraEm);

public record EscolaResumo(int Cod, string Nome, string DiretorNome, int TotalTurmas);

public record TurmaResumo(
    int Cod,
    int EscolaCod,
    string EscolaNome,
    string TipoCurso,
    int ProfessorCod,
    string ProfessorNome,
    int Semestre,
    int Ano,
    int TotalAlunos);

public record ProfessorResumo(int ProfessorCod, string Nome, IReadOnlyList<string> Materias);

public record ProfessorDaEscola(
    int ProfessorCod,
    string Nome,
    IReadOnlyList<string> Materias,
    IReadOnlyList<TurmaResumo> Turmas);

public record AlunoResumo(int AlunoCod, string Nome, string NumDocumento, int TotalTurmas);

public record AlunoDaEscola(
    int AlunoCod,
    string Nome,
    string NumDocumento,
    int TotalTurmas,
    IReadOnlyList<TurmaResumo> Turmas);

public record AlunoDaTurma(int TurmaAlunoCod, int AlunoCod, string Nome);

public record AvaliacaoResumo(
    int Cod,
    string Nome,
    int CodTipoNota,
    string TipoNota,
    DateOnly? DataAvaliacao,
    int NotasLancadas);

/// <summary>TurmaResumo com alunos e avaliações, no mesmo nível do JSON (como no app).</summary>
public record TurmaDetalhe(
    int Cod,
    int EscolaCod,
    string EscolaNome,
    string TipoCurso,
    int ProfessorCod,
    string ProfessorNome,
    int Semestre,
    int Ano,
    int TotalAlunos,
    IReadOnlyList<AlunoDaTurma> Alunos,
    IReadOnlyList<AvaliacaoResumo> Avaliacoes)
{
    public static TurmaDetalhe De(TurmaResumo t, IReadOnlyList<AlunoDaTurma> alunos, IReadOnlyList<AvaliacaoResumo> avaliacoes) =>
        new(t.Cod, t.EscolaCod, t.EscolaNome, t.TipoCurso, t.ProfessorCod, t.ProfessorNome,
            t.Semestre, t.Ano, t.TotalAlunos, alunos, avaliacoes);
}

public record NotaLancada(int TurmaAlunoCod, string AlunoNome, decimal? Nota);

/// <summary>AvaliacaoResumo com a turma e as notas de cada aluno.</summary>
public record AvaliacaoDetalhe(
    int Cod,
    string Nome,
    int CodTipoNota,
    string TipoNota,
    DateOnly? DataAvaliacao,
    int NotasLancadas,
    TurmaResumo Turma,
    IReadOnlyList<NotaLancada> Notas);

public record ItemChamada(int TurmaAlunoCod, string AlunoNome, int? CodTipoPresenca, string? Observacao);

/// <summary>Pessoa do schema.ts: colunas em snake_case.</summary>
public record PessoaResponse(
    int Cod,
    string Nome,
    [property: JsonPropertyName("num_documento")] string NumDocumento,
    [property: JsonPropertyName("data_criacao")] DateTime DataCriacao)
{
    /// <summary>Data sem fuso, como o LocalDateTime do Java ("2026-11-18T10:00:00").</summary>
    public static PessoaResponse De(Dados.Pessoa p) =>
        new(p.Cod, p.Nome, p.NumDocumento, DateTime.SpecifyKind(p.DataCriacao, DateTimeKind.Unspecified));
}

/// <summary>Cod de cada papel da pessoa; papel que ela não tem fica fora do JSON (como no Java).</summary>
public record PapeisResponse(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? DiretorCod,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? ProfessorCod,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? GuardiaoCod,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? AlunoCod);

/// <summary>Sessao do app: resposta do login e do cadastro.</summary>
public record SessaoResponse(string Token, int UsuarioCod, PessoaResponse Pessoa, PapeisResponse Papeis);

public record DependenteResponse(int AlunoCod, string Nome, string Parentesco);

public record NotaDoAlunoResponse(
    int AvaliacaoCod,
    string AvaliacaoNome,
    string TipoNota,
    DateOnly? DataAvaliacao,
    double? Nota);

public record PresencaDoAlunoResponse(DateOnly Data, string Tipo, string? Observacao);

/// <summary>Frequência é fração de 0 a 1. Média e frequência são null sem registros.</summary>
public record BoletimTurmaResponse(
    TurmaResumo Turma,
    IReadOnlyList<NotaDoAlunoResponse> Notas,
    double? Media,
    IReadOnlyList<PresencaDoAlunoResponse> Presencas,
    double? Frequencia);

public record BoletimAlunoResponse(int AlunoCod, string Nome, IReadOnlyList<BoletimTurmaResponse> Turmas);
