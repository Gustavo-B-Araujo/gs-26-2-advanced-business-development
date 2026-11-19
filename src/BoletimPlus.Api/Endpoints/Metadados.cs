using BoletimPlus.Api.Infra;

namespace BoletimPlus.Api.Endpoints;

/// <summary>Marca o endpoint que exige Idempotency-Key. O Swagger mostra o header nele.</summary>
public sealed class ExigeIdempotencia;

public static class Metadados
{
    /// <summary>Respostas de erro (ProblemDetails) comuns a uma rota autenticada.</summary>
    public static RouteHandlerBuilder ComErros(this RouteHandlerBuilder rota, params int[] status)
    {
        rota.ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
        foreach (var s in status) rota.ProducesProblem(s);
        return rota;
    }

    /// <summary>POST que cria recurso: Idempotency-Key obrigatória e limite de escrita.</summary>
    public static RouteHandlerBuilder Idempotente(this RouteHandlerBuilder rota) =>
        rota.AddEndpointFilter<IdempotenciaFilter>()
            .WithMetadata(new ExigeIdempotencia())
            .RequireRateLimiting(LimiteDeRequisicoes.PoliticaEscrita)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
}
