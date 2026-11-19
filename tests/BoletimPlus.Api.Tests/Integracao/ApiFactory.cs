using System.Net.Http.Headers;
using BoletimPlus.Api.Dados;
using BoletimPlus.Api.Seguranca;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace BoletimPlus.Api.Tests.Integracao;

/// <summary>
/// Sobe a API inteira em memória (pipeline real: JWT, rate limit, idempotência, endpoints).
/// Troca só o Oracle por um SQLite em memória com o mesmo modelo e os dados de 02_dml.sql.
/// Cada teste cria a sua fábrica: banco limpo, sem um teste afetar o outro.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string Segredo = "segredo-dos-testes-com-pelo-menos-32-caracteres";

    private readonly SqliteConnection _conexao = new("DataSource=:memory:");
    private readonly int _limitePorJanela;
    private readonly int _limiteEscrita;

    public ApiFactory(int limitePorJanela = 10_000, int limiteEscrita = 10_000)
    {
        _limitePorJanela = limitePorJanela;
        _limiteEscrita = limiteEscrita;
        _conexao.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("JWT_SECRET", Segredo);
        builder.UseSetting("ConnectionStrings:Oracle", "Data Source=substituido-pelo-sqlite");
        builder.UseSetting("RateLimit:LimitePorJanela", _limitePorJanela.ToString());
        builder.UseSetting("RateLimit:LimiteEscrita", _limiteEscrita.ToString());

        builder.ConfigureServices(servicos =>
        {
            servicos.RemoveAll<DbContextOptions<BoletimDbContext>>();
            servicos.AddDbContext<BoletimDbContext>(o => o.UseSqlite(_conexao));
        });
    }

    /// <summary>Depois que a API sobe: cria as tabelas pelo modelo e carrega a semente.</summary>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var escopo = host.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<BoletimDbContext>();
        db.Database.EnsureCreated();
        Semente.Popular(db);
        return host;
    }

    /// <summary>Cliente HTTP logado como o Usuario.cod informado (token igual ao do Java).</summary>
    public HttpClient ClienteComo(int usuarioCod)
    {
        var cliente = CreateClient();
        var (token, _) = Services.GetRequiredService<GeradorToken>().Gerar(usuarioCod);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return cliente;
    }

    public T NoBanco<T>(Func<BoletimDbContext, T> consulta)
    {
        using var escopo = Services.CreateScope();
        return consulta(escopo.ServiceProvider.GetRequiredService<BoletimDbContext>());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _conexao.Dispose();
    }
}

/// <summary>Usuários da 02_dml.sql (Usuario.cod).</summary>
public static class Usuarios
{
    public const int Skinner = 1;  // Diretor 1, Escola 1
    public const int Edna = 2;     // Professor 1 (turmas 1 e 2)
    public const int Homer = 3;    // Guardião 1, BLOQUEADO
    public const int Marge = 4;    // Guardiã 2 (Bart e Lisa)
    public const int Bart = 5;     // Aluno 1
}
