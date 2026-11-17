using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EscolaPlus.Frequencia.Api.Contracts;
using EscolaPlus.Frequencia.Api.Domain;
using EscolaPlus.Frequencia.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EscolaPlus.Frequencia.Api.Endpoints;

public static class FrequenciaEndpoints
{
    public static void MapFrequenciaEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/frequencias")
                       .WithTags("Frequencias")
                       .RequireAuthorization("ApenasGestaoEscolar") 
                       .RequireRateLimiting("porUsuario");

        // ROTA POST: Criar Frequencia
        group.MapPost("/", async (
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            FrequenciaRequest request,
            AppDbContext db,
            HttpContext http) =>
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                return Results.BadRequest(new ProblemDetails { Title = "Idempotency-Key obrigatório", Status = 400 });

            if (request.Data > DateOnly.FromDateTime(DateTime.UtcNow))
                return Results.UnprocessableEntity(new ProblemDetails { Title = "Data futura não permitida", Status = 422 });

            if (!Enum.TryParse<StatusFrequencia>(request.Status, true, out var status))
                return Results.BadRequest(new ProblemDetails { Title = "Status inválido", Status = 400 });

            if (!Enum.TryParse<OrigemFrequencia>(request.Origem, true, out var origem))
                origem = OrigemFrequencia.APP;

            var aluno = await db.Alunos.FindAsync(request.AlunoId);
            if (aluno == null)
                return Results.NotFound(new ProblemDetails { Title = "Aluno não encontrado", Status = 404 });

            if (aluno.TurmaId != request.TurmaId)
                return Results.UnprocessableEntity(new ProblemDetails { Title = "Turma inconsistente", Status = 422 });

            var bodyJson = JsonSerializer.Serialize(request);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(bodyJson)));

            var existing = await db.IdempotencyRecords.FirstOrDefaultAsync(i => i.Chave == idempotencyKey);
            if (existing != null)
            {
                if (existing.HashCorpo != hash)
                    return Results.Conflict(new ProblemDetails { Title = "Idempotency-Key já usada com corpo diferente", Status = 409 });

                var storedResponse = JsonSerializer.Deserialize<FrequenciaResponse>(existing.CorpoResposta);
                return Results.Ok(storedResponse);
            }

            var duplicado = await db.Frequencias.AnyAsync(f => f.AlunoId == request.AlunoId && f.Data == request.Data);
            if (duplicado)
                return Results.Conflict(new ProblemDetails { Title = "Registro já existe para aluno e data", Status = 409 });

            var frequencia = new Domain.Frequencia
            {
                AlunoId = request.AlunoId,
                TurmaId = request.TurmaId,
                Data = request.Data,
                Status = status,
                Origem = origem,
                RegistradoPor = "professor_teste",
                CriadoEm = DateTime.UtcNow
            };

            db.Frequencias.Add(frequencia);
            
            var response = new FrequenciaResponse
            {
                Id = frequencia.Id,
                AlunoId = frequencia.AlunoId,
                TurmaId = frequencia.TurmaId,
                Data = frequencia.Data,
                Status = frequencia.Status.ToString(),
                Origem = frequencia.Origem.ToString(),
                CriadoEm = frequencia.CriadoEm
            };

            var idempotency = new IdempotencyRecord
            {
                Chave = idempotencyKey,
                HashCorpo = hash,
                StatusHttp = 201,
                CorpoResposta = JsonSerializer.Serialize(response),
                CriadoEm = DateTime.UtcNow
            };
            db.IdempotencyRecords.Add(idempotency);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                var vencedora = await db.IdempotencyRecords.FirstOrDefaultAsync(i => i.Chave == idempotencyKey);
                if (vencedora != null)
                {
                    var stored = JsonSerializer.Deserialize<FrequenciaResponse>(vencedora.CorpoResposta);
                    return Results.Ok(stored);
                }
                throw;
            }

            return Results.Created($"/frequencias/{frequencia.Id}", response);
        })
        .WithName("CriarFrequencia")
        .Produces<FrequenciaResponse>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity);

        // ROTA GET: Listar Frequencias com Filtros e Paginação
        group.MapGet("/", async (
            [FromQuery] int? turmaId,
            [FromQuery] DateOnly? de,
            [FromQuery] DateOnly? ate,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            AppDbContext db = null!) =>
        {
            var query = db.Frequencias.AsNoTracking();

            if (turmaId.HasValue)
                query = query.Where(f => f.TurmaId == turmaId.Value);
            if (de.HasValue)
                query = query.Where(f => f.Data >= de.Value);
            if (ate.HasValue)
                query = query.Where(f => f.Data <= ate.Value);

            var totalItems = await query.CountAsync();
            
            var items = await query
                .OrderBy(f => f.Data)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(f => new FrequenciaResponse
                {
                    Id = f.Id,
                    AlunoId = f.AlunoId,
                    TurmaId = f.TurmaId,
                    Data = f.Data,
                    Status = f.Status.ToString(),
                    Origem = f.Origem.ToString(),
                    CriadoEm = f.CriadoEm
                })
                .ToListAsync();

            return Results.Ok(new
            {
                Total = totalItems,
                Page = page,
                PageSize = pageSize,
                Items = items
            });
        })
        .WithName("ListarFrequencias")
        .Produces(StatusCodes.Status200OK);
    }
}