using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BoletimPlus.Api.Contratos;
using Microsoft.IdentityModel.JsonWebTokens;

namespace BoletimPlus.Api.Tests.Integracao;

/// <summary>Conta: login, cadastro, senha, nome e logout (mesmo contrato do Java).</summary>
public class ContaTests
{
    private const string CpfSkinner = "12345678900";
    private const string CpfEdna = "12345678901";
    private const string CpfHomer = "12345678902";
    private const string CpfMarge = "12345678903";
    private const string CpfBart = "12345678904";

    [Fact]
    public async Task Login_devolve_sessao_com_token_pessoa_e_papeis()
    {
        using var api = new ApiFactory();
        var resposta = await Entrar(api.CreateClient(), CpfSkinner, Semente.Senha);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var corpo = await resposta.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(corpo);
        var raiz = json.RootElement;
        Assert.Equal(Usuarios.Skinner, raiz.GetProperty("usuarioCod").GetInt32());
        Assert.Equal("Seymour Skinner", raiz.GetProperty("pessoa").GetProperty("nome").GetString());
        Assert.Equal(CpfSkinner, raiz.GetProperty("pessoa").GetProperty("num_documento").GetString());
        Assert.True(raiz.GetProperty("pessoa").TryGetProperty("data_criacao", out _));
        // Papel que a pessoa não tem fica fora do JSON, como no Java.
        var papeis = raiz.GetProperty("papeis");
        Assert.Equal(1, papeis.GetProperty("diretorCod").GetInt32());
        Assert.False(papeis.TryGetProperty("professorCod", out _));
        Assert.False(papeis.TryGetProperty("guardiaoCod", out _));
        Assert.False(papeis.TryGetProperty("alunoCod", out _));
    }

    [Fact]
    public async Task Token_do_login_e_igual_ao_do_java()
    {
        using var api = new ApiFactory();
        var sessao = await (await Entrar(api.CreateClient(), CpfBart, Semente.Senha)).Ler<SessaoResponse>();
        var token = new JsonWebToken(sessao.Token);

        Assert.Equal("HS256", token.Alg);
        Assert.Equal(Usuarios.Bart.ToString(), token.Subject);
        Assert.Equal("boletim-plus", token.Issuer);
        Assert.InRange((token.ValidTo - token.IssuedAt).TotalHours, 7.9, 8.1);
    }

    [Fact]
    public async Task Token_do_login_vale_nas_rotas_existentes()
    {
        using var api = new ApiFactory();
        var cliente = api.CreateClient();
        var sessao = await (await Entrar(cliente, CpfEdna, Semente.Senha)).Ler<SessaoResponse>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessao.Token);

