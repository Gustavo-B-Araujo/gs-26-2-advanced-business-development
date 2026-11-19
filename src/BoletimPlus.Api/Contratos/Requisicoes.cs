using System.Text.Json.Serialization;

namespace BoletimPlus.Api.Contratos;

// Corpos de entrada. Os nomes JSON seguem o contrato do app (README do protótipo):
// campos de coluna em snake_case (num_documento, cod_tipo_curso) e o resto em camelCase.
// Campos são anuláveis para a API responder 400 com mensagem clara quando faltam.

/// <summary>POST /tipos/curso.</summary>
public record NovoCursoRequest(string? Enumerador);

/// <summary>POST /professores e POST /alunos. No aluno, senha nula = sem Usuario (não entra no app).</summary>
public record NovaPessoaRequest(
    string? Nome,
    [property: JsonPropertyName("num_documento")] string? NumDocumento,
    string? Senha);

/// <summary>PUT /professores/:cod e PUT /alunos/:cod.</summary>
public record RenomearRequest(string? Nome);

/// <summary>POST /turmas/:cod/alunos.</summary>
public record MatriculaRequest([property: JsonPropertyName("cod_alunos")] List<int>? CodAlunos);

/// <summary>POST /escolas/:cod/turmas.</summary>
public record NovaTurmaRequest(
    [property: JsonPropertyName("cod_tipo_curso")] int? CodTipoCurso,
    [property: JsonPropertyName("cod_professor")] int? CodProfessor,
    [property: JsonPropertyName("semestre_referencia")] int? SemestreReferencia,
    [property: JsonPropertyName("ano_referencia")] int? AnoReferencia);

/// <summary>POST /turmas/:cod/avaliacoes e PUT /avaliacoes/:cod. Data no formato YYYY-MM-DD.</summary>
public record DadosAvaliacaoRequest(string? Nome, int? CodTipoNota, DateOnly? DataAvaliacao);

/// <summary>PUT /avaliacoes/:cod/notas. Nota nula apaga a nota lançada.</summary>
public record NotasRequest(List<NotaItem>? Notas);

public record NotaItem(int TurmaAlunoCod, decimal? Nota);

/// <summary>PUT /turmas/:cod/chamada?data=YYYY-MM-DD.</summary>
public record ChamadaRequest(List<ChamadaItem>? Itens);

public record ChamadaItem(int TurmaAlunoCod, int CodTipoPresenca, string? Observacao);

/// <summary>POST /dev/token (só em Development).</summary>
public record TokenDevRequest(
    [property: JsonPropertyName("num_documento")] string? NumDocumento,
    string? Senha);

/// <summary>POST /auth/login.</summary>
public record LoginRequest(
    [property: JsonPropertyName("num_documento")] string? NumDocumento,
    string? Senha);

/// <summary>POST /auth/cadastro: cadastro de responsável (Guardiao).</summary>
public record CadastroRequest(
    string? Nome,
    [property: JsonPropertyName("num_documento")] string? NumDocumento,
    string? Senha);

/// <summary>PUT /auth/senha.</summary>
public record AlterarSenhaRequest(
    [property: JsonPropertyName("senha_atual")] string? SenhaAtual,
    [property: JsonPropertyName("nova_senha")] string? NovaSenha);
