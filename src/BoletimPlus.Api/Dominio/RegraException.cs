namespace BoletimPlus.Api.Dominio;

/// <summary>Regra de negócio violada. O tratador de erros converte em ProblemDetails com o status.</summary>
public class RegraException(int status, string mensagem) : Exception(mensagem)
{
    public int Status { get; } = status;

    public static RegraException Invalido(string mensagem) => new(StatusCodes.Status400BadRequest, mensagem);
    public static RegraException Proibido(string mensagem = "Acesso negado.") => new(StatusCodes.Status403Forbidden, mensagem);
    public static RegraException NaoEncontrado(string mensagem) => new(StatusCodes.Status404NotFound, mensagem);
    public static RegraException Conflito(string mensagem) => new(StatusCodes.Status409Conflict, mensagem);

    /// <summary>Lança 400 com a primeira mensagem de erro não nula.</summary>
    public static void SeHouver(params string?[] erros)
    {
        var erro = erros.FirstOrDefault(e => e is not null);
        if (erro is not null) throw Invalido(erro);
    }
}
