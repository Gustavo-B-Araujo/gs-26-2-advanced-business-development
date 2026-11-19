using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace BoletimPlus.Api.Dominio;

/// <summary>
/// Regras de negócio puras, sem banco: os limites vêm das constraints de 01_ddl.sql e as
/// regras de texto são as mesmas do app (src/lib/validacao.ts). Cada método devolve a mensagem
/// do erro, ou null quando o valor é válido.
/// </summary>
public static partial class Regras
{
    public const int NomePessoaMax = 150;
    public const int NomeAvaliacaoMax = 50;
    public const int TipoCursoMax = 30;
    public const int ObservacaoMax = 150;
    public const int SenhaMin = 8;
    public const int AnoMin = 2000;
    public const decimal NotaMin = 0;
    public const decimal NotaMax = 100;
    public static readonly DateOnly DataPresencaMin = new(2000, 1, 1);

    public static string? ValidarNome(string? nome)
    {
        var limpo = nome?.Trim() ?? "";
        if (limpo.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 2)
            return "Informe nome e sobrenome.";
        if (limpo.Length > NomePessoaMax) return $"Máximo de {NomePessoaMax} caracteres.";
        return null;
    }

    /// <summary>11 dígitos, sem todos iguais, com os 2 dígitos verificadores certos.</summary>
    public static string? ValidarCpf(string? cpf)
    {
        var d = cpf ?? "";
        if (d.Length != 11 || !d.All(char.IsAsciiDigit)) return "O CPF tem 11 dígitos, sem máscara.";
        if (d.Distinct().Count() == 1) return "CPF inválido.";
        int Digito(int tamanho)
        {
            var soma = 0;
            for (var i = 0; i < tamanho; i++) soma += (d[i] - '0') * (tamanho + 1 - i);
            var resto = soma * 10 % 11;
            return resto == 10 ? 0 : resto;
        }
        if (Digito(9) != d[9] - '0' || Digito(10) != d[10] - '0') return "CPF inválido.";
        return null;
    }

    public static string? ValidarSenha(string? senha)
    {
        if (senha is null || senha.Length < SenhaMin) return $"Mínimo de {SenhaMin} caracteres.";
        if (!LetraRegex().IsMatch(senha) || !DigitoRegex().IsMatch(senha)) return "Use letras e números.";
        return null;
    }

    /// <summary>
    /// CPF do login e do cadastro de responsável: só 11 dígitos, como o Java (dto/Regras.CPF).
    /// Os dígitos verificadores o app já confere antes de enviar.
    /// </summary>
    public static string? ValidarCpfFormato(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf)) return "Informe o CPF.";
        return cpf.Length == 11 && cpf.All(char.IsAsciiDigit) ? null : "O CPF tem 11 dígitos, sem máscara.";
    }

    /// <summary>Campo obrigatório (senha do login, senha atual). Não aplica a regra de força.</summary>
    public static string? ValidarInformado(string? valor, string mensagem) =>
        string.IsNullOrWhiteSpace(valor) ? mensagem : null;

    /// <summary>Senha nova (cadastro e troca): obrigatória, 8+ caracteres, letras e números.</summary>
    public static string? ValidarSenhaNova(string? senha, string mensagemVazia) =>
        ValidarInformado(senha, mensagemVazia) ?? ValidarSenha(senha);

    /// <summary>Média simples das notas lançadas; null sem notas.</summary>
    public static double? Media(IEnumerable<double> notas)
    {
        var lista = notas.ToList();
        return lista.Count == 0 ? null : lista.Average();
    }

    /// <summary>Fração de 0 a 1. Só 'Presente' conta como presença; null sem chamadas.</summary>
    public static double? Frequencia(IEnumerable<string> tipos)
    {
        var lista = tipos.ToList();
        if (lista.Count == 0) return null;
        return (double)lista.Count(t => ChaveEnum(t) == "PRESENTE") / lista.Count;
    }

    public static string? ValidarNomeAvaliacao(string? nome)
    {
        var limpo = nome?.Trim() ?? "";
        return limpo.Length is 0 or > NomeAvaliacaoMax
            ? $"O nome precisa ter de 1 a {NomeAvaliacaoMax} caracteres."
            : null;
    }

    public static string? ValidarTipoCurso(string? enumerador)
    {
        var limpo = enumerador?.Trim() ?? "";
        return limpo.Length is 0 or > TipoCursoMax
            ? $"O nome precisa ter de 1 a {TipoCursoMax} caracteres."
            : null;
    }

    /// <summary>CHECK (semestre_referencia IN (1, 2)).</summary>
    public static string? ValidarSemestre(int? semestre) =>
        semestre is 1 or 2 ? null : "Semestre inválido: use 1 ou 2.";

    /// <summary>CHECK (ano_referencia &gt;= 2000).</summary>
    public static string? ValidarAno(int? ano) =>
        ano is >= AnoMin ? null : $"O ano precisa ser {AnoMin} ou depois.";

    /// <summary>CHECK (nota BETWEEN 0 AND 100). Nota nula é válida: apaga a nota lançada.</summary>
    public static string? ValidarNota(decimal? nota) =>
        nota is null or (>= NotaMin and <= NotaMax)
            ? null
            : $"A nota precisa estar entre {NotaMin} e {NotaMax}.";

    public static string? ValidarObservacao(string? observacao) =>
        (observacao?.Length ?? 0) > ObservacaoMax
            ? $"Observação com mais de {ObservacaoMax} caracteres."
            : null;

    /// <summary>CHECK da data mínima e trigger trg_valida_data_presenca (sem data futura).</summary>
    public static string? ValidarDataPresenca(DateOnly data, DateOnly hoje)
    {
        if (data < DataPresencaMin) return "Data inválida.";
        if (data > hoje) return "Não é permitido registrar presença em data futura.";
        return null;
    }

    /// <summary>Lê a data só no formato do contrato (YYYY-MM-DD). Outro formato devolve null.</summary>
    public static DateOnly? LerData(string? texto) =>
        DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)
            ? data
            : null;

    /// <summary>NUMBER(5,2): guarda a nota com 2 casas.</summary>
    public static decimal ArredondarNota(decimal nota) => Math.Round(nota, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Chave do enumerador, igual à do app: sem acento, maiúscula, espaço vira "_".
    /// 'Falta Justificada' vira FALTA_JUSTIFICADA; 'História' e 'historia' dão a mesma chave.
    /// </summary>
    public static string ChaveEnum(string enumerador)
    {
        var semAcento = new StringBuilder();
        foreach (var c in enumerador.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                semAcento.Append(c);
        }
        return EspacoRegex().Replace(semAcento.ToString().Trim().ToUpperInvariant(), "_");
    }

    [GeneratedRegex("[A-Za-z]")]
    private static partial Regex LetraRegex();

    [GeneratedRegex(@"\d")]
    private static partial Regex DigitoRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex EspacoRegex();
}
