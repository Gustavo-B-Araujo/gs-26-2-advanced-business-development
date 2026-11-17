using EscolaPlus.Frequencia.Api.Auth;
using EscolaPlus.Frequencia.Api.Endpoints;
using EscolaPlus.Frequencia.Api.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// 1. Banco de Dados (Oracle)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseOracle(connectionString));

// 2. Autenticação e Autorização (JWT)
builder.Services.AddJwtAuthentication(builder.Configuration);

// 3. Rate Limiting
var maxRequests = builder.Configuration.GetValue<int>("RateLimiting:MaxRequests", 10);
var windowSeconds = builder.Configuration.GetValue<int>("RateLimiting:WindowInSeconds", 60);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("porUsuario", opt =>
    {
        opt.PermitLimit = maxRequests;
        opt.Window = TimeSpan.FromSeconds(windowSeconds);
        opt.QueueLimit = 0;
    });
});

// 4. HealthChecks (API e Banco)
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("OracleDatabase");

// 5. Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Insira o token JWT desta forma: {seu token}"
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// 6. Mapeamento de Rotas
app.MapGet("/", () => "API Escola+ rodando!");
app.MapFrequenciaEndpoints();
app.MapRelatorioEndpoints();

// 7. Rotas de HealthCheck
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false // Apenas verifica se o processo está vivo
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready") || check.Name == "OracleDatabase" 
});

app.Run();

public partial class Program { }