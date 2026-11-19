using System.Net;
using BoletimPlus.Api.Contratos;

namespace BoletimPlus.Api.Tests.Integracao;

/// <summary>POST repetido com a mesma Idempotency-Key não duplica o cadastro.</summary>
public class IdempotenciaTests
{
    private static readonly object NovoProfessor = new { nome = "Dewey Largo", num_documento = "52998224725", senha = "musica2026" };

    [Fact]
    public async Task Mesma_chave_devolve_a_mesma_resposta_sem_criar_de_novo()
    {
        using var api = new ApiFactory();
        var cliente = api.ClienteComo(Usuarios.Skinner);
        var antes = api.NoBanco(db => db.Professor.Count());

        var primeira = await cliente.PostIdempotente("/professores", NovoProfessor, "chave-1");
        var segunda = await cliente.PostIdempotente("/professores", NovoProfessor, "chave-1");

        Assert.Equal(HttpStatusCode.Created, primeira.StatusCode);
        Assert.Equal(HttpStatusCode.Created, segunda.StatusCode);
        Assert.Equal((await primeira.Ler<ProfessorCriadoResponse>()).ProfessorCod, (await segunda.Ler<ProfessorCriadoResponse>()).ProfessorCod);
        Assert.Equal("true", segunda.Headers.GetValues("Idempotent-Replayed").Single());
        Assert.Equal(antes + 1, api.NoBanco(db => db.Professor.Count()));
    }

    [Fact]
    public async Task Mesma_chave_com_outro_corpo_responde_422()
    {
        using var api = new ApiFactory();
        var cliente = api.ClienteComo(Usuarios.Skinner);
        await cliente.PostIdempotente("/professores", NovoProfessor, "chave-2");

        var outro = new { nome = "Otto Mann", num_documento = "11144477735", senha = "onibus2026" };
        var resposta = await cliente.PostIdempotente("/professores", outro, "chave-2");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
    }

    [Fact]
    public async Task Sem_a_chave_responde_400()
    {
        using var api = new ApiFactory();
        var resposta = await api.ClienteComo(Usuarios.Skinner).PostAsync("/professores",
            System.Net.Http.Json.JsonContent.Create(NovoProfessor));
        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Avaliacao_repetida_com_a_mesma_chave_volta_a_mesma_avaliacao()
    {
        using var api = new ApiFactory();
        var cliente = api.ClienteComo(Usuarios.Edna);
        var corpo = new { nome = "Prova 2", codTipoNota = 1, dataAvaliacao = "2026-11-20" };

        var primeira = await (await cliente.PostIdempotente("/turmas/1/avaliacoes", corpo, "av-1")).Ler<CodResponse>();
        var segunda = await (await cliente.PostIdempotente("/turmas/1/avaliacoes", corpo, "av-1")).Ler<CodResponse>();

        Assert.Equal(primeira.Cod, segunda.Cod);
        Assert.Equal(1, api.NoBanco(db => db.Avaliacao.Count(a => a.Nome == "Prova 2")));
    }

    [Fact]
    public async Task Matricula_repetida_nao_duplica_o_aluno_na_turma()
    {
        using var api = new ApiFactory();
        var cliente = api.ClienteComo(Usuarios.Skinner);
        var corpo = System.Net.Http.Json.JsonContent.Create(new { cod_alunos = new[] { 2 } });

        Assert.Equal(HttpStatusCode.NoContent, (await cliente.PostAsync("/turmas/1/alunos", corpo)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.PostAsync("/turmas/1/alunos", corpo)).StatusCode);
        Assert.Equal(1, api.NoBanco(db => db.TurmaAluno.Count(ta => ta.CodTurma == 1 && ta.CodAluno == 2)));
    }
}
