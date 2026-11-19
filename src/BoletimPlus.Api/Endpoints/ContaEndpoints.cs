using System.Security.Claims;
using BoletimPlus.Api.Contratos;
using BoletimPlus.Api.Infra;
using BoletimPlus.Api.Seguranca;
using BoletimPlus.Api.Servicos;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BoletimPlus.Api.Endpoints;

/// <summary>Conta: login, cadastro de responsável, senha, nome e logout. Mesmo contrato do Java.</summary>
public static class ContaEndpoints
{
    public static IEndpointRouteBuilder MapConta(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/auth").WithTags("Conta");

        auth.MapPost("/login", async (LoginRequest req, ContaServico s) => TypedResults.Ok(await s.EntrarAsync(req)))
            .AllowAnonymous()
            .RequireRateLimiting(LimiteDeRequisicoes.PoliticaEscrita)
            .WithSummary("Entra com CPF e senha. Devolve o JWT e os papéis.")
            .WithDescription("Senha conferida com bcrypt. 401: CPF ou senha inválidos. 403: usuário não Ativo.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        auth.MapPost("/cadastro", async Task<Created<SessaoResponse>> (CadastroRequest req, ContaServico s) =>
            {
                var sessao = await s.CadastrarAsync(req);
                return TypedResults.Created($"/guardioes/{sessao.Papeis.GuardiaoCod}/dependentes", sessao);
            })
            .AllowAnonymous()
            .RequireRateLimiting(LimiteDeRequisicoes.PoliticaEscrita)
            .WithSummary("Cadastra um responsável (Pessoa + Usuario Ativo + Guardiao) e já entra.")
            .WithDescription("Nome e sobrenome; CPF com 11 dígitos; senha com 8+ caracteres, letras e números. CPF repetido responde 409.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        auth.MapPut("/senha", async Task<NoContent> (AlterarSenhaRequest req, ClaimsPrincipal user, ContaServico s) =>
            {
                await s.AlterarSenhaAsync(user.Papeis(), req);
                return TypedResults.NoContent();
            })
            .RequireAuthorization()
            .RequireRateLimiting(LimiteDeRequisicoes.PoliticaEscrita)
            .WithSummary("Altera a própria senha.")
            .WithDescription("400 (não 401) quando a senha atual não confere: o app trata 401 como sessão vencida.")
            .ComErros(400);

        auth.MapPut("/nome", async (RenomearRequest req, ClaimsPrincipal user, ContaServico s) =>
                TypedResults.Ok(await s.AlterarNomeAsync(user.Papeis(), req)))
            .RequireAuthorization()
            .WithSummary("Altera o próprio nome e devolve a Pessoa. Só diretor e responsável.")
            .ComErros(400, 403);

        auth.MapPost("/logout", () => TypedResults.NoContent())
            .RequireAuthorization()
            .WithSummary("Sai. O JWT é stateless: o app apaga o token; o servidor só confirma.")
            .ComErros();

        return app;
    }
}
