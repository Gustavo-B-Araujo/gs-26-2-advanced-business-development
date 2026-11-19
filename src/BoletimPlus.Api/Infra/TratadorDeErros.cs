using BoletimPlus.Api.Dominio;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BoletimPlus.Api.Infra;

/// <summary>
/// Converte exceções em ProblemDetails (RFC 9457). O app lê o campo detail e mostra na tela.
/// Erro inesperado responde 500 sem detalhe interno: a pilha fica só no log.
/// </summary>
public sealed class TratadorDeErros(IProblemDetailsService problemas, ILogger<TratadorDeErros> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception excecao, CancellationToken cancelamento)
    {
        var (status, titulo, detalhe) = excecao switch
        {
            RegraException r => (r.Status, Titulo(r.Status), r.Message),
            BadHttpRequestException b => (StatusCodes.Status400BadRequest, "Requisição inválida",
                "Corpo ou parâmetro em formato inválido. Datas usam YYYY-MM-DD."),
            DbUpdateException => (StatusCodes.Status409Conflict, "Conflito",
                "A operação conflita com um dado que já existe."),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno",
                "Erro inesperado no servidor. Tente de novo."),
        };

        if (status >= 500) logger.LogError(excecao, "Erro não tratado em {Caminho}", http.Request.Path);
        else if (excecao is DbUpdateException) logger.LogWarning(excecao, "Conflito no banco em {Caminho}", http.Request.Path);

        http.Response.StatusCode = status;
        return await problemas.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = excecao,
            ProblemDetails = new ProblemDetails { Status = status, Title = titulo, Detail = detalhe },
        });
    }

    private static string Titulo(int status) => status switch
    {
        400 => "Dados inválidos",
        401 => "Não autenticado",
        403 => "Acesso negado",
        404 => "Não encontrado",
        409 => "Conflito",
        _ => "Erro",
    };
}
