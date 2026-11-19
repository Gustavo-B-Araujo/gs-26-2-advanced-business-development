using System.Net;
using System.Net.Http.Json;
using BoletimPlus.Api.Contratos;

namespace BoletimPlus.Api.Tests.Integracao;

/// <summary>Avaliações, notas e chamada (professor da turma).</summary>
public class SalaDeAulaTests
{
    [Fact]
    public async Task Turma_traz_alunos_e_avaliacoes_em_ordem()
    {
        using var api = new ApiFactory();
        var turma = await (await api.ClienteComo(Usuarios.Edna).GetAsync("/turmas/1")).Ler<TurmaDetalhe>();

        Assert.Equal("Português", turma.TipoCurso);
        Assert.Equal(["Bart Simpson", "Milhouse Van Houten"], turma.Alunos.Select(a => a.Nome));
        Assert.Equal(["Prova 1", "Trabalho 1"], turma.Avaliacoes.Select(a => a.Nome));
        Assert.Equal(new DateOnly(2026, 9, 15), turma.Avaliacoes[0].DataAvaliacao);
    }

    [Fact]
    public async Task Professor_cria_edita_e_exclui_avaliacao()
    {
        using var api = new ApiFactory();
        var cliente = api.ClienteComo(Usuarios.Edna);

        var cod = (await (await cliente.PostIdempotente("/turmas/1/avaliacoes",
            new { nome = "Prova 2", codTipoNota = 1, dataAvaliacao = "2026-11-20" })).Ler<CodResponse>()).Cod;
        var editada = await cliente.PutAsJsonAsync($"/avaliacoes/{cod}", new { nome = "Prova 2 (recuperação)", codTipoNota = 2, dataAvaliacao = (string?)null });
        var lida = await (await cliente.GetAsync($"/avaliacoes/{cod}")).Ler<AvaliacaoDetalhe>();
        var excluida = await cliente.DeleteAsync($"/avaliacoes/{cod}");

        Assert.Equal(HttpStatusCode.NoContent, editada.StatusCode);
        Assert.Equal("Prova 2 (recuperação)", lida.Nome);
        Assert.Equal("Trabalho", lida.TipoNota);
        Assert.Null(lida.DataAvaliacao);
        Assert.Equal(HttpStatusCode.NoContent, excluida.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync($"/avaliacoes/{cod}")).StatusCode);
    }

    [Fact]
    public async Task Excluir_avaliacao_apaga_as_notas_antes()
    {
        using var api = new ApiFactory();
        var resposta = await api.ClienteComo(Usuarios.Edna).DeleteAsync("/avaliacoes/1");

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        Assert.False(api.NoBanco(db => db.Boletim.Any(b => b.CodAvaliacao == 1)));
    }

    [Fact]
    public async Task Nome_de_avaliacao_repetido_na_turma_responde_409()
    {
        using var api = new ApiFactory();
        var resposta = await api.ClienteComo(Usuarios.Edna).PostIdempotente("/turmas/1/avaliacoes",
            new { nome = "Prova 1", codTipoNota = 1, dataAvaliacao = (string?)null });
        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
    }

    [Theory]
    [InlineData(Usuarios.Skinner)]
    [InlineData(Usuarios.Bart)]
    public async Task So_o_professor_da_turma_cria_avaliacao(int usuario)
    {
        using var api = new ApiFactory();
        var resposta = await api.ClienteComo(usuario).PostIdempotente("/turmas/1/avaliacoes",
            new { nome = "Prova X", codTipoNota = 1, dataAvaliacao = (string?)null });
        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task Professor_de_outra_turma_recebe_403()
    {
        using var api = new ApiFactory();
        var resposta = await api.ClienteComo(Usuarios.Edna).PostIdempotente("/turmas/3/avaliacoes",
            new { nome = "Prova X", codTipoNota = 1, dataAvaliacao = (string?)null });
        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task Notas_fazem_upsert_e_nota_nula_apaga()
    {
        using var api = new ApiFactory();
        var cliente = api.ClienteComo(Usuarios.Edna);

        var resposta = await cliente.PutAsJsonAsync("/avaliacoes/1/notas", new
        {
            notas = new object[] { new { turmaAlunoCod = 1, nota = 88.456 }, new { turmaAlunoCod = 2, nota = (decimal?)null } },
        });
        var avaliacao = await (await cliente.GetAsync("/avaliacoes/1")).Ler<AvaliacaoDetalhe>();

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        Assert.Equal(88.46m, avaliacao.Notas.Single(n => n.TurmaAlunoCod == 1).Nota);
        Assert.Null(avaliacao.Notas.Single(n => n.TurmaAlunoCod == 2).Nota);
        Assert.Equal(1, avaliacao.NotasLancadas);
    }

    [Theory]
    [InlineData(1, 100.5, "A nota precisa estar entre 0 e 100.")]
    [InlineData(5, 50, "Aluno fora da turma.")]
    public async Task Nota_invalida_responde_400(int turmaAluno, double nota, string erro)
    {
        using var api = new ApiFactory();
        var resposta = await api.ClienteComo(Usuarios.Edna).PutAsJsonAsync("/avaliacoes/1/notas",
            new { notas = new[] { new { turmaAlunoCod = turmaAluno, nota } } });
        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal(erro, await resposta.Detalhe());
    }

    [Fact]
    public async Task Chamada_do_dia_traz_a_presenca_lancada()
    {
        using var api = new ApiFactory();
        var itens = await (await api.ClienteComo(Usuarios.Edna).GetAsync("/turmas/1/chamada?data=2026-11-05")).Ler<List<ItemChamada>>();

        var bart = itens.Single(i => i.AlunoNome == "Bart Simpson");
        Assert.Equal(3, bart.CodTipoPresenca);
        Assert.Equal("Atestado médico", bart.Observacao);
        Assert.Null(itens.Single(i => i.AlunoNome == "Milhouse Van Houten").CodTipoPresenca);
    }

    [Fact]
    public async Task Salvar_chamada_faz_upsert_por_aluno_e_data()
    {
        using var api = new ApiFactory();
        var cliente = api.ClienteComo(Usuarios.Edna);
        var corpo = new { itens = new[] { new { turmaAlunoCod = 1, codTipoPresenca = 1, observacao = (string?)null }, new { turmaAlunoCod = 2, codTipoPresenca = 2, observacao = (string?)" Chegou atrasado " } } };

        var primeira = await cliente.PutAsJsonAsync("/turmas/1/chamada?data=2026-11-04", corpo);
        var segunda = await cliente.PutAsJsonAsync("/turmas/1/chamada?data=2026-11-04", corpo);
        var itens = await (await cliente.GetAsync("/turmas/1/chamada?data=2026-11-04")).Ler<List<ItemChamada>>();

        Assert.Equal(HttpStatusCode.NoContent, primeira.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, segunda.StatusCode);
        Assert.Equal(1, itens.Single(i => i.TurmaAlunoCod == 1).CodTipoPresenca);
        Assert.Equal("Chegou atrasado", itens.Single(i => i.TurmaAlunoCod == 2).Observacao);
        Assert.Equal(2, api.NoBanco(db => db.ListaPresenca.Count(l => l.DataPresenca == new DateTime(2026, 11, 4))));
    }

    [Fact]
    public async Task Chamada_em_data_futura_responde_400()
    {
        using var api = new ApiFactory();
        var amanha = DateOnly.FromDateTime(DateTime.Now.AddDays(1)).ToString("yyyy-MM-dd");
        var resposta = await api.ClienteComo(Usuarios.Edna).PutAsJsonAsync($"/turmas/1/chamada?data={amanha}",
            new { itens = new[] { new { turmaAlunoCod = 1, codTipoPresenca = 1, observacao = (string?)null } } });
        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal("Não é permitido registrar presença em data futura.", await resposta.Detalhe());
    }

    [Fact]
    public async Task Data_em_formato_errado_responde_400()
    {
        using var api = new ApiFactory();
        var resposta = await api.ClienteComo(Usuarios.Edna).GetAsync("/turmas/1/chamada?data=05-11-2026");
        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }
}
