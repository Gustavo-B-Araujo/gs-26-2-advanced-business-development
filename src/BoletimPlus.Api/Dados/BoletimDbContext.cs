using System.Text;
using Microsoft.EntityFrameworkCore;

namespace BoletimPlus.Api.Dados;

/// <summary>
/// Mapeia as tabelas de 01_ddl.sql sem mudar o DDL. No Oracle, nome sem aspas fica em
/// maiúsculas: a classe Turma vira TURMA e a propriedade CodTipoCurso vira COD_TIPO_CURSO.
/// </summary>
public class BoletimDbContext(DbContextOptions<BoletimDbContext> options) : DbContext(options)
{
    public DbSet<StatusUsuario> StatusUsuario => Set<StatusUsuario>();
    public DbSet<TipoParentesco> TipoParentesco => Set<TipoParentesco>();
    public DbSet<TipoCurso> TipoCurso => Set<TipoCurso>();
    public DbSet<TipoNota> TipoNota => Set<TipoNota>();
    public DbSet<TipoPresenca> TipoPresenca => Set<TipoPresenca>();
    public DbSet<Pessoa> Pessoa => Set<Pessoa>();
    public DbSet<Usuario> Usuario => Set<Usuario>();
    public DbSet<Diretor> Diretor => Set<Diretor>();
    public DbSet<Professor> Professor => Set<Professor>();
    public DbSet<Guardiao> Guardiao => Set<Guardiao>();
    public DbSet<Aluno> Aluno => Set<Aluno>();
    public DbSet<AlunoGuardiao> AlunoGuardiao => Set<AlunoGuardiao>();
    public DbSet<Escola> Escola => Set<Escola>();
    public DbSet<Turma> Turma => Set<Turma>();
    public DbSet<TurmaAluno> TurmaAluno => Set<TurmaAluno>();
    public DbSet<Avaliacao> Avaliacao => Set<Avaliacao>();
    public DbSet<Boletim> Boletim => Set<Boletim>();
    public DbSet<ListaPresenca> ListaPresenca => Set<ListaPresenca>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // A chave de toda tabela é COD (GENERATED AS IDENTITY); o EF não reconhece COD sozinho.
        Type[] tabelas =
        [
            typeof(Pessoa), typeof(Usuario), typeof(AlunoGuardiao), typeof(Escola), typeof(Turma),
            typeof(TurmaAluno), typeof(Avaliacao), typeof(Boletim), typeof(ListaPresenca),
        ];
        foreach (var tabela in tabelas) modelBuilder.Entity(tabela).HasKey("Cod");

        MapearEnumerador<StatusUsuario>(modelBuilder, 9);
        MapearEnumerador<TipoParentesco>(modelBuilder, 20);
        MapearEnumerador<TipoCurso>(modelBuilder, 30);
        MapearEnumerador<TipoNota>(modelBuilder, 30);
        MapearEnumerador<TipoPresenca>(modelBuilder, 30);

        modelBuilder.Entity<Pessoa>(e =>
        {
            e.Property(p => p.Nome).HasMaxLength(150).IsRequired();
            e.Property(p => p.NumDocumento).HasMaxLength(11).IsRequired();
            e.HasIndex(p => p.NumDocumento).IsUnique();
        });

        modelBuilder.Entity<Usuario>(e =>
        {
            e.Property(u => u.SenhaHash).HasMaxLength(60).IsRequired();
            e.HasIndex(u => u.CodPessoa).IsUnique();
            e.HasOne(u => u.Status).WithMany().HasForeignKey(u => u.CodStatus);
            e.HasOne(u => u.Pessoa).WithMany().HasForeignKey(u => u.CodPessoa);
        });

        MapearPapel<Diretor>(modelBuilder);
        MapearPapel<Professor>(modelBuilder);
        MapearPapel<Guardiao>(modelBuilder);
        MapearPapel<Aluno>(modelBuilder);

        modelBuilder.Entity<AlunoGuardiao>(e =>
        {
            e.HasOne<Guardiao>().WithMany().HasForeignKey(ag => ag.CodGuardiao);
            e.HasOne<Aluno>().WithMany().HasForeignKey(ag => ag.CodAluno);
            e.HasOne<TipoParentesco>().WithMany().HasForeignKey(ag => ag.CodTipoParentesco);
            e.HasIndex(ag => new { ag.CodGuardiao, ag.CodAluno }).IsUnique();
        });

