using BoletimPlus.Api.Contratos;
using BoletimPlus.Api.Dados;
using BoletimPlus.Api.Dominio;
using BoletimPlus.Api.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace BoletimPlus.Api.Servicos;

/// <summary>
/// Professores e alunos da rede. Qualquer diretor cadastra e renomeia: professor e aluno não
/// pertencem a uma escola (o DDL só os liga à escola pela Turma).
/// </summary>
public class PessoasServico(BoletimDbContext db, Consultas consultas)
{
    public async Task<List<ProfessorResumo>> ListarProfessoresAsync()
    {
        var materias = await consultas.MateriasPorProfessorAsync();
        var professores = await db.Professor.AsNoTracking()
            .Select(p => new { p.Cod, p.Pessoa.Nome })
            .ToListAsync();
        return professores
            .Select(p => new ProfessorResumo(p.Cod, p.Nome, materias.GetValueOrDefault(p.Cod) ?? []))
            .OrderBy(p => p.Nome, Consultas.OrdemNome)
            .ToList();
    }

    /// <summary>Uma transação (um SaveChanges): Pessoa + Usuario ATIVO + Professor.</summary>
    public async Task<int> CadastrarProfessorAsync(NovaPessoaRequest req)
    {
        RegraException.SeHouver(
            Regras.ValidarNome(req.Nome),
            Regras.ValidarCpf(req.NumDocumento),
            Regras.ValidarSenha(req.Senha));

        var pessoa = await NovaPessoaAsync(req);
        db.Usuario.Add(await NovoUsuarioAsync(pessoa, req.Senha!));
        var professor = new Professor { Pessoa = pessoa };
        db.Professor.Add(professor);
        await db.SaveChangesAsync();
        return professor.Cod;
    }

    public Task RenomearProfessorAsync(int professorCod, RenomearRequest req) =>
        RenomearAsync(db.Professor, professorCod, req, "Professor não encontrado.");

    public async Task<List<TurmaResumo>> TurmasDoProfessorAsync(Papeis p, int professorCod)
    {
        if (p.ProfessorCod != professorCod) throw RegraException.Proibido();
        return await consultas.TurmasAsync(t => t.CodProfessor == professorCod);
    }

    public async Task<List<AlunoResumo>> ListarAlunosAsync()
    {
        var alunos = await db.Aluno.AsNoTracking()
            .Select(a => new AlunoResumo(
                a.Cod,
                a.Pessoa.Nome,
                a.Pessoa.NumDocumento,
                db.TurmaAluno.Count(ta => ta.CodAluno == a.Cod)))
            .ToListAsync();
        return alunos.OrderBy(a => a.Nome, Consultas.OrdemNome).ToList();
    }

    /// <summary>Uma transação: Pessoa + Aluno, e Usuario ATIVO só quando vem senha.</summary>
    public async Task<int> CadastrarAlunoAsync(NovaPessoaRequest req)
    {
        RegraException.SeHouver(
            Regras.ValidarNome(req.Nome),
            Regras.ValidarCpf(req.NumDocumento),
            req.Senha is null ? null : Regras.ValidarSenha(req.Senha));

        var pessoa = await NovaPessoaAsync(req);
        if (req.Senha is not null) db.Usuario.Add(await NovoUsuarioAsync(pessoa, req.Senha));
        var aluno = new Aluno { Pessoa = pessoa };
        db.Aluno.Add(aluno);
        await db.SaveChangesAsync();
        return aluno.Cod;
    }

    public Task RenomearAlunoAsync(int alunoCod, RenomearRequest req) =>
        RenomearAsync(db.Aluno, alunoCod, req, "Aluno não encontrado.");

    private async Task<Pessoa> NovaPessoaAsync(NovaPessoaRequest req)
    {
        if (await db.Pessoa.AnyAsync(p => p.NumDocumento == req.NumDocumento))
            throw RegraException.Conflito("Este CPF já tem cadastro.");
        return new Pessoa { Nome = req.Nome!.Trim(), NumDocumento = req.NumDocumento! };
    }

    private async Task<Usuario> NovoUsuarioAsync(Pessoa pessoa, string senha)
    {
        var ativo = await db.StatusUsuario.AsNoTracking().ToListAsync();
        var codAtivo = ativo.FirstOrDefault(s => Regras.ChaveEnum(s.Valor) == "ATIVO")?.Cod
                       ?? throw new InvalidOperationException("StatusUsuario 'Ativo' não existe no banco.");
        return new Usuario
        {
            Pessoa = pessoa,
            CodStatus = codAtivo,
            SenhaHash = BCrypt.Net.BCrypt.HashPassword(senha, workFactor: 10),
        };
    }

    /// <summary>UPDATE Pessoa SET nome: o nome vale para todos os papéis da pessoa. O CPF não muda.</summary>
    private async Task RenomearAsync<T>(DbSet<T> tabela, int cod, RenomearRequest req, string naoEncontrado)
        where T : Dados.Papel
    {
        RegraException.SeHouver(Regras.ValidarNome(req.Nome));
        var pessoa = await tabela.Where(x => x.Cod == cod).Select(x => x.Pessoa).FirstOrDefaultAsync()
                     ?? throw RegraException.NaoEncontrado(naoEncontrado);
        pessoa.Nome = req.Nome!.Trim();
        await db.SaveChangesAsync();
    }
}
