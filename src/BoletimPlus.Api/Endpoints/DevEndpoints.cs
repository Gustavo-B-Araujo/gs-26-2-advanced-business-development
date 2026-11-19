using BoletimPlus.Api.Contratos;
using BoletimPlus.Api.Dados;
using BoletimPlus.Api.Dominio;
using BoletimPlus.Api.Seguranca;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace BoletimPlus.Api.Endpoints;

/// <summary>
/// Só em Development: emite um token por CPF e senha, sem conferir o status do usuário (o status é
/// conferido em cada rota). Em produção a rota não existe; o token vem do POST /auth/login.
/// </summary>
public static class DevEndpoints
{
    public static IEndpointRouteBuilder MapDev(this IEndpointRouteBuilder app)
    {
        app.MapPost("/dev/token", async Task<Ok<TokenResponse>> (TokenDevRequest req, BoletimDbContext db, GeradorToken gerador) =>
            {
                var usuario = await db.Usuario.AsNoTracking()
                    .Where(u => u.Pessoa.NumDocumento == req.NumDocumento)
                    .Select(u => new { u.Cod, u.SenhaHash })
                    .FirstOrDefaultAsync();
                if (usuario is null || req.Senha is null || !BCrypt.Net.BCrypt.Verify(req.Senha, usuario.SenhaHash))
                    throw new RegraException(StatusCodes.Status401Unauthorized, "CPF ou senha inválidos.");

                var (token, expiraEm) = gerador.Gerar(usuario.Cod);
                return TypedResults.Ok(new TokenResponse(token, usuario.Cod, expiraEm));
            })
            .AllowAnonymous()
            .WithTags("Desenvolvimento")
            .WithSummary("Gera um JWT de teste por CPF e senha (só em Development).")
            .WithDescription("O status do usuário é conferido em cada rota: bloqueado recebe 401 mesmo com token.")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }
}
