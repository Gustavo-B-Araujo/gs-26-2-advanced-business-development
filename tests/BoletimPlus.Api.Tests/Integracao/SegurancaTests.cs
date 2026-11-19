using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BoletimPlus.Api.Tests.Integracao;

/// <summary>JWT: autenticação (token válido do Java) e autorização (papel lido no banco).</summary>
public class SegurancaTests
{
    [Fact]
    public async Task Sem_token_responde_401()
    {
        using var api = new ApiFactory();
        var resposta = await api.CreateClient().GetAsync("/tipos/presenca");
        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Token_assinado_com_outra_chave_responde_401()
    {
        using var api = new ApiFactory();
        var cliente = api.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            Token(Usuarios.Skinner, "uma-chave-diferente-com-mais-de-32-caracteres", DateTime.UtcNow.AddMinutes(5)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/tipos/presenca")).StatusCode);
    }

    [Fact]
    public async Task Token_vencido_responde_401()
    {
        using var api = new ApiFactory();
        var cliente = api.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            Token(Usuarios.Skinner, ApiFactory.Segredo, DateTime.UtcNow.AddMinutes(-5)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/tipos/presenca")).StatusCode);
    }

    [Fact]
    public async Task Usuario_bloqueado_com_token_valido_responde_401()
    {
        using var api = new ApiFactory();
        var resposta = await api.ClienteComo(Usuarios.Homer).GetAsync("/tipos/presenca");
        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Token_valido_lista_os_tipos_de_presenca()
    {
        using var api = new ApiFactory();
        var resposta = await api.ClienteComo(Usuarios.Bart).GetAsync("/tipos/presenca");
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("Falta Justificada", await resposta.Content.ReadAsStringAsync());
        Assert.Contains("data_criacao", await resposta.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(Usuarios.Edna)]
    [InlineData(Usuarios.Marge)]
    [InlineData(Usuarios.Bart)]
    public async Task Rota_de_diretor_responde_403_para_outro_papel(int usuario)
    {
        using var api = new ApiFactory();
        var resposta = await api.ClienteComo(usuario).GetAsync("/professores");
        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task Diretor_so_ve_as_proprias_escolas()
    {
        using var api = new ApiFactory();
        var cliente = api.ClienteComo(Usuarios.Skinner);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/diretores/1/escolas")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/diretores/2/escolas")).StatusCode);
    }

    [Fact]
    public async Task Professor_nao_le_turma_de_outro_professor()
    {
        using var api = new ApiFactory();
        var cliente = api.ClienteComo(Usuarios.Edna);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/turmas/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/turmas/3")).StatusCode);
    }

    [Fact]
    public async Task Guardia_le_a_turma_do_dependente()
    {
        using var api = new ApiFactory();
        var resposta = await api.ClienteComo(Usuarios.Marge).GetAsync("/turmas/3");
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    private static string Token(int usuarioCod, string segredo, DateTime expira) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim("sub", usuarioCod.ToString())]),
            NotBefore = expira.AddMinutes(-30),
            IssuedAt = expira.AddMinutes(-30),
            Expires = expira,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(segredo)), SecurityAlgorithms.HmacSha256),
        });
}
