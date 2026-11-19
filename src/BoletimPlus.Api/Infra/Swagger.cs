using BoletimPlus.Api.Endpoints;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BoletimPlus.Api.Infra;

/// <summary>OpenAPI/Swagger: documentação interativa com o botão Authorize (Bearer JWT).</summary>
public static class Swagger
{
    public static IServiceCollection AddSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(o =>
        {
            o.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Boletim Plus API (.NET)",
                Version = "v1",
                Description = "Contrato do Boletim Plus (Global Solution 2026/2, CitizenTech), sem a análise com IA: " +
                              "conta, gestão escolar, sala de aula e boletim. " +
                              "O professor lança notas e chamada; o responsável acompanha o aluno. " +
                              "Pegue o token em POST /auth/login e cole em Authorize.",
            });

            var bearer = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Cole só o token, sem a palavra Bearer.",
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
            };
            o.AddSecurityDefinition("Bearer", bearer);
            o.AddSecurityRequirement(new OpenApiSecurityRequirement { [bearer] = [] });
            o.OperationFilter<IdempotencyKeyOperationFilter>();
        });
        return services;
    }
}

/// <summary>Mostra o header Idempotency-Key nos endpoints marcados com ExigeIdempotencia.</summary>
public sealed class IdempotencyKeyOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operacao, OperationFilterContext contexto)
    {
        var exige = contexto.ApiDescription.ActionDescriptor.EndpointMetadata.OfType<ExigeIdempotencia>().Any();
        if (!exige) return;
        operacao.Parameters ??= [];
        operacao.Parameters.Add(new OpenApiParameter
        {
            Name = IdempotenciaFilter.Header,
            In = ParameterLocation.Header,
            Required = true,
            Description = "Chave única da operação (ex.: um UUID). Repetir a chave devolve a mesma resposta, sem criar de novo.",
            Schema = new OpenApiSchema { Type = "string", Example = new OpenApiString(Guid.Empty.ToString()) },
        });
    }
}
