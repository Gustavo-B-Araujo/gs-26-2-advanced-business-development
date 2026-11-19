using System.Security.Claims;
using BoletimPlus.Api.Contratos;
using BoletimPlus.Api.Seguranca;
using BoletimPlus.Api.Servicos;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BoletimPlus.Api.Endpoints;

/// <summary>Professores e alunos da rede.</summary>
public static class PessoasEndpoints
{
    public static IEndpointRouteBuilder MapPessoas(this IEndpointRouteBuilder app)
    {
        var professores = app.MapGroup("/professores").WithTags("Professores").RequireAuthorization();

        professores.MapGet("", async (PessoasServico s) => TypedResults.Ok(await s.ListarProfessoresAsync()))
            .RequireAuthorization(NomePapel.Diretor)
            .WithSummary("Lista os professores da rede, com as matérias das turmas deles. Só diretor.")
            .ComErros(403);

        professores.MapPost("", async Task<Created<ProfessorCriadoResponse>> (NovaPessoaRequest req, PessoasServico s) =>
            {
                var cod = await s.CadastrarProfessorAsync(req);
                return TypedResults.Created($"/professores/{cod}", new ProfessorCriadoResponse(cod));
            })
            .RequireAuthorization(NomePapel.Diretor)
            .Idempotente()
            .WithSummary("Cadastra um professor na rede (Pessoa + Usuario ATIVO + Professor). Só diretor.")
            .WithDescription("Nome e sobrenome; CPF com 11 dígitos válidos; senha com 8+ caracteres, letras e números. CPF repetido responde 409.")
            .ComErros(400, 403, 409);

        professores.MapPut("/{cod:int}", async Task<NoContent> (int cod, RenomearRequest req, PessoasServico s) =>
            {
                await s.RenomearProfessorAsync(cod, req);
                return TypedResults.NoContent();
            })
            .RequireAuthorization(NomePapel.Diretor)
            .WithSummary("Altera o nome do professor. Só diretor.")
            .ComErros(400, 403, 404);

        professores.MapGet("/{cod:int}/turmas", async (int cod, ClaimsPrincipal user, PessoasServico s) =>
                TypedResults.Ok(await s.TurmasDoProfessorAsync(user.Papeis(), cod)))
            .RequireAuthorization(NomePapel.Professor)
            .WithSummary("Lista as turmas do professor. Só o próprio professor.")
            .ComErros(403);

        var alunos = app.MapGroup("/alunos").WithTags("Alunos").RequireAuthorization(NomePapel.Diretor);

        alunos.MapGet("", async (PessoasServico s) => TypedResults.Ok(await s.ListarAlunosAsync()))
            .WithSummary("Lista os alunos da rede, com o total de matrículas (0 = sem turma). Só diretor.")
            .ComErros(403);

        alunos.MapPost("", async Task<Created<AlunoCriadoResponse>> (NovaPessoaRequest req, PessoasServico s) =>
            {
                var cod = await s.CadastrarAlunoAsync(req);
                return TypedResults.Created($"/alunos/{cod}", new AlunoCriadoResponse(cod));
            })
            .Idempotente()
            .WithSummary("Cadastra um aluno na rede (Pessoa + Aluno, e Usuario só com senha). Só diretor.")
            .WithDescription("Sem senha (null), o aluno não entra no app: o responsável acompanha por ele. CPF repetido responde 409.")
            .ComErros(400, 403, 409);

        alunos.MapPut("/{cod:int}", async Task<NoContent> (int cod, RenomearRequest req, PessoasServico s) =>
            {
                await s.RenomearAlunoAsync(cod, req);
                return TypedResults.NoContent();
            })
            .WithSummary("Altera o nome do aluno. Só diretor.")
            .ComErros(400, 403, 404);

        return app;
    }
}
