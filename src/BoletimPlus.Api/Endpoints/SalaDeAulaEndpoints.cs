using System.Security.Claims;
using BoletimPlus.Api.Contratos;
using BoletimPlus.Api.Seguranca;
using BoletimPlus.Api.Servicos;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BoletimPlus.Api.Endpoints;

/// <summary>Sala de aula: turma, avaliações, notas e chamada. Escrita só do professor da turma.</summary>
public static class SalaDeAulaEndpoints
{
    public static IEndpointRouteBuilder MapSalaDeAula(this IEndpointRouteBuilder app)
    {
        var turmas = app.MapGroup("/turmas/{cod:int}").RequireAuthorization();

        turmas.MapGet("", async (int cod, ClaimsPrincipal user, SalaDeAulaServico s) =>
                TypedResults.Ok(await s.ObterTurmaAsync(user.Papeis(), cod)))
            .WithTags("Turmas")
            .WithSummary("Turma com alunos e avaliações.")
            .WithDescription("Professor da turma, diretor da escola, aluno matriculado ou responsável por um matriculado.")
            .ComErros(403, 404);

        turmas.MapPost("/avaliacoes", async Task<Created<CodResponse>> (int cod, DadosAvaliacaoRequest req, ClaimsPrincipal user, SalaDeAulaServico s) =>
            {
                var avaliacao = await s.CriarAvaliacaoAsync(user.Papeis(), cod, req);
                return TypedResults.Created($"/avaliacoes/{avaliacao}", new CodResponse(avaliacao));
            })
            .WithTags("Avaliações")
            .RequireAuthorization(NomePapel.Professor)
            .Idempotente()
            .WithSummary("Cria uma avaliação na turma. Só o professor da turma.")
            .WithDescription("Nome de 1 a 50, único na turma (409). A mesma Idempotency-Key devolve a mesma avaliação.")
            .ComErros(400, 403, 404, 409);

        turmas.MapGet("/chamada", async (int cod, string data, ClaimsPrincipal user, SalaDeAulaServico s) =>
                TypedResults.Ok(await s.ObterChamadaAsync(user.Papeis(), cod, Datas.Ler(data))))
            .WithTags("Chamada")
            .WithSummary("Chamada do dia: cada aluno da turma com a presença lançada (ou null).")
            .WithDescription("Parâmetro data no formato YYYY-MM-DD.")
            .ComErros(400, 403, 404);

        turmas.MapPut("/chamada", async Task<NoContent> (int cod, string data, ChamadaRequest req, ClaimsPrincipal user, SalaDeAulaServico s) =>
            {
                await s.SalvarChamadaAsync(user.Papeis(), cod, Datas.Ler(data), req);
                return TypedResults.NoContent();
            })
            .WithTags("Chamada")
            .RequireAuthorization(NomePapel.Professor)
            .WithSummary("Salva a chamada do dia (upsert por aluno e data). Só o professor da turma.")
            .WithDescription("Sem data futura. Repetir o PUT com o mesmo corpo deixa o mesmo estado (idempotente).")
            .ComErros(400, 403, 404);

        var avaliacoes = app.MapGroup("/avaliacoes/{cod:int}").WithTags("Avaliações").RequireAuthorization();

        avaliacoes.MapGet("", async (int cod, ClaimsPrincipal user, SalaDeAulaServico s) =>
                TypedResults.Ok(await s.ObterAvaliacaoAsync(user.Papeis(), cod)))
            .WithSummary("Avaliação com a turma e a nota de cada aluno.")
            .ComErros(403, 404);

        avaliacoes.MapPut("", async Task<NoContent> (int cod, DadosAvaliacaoRequest req, ClaimsPrincipal user, SalaDeAulaServico s) =>
            {
                await s.AtualizarAvaliacaoAsync(user.Papeis(), cod, req);
                return TypedResults.NoContent();
            })
            .RequireAuthorization(NomePapel.Professor)
            .WithSummary("Edita a avaliação. Só o professor da turma.")
            .ComErros(400, 403, 404, 409);

        avaliacoes.MapDelete("", async Task<NoContent> (int cod, ClaimsPrincipal user, SalaDeAulaServico s) =>
            {
                await s.ExcluirAvaliacaoAsync(user.Papeis(), cod);
                return TypedResults.NoContent();
            })
            .RequireAuthorization(NomePapel.Professor)
            .WithSummary("Exclui a avaliação e as notas dela. Só o professor da turma.")
            .ComErros(403, 404);

        avaliacoes.MapPut("/notas", async Task<NoContent> (int cod, NotasRequest req, ClaimsPrincipal user, SalaDeAulaServico s) =>
            {
                await s.SalvarNotasAsync(user.Papeis(), cod, req);
                return TypedResults.NoContent();
            })
            .RequireAuthorization(NomePapel.Professor)
            .WithSummary("Lança as notas (upsert por aluno). Nota null apaga. Só o professor da turma.")
            .WithDescription("Nota de 0 a 100, com até 2 casas. Repetir o PUT deixa o mesmo estado (idempotente).")
            .ComErros(400, 403, 404);

        return app;
    }
}