        Assert.Equal(1, sessao.Papeis.ProfessorCod);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/professores/1/turmas")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/turmas/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/professores")).StatusCode);
    }

    [Theory]
    [InlineData(CpfSkinner, "senha-errada1")]
    [InlineData("98765432100", "senha123")]
    public async Task Login_com_cpf_ou_senha_errados_responde_401(string cpf, string senha)
    {
        using var api = new ApiFactory();
        var resposta = await Entrar(api.CreateClient(), cpf, senha);
        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Equal("CPF ou senha inválidos.", await resposta.Detalhe());
    }

    [Fact]
    public async Task Login_de_usuario_bloqueado_responde_403()
    {
        using var api = new ApiFactory();
        var resposta = await Entrar(api.CreateClient(), CpfHomer, Semente.Senha);
        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        Assert.Equal("Usuário bloqueado. Procure a secretaria.", await resposta.Detalhe());
    }

    [Theory]
    [InlineData("123.456.789-00", "senha123", "O CPF tem 11 dígitos, sem máscara.")]
    [InlineData("", "senha123", "Informe o CPF.")]
    [InlineData(CpfSkinner, "", "Informe a senha.")]
    public async Task Login_sem_cpf_ou_senha_responde_400(string cpf, string senha, string erro)
    {
        using var api = new ApiFactory();
        var resposta = await Entrar(api.CreateClient(), cpf, senha);
        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal(erro, await resposta.Detalhe());
    }

    [Fact]
    public async Task Login_confere_hash_bcrypt_2a_do_spring()
    {
        using var api = new ApiFactory();
        // Hash no formato do BCryptPasswordEncoder do Spring ($2a$, custo 10).
        var hashDoSpring = BCrypt.Net.BCrypt.HashPassword("123456", BCrypt.Net.BCrypt.GenerateSalt(10, 'a'));
        Assert.StartsWith("$2a$10$", hashDoSpring);
        api.NoBanco(db =>
        {
            db.Usuario.Single(u => u.Cod == Usuarios.Marge).SenhaHash = hashDoSpring;
            return db.SaveChanges();
        });

        var resposta = await Entrar(api.CreateClient(), CpfMarge, "123456");
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task Cadastro_cria_responsavel_ativo_e_ja_entra()
    {
        using var api = new ApiFactory();
        var cliente = api.CreateClient();
        var resposta = await cliente.PostAsJsonAsync("/auth/cadastro",
            new { nome = "Ned Flanders", num_documento = "52998224725", senha = "okilydokily1" });

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var sessao = await resposta.Ler<SessaoResponse>();
        Assert.Equal("Ned Flanders", sessao.Pessoa.Nome);
        Assert.NotNull(sessao.Papeis.GuardiaoCod);
        Assert.Null(sessao.Papeis.DiretorCod);
        Assert.True(api.NoBanco(db => db.Usuario.Any(u =>
            u.Cod == sessao.UsuarioCod && u.CodStatus == 1 && u.SenhaHash.StartsWith("$2a$10$"))));

        // O token do cadastro já vale; o responsável novo ainda não tem dependentes.
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessao.Token);
        var dependentes = await cliente.GetAsync($"/guardioes/{sessao.Papeis.GuardiaoCod}/dependentes");
        Assert.Equal(HttpStatusCode.OK, dependentes.StatusCode);
        Assert.Empty(await dependentes.Ler<List<DependenteResponse>>());

        // E entra de novo com a senha cadastrada.
        Assert.Equal(HttpStatusCode.OK, (await Entrar(api.CreateClient(), "52998224725", "okilydokily1")).StatusCode);
    }

    [Fact]
    public async Task Cadastro_com_cpf_repetido_responde_409()
    {
        using var api = new ApiFactory();
        var resposta = await api.CreateClient().PostAsJsonAsync("/auth/cadastro",
            new { nome = "Bart Simpson", num_documento = CpfBart, senha = "skate2026" });
        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
        Assert.Equal("Este CPF já tem cadastro. Entre com sua senha.", await resposta.Detalhe());
    }

    [Theory]
    [InlineData("Ned", "52998224725", "okilydokily1", "Informe nome e sobrenome.")]
    [InlineData("Ned Flanders", "5299822472", "okilydokily1", "O CPF tem 11 dígitos, sem máscara.")]
    [InlineData("Ned Flanders", "52998224725", "curta1", "Mínimo de 8 caracteres.")]
    [InlineData("Ned Flanders", "52998224725", "somenteletras", "Use letras e números.")]
    public async Task Cadastro_invalido_responde_400(string nome, string cpf, string senha, string erro)
    {
        using var api = new ApiFactory();
        var resposta = await api.CreateClient().PostAsJsonAsync("/auth/cadastro", new { nome, num_documento = cpf, senha });
        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal(erro, await resposta.Detalhe());
    }

    [Fact]
    public async Task Alterar_senha_responde_204_e_a_nova_senha_entra()
    {
        using var api = new ApiFactory();
        var resposta = await api.ClienteComo(Usuarios.Bart).PutAsJsonAsync("/auth/senha",
            new { senha_atual = Semente.Senha, nova_senha = "skate2026" });

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Entrar(api.CreateClient(), CpfBart, Semente.Senha)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Entrar(api.CreateClient(), CpfBart, "skate2026")).StatusCode);
    }

    [Theory]
    [InlineData("errada123", "skate2026", "A senha atual não confere.")]
    [InlineData(Semente.Senha, Semente.Senha, "A nova senha precisa ser diferente da atual.")]
    [InlineData(Semente.Senha, "skate", "Mínimo de 8 caracteres.")]
    [InlineData(Semente.Senha, "skateboard", "Use letras e números.")]
    [InlineData("", "skate2026", "Informe a senha atual.")]
    public async Task Alterar_senha_invalida_responde_400(string atual, string nova, string erro)
    {
        using var api = new ApiFactory();
        var resposta = await api.ClienteComo(Usuarios.Bart).PutAsJsonAsync("/auth/senha",
            new { senha_atual = atual, nova_senha = nova });
        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal(erro, await resposta.Detalhe());
    }

    [Theory]
    [InlineData(Usuarios.Marge, "Marjorie Simpson")]
    [InlineData(Usuarios.Skinner, "Seymour Agnes Skinner")]
    public async Task Diretor_e_responsavel_alteram_o_proprio_nome(int usuario, string nome)
    {
        using var api = new ApiFactory();
        var resposta = await api.ClienteComo(usuario).PutAsJsonAsync("/auth/nome", new { nome = $"  {nome} " });

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var pessoa = await resposta.Ler<PessoaResponse>();
        Assert.Equal(nome, pessoa.Nome);
        Assert.Equal(nome, api.NoBanco(db => db.Pessoa.Single(p => p.Cod == pessoa.Cod).Nome));
    }

    [Theory]
    [InlineData(Usuarios.Edna)]
    [InlineData(Usuarios.Bart)]
    public async Task Professor_e_aluno_nao_alteram_o_proprio_nome(int usuario)
    {
        using var api = new ApiFactory();
        var resposta = await api.ClienteComo(usuario).PutAsJsonAsync("/auth/nome", new { nome = "Outro Nome" });
        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        Assert.Equal("Só diretor e responsável alteram o próprio nome.", await resposta.Detalhe());
    }

    [Fact]
    public async Task Nome_sem_sobrenome_responde_400()
    {
        using var api = new ApiFactory();
        var resposta = await api.ClienteComo(Usuarios.Marge).PutAsJsonAsync("/auth/nome", new { nome = "Marge" });
        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal("Informe nome e sobrenome.", await resposta.Detalhe());
    }

    [Fact]
    public async Task Logout_responde_204_e_exige_token()
    {
        using var api = new ApiFactory();
        Assert.Equal(HttpStatusCode.NoContent, (await api.ClienteComo(Usuarios.Bart).PostAsync("/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.CreateClient().PostAsync("/auth/logout", null)).StatusCode);
    }

    [Fact]
    public async Task Swagger_documenta_as_rotas_de_conta()
    {
        using var api = new ApiFactory();
        var json = await api.CreateClient().GetStringAsync("/swagger/v1/swagger.json");
        Assert.Contains("/auth/login", json);
        Assert.Contains("/auth/cadastro", json);
        Assert.Contains("/alunos/{cod}/boletim", json);
        Assert.DoesNotContain("/analise", json);
    }

    private static Task<HttpResponseMessage> Entrar(HttpClient cliente, string cpf, string senha) =>
        cliente.PostAsJsonAsync("/auth/login", new { num_documento = cpf, senha });
}
