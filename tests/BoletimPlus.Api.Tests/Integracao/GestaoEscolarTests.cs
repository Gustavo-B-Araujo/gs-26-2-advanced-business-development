using System.Net;
using System.Net.Http.Json;
using BoletimPlus.Api.Contratos;

namespace BoletimPlus.Api.Tests.Integracao;

/// <summary>Cursos, professores, alunos, turmas e matrícula (diretor).</summary>
public class GestaoEscolarTests
{
    [Fact]
    public async Task Diretor_cadastra_curso_e_nome_repetido_sem_acento_responde_409()
    {
        using var api = new ApiFactory();
        var cliente = api.ClienteComo(Usuarios.Skinner);

        var criado = await cliente.PostAsJsonAsync("/tipos/curso", new { enumerador = "Ciências" });
        var repetido = await cliente.PostAsJsonAsync("/tipos/curso", new { enumerador = "historia" });

        Assert.Equal(HttpStatusCode.Created, criado.StatusCode);
        Assert.Equal("Ciências", (await criado.Ler<EnumeradorResponse>()).Enumerador);
        Assert.Equal(HttpStatusCode.Conflict, repetido.StatusCode);
        Assert.Equal("Este curso já existe.", await repetido.Detalhe());
    }

    [Fact]
    public async Task Professor_cadastrado_aparece_na_rede_sem_materias()
    {
        using var api = new ApiFactory();
        var cliente = api.ClienteComo(Usuarios.Skinner);

        var resposta = await cliente.PostIdempotente("/professores",
            new { nome = "Dewey Largo", num_documento = "52998224725", senha = "musica2026" });
        var cod = (await resposta.Ler<ProfessorCriadoResponse>()).ProfessorCod;
        var rede = await (await cliente.GetAsync("/professores")).Ler<List<ProfessorResumo>>();

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        Assert.Equal($"/professores/{cod}", resposta.Headers.Location!.OriginalString);
        Assert.Empty(rede.Single(p => p.ProfessorCod == cod).Materias);
        Assert.Equal(["História"], rede.Single(p => p.Nome == "Elizabeth Hoover").Materias);
        Assert.True(api.NoBanco(db => db.Usuario.Any(u => u.Pessoa.NumDocumento == "52998224725" && u.SenhaHash.StartsWith("$2"))));
    }

    [Theory]
    [InlineData("Dewey", "52998224725", "musica2026", "Informe nome e sobrenome.")]
    [InlineData("Dewey Largo", "52998224724", "musica2026", "CPF inválido.")]
    [InlineData("Dewey Largo", "52998224725", "curta", "Mínimo de 8 caracteres.")]
    public async Task Cadastro_de_professor_invalido_responde_400(string nome, string cpf, string senha, string erro)
    {
        using var api = new ApiFactory();
        var resposta = await api.ClienteComo(Usuarios.Skinner)
            .PostIdempotente("/professores", new { nome, num_documento = cpf, senha });
        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal(erro, await resposta.Detalhe());
    }

    [Fact]
    public async Task Cpf_repetido_responde_409()
    {
        using var api = new ApiFactory();
        var cliente = api.ClienteComo(Usuarios.Skinner);
        var corpo = new { nome = "Dewey Largo", num_documento = "52998224725", senha = "musica2026" };

        await cliente.PostIdempotente("/professores", corpo);
        var repetido = await cliente.PostIdempotente("/alunos", corpo);

        Assert.Equal(HttpStatusCode.Conflict, repetido.StatusCode);
    }

    [Fact]
    public async Task Aluno_sem_senha_nao_ganha_usuario_e_aparece_sem_turma()
    {
        using var api = new ApiFactory();
        var cliente = api.ClienteComo(Usuarios.Skinner);

        var resposta = await cliente.PostIdempotente("/alunos",
            new { nome = "Nelson Muntz", num_documento = "11144477735", senha = (string?)null });
        var cod = (await resposta.Ler<AlunoCriadoResponse>()).AlunoCod;
        var alunos = await (await cliente.GetAsync("/alunos")).Ler<List<AlunoResumo>>();

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        Assert.Equal(0, alunos.Single(a => a.AlunoCod == cod).TotalTurmas);
        Assert.False(api.NoBanco(db => db.Usuario.Any(u => u.Pessoa.NumDocumento == "11144477735")));
    }

