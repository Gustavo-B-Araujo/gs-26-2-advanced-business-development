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
                       .WithTags("Frequencias");

        group.MapPost("/", async (
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            FrequenciaRequest request,
            AppDbContext db,
            HttpContext http) =>
        {
            // 1. Valida se a chave de idempotência foi enviada (Obrigatório 400)
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                return Results.BadRequest(new ProblemDetails { Title = "Idempotency-Key obrigatório", Status = 400 });

            // 2. Validações de negócio básicas (Data futura e Status inválido)
            if (request.Data > DateOnly.FromDateTime(DateTime.UtcNow))
                return Results.UnprocessableEntity(new ProblemDetails { Title = "Data futura não permitida", Status = 422 });

            if (!Enum.TryParse<StatusFrequencia>(request.Status, true, out var status))
                return Results.BadRequest(new ProblemDetails { Title = "Status inválido", Status = 400 });

            if (!Enum.TryParse<OrigemFrequencia>(request.Origem, true, out var origem))
                origem = OrigemFrequencia.APP;

            // 3. Valida se o aluno existe e se pertence à turma
            var aluno = await db.Alunos.FindAsync(request.AlunoId);
            if (aluno == null)
                return Results.NotFound(new ProblemDetails { Title = "Aluno não encontrado", Status = 404 });

            if (aluno.TurmaId != request.TurmaId)
                return Results.UnprocessableEntity(new ProblemDetails { Title = "Turma inconsistente", Status = 422 });

            // 4. Lógica de Idempotência: Gera um hash único do corpo da requisição
            var bodyJson = JsonSerializer.Serialize(request);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(bodyJson)));

            // Verifica se a chave já foi usada antes
            var existing = await db.IdempotencyRecords.FirstOrDefaultAsync(i => i.Chave == idempotencyKey);

            if (existing != null)
            {
                // Se a chave existe mas o corpo é diferente -> 409 Conflito
                if (existing.HashCorpo != hash)
                    return Results.Conflict(new ProblemDetails { Title = "Idempotency-Key já usada com corpo diferente", Status = 409 });

                // Se a chave e o corpo são iguais -> Retorna a resposta salva (Replay 200 OK)
                var storedResponse = JsonSerializer.Deserialize<FrequenciaResponse>(existing.CorpoResposta);
                return Results.Ok(storedResponse);
            }

            // 5. Regra de unicidade: Aluno já tem frequência nesta data?
            var duplicado = await db.Frequencias.AnyAsync(f => f.AlunoId == request.AlunoId && f.Data == request.Data);
            if (duplicado)
                return Results.Conflict(new ProblemDetails { Title = "Registro já existe para aluno e data", Status = 409 });

            // 6. Cria o registro no banco
            var frequencia = new Domain.Frequencia
            {
                AlunoId = request.AlunoId,
                TurmaId = request.TurmaId,
                Data = request.Data,
                Status = status,
                Origem = origem,
                RegistradoPor = "professor_teste", // Depois puxaremos do JWT
                CriadoEm = DateTime.UtcNow
            };

            db.Frequencias.Add(frequencia);

            var response = new FrequenciaResponse
            {
                Id = frequencia.Id, // O ID será gerado no SaveChanges
                AlunoId = frequencia.AlunoId,
                TurmaId = frequencia.TurmaId,
                Data = frequencia.Data,
                Status = frequencia.Status.ToString(),
                Origem = frequencia.Origem.ToString(),
                CriadoEm = frequencia.CriadoEm
            };

            // 7. Salva a idempotência na MESMA transação
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
                // Tratamento de concorrência: se duas requisições chegarem juntas, a que perder pega a resposta da vencedora
                var vencedora = await db.IdempotencyRecords.FirstOrDefaultAsync(i => i.Chave == idempotencyKey);
                if (vencedora != null)
                {
                    var stored = JsonSerializer.Deserialize<FrequenciaResponse>(vencedora.CorpoResposta);
                    return Results.Ok(stored);
                }
                throw;
            }

            // Retorna 201 Created com o cabeçalho Location
            return Results.Created($"/frequencias/{frequencia.Id}", response);
        })
        .WithName("CriarFrequencia")
        .Produces<FrequenciaResponse>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity);
    }
}