using EscolaPlus.Frequencia.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace EscolaPlus.Frequencia.Api.Infrastructure;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Turma> Turmas => Set<Turma>();
    public DbSet<Aluno> Alunos => Set<Aluno>();
    public DbSet<Domain.Frequencia> Frequencias => Set<Domain.Frequencia>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 1. Regra de unicidade: Um registro de frequência por aluno por data
        modelBuilder.Entity<Domain.Frequencia>()
            .HasIndex(f => new { f.AlunoId, f.Data })
            .IsUnique();

        // 2. Regra de unicidade: A chave de idempotência não pode repetir
        modelBuilder.Entity<IdempotencyRecord>()
            .HasIndex(i => i.Chave)
            .IsUnique();

        // 3. Salvar o Status e Origem como texto no banco (mais fácil de ler)
        modelBuilder.Entity<Domain.Frequencia>()
            .Property(f => f.Status)
            .HasConversion<string>();
            
        modelBuilder.Entity<Domain.Frequencia>()
            .Property(f => f.Origem)
            .HasConversion<string>();

        // 4. Dados Fictícios (Seed) para testes
        modelBuilder.Entity<Turma>().HasData(
            new Turma { Id = 1, Nome = "Turma A - Fictícia", ProfessorId = 101 }
        );
        modelBuilder.Entity<Aluno>().HasData(
            new Aluno { Id = 1, Apelido = "Aluno Fictício 1", TurmaId = 1 },
            new Aluno { Id = 2, Apelido = "Aluno Fictício 2", TurmaId = 1 }
        );
    }
}