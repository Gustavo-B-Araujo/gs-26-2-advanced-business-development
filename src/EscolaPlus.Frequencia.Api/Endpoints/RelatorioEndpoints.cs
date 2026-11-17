using EscolaPlus.Frequencia.Api.Domain.Services;
using EscolaPlus.Frequencia.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EscolaPlus.Frequencia.Api.Endpoints;

public static class RelatorioEndpoints
{
    public static void MapRelatorioEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/relatorios")
                       .WithTags("Relatorios")
                       .RequireAuthorization("ApenasGestaoEscolar")
                       .RequireRateLimiting("porUsuario");

        group.MapGet("/frequencia", async (
            [FromQuery] int turmaId,
            [FromQuery] int alunoId,
            [FromQuery] DateOnly de,
            [FromQuery] DateOnly ate,
            AppDbContext db) =>
        {
            var frequencias = await db.Frequencias
                .Where(f => f.TurmaId == turmaId && f.AlunoId == alunoId && f.Data >= de && f.Data <= ate)
                .ToListAsync();

            if (!frequencias.Any())
                return Results.NotFound(new ProblemDetails { Title = "Nenhum registro encontrado para o período", Status = 404 });

            var taxa = FrequenciaCalculator.CalcularTaxaFrequencia(frequencias, de, ate);
            var faltasConsecutivas = FrequenciaCalculator.CalcularFaltasConsecutivas(frequencias, de);

            return Results.Ok(new
            {
                AlunoId = alunoId,
                TurmaId = turmaId,
                PeriodoInicio = de,
                PeriodoFim = ate,
                TaxaFrequencia = taxa,
                FaltasConsecutivas = faltasConsecutivas
            });
        })
        .WithName("ObterRelatorioFrequencia")
        .Produces(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }
}