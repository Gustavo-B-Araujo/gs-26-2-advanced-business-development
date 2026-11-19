using System.Security.Claims;
using BoletimPlus.Api.Contratos;
using BoletimPlus.Api.Seguranca;
using BoletimPlus.Api.Servicos;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BoletimPlus.Api.Endpoints;

/// <summary>Gestão escolar: tudo aqui é do diretor da escola.</summary>
public static class EscolasEndpoints
{
    public static IEndpointRouteBuilder MapEscolas(this IEndpointRouteBuilder app)
    {
        app.MapGet("/diretores/{cod:int}/escolas", async (int cod, ClaimsPrincipal user, EscolasServico s) =>
                TypedResults.Ok(await s.EscolasDoDiretorAsync(user.Papeis(), cod)))
            .WithTags("Escolas")
            .RequireAuthorization(NomePapel.Diretor)
            .WithSummary("Lista as escolas do diretor. Só o próprio diretor.")
            .ComErros(403);

        var escolas = app.MapGroup("/escolas/{cod:int}").WithTags("Escolas").RequireAuthorization(NomePapel.Diretor);

        escolas.MapGet("", async (int cod, ClaimsPrincipal user, EscolasServico s) =>
                TypedResults.Ok(await s.ObterAsync(user.Papeis(), cod)))
            .WithSummary("Dados da escola. Só o diretor da escola.")
            .ComErros(403, 404);

        escolas.MapGet("/turmas", async (int cod, ClaimsPrincipal user, EscolasServico s) =>
                TypedResults.Ok(await s.TurmasAsync(user.Papeis(), cod)))
            .WithSummary("Turmas da escola, da mais recente para a mais antiga.")
            .ComErros(403, 404);

        escolas.MapPost("/turmas", async Task<Created<CodResponse>> (int cod, NovaTurmaRequest req, ClaimsPrincipal user, EscolasServico s) =>
            {
                var turma = await s.CriarTurmaAsync(user.Papeis(), cod, req);
                return TypedResults.Created($"/turmas/{turma}", new CodResponse(turma));
            })
            .Idempotente()
            .WithSummary("Cria uma turma na escola. Só o diretor da escola.")
            .WithDescription("Professor obrigatório (qualquer um da rede); semestre 1 ou 2; ano a partir de 2000.")
            .ComErros(400, 403, 404);

        escolas.MapGet("/professores", async (int cod, ClaimsPrincipal user, EscolasServico s) =>
                TypedResults.Ok(await s.ProfessoresAsync(user.Papeis(), cod)))
            .WithSummary("Professores que dão aula em turmas da escola, com essas turmas.")
            .ComErros(403, 404);

        escolas.MapGet("/alunos", async (int cod, ClaimsPrincipal user, EscolasServico s) =>
                TypedResults.Ok(await s.AlunosAsync(user.Papeis(), cod)))
            .WithSummary("Alunos matriculados em turmas da escola, com essas turmas.")
            .ComErros(403, 404);

        app.MapPost("/turmas/{cod:int}/alunos", async Task<NoContent> (int cod, MatriculaRequest req, ClaimsPrincipal user, EscolasServico s) =>
            {
                await s.MatricularAsync(user.Papeis(), cod, req);
                return TypedResults.NoContent();
            })
            .WithTags("Turmas")
            .RequireAuthorization(NomePapel.Diretor)
            .WithSummary("Matricula alunos da rede na turma. Só o diretor da escola da turma.")
            .WithDescription("Aluno já matriculado é ignorado: repetir a chamada não duplica. Lista vazia responde 400; aluno inexistente, 404.")
            .ComErros(400, 403, 404);

        return app;
    }
}