        modelBuilder.Entity<Escola>(e =>
        {
            e.Property(x => x.Nome).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Nome).IsUnique();
            e.HasOne(x => x.Diretor).WithMany().HasForeignKey(x => x.CodDiretor);
        });

        modelBuilder.Entity<Turma>(e =>
        {
            e.HasOne(t => t.Escola).WithMany(x => x.Turmas).HasForeignKey(t => t.CodEscola);
            e.HasOne(t => t.TipoCurso).WithMany().HasForeignKey(t => t.CodTipoCurso);
            e.HasOne(t => t.Professor).WithMany().HasForeignKey(t => t.CodProfessor);
        });

        modelBuilder.Entity<TurmaAluno>(e =>
        {
            e.HasOne(ta => ta.Turma).WithMany(t => t.Alunos).HasForeignKey(ta => ta.CodTurma);
            e.HasOne(ta => ta.Aluno).WithMany().HasForeignKey(ta => ta.CodAluno);
            e.HasIndex(ta => new { ta.CodTurma, ta.CodAluno }).IsUnique();
        });

        modelBuilder.Entity<Avaliacao>(e =>
        {
            e.Property(a => a.Nome).HasMaxLength(50).IsRequired();
            e.Property(a => a.DataAvaliacao).HasColumnType("DATE");
            e.HasOne(a => a.Turma).WithMany(t => t.Avaliacoes).HasForeignKey(a => a.CodTurma);
            e.HasOne(a => a.TipoNota).WithMany().HasForeignKey(a => a.CodTipoNota);
            e.HasIndex(a => new { a.CodTurma, a.Nome }).IsUnique();
        });

        modelBuilder.Entity<Boletim>(e =>
        {
            e.Property(b => b.Nota).HasPrecision(5, 2);
            e.HasOne(b => b.TurmaAluno).WithMany().HasForeignKey(b => b.CodTurmaAluno);
            e.HasOne(b => b.Avaliacao).WithMany(a => a.Notas).HasForeignKey(b => b.CodAvaliacao);
            e.HasIndex(b => new { b.CodTurmaAluno, b.CodAvaliacao }).IsUnique();
        });

        modelBuilder.Entity<ListaPresenca>(e =>
        {
            e.Property(l => l.Observacao).HasMaxLength(150);
            e.Property(l => l.DataPresenca).HasColumnType("DATE");
            e.HasOne(l => l.TurmaAluno).WithMany().HasForeignKey(l => l.CodTurmaAluno);
            e.HasOne<TipoPresenca>().WithMany().HasForeignKey(l => l.CodTipoPresenca);
            e.HasIndex(l => new { l.CodTurmaAluno, l.DataPresenca }).IsUnique();
        });

        AplicarNomesDoOracle(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var agora = DateTime.Now;
        foreach (var entrada in ChangeTracker.Entries<IComDataCriacao>())
        {
            if (entrada.State == EntityState.Added && entrada.Entity.DataCriacao == default)
                entrada.Entity.DataCriacao = agora;
        }
        return base.SaveChangesAsync(cancellationToken);
    }

    private static void MapearEnumerador<T>(ModelBuilder modelBuilder, int tamanho) where T : Enumerador
    {
        modelBuilder.Entity<T>(e =>
        {
            e.HasKey(x => x.Cod);
            e.Property(x => x.Valor).HasColumnName("ENUMERADOR").HasMaxLength(tamanho).IsRequired();
            e.HasIndex(x => x.Valor).IsUnique();
        });
    }

    private static void MapearPapel<T>(ModelBuilder modelBuilder) where T : Papel
    {
        modelBuilder.Entity<T>(e =>
        {
            e.HasKey(x => x.Cod);
            e.HasIndex(x => x.CodPessoa).IsUnique();
            e.HasOne(x => x.Pessoa).WithMany().HasForeignKey(x => x.CodPessoa);
        });
    }

    /// <summary>Tabela = nome da classe em maiúsculas; coluna = propriedade em SNAKE_CASE maiúsculo; texto VARCHAR2; FK sem cascade.</summary>
    private static void AplicarNomesDoOracle(ModelBuilder modelBuilder)
    {
        foreach (var entidade in modelBuilder.Model.GetEntityTypes())
        {
            entidade.SetTableName(entidade.ClrType.Name.ToUpperInvariant());
            foreach (var propriedade in entidade.GetProperties())
            {
                if (propriedade.GetColumnName() == propriedade.Name)
                    propriedade.SetColumnName(ParaSnakeMaiusculo(propriedade.Name));
                // VARCHAR2 no DDL: parâmetro NVARCHAR2 faria conversão implícita e ignoraria o índice.
                if (propriedade.ClrType == typeof(string)) propriedade.SetIsUnicode(false);
            }
            // O DDL não tem ON DELETE CASCADE: apagar pai com filho falha, como no banco.
            foreach (var fk in entidade.GetForeignKeys()) fk.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }

    public static string ParaSnakeMaiusculo(string nome)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < nome.Length; i++)
        {
            if (i > 0 && char.IsUpper(nome[i])) sb.Append('_');
            sb.Append(char.ToUpperInvariant(nome[i]));
        }
        return sb.ToString();
    }
}
