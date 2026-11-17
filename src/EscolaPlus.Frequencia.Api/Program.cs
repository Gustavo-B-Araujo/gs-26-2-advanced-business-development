using EscolaPlus.Frequencia.Api.Auth;
using EscolaPlus.Frequencia.Api.Endpoints;
using EscolaPlus.Frequencia.Api.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi; // <-- Namespace atualizado para o .NET 10
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseOracle(connectionString));


builder.Services.AddJwtAuthentication(builder.Configuration);


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


app.MapGet("/", () => "API Escola+ rodando!");
app.MapFrequenciaEndpoints();

app.Run();

// Necessário para os testes de integração
public partial class Program { }