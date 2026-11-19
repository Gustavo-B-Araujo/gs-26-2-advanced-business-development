using BoletimPlus.Api.Contratos;
using BoletimPlus.Api.Dados;
using BoletimPlus.Api.Seguranca;
using BoletimPlus.Api.Servicos;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BoletimPlus.Api.Endpoints;

public static class TiposEndpoints
{
    public static IEndpointRouteBuilder MapTipos(this IEndpointRouteBuilder app)
    {
        var tipos = app.MapGroup("/tipos").WithTags("Tipos").RequireAuthorization();

        tipos.MapGet("/presenca", async (TiposServico s) => TypedResults.Ok(await s.ListarAsync<TipoPresenca>()))
            .WithSummary("Lista os tipos de presença (Presente, Falta, Falta Justificada).")
            .ComErros();

        tipos.MapGet("/nota", async (TiposServico s) => TypedResults.Ok(await s.ListarAsync<TipoNota>()))
            .WithSummary("Lista os tipos de nota (Prova, Trabalho, Caderno).")
            .ComErros();

        tipos.MapGet("/curso", async (TiposServico s) => TypedResults.Ok(await s.ListarAsync<TipoCurso>()))
            .WithSummary("Lista os cursos (matérias) da rede.")
            .ComErros();

        tipos.MapPost("/curso", async Task<Created<EnumeradorResponse>> (NovoCursoRequest req, TiposServico s) =>
            {
                var curso = await s.CriarCursoAsync(req);
                return TypedResults.Created("/tipos/curso", curso);
            })
            .RequireAuthorization(NomePapel.Diretor)
            .WithSummary("Cadastra um curso (matéria) na rede. Só diretor.")
            .WithDescription("Nome de 1 a 30 caracteres. Nome repetido (sem considerar acento e maiúscula) responde 409.")
            .ComErros(400, 403, 409);

        return app;
    }
}
