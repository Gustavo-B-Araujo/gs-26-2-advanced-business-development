using EscolaPlus.Frequencia.Api.Endpoints;
using EscolaPlus.Frequencia.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Configura o Banco de Dados (Oracle)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseOracle(connectionString));

// 2. Configura o Swagger (Documentação)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 3. Ativa o Swagger em ambiente de desenvolvimento
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Rota básica de teste
app.MapGet("/", () => "API Escola+ rodando!");

// 4. Mapeia as nossas rotas de frequência
app.MapFrequenciaEndpoints();

app.Run();

// Necessário para os testes de integração
public partial class Program { }