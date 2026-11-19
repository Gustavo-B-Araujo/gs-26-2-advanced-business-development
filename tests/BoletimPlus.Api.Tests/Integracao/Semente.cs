using BoletimPlus.Api.Dados;

namespace BoletimPlus.Api.Tests.Integracao;

/// <summary>Os mesmos dados de 02_dml.sql, com os mesmos cods. Senha de todos: <see cref="Senha"/>.</summary>
public static class Semente
{
    public const string Senha = "senha123";
    private static readonly string Hash = BCrypt.Net.BCrypt.HashPassword(Senha, workFactor: 4);

    public static void Popular(BoletimDbContext db)
    {
        if (db.StatusUsuario.Any()) return;

        Enumeradores(db.StatusUsuario, "Ativo", "Inativo", "Bloqueado");
        Enumeradores(db.TipoParentesco, "Pai", "Mãe", "Representante Legal");
        Enumeradores(db.TipoCurso, "Português", "Matemática", "História");
        Enumeradores(db.TipoNota, "Prova", "Trabalho", "Caderno");
        Enumeradores(db.TipoPresenca, "Presente", "Falta", "Falta Justificada");

        string[] nomes =
        [
            "Seymour Skinner", "Edna Krabappel", "Homer Simpson", "Marge Simpson", "Bart Simpson",
            "Lisa Simpson", "Milhouse Van Houten", "Kirk Van Houten", "Elizabeth Hoover",
        ];
        for (var i = 0; i < nomes.Length; i++)
            db.Pessoa.Add(new Pessoa { Cod = i + 1, Nome = nomes[i], NumDocumento = $"1234567890{i}" });

        (int Status, int Pessoa)[] usuarios = [(1, 1), (1, 2), (3, 3), (1, 4), (1, 5)];
        for (var i = 0; i < usuarios.Length; i++)
            db.Usuario.Add(new Usuario { Cod = i + 1, CodStatus = usuarios[i].Status, CodPessoa = usuarios[i].Pessoa, SenhaHash = Hash });

        db.Diretor.Add(new Diretor { Cod = 1, CodPessoa = 1 });
        db.Professor.AddRange(new Professor { Cod = 1, CodPessoa = 2 }, new Professor { Cod = 2, CodPessoa = 9 });
        db.Guardiao.AddRange(
            new Guardiao { Cod = 1, CodPessoa = 3 }, new Guardiao { Cod = 2, CodPessoa = 4 }, new Guardiao { Cod = 3, CodPessoa = 8 });
        db.Aluno.AddRange(new Aluno { Cod = 1, CodPessoa = 5 }, new Aluno { Cod = 2, CodPessoa = 6 }, new Aluno { Cod = 3, CodPessoa = 7 });

        (int G, int A, int P)[] vinculos = [(1, 1, 1), (2, 1, 2), (1, 2, 1), (2, 2, 2), (3, 3, 1)];
        for (var i = 0; i < vinculos.Length; i++)
            db.AlunoGuardiao.Add(new AlunoGuardiao { Cod = i + 1, CodGuardiao = vinculos[i].G, CodAluno = vinculos[i].A, CodTipoParentesco = vinculos[i].P });

        db.Escola.Add(new Escola { Cod = 1, CodDiretor = 1, Nome = "Escola Primária de Springfield" });

        (int Curso, int Professor)[] turmas = [(1, 1), (2, 1), (3, 2)];
        for (var i = 0; i < turmas.Length; i++)
            db.Turma.Add(new Turma { Cod = i + 1, CodEscola = 1, CodTipoCurso = turmas[i].Curso, CodProfessor = turmas[i].Professor, SemestreReferencia = 2, AnoReferencia = 2026 });

        (int Turma, int Aluno)[] matriculas = [(1, 1), (1, 3), (2, 1), (2, 3), (3, 2), (3, 1)];
        for (var i = 0; i < matriculas.Length; i++)
            db.TurmaAluno.Add(new TurmaAluno { Cod = i + 1, CodTurma = matriculas[i].Turma, CodAluno = matriculas[i].Aluno });

        (int Turma, int Tipo, string Nome, DateTime Data)[] avaliacoes =
        [
            (1, 1, "Prova 1", new(2026, 9, 15)), (1, 2, "Trabalho 1", new(2026, 10, 10)),
            (2, 1, "Prova 1", new(2026, 9, 16)), (2, 3, "Caderno", new(2026, 11, 5)), (3, 1, "Prova 1", new(2026, 9, 17)),
        ];
        for (var i = 0; i < avaliacoes.Length; i++)
        {
            var a = avaliacoes[i];
            db.Avaliacao.Add(new Avaliacao { Cod = i + 1, CodTurma = a.Turma, CodTipoNota = a.Tipo, Nome = a.Nome, DataAvaliacao = a.Data });
        }

        (int TurmaAluno, int Avaliacao, decimal Nota)[] notas =
        [
            (1, 1, 45m), (1, 2, 60m), (2, 1, 70m), (2, 2, 75.5m), (3, 3, 30m),
            (3, 4, 55m), (4, 3, 68m), (4, 4, 80m), (5, 5, 100m), (6, 5, 40m),
        ];
        for (var i = 0; i < notas.Length; i++)
            db.Boletim.Add(new Boletim { Cod = i + 1, CodTurmaAluno = notas[i].TurmaAluno, CodAvaliacao = notas[i].Avaliacao, Nota = notas[i].Nota });

        (int TurmaAluno, int Tipo, string? Obs, DateTime Data)[] presencas =
        [
            (1, 1, null, new(2026, 11, 3)), (1, 2, null, new(2026, 11, 4)), (1, 3, "Atestado médico", new(2026, 11, 5)),
            (2, 1, null, new(2026, 11, 3)), (2, 1, null, new(2026, 11, 4)), (5, 1, null, new(2026, 11, 3)), (6, 2, null, new(2026, 11, 3)),
        ];
        for (var i = 0; i < presencas.Length; i++)
        {
            var p = presencas[i];
            db.ListaPresenca.Add(new ListaPresenca { Cod = i + 1, CodTurmaAluno = p.TurmaAluno, CodTipoPresenca = p.Tipo, Observacao = p.Obs, DataPresenca = p.Data });
        }

        db.SaveChangesAsync().GetAwaiter().GetResult();
    }

    private static void Enumeradores<T>(Microsoft.EntityFrameworkCore.DbSet<T> tabela, params string[] valores)
        where T : Enumerador, new()
    {
        for (var i = 0; i < valores.Length; i++) tabela.Add(new T { Cod = i + 1, Valor = valores[i] });
    }
}
