using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace EscolaPlus.Frequencia.Api.Auth;

public static class JwtConfig
{
    public static void AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var secret = configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret não configurado.");
        var issuer = configuration["Jwt:Issuer"];
        var audience = configuration["Jwt:Audience"];

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                    ClockSkew = TimeSpan.Zero, // Tolerância zero para expiração (segurança)
                   // RoleClaimType = "role" // Mapeia o claim do Java para o padrão do .NET
                };
            });

        services.AddAuthorization(options =>
        {
            // Política restrita: Apenas Professor, Coordenador ou Diretor podem acessar a API
            options.AddPolicy("ApenasGestaoEscolar", policy =>
                policy.RequireRole("PROFESSOR", "COORDENADOR", "DIRETOR"));
        });
    }
}