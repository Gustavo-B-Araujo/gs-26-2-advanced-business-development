using System.Globalization;
using System.Linq.Expressions;
using BoletimPlus.Api.Contratos;
using BoletimPlus.Api.Dados;
using BoletimPlus.Api.Dominio;
using BoletimPlus.Api.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace BoletimPlus.Api.Servicos;

/// <summary>Projeções e regras de acesso usadas por mais de um serviço.</summary>
public class Consultas(BoletimDbContext db)
{
    /// <summary>Ordem alfabética do português (acento não muda a ordem).</summary>
    public static readonly StringComparer OrdemNome = StringComparer.Create(new CultureInfo("pt-BR"), ignoreCase: true);

    public static readonly Expression<Func<Turma, TurmaResumo>> ParaTurmaResumo = t => new TurmaResumo(
        t.Cod,
        t.CodEscola,
        t.Escola.Nome,
        t.TipoCurso.Valor,
        t.CodProfessor,
        t.Professor.Pessoa.Nome,
        t.SemestreReferencia,
        t.AnoReferencia,
        t.Alunos.Count);

    /// <summary>Mais recente primeiro: ano e semestre decrescentes, depois cod.</summary>
    public static IEnumerable<TurmaResumo> Ordenar(IEnumerable<TurmaResumo> turmas) =>
        turmas.OrderByDescending(t => t.Ano).ThenByDescending(t => t.Semestre).ThenBy(t => t.Cod);

    public async Task<List<TurmaResumo>> TurmasAsync(Expression<Func<Turma, bool>> filtro) =>
        Ordenar(await db.Turma.AsNoTracking().Where(filtro).Select(ParaTurmaResumo).ToListAsync()).ToList();

    public async Task<TurmaResumo> TurmaResumoAsync(int turmaCod) =>
        await db.Turma.AsNoTracking().Where(t => t.Cod == turmaCod).Select(ParaTurmaResumo).FirstOrDefaultAsync()
        ?? throw RegraException.NaoEncontrado("Turma não encontrada.");

    /// <summary>Matérias (TipoCurso) das turmas de cada professor, em toda a rede, sem repetir.</summary>
    public async Task<Dictionary<int, List<string>>> MateriasPorProfessorAsync()
    {
        var pares = await db.Turma.AsNoTracking()
            .Select(t => new { t.CodProfessor, Materia = t.TipoCurso.Valor })
            .Distinct()
            .ToListAsync();
        return pares.GroupBy(p => p.CodProfessor)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Materia).Order(OrdemNome).ToList());
    }

    public Task<int> TotalTurmasDoAlunoAsync(int alunoCod) =>
        db.TurmaAluno.CountAsync(ta => ta.CodAluno == alunoCod);

    public async Task<List<AlunoDaTurma>> AlunosDaTurmaAsync(int turmaCod)
    {
        var alunos = await db.TurmaAluno.AsNoTracking()
            .Where(ta => ta.CodTurma == turmaCod)
            .Select(ta => new AlunoDaTurma(ta.Cod, ta.CodAluno, ta.Aluno.Pessoa.Nome))
            .ToListAsync();
        return alunos.OrderBy(a => a.Nome, OrdemNome).ToList();
    }

    /// <summary>Avaliações da turma pela data; sem data vão para o fim.</summary>
    public async Task<List<AvaliacaoResumo>> AvaliacoesDaTurmaAsync(int turmaCod)
    {
        var avaliacoes = await db.Avaliacao.AsNoTracking()
            .Where(a => a.CodTurma == turmaCod)
            .Select(a => new { a.Cod, a.Nome, a.CodTipoNota, TipoNota = a.TipoNota.Valor, a.DataAvaliacao, Notas = a.Notas.Count })
            .ToListAsync();
        return avaliacoes
            .OrderBy(a => a.DataAvaliacao ?? DateTime.MaxValue).ThenBy(a => a.Cod)
            .Select(a => new AvaliacaoResumo(a.Cod, a.Nome, a.CodTipoNota, a.TipoNota, ParaData(a.DataAvaliacao), a.Notas))
            .ToList();
    }

    public async Task<Turma> BuscarTurmaAsync(int turmaCod) =>
        await db.Turma.AsNoTracking().FirstOrDefaultAsync(t => t.Cod == turmaCod)
        ?? throw RegraException.NaoEncontrado("Turma não encontrada.");

    // --- Acesso por recurso ------------------------------------------------

    public Task<bool> EhDiretorDaEscolaAsync(Papeis p, int escolaCod) =>
        p.DiretorCod is null
            ? Task.FromResult(false)
            : db.Escola.AnyAsync(e => e.Cod == escolaCod && e.CodDiretor == p.DiretorCod);

    public async Task ExigirDiretorDaEscolaAsync(Papeis p, int escolaCod)
    {
        if (!await db.Escola.AnyAsync(e => e.Cod == escolaCod))
            throw RegraException.NaoEncontrado("Escola não encontrada.");
        if (!await EhDiretorDaEscolaAsync(p, escolaCod))
            throw RegraException.Proibido("Só o diretor desta escola tem acesso.");
    }

    /// <summary>Professor da turma, diretor da escola, aluno matriculado ou guardião de um matriculado.</summary>
    public async Task<Turma> ExigirLeituraDaTurmaAsync(Papeis p, int turmaCod)
    {
        var turma = await BuscarTurmaAsync(turmaCod);
        if (turma.CodProfessor == p.ProfessorCod) return turma;
        if (await EhDiretorDaEscolaAsync(p, turma.CodEscola)) return turma;
        var matriculados = db.TurmaAluno.Where(ta => ta.CodTurma == turmaCod).Select(ta => ta.CodAluno);
        if (p.AlunoCod is not null && await matriculados.ContainsAsync(p.AlunoCod.Value)) return turma;
        if (p.GuardiaoCod is not null &&
            await db.AlunoGuardiao.AnyAsync(ag => ag.CodGuardiao == p.GuardiaoCod && matriculados.Contains(ag.CodAluno)))
            return turma;
        throw RegraException.Proibido("Você não tem acesso a esta turma.");
    }

    /// <summary>Escrita (chamada, notas, avaliações): só o professor da turma (Turma.cod_professor).</summary>
    public async Task<Turma> ExigirProfessorDaTurmaAsync(Papeis p, int turmaCod)
    {
        var turma = await BuscarTurmaAsync(turmaCod);
        if (p.ProfessorCod is null || turma.CodProfessor != p.ProfessorCod)
            throw RegraException.Proibido("Só o professor da turma pode lançar dados.");
        return turma;
    }

    public static DateOnly? ParaData(DateTime? data) => data is null ? null : DateOnly.FromDateTime(data.Value);

    public static DateTime ParaDateTime(DateOnly data) => data.ToDateTime(TimeOnly.MinValue);
}
