using System.Security.Claims;
using BoletimPlus.Api.Seguranca;
using BoletimPlus.Api.Servicos;

namespace BoletimPlus.Api.Endpoints;

/// <summary>Acompanhamento: dependentes do responsável e boletim do aluno. A análise com IA fica só no Java.</summary>
public static class AcompanhamentoEndpoints
{
    public static IEndpointRouteBuilder MapAcompanhamento(this IEndpointRouteBuilder app)
    {
        app.MapGet("/guardioes/{cod:int}/dependentes", async (int cod, ClaimsPrincipal user, AcompanhamentoServico s) =>
                TypedResults.Ok(await s.DependentesAsync(user.Papeis(), cod)))
            .WithTags("Responsáveis")
            .RequireAuthorization(NomePapel.Guardiao)
            .WithSummary("Dependentes do responsável (AlunoGuardiao), com o parentesco. Só o próprio responsável.")
            .ComErros(403);

        app.MapGet("/alunos/{cod:int}/boletim", async (int cod, ClaimsPrincipal user, AcompanhamentoServico s) =>
                TypedResults.Ok(await s.BoletimAsync(user.Papeis(), cod)))
            .WithTags("Boletim")
            .RequireAuthorization()
            .WithSummary("Boletim do aluno: notas e média por turma, presenças e frequência (0 a 1).")
            .WithDescription("O próprio aluno, um responsável vinculado, o professor ou o diretor de uma turma dele. Só 'Presente' conta na frequência.")
            .ComErros(403, 404);

        return app;
    }
}
