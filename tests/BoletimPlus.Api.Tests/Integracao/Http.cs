using System.Net.Http.Json;
using System.Text.Json;

namespace BoletimPlus.Api.Tests.Integracao;

public static class Http
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static Task<HttpResponseMessage> PostIdempotente(this HttpClient cliente, string url, object corpo, string? chave = null)
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(corpo) };
        requisicao.Headers.Add("Idempotency-Key", chave ?? Guid.NewGuid().ToString());
        return cliente.SendAsync(requisicao);
    }

    public static async Task<T> Ler<T>(this HttpResponseMessage resposta) =>
        (await resposta.Content.ReadFromJsonAsync<T>(Json))!;

    /// <summary>Campo detail do ProblemDetails: a mensagem que o app mostra.</summary>
    public static async Task<string?> Detalhe(this HttpResponseMessage resposta)
    {
        using var doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("detail", out var d) ? d.GetString() : null;
    }
}
