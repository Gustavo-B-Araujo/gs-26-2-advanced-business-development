using BoletimPlus.Api.Dominio;
using BoletimPlus.Api.Seguranca;
using Microsoft.Extensions.Configuration;

namespace BoletimPlus.Api.Tests.Unidade;

/// <summary>Regras da conta e do boletim (as mesmas do Java) e os padrões do JWT emitido.</summary>
public class AcompanhamentoRegrasTests
{
    [Fact]
    public void Media_e_a_media_simples_das_notas() => Assert.Equal(52.5, Regras.Media([45, 60]));

    [Fact]
    public void Media_sem_notas_e_null() => Assert.Null(Regras.Media([]));

    [Fact]
    public void Frequencia_conta_so_presente_sem_acento_e_maiuscula() =>
        Assert.Equal(0.5, Regras.Frequencia(["Presente", "Falta Justificada", "PRESENTE", "Falta"]));

    [Fact]
    public void Frequencia_sem_chamadas_e_null() => Assert.Null(Regras.Frequencia([]));

    [Theory]
    [InlineData("12345678900", null)] // CPF da DML: só o formato, como no Java
    [InlineData("1234567890", "O CPF tem 11 dígitos, sem máscara.")]
    [InlineData("123.456.789", "O CPF tem 11 dígitos, sem máscara.")]
    [InlineData(" ", "Informe o CPF.")]
    [InlineData(null, "Informe o CPF.")]
    public void ValidarCpfFormato_exige_11_digitos(string? cpf, string? erro) =>
        Assert.Equal(erro, Regras.ValidarCpfFormato(cpf));

    [Theory]
    [InlineData(null, "Informe a senha.")]
    [InlineData("abc", "Mínimo de 8 caracteres.")]
    [InlineData("bart2026", null)]
    public void ValidarSenhaNova_exige_senha_e_a_regra_de_forca(string? senha, string? erro) =>
        Assert.Equal(erro, Regras.ValidarSenhaNova(senha, "Informe a senha."));

    [Fact]
    public void Jwt_sem_issuer_configurado_emite_o_padrao_do_java_e_valida_8_horas()
    {
        var config = JwtConfig.Ler(Config(("JWT_SECRET", new string('x', 32))));
        Assert.Equal("boletim-plus", config.EmissorDoToken);
        Assert.Equal(480, config.ExpiracaoMinutos);
        Assert.False(config.ParametrosDeValidacao().ValidateIssuer);
    }

    [Fact]
    public void Jwt_com_issuer_configurado_emite_e_valida_o_mesmo()
    {
        var config = JwtConfig.Ler(Config(("JWT_SECRET", new string('x', 32)), ("JWT_ISSUER", "outro"), ("JWT_EXPIRATION_MINUTES", "30")));
        Assert.Equal("outro", config.EmissorDoToken);
        Assert.Equal(30, config.ExpiracaoMinutos);
        Assert.True(config.ParametrosDeValidacao().ValidateIssuer);
    }

    private static IConfiguration Config(params (string Chave, string Valor)[] valores) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(valores.Select(v => new KeyValuePair<string, string?>(v.Chave, v.Valor)))
            .Build();
}
