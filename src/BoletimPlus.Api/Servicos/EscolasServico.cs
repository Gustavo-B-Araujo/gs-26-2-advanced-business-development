using BoletimPlus.Api.Contratos;
using BoletimPlus.Api.Dados;
using BoletimPlus.Api.Dominio;
using BoletimPlus.Api.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace BoletimPlus.Api.Servicos;

/// <summary>Gestão escolar: escolas do diretor, turmas, professores e alunos da escola, matrícula.</summary>
public class EscolasServico(BoletimDbContext db, Consultas consultas)
{
    public async Task<List<EscolaResumo>> EscolasDoDiretorAsync(Papeis p, int diretorCod)
    {
        if (p.DiretorCod != diretorCod) throw RegraException.Proibido();
        return await db.Escola.AsNoTracking()
            .Where(e => e.CodDiretor == diretorCod)
            .OrderBy(e => e.Nome)
            .Select(e => new EscolaResumo(e.Cod, e.Nome, e.Diretor.Pessoa.Nome, e.Turmas.Count))
            .ToListAsync();
    }

    public async Task<EscolaResumo> ObterAsync(Papeis p, int escolaCod)
    {
        await consultas.ExigirDiretorDaEscolaAsync(p, escolaCod);
        return await db.Escola.AsNoTracking()
            .Where(e => e.Cod == escolaCod)
            .Select(e => new EscolaResumo(e.Cod, e.Nome, e.Diretor.Pessoa.Nome, e.Turmas.Count))
            .FirstAsync();
    }

    public async Task<List<TurmaResumo>> TurmasAsync(Papeis p, int escolaCod)
    {
        await consultas.ExigirDiretorDaEscolaAsync(p, escolaCod);
        return await consultas.TurmasAsync(t => t.CodEscola == escolaCod);
    }

    /// <summary>
    /// Só o diretor da escola. O professor é obrigatório (cod_professor NOT NULL) e pode ser
    /// qualquer um da rede. Valida os CHECKs de semestre e ano antes do banco.
    /// </summary>
    public async Task<int> CriarTurmaAsync(Papeis p, int escolaCod, NovaTurmaRequest req)
    {
        await consultas.ExigirDiretorDaEscolaAsync(p, escolaCod);
        RegraException.SeHouver(
            req.CodTipoCurso is null ? "Informe o curso (cod_tipo_curso)." : null,
            req.CodProfessor is null ? "Informe o professor (cod_professor)." : null,
            Regras.ValidarSemestre(req.SemestreReferencia),
            Regras.ValidarAno(req.AnoReferencia));
        if (!await db.TipoCurso.AnyAsync(t => t.Cod == req.CodTipoCurso))
            throw RegraException.Invalido("Tipo de curso inválido.");
        if (!await db.Professor.AnyAsync(x => x.Cod == req.CodProfessor))
            throw RegraException.NaoEncontrado("Professor não encontrado.");

        var turma = new Turma
        {
            CodEscola = escolaCod,
            CodTipoCurso = req.CodTipoCurso!.Value,
            CodProfessor = req.CodProfessor!.Value,
            SemestreReferencia = req.SemestreReferencia!.Value,
            AnoReferencia = req.AnoReferencia!.Value,
        };
        db.Turma.Add(turma);
        await db.SaveChangesAsync();
        return turma.Cod;
    }

    /// <summary>Professores da escola = quem dá aula em alguma turma dela.</summary>
    public async Task<List<ProfessorDaEscola>> ProfessoresAsync(Papeis p, int escolaCod)
    {
        await consultas.ExigirDiretorDaEscolaAsync(p, escolaCod);
        var turmas = await consultas.TurmasAsync(t => t.CodEscola == escolaCod);
        var materias = await consultas.MateriasPorProfessorAsync();
        return turmas
            .GroupBy(t => t.ProfessorCod)
            .Select(g => new ProfessorDaEscola(
                g.Key,
                g.First().ProfessorNome,
                materias.GetValueOrDefault(g.Key) ?? [],
                g.ToList()))
            .OrderBy(x => x.Nome, Consultas.OrdemNome)
            .ToList();
    }

    /// <summary>Alunos com matrícula em turma da escola, uma vez cada, com as turmas dele ali.</summary>
    public async Task<List<AlunoDaEscola>> AlunosAsync(Papeis p, int escolaCod)
    {
        await consultas.ExigirDiretorDaEscolaAsync(p, escolaCod);
        var turmas = (await consultas.TurmasAsync(t => t.CodEscola == escolaCod)).ToDictionary(t => t.Cod);
        var matriculas = await db.TurmaAluno.AsNoTracking()
            .Where(ta => ta.Turma.CodEscola == escolaCod)
            .Select(ta => new
            {
                ta.CodTurma,
                ta.CodAluno,
                ta.Aluno.Pessoa.Nome,
                ta.Aluno.Pessoa.NumDocumento,
                TotalTurmas = db.TurmaAluno.Count(x => x.CodAluno == ta.CodAluno),
            })
            .ToListAsync();
        return matriculas
            .GroupBy(m => m.CodAluno)
            .Select(g => new AlunoDaEscola(
                g.Key,
                g.First().Nome,
                g.First().NumDocumento,
                g.First().TotalTurmas,
                Consultas.Ordenar(g.Select(m => turmas[m.CodTurma])).ToList()))
            .OrderBy(a => a.Nome, Consultas.OrdemNome)
            .ToList();
    }

    /// <summary>
    /// Só o diretor da escola da turma. Aluno já matriculado é ignorado (uq_turma_aluno),
    /// então repetir a chamada é seguro: o PUT/POST é idempotente por natureza.
    /// </summary>
    public async Task MatricularAsync(Papeis p, int turmaCod, MatriculaRequest req)
    {
        var turma = await consultas.BuscarTurmaAsync(turmaCod);
        if (!await consultas.EhDiretorDaEscolaAsync(p, turma.CodEscola))
            throw RegraException.Proibido("Só o diretor da escola da turma matricula alunos.");

        var codigos = (req.CodAlunos ?? []).Distinct().ToList();
        if (codigos.Count == 0) throw RegraException.Invalido("Escolha ao menos um aluno.");

        var existentes = await db.Aluno.Where(a => codigos.Contains(a.Cod)).Select(a => a.Cod).ToListAsync();
        if (existentes.Count != codigos.Count) throw RegraException.NaoEncontrado("Aluno não encontrado.");

        var jaMatriculados = await db.TurmaAluno
            .Where(ta => ta.CodTurma == turmaCod && codigos.Contains(ta.CodAluno))
            .Select(ta => ta.CodAluno)
            .ToListAsync();
        foreach (var cod in codigos.Except(jaMatriculados))
            db.TurmaAluno.Add(new TurmaAluno { CodTurma = turmaCod, CodAluno = cod });
        await db.SaveChangesAsync();
    }
}
