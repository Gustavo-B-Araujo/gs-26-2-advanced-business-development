using BoletimPlus.Api.Dominio;

namespace BoletimPlus.Api.Tests.Unidade;

public class RegrasTests
{
    [Theory]
    [InlineData("Bart Simpson")]
    [InlineData("  Edna   Krabappel  ")]
    public void ValidarNome_aceita_nome_e_sobrenome(string nome) =>
        Assert.Null(Regras.ValidarNome(nome));

    [Theory]
    [InlineData("Bart")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ValidarNome_recusa_sem_sobrenome(string? nome) =>
        Assert.Equal("Informe nome e sobrenome.", Regras.ValidarNome(nome));

    [Fact]
    public void ValidarNome_recusa_mais_de_150_caracteres() =>
        Assert.NotNull(Regras.ValidarNome("Bart " + new string('a', 150)));

    [Theory]
    [InlineData("52998224725")]
    [InlineData("11144477735")]
    public void ValidarCpf_aceita_cpf_valido(string cpf) => Assert.Null(Regras.ValidarCpf(cpf));

    [Theory]
    [InlineData("52998224724")]
    [InlineData("11111111111")]
    [InlineData("529.982.247-25")]
    [InlineData("123")]
    [InlineData(null)]
    public void ValidarCpf_recusa_cpf_invalido(string? cpf) => Assert.NotNull(Regras.ValidarCpf(cpf));

    [Theory]
    [InlineData("abc12", "Mínimo de 8 caracteres.")]
    [InlineData("abcdefgh", "Use letras e números.")]
    [InlineData("12345678", "Use letras e números.")]
    public void ValidarSenha_recusa_senha_fraca(string senha, string erro) =>
        Assert.Equal(erro, Regras.ValidarSenha(senha));

    [Fact]
    public void ValidarSenha_aceita_letras_e_numeros() => Assert.Null(Regras.ValidarSenha("bart2026"));

    [Theory]
    [InlineData(0.0)]
    [InlineData(100.0)]
    [InlineData(75.5)]
    [InlineData(null)]
    public void ValidarNota_aceita_de_0_a_100_ou_nula(double? nota) =>
        Assert.Null(Regras.ValidarNota((decimal?)nota));

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void ValidarNota_recusa_fora_do_check(double nota) =>
        Assert.NotNull(Regras.ValidarNota((decimal)nota));

    [Fact]
    public void ArredondarNota_guarda_2_casas_como_number_5_2() =>
        Assert.Equal(75.56m, Regras.ArredondarNota(75.555m));

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(null, false)]
    public void ValidarSemestre_segue_o_check_do_ddl(int? semestre, bool valido) =>
        Assert.Equal(valido, Regras.ValidarSemestre(semestre) is null);

    [Theory]
    [InlineData(2000, true)]
    [InlineData(2026, true)]
    [InlineData(1999, false)]
    public void ValidarAno_segue_o_check_do_ddl(int ano, bool valido) =>
        Assert.Equal(valido, Regras.ValidarAno(ano) is null);

    [Fact]
    public void ValidarDataPresenca_recusa_data_futura_como_o_trigger()
    {
        var hoje = new DateOnly(2026, 11, 18);
        Assert.Null(Regras.ValidarDataPresenca(hoje, hoje));
        Assert.Equal("Não é permitido registrar presença em data futura.", Regras.ValidarDataPresenca(hoje.AddDays(1), hoje));
        Assert.Equal("Data inválida.", Regras.ValidarDataPresenca(new DateOnly(1999, 12, 31), hoje));
    }

    [Fact]
    public void ValidarNomeAvaliacao_aceita_de_1_a_50_caracteres()
    {
        Assert.Null(Regras.ValidarNomeAvaliacao("Prova 1"));
        Assert.NotNull(Regras.ValidarNomeAvaliacao(" "));
        Assert.NotNull(Regras.ValidarNomeAvaliacao(new string('a', 51)));
    }

    [Fact]
    public void ValidarObservacao_aceita_ate_150_caracteres()
    {
        Assert.Null(Regras.ValidarObservacao(null));
        Assert.Null(Regras.ValidarObservacao(new string('a', 150)));
        Assert.NotNull(Regras.ValidarObservacao(new string('a', 151)));
    }

    [Theory]
    [InlineData("Falta Justificada", "FALTA_JUSTIFICADA")]
    [InlineData("História", "HISTORIA")]
    [InlineData(" historia ", "HISTORIA")]
    [InlineData("Ativo", "ATIVO")]
    public void ChaveEnum_normaliza_como_o_app(string enumerador, string chave) =>
        Assert.Equal(chave, Regras.ChaveEnum(enumerador));

    [Theory]
    [InlineData("2026-11-05", true)]
    [InlineData("05-11-2026", false)]
    [InlineData("2026-13-01", false)]
    [InlineData(null, false)]
    public void LerData_aceita_so_o_formato_do_contrato(string? texto, bool valida) =>
        Assert.Equal(valida, Regras.LerData(texto) is not null);
}
