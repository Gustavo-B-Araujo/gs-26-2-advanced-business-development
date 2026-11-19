using System.Net;
using System.Text.Json;
using BoletimPlus.Api.Contratos;

namespace BoletimPlus.Api.Tests.Integracao;

/// <summary>Dependentes do responsável e boletim do aluno (regras de acesso do Java).</summary>
public class AcompanhamentoTests
{
    [Fact]
    public async Task Responsavel_lista_os_dependentes_com_parentesco()
    {
        using var api = new ApiFactory();
        var resposta = await api.ClienteComo(Usuarios.Marge).GetAsync("/guardioes/2/dependentes");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var dependentes = await resposta.Ler<List<DependenteResponse>>();
        Assert.Equal(
            [new DependenteResponse(1, "Bart Simpson", "Mãe"), new DependenteResponse(2, "Lisa Simpson", "Mãe")],
            dependentes);
    }

    [Fact]
    public async Task Responsavel_nao_lista_dependentes_de_outro()
    {
        using var api = new ApiFactory();
        var resposta = await api.ClienteComo(Usuarios.Marge).GetAsync("/guardioes/3/dependentes");
        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        Assert.Equal("Acesso negado.", await resposta.Detalhe());
    }

    [Theory]
    [InlineData(Usuarios.Skinner)]
    [InlineData(Usuarios.Bart)]
    public async Task Quem_nao_e_responsavel_recebe_403_nos_dependentes(int usuario)
    {
        using var api = new ApiFactory();
        var resposta = await api.ClienteComo(usuario).GetAsync("/guardioes/2/dependentes");
        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task Boletim_traz_notas_media_presencas_e_frequencia_por_turma()
    {
        using var api = new ApiFactory();
        var resposta = await api.ClienteComo(Usuarios.Bart).GetAsync("/alunos/1/boletim");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var boletim = await resposta.Ler<BoletimAlunoResponse>();
        Assert.Equal(1, boletim.AlunoCod);
        Assert.Equal("Bart Simpson", boletim.Nome);
        Assert.Equal([1, 2, 3], boletim.Turmas.Select(t => t.Turma.Cod));

        var portugues = boletim.Turmas[0];
        Assert.Equal("Português", portugues.Turma.TipoCurso);
        Assert.Equal(2, portugues.Turma.TotalAlunos);
        Assert.Equal(["Prova 1", "Trabalho 1"], portugues.Notas.Select(n => n.AvaliacaoNome));
        Assert.Equal([45.0, 60.0], portugues.Notas.Select(n => n.Nota!.Value));
        Assert.Equal(52.5, portugues.Media);
        // Mais recente primeiro; só 'Presente' conta.
        Assert.Equal(["Falta Justificada", "Falta", "Presente"], portugues.Presencas.Select(p => p.Tipo));
        Assert.Equal("Atestado médico", portugues.Presencas[0].Observacao);
        Assert.Equal(new DateOnly(2026, 11, 5), portugues.Presencas[0].Data);
        Assert.Equal(1.0 / 3, portugues.Frequencia!.Value, 10);

        var matematica = boletim.Turmas[1];
        Assert.Equal(42.5, matematica.Media);
        Assert.Empty(matematica.Presencas);
        Assert.Null(matematica.Frequencia);

        var historia = boletim.Turmas[2];
        Assert.Equal(0.0, historia.Frequencia);
    }

    [Fact]
    public async Task Boletim_usa_os_nomes_json_do_contrato()
    {
        using var api = new ApiFactory();
        var corpo = await (await api.ClienteComo(Usuarios.Bart).GetAsync("/alunos/1/boletim")).Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(corpo);
        var turma = json.RootElement.GetProperty("turmas")[0];
        var nota = turma.GetProperty("notas")[0];

        Assert.Equal("2026-09-15", nota.GetProperty("dataAvaliacao").GetString());
        Assert.Equal("Prova", nota.GetProperty("tipoNota").GetString());
        Assert.Equal(1, nota.GetProperty("avaliacaoCod").GetInt32());
        Assert.Equal("2026-11-05", turma.GetProperty("presencas")[0].GetProperty("data").GetString());
        Assert.Equal("Escola Primária de Springfield", turma.GetProperty("turma").GetProperty("escolaNome").GetString());
    }

    [Fact]
    public async Task Nota_nao_lancada_vem_null_e_fica_fora_da_media()
    {
        using var api = new ApiFactory();
        api.NoBanco(db =>
        {
            db.Boletim.Remove(db.Boletim.Single(b => b.Cod == 2)); // Trabalho 1 do Bart
            return db.SaveChanges();
        });

        var boletim = await (await api.ClienteComo(Usuarios.Bart).GetAsync("/alunos/1/boletim")).Ler<BoletimAlunoResponse>();
        Assert.Null(boletim.Turmas[0].Notas[1].Nota);
        Assert.Equal(45.0, boletim.Turmas[0].Media);
    }

    [Theory]
    [InlineData(Usuarios.Bart, 1)]     // o próprio aluno
    [InlineData(Usuarios.Marge, 1)]    // responsável vinculado
    [InlineData(Usuarios.Marge, 2)]
    [InlineData(Usuarios.Edna, 1)]     // professor de uma turma dele
    [InlineData(Usuarios.Skinner, 2)]  // diretor da escola de uma turma dele
    public async Task Boletim_liberado_para_quem_acompanha_o_aluno(int usuario, int aluno)
    {
        using var api = new ApiFactory();
        var resposta = await api.ClienteComo(usuario).GetAsync($"/alunos/{aluno}/boletim");
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Theory]
    [InlineData(Usuarios.Bart, 2)]     // aluno vendo outro aluno
    [InlineData(Usuarios.Marge, 3)]    // responsável sem vínculo
    [InlineData(Usuarios.Edna, 2)]     // professor sem turma com o aluno
    [InlineData(Usuarios.Skinner, 99)] // aluno inexistente: sem acesso, como no Java
    public async Task Boletim_de_outro_aluno_responde_403(int usuario, int aluno)
    {
        using var api = new ApiFactory();
        var resposta = await api.ClienteComo(usuario).GetAsync($"/alunos/{aluno}/boletim");
        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        Assert.Equal("Você não tem acesso a este aluno.", await resposta.Detalhe());
    }

    [Fact]
    public async Task Boletim_sem_token_responde_401()
    {
        using var api = new ApiFactory();
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.CreateClient().GetAsync("/alunos/1/boletim")).StatusCode);
    }

    [Fact]
    public async Task Rotas_de_aluno_do_diretor_continuam_restritas()
    {
        using var api = new ApiFactory();
        Assert.Equal(HttpStatusCode.Forbidden, (await api.ClienteComo(Usuarios.Bart).GetAsync("/alunos")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.ClienteComo(Usuarios.Skinner).GetAsync("/alunos")).StatusCode);
    }
}
