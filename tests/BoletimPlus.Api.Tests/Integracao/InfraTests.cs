using System.Net;
using System.Text.Json;

namespace BoletimPlus.Api.Tests.Integracao;

/// <summary>Rate limit, health checks e documentação OpenAPI.</summary>
public class InfraTests
{
    [Fact]
    public async Task Passar_do_limite_responde_429_com_retry_after()
    {
        using var api = new ApiFactory(limitePorJanela: 3);
        var cliente = api.ClienteComo(Usuarios.Bart);

        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/tipos/nota")).StatusCode);
        var bloqueada = await cliente.GetAsync("/tipos/nota");

        Assert.Equal(HttpStatusCode.TooManyRequests, bloqueada.StatusCode);
        Assert.True(int.Parse(bloqueada.Headers.GetValues("Retry-After").Single()) > 0);
        Assert.StartsWith("Limite de requisições atingido.", await bloqueada.Detalhe());
    }

    [Fact]
    public async Task Limite_e_por_usuario()
    {
        using var api = new ApiFactory(limitePorJanela: 1);
        Assert.Equal(HttpStatusCode.OK, (await api.ClienteComo(Usuarios.Bart).GetAsync("/tipos/nota")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.ClienteComo(Usuarios.Edna).GetAsync("/tipos/nota")).StatusCode);
    }

    [Fact]
    public async Task Limite_de_escrita_protege_os_cadastros()
    {
        using var api = new ApiFactory(limiteEscrita: 1);
        var cliente = api.ClienteComo(Usuarios.Skinner);
        await cliente.PostIdempotente("/alunos", new { nome = "Nelson Muntz", num_documento = "11144477735", senha = (string?)null });
        var segunda = await cliente.PostIdempotente("/alunos", new { nome = "Ralph Wiggum", num_documento = "52998224725", senha = (string?)null });
        Assert.Equal(HttpStatusCode.TooManyRequests, segunda.StatusCode);
    }

    [Fact]
    public async Task Health_mostra_api_e_banco_sem_depender_do_java()
    {
        using var api = new ApiFactory();
        var resposta = await api.CreateClient().GetAsync("/health");
        using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        var checagens = json.RootElement.GetProperty("checagens").EnumerateArray()
            .ToDictionary(c => c.GetProperty("nome").GetString()!, c => c.GetProperty("status").GetString());

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("Healthy", checagens["api"]);
        Assert.Equal("Healthy", checagens["oracle"]);
        Assert.False(checagens.ContainsKey("java-api"));
        Assert.Equal("Healthy", json.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Health_live_nao_depende_do_banco()
    {
        using var api = new ApiFactory();
        var resposta = await api.CreateClient().GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.DoesNotContain("oracle", await resposta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Swagger_documenta_rotas_jwt_e_idempotency_key()
    {
        using var api = new ApiFactory();
        var json = await api.CreateClient().GetStringAsync("/swagger/v1/swagger.json");

        Assert.Contains("/turmas/{cod}/avaliacoes", json);
        Assert.Contains("/avaliacoes/{cod}/notas", json);
        Assert.Contains("\"bearer\"", json);
        Assert.Contains("Idempotency-Key", json);
    }
}