    [Fact]
    public async Task Diretor_renomeia_aluno_e_codigo_inexistente_responde_404()
    {
        using var api = new ApiFactory();
        var cliente = api.ClienteComo(Usuarios.Skinner);

        var ok = await cliente.PutAsJsonAsync("/alunos/1", new { nome = "Bartholomew Simpson" });
        var inexistente = await cliente.PutAsJsonAsync("/alunos/99", new { nome = "Ninguém Aqui" });

        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
        Assert.Equal("Bartholomew Simpson", api.NoBanco(db => db.Pessoa.Single(p => p.Cod == 5).Nome));
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
    }

    [Fact]
    public async Task Diretor_cria_turma_e_ela_aparece_na_escola()
    {
        using var api = new ApiFactory();
        var cliente = api.ClienteComo(Usuarios.Skinner);

        var resposta = await cliente.PostIdempotente("/escolas/1/turmas",
            new { cod_tipo_curso = 3, cod_professor = 1, semestre_referencia = 1, ano_referencia = 2027 });
        var cod = (await resposta.Ler<CodResponse>()).Cod;
        var turmas = await (await cliente.GetAsync("/escolas/1/turmas")).Ler<List<TurmaResumo>>();

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        Assert.Equal(cod, turmas[0].Cod);
        Assert.Equal("Edna Krabappel", turmas[0].ProfessorNome);
    }

    [Theory]
    [InlineData(1, 1, 3, 2026, HttpStatusCode.BadRequest)]
    [InlineData(1, 1, 1, 1999, HttpStatusCode.BadRequest)]
    [InlineData(99, 1, 1, 2026, HttpStatusCode.BadRequest)]
    [InlineData(1, 99, 1, 2026, HttpStatusCode.NotFound)]
    public async Task Turma_invalida_e_recusada(int curso, int professor, int semestre, int ano, HttpStatusCode esperado)
    {
        using var api = new ApiFactory();
        var resposta = await api.ClienteComo(Usuarios.Skinner).PostIdempotente("/escolas/1/turmas",
            new { cod_tipo_curso = curso, cod_professor = professor, semestre_referencia = semestre, ano_referencia = ano });
        Assert.Equal(esperado, resposta.StatusCode);
    }

    [Fact]
    public async Task Matricula_vazia_responde_400_e_aluno_inexistente_404()
    {
        using var api = new ApiFactory();
        var cliente = api.ClienteComo(Usuarios.Skinner);

        var vazia = await cliente.PostAsJsonAsync("/turmas/1/alunos", new { cod_alunos = Array.Empty<int>() });
        var inexistente = await cliente.PostAsJsonAsync("/turmas/1/alunos", new { cod_alunos = new[] { 99 } });

        Assert.Equal(HttpStatusCode.BadRequest, vazia.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
    }

    [Fact]
    public async Task Alunos_da_escola_aparecem_uma_vez_com_as_turmas()
    {
        using var api = new ApiFactory();
        var alunos = await (await api.ClienteComo(Usuarios.Skinner).GetAsync("/escolas/1/alunos")).Ler<List<AlunoDaEscola>>();

        var bart = alunos.Single(a => a.Nome == "Bart Simpson");
        Assert.Equal(3, bart.Turmas.Count);
        Assert.Equal(3, bart.TotalTurmas);
        Assert.Equal(["Bart Simpson", "Lisa Simpson", "Milhouse Van Houten"], alunos.Select(a => a.Nome));
    }

    [Fact]
    public async Task Professores_da_escola_trazem_as_turmas_deles()
    {
        using var api = new ApiFactory();
        var professores = await (await api.ClienteComo(Usuarios.Skinner).GetAsync("/escolas/1/professores")).Ler<List<ProfessorDaEscola>>();

        var edna = professores.Single(p => p.Nome == "Edna Krabappel");
        Assert.Equal(["Matemática", "Português"], edna.Materias);
        Assert.Equal(2, edna.Turmas.Count);
    }
}
