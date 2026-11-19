using BoletimPlus.Api.Dados;
using Microsoft.EntityFrameworkCore;

namespace BoletimPlus.Api.Tests.Unidade;

/// <summary>O modelo do EF Core precisa bater com os nomes do Oracle (01_ddl.sql sem aspas = maiúsculas).</summary>
public class MapeamentoOracleTests
{
    private static readonly BoletimDbContext Db = new(
        new DbContextOptionsBuilder<BoletimDbContext>().UseOracle("Data Source=nao-conecta").Options);

    [Theory]
    [InlineData("CodTurmaAluno", "COD_TURMA_ALUNO")]
    [InlineData("SemestreReferencia", "SEMESTRE_REFERENCIA")]
    [InlineData("Cod", "COD")]
    public void ParaSnakeMaiusculo_converte_propriedade_em_coluna(string propriedade, string coluna) =>
        Assert.Equal(coluna, BoletimDbContext.ParaSnakeMaiusculo(propriedade));

    [Theory]
    [InlineData(typeof(Turma), "TURMA")]
    [InlineData(typeof(TurmaAluno), "TURMAALUNO")]
    [InlineData(typeof(ListaPresenca), "LISTAPRESENCA")]
    [InlineData(typeof(TipoCurso), "TIPOCURSO")]
    public void Tabela_tem_o_nome_do_ddl(Type entidade, string tabela) =>
        Assert.Equal(tabela, Db.Model.FindEntityType(entidade)!.GetTableName());

    [Theory]
    [InlineData(typeof(Turma), nameof(Turma.CodTipoCurso), "COD_TIPO_CURSO")]
    [InlineData(typeof(Usuario), nameof(Usuario.SenhaHash), "SENHA_HASH")]
    [InlineData(typeof(Boletim), nameof(Boletim.CodTurmaAluno), "COD_TURMA_ALUNO")]
    [InlineData(typeof(TipoNota), nameof(TipoNota.Valor), "ENUMERADOR")]
    [InlineData(typeof(Pessoa), nameof(Pessoa.NumDocumento), "NUM_DOCUMENTO")]
    public void Coluna_tem_o_nome_do_ddl(Type entidade, string propriedade, string coluna) =>
        Assert.Equal(coluna, Db.Model.FindEntityType(entidade)!.FindProperty(propriedade)!.GetColumnName());

    [Fact]
    public void Nota_tem_a_precisao_number_5_2()
    {
        var nota = Db.Model.FindEntityType(typeof(Boletim))!.FindProperty(nameof(Boletim.Nota))!;
        Assert.Equal(5, nota.GetPrecision());
        Assert.Equal(2, nota.GetScale());
    }
}
