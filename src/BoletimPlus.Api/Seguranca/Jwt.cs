using System.Security.Claims;
using System.Text;
using BoletimPlus.Api.Dados;
using BoletimPlus.Api.Dominio;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BoletimPlus.Api.Seguranca;

/// <summary>
/// Configuração do JWT, lida das variáveis de ambiente. A API emite o token no login e no cadastro,
/// igual ao do Java (HS256, sub = Usuario.cod, iss, iat, exp de 8 h), e valida com a mesma chave
/// (JWT_SECRET, bytes UTF-8, mínimo 32 caracteres). Com o mesmo JWT_SECRET, um token de um
/// backend vale no outro.
/// </summary>
public sealed class JwtConfig
{
    public const int SegredoMin = 32;

    /// <summary>Mesmo padrão do Java (boletim.jwt.issuer = ${JWT_ISSUER:boletim-plus}).</summary>
    public const string EmissorPadrao = "boletim-plus";

    /// <summary>8 h, como boletim.jwt.validade do Java.</summary>
    public const int ExpiracaoPadraoMinutos = 480;

    public required string Segredo { get; init; }
    /// <summary>JWT_ISSUER: quando definido, o iss é validado; sem ele, qualquer iss passa.</summary>
    public string? Emissor { get; init; }
    public string? Audiencia { get; init; }
    public int ExpiracaoMinutos { get; init; } = ExpiracaoPadraoMinutos;

    /// <summary>iss dos tokens emitidos aqui: JWT_ISSUER ou o padrão do Java (o Java exige o iss).</summary>
    public string EmissorDoToken => string.IsNullOrWhiteSpace(Emissor) ? EmissorPadrao : Emissor;

    public SymmetricSecurityKey Chave => new(Encoding.UTF8.GetBytes(Segredo));

    public static JwtConfig Ler(IConfiguration config)
    {
        var segredo = config["JWT_SECRET"];
        if (string.IsNullOrWhiteSpace(segredo) || segredo.Length < SegredoMin)
            throw new InvalidOperationException(
                $"Defina a variável de ambiente JWT_SECRET com pelo menos {SegredoMin} caracteres (a mesma do backend Java).");
        return new JwtConfig
        {
            Segredo = segredo,
            Emissor = config["JWT_ISSUER"],
            Audiencia = config["JWT_AUDIENCE"],
            ExpiracaoMinutos = int.TryParse(config["JWT_EXPIRATION_MINUTES"], out var m) && m > 0 ? m : ExpiracaoPadraoMinutos,
        };
    }

    public TokenValidationParameters ParametrosDeValidacao() => new()
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = Chave,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ValidateIssuer = !string.IsNullOrWhiteSpace(Emissor),
        ValidIssuer = Emissor,
        ValidateAudience = !string.IsNullOrWhiteSpace(Audiencia),
        ValidAudience = Audiencia,
        ValidateLifetime = true,
        RequireExpirationTime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
    };
}

/// <summary>Emite um token igual ao do Java. Usado pelo login, pelo cadastro e pelo POST /dev/token.</summary>
public sealed class GeradorToken(JwtConfig config)
{
    public (string Token, DateTime ExpiraEm) Gerar(int usuarioCod)
    {
        var expiraEm = DateTime.UtcNow.AddMinutes(config.ExpiracaoMinutos);
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, usuarioCod.ToString())]),
            Expires = expiraEm,
            Issuer = config.EmissorDoToken,
            Audience = config.Audiencia,
            SigningCredentials = new SigningCredentials(config.Chave, SecurityAlgorithms.HmacSha256),
        });
        return (token, expiraEm);
    }
}

/// <summary>Papéis da pessoa logada, lidos no banco. Cada um guarda o cod da tabela do papel.</summary>
public sealed record Papeis(
    int UsuarioCod,
    int PessoaCod,
    int? DiretorCod,
    int? ProfessorCod,
    int? GuardiaoCod,
    int? AlunoCod);

public static class NomePapel
{
    public const string Diretor = "Diretor";
    public const string Professor = "Professor";
    public const string Guardiao = "Guardiao";
    public const string Aluno = "Aluno";
}

public static class ClaimsDoBoletim
{
    public const string UsuarioCod = "usuario_cod";
    public const string PessoaCod = "pessoa_cod";
    public const string DiretorCod = "diretor_cod";
    public const string ProfessorCod = "professor_cod";
    public const string GuardiaoCod = "guardiao_cod";
    public const string AlunoCod = "aluno_cod";

    /// <summary>
    /// Roda depois que a assinatura e a validade do token passam. Lê o Usuario do sub:
    /// só StatusUsuario ATIVO segue (bloqueado ou inativo recebe 401). Depois lê os papéis no
    /// banco e os põe como claims: nenhum papel vem do app nem do token.
    /// </summary>
    public static async Task CarregarPapeis(TokenValidatedContext contexto)
    {
        var sub = contexto.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!int.TryParse(sub, out var usuarioCod))
        {
            contexto.Fail("Token sem sub válido.");
            return;
        }

        var db = contexto.HttpContext.RequestServices.GetRequiredService<BoletimDbContext>();
        var usuario = await db.Usuario.AsNoTracking()
            .Where(u => u.Cod == usuarioCod)
            .Select(u => new { u.CodPessoa, Status = u.Status.Valor })
            .FirstOrDefaultAsync();
        if (usuario is null || Regras.ChaveEnum(usuario.Status) != "ATIVO")
        {
            contexto.Fail("Usuário inexistente, inativo ou bloqueado.");
            return;
        }

        var pessoa = usuario.CodPessoa;
        var diretor = await CodDoPapel(db.Diretor, pessoa);
        var professor = await CodDoPapel(db.Professor, pessoa);
        var guardiao = await CodDoPapel(db.Guardiao, pessoa);
        var aluno = await CodDoPapel(db.Aluno, pessoa);

        var claims = new List<Claim>
        {
            new(UsuarioCod, usuarioCod.ToString()),
            new(PessoaCod, pessoa.ToString()),
        };
        void Adicionar(int? cod, string tipo, string papel)
        {
            if (cod is null) return;
            claims.Add(new Claim(tipo, cod.Value.ToString()));
            claims.Add(new Claim(ClaimTypes.Role, papel));
        }
        Adicionar(diretor, DiretorCod, NomePapel.Diretor);
        Adicionar(professor, ProfessorCod, NomePapel.Professor);
        Adicionar(guardiao, GuardiaoCod, NomePapel.Guardiao);
        Adicionar(aluno, AlunoCod, NomePapel.Aluno);

        contexto.Principal!.AddIdentity(new ClaimsIdentity(claims, "BoletimPlus"));
    }

    public static Papeis Papeis(this ClaimsPrincipal usuario)
    {
        int? Ler(string tipo) => int.TryParse(usuario.FindFirstValue(tipo), out var v) ? v : null;
        return new Papeis(
            Ler(UsuarioCod) ?? throw new RegraException(StatusCodes.Status401Unauthorized, "Sessão inválida."),
            Ler(PessoaCod) ?? 0,
            Ler(DiretorCod),
            Ler(ProfessorCod),
            Ler(GuardiaoCod),
            Ler(AlunoCod));
    }

    private static Task<int?> CodDoPapel<T>(IQueryable<T> tabela, int pessoaCod) where T : Dados.Papel =>
        tabela.AsNoTracking().Where(p => p.CodPessoa == pessoaCod).Select(p => (int?)p.Cod).FirstOrDefaultAsync();
}
