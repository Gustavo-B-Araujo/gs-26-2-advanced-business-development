using System.Text.Encodings.Web;
using System.Text.Unicode;
using BoletimPlus.Api.Dados;
using BoletimPlus.Api.Endpoints;
using BoletimPlus.Api.Infra;
using BoletimPlus.Api.Seguranca;
using BoletimPlus.Api.Servicos;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var servicos = builder.Services;

// Banco: Oracle da disciplina de banco de dados, com o DDL de 01_ddl.sql (sem migrations).
servicos.AddDbContext<BoletimDbContext>((sp, o) =>
    o.UseOracle(sp.GetRequiredService<IConfiguration>().GetConnectionString("Oracle")));

// JWT: emitido no login e no cadastro, igual ao do Java (sub = Usuario.cod, mesma chave HS256).
servicos.AddSingleton(sp => JwtConfig.Ler(sp.GetRequiredService<IConfiguration>()));
servicos.AddSingleton<GeradorToken>();
servicos.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.Events = new JwtBearerEvents { OnTokenValidated = ClaimsDoBoletim.CarregarPapeis };
    });
servicos.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<JwtConfig>((o, jwt) => o.TokenValidationParameters = jwt.ParametrosDeValidacao());
servicos.AddAuthorizationBuilder()
    .AddPolicy(NomePapel.Diretor, p => p.RequireRole(NomePapel.Diretor))
    .AddPolicy(NomePapel.Professor, p => p.RequireRole(NomePapel.Professor))
    .AddPolicy(NomePapel.Guardiao, p => p.RequireRole(NomePapel.Guardiao));

// Requisitos da disciplina: idempotência, rate limit, health checks, Swagger.
servicos.AddDistributedMemoryCache();
servicos.AddScoped<IdempotenciaFilter>();
servicos.AddLimiteDeRequisicoes(builder.Configuration);
servicos.AddSaude();
servicos.AddSwagger();

// JSON com acento legível ("Português", não "Portugu\u00EAs").
servicos.ConfigureHttpJsonOptions(o => o.SerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All));

// Erros em ProblemDetails; corpo inválido também passa pelo tratador.
servicos.AddProblemDetails();
servicos.AddExceptionHandler<TratadorDeErros>();
servicos.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);

// App mobile (Expo) e Expo web: token no header, sem cookie, então qualquer origem.
servicos.AddCors(o => o.AddDefaultPolicy(p => p
    .AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()
    .WithExposedHeaders("Retry-After", IdempotenciaFilter.HeaderRepeticao)));

// Regras de negócio.
servicos.AddSingleton(TimeProvider.System);
servicos.AddScoped<Consultas>();
servicos.AddScoped<TiposServico>();
servicos.AddScoped<PessoasServico>();
servicos.AddScoped<EscolasServico>();
servicos.AddScoped<SalaDeAulaServico>();
servicos.AddScoped<ContaServico>();
servicos.AddScoped<AcompanhamentoServico>();

var app = builder.Build();

// Falha na subida, não na primeira requisição, se faltar configuração.
app.Services.GetRequiredService<JwtConfig>();
if (string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("Oracle")))
    throw new InvalidOperationException("Defina a connection string ConnectionStrings__Oracle.");

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(o => o.DocumentTitle = "Boletim Plus API (.NET)");
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.MapSaude();
app.MapConta();
app.MapAcompanhamento();
app.MapTipos();
app.MapPessoas();
app.MapEscolas();
app.MapSalaDeAula();
if (app.Environment.IsDevelopment()) app.MapDev();

app.Run();

/// <summary>Visível para o WebApplicationFactory dos testes de integração.</summary>
public partial class Program;
