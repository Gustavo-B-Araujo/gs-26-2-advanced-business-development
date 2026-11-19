using BoletimPlus.Api.Contratos;
using BoletimPlus.Api.Dados;
using BoletimPlus.Api.Dominio;
using BoletimPlus.Api.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace BoletimPlus.Api.Servicos;

/// <summary>
/// Acompanhamento do aluno: dependentes do responsável e boletim (notas, média, presenças,
/// frequência). Mesmas regras do GuardiaoService, AcessoService e BoletimService do Java.
/// A análise com IA fica só no Java.
/// </summary>
public class AcompanhamentoServico(BoletimDbContext db, Consultas consultas)
{
    /// <summary>Só o próprio responsável lista os dependentes dele, em ordem de nome.</summary>
    public async Task<List<DependenteResponse>> DependentesAsync(Papeis p, int guardiaoCod)
    {
        if (p.GuardiaoCod != guardiaoCod) throw RegraException.Proibido("Acesso negado.");

        var dependentes = await (
                from ag in db.AlunoGuardiao.AsNoTracking()
                join a in db.Aluno on ag.CodAluno equals a.Cod
                join tp in db.TipoParentesco on ag.CodTipoParentesco equals tp.Cod
                where ag.CodGuardiao == guardiaoCod
                select new DependenteResponse(a.Cod, a.Pessoa.Nome, tp.Valor))
            .ToListAsync();
        return dependentes.OrderBy(d => d.Nome, Consultas.OrdemNome).ToList();
    }

    /// <summary>
    /// Quem vê o aluno: ele mesmo, um responsável vinculado (AlunoGuardiao), o professor de uma
    /// turma dele ou o diretor da escola de uma turma dele.
    /// </summary>
    public async Task<bool> PodeVerAlunoAsync(Papeis p, int alunoCod)
    {
        if (p.AlunoCod == alunoCod) return true;
        if (p.GuardiaoCod is { } guardiao &&
            await db.AlunoGuardiao.AnyAsync(ag => ag.CodGuardiao == guardiao && ag.CodAluno == alunoCod))
            return true;
        if (p.ProfessorCod is null && p.DiretorCod is null) return false;

        var professor = p.ProfessorCod ?? -1;
        var diretor = p.DiretorCod ?? -1;
        return await db.TurmaAluno.AnyAsync(ta => ta.CodAluno == alunoCod &&
                                                  (ta.Turma.CodProfessor == professor || ta.Turma.Escola.CodDiretor == diretor));
    }

    /// <summary>Notas e média por turma (mais recente primeiro), presenças e frequência.</summary>
    public async Task<BoletimAlunoResponse> BoletimAsync(Papeis p, int alunoCod)
    {
        if (!await PodeVerAlunoAsync(p, alunoCod))
            throw RegraException.Proibido("Você não tem acesso a este aluno.");

        var nome = await db.Aluno.AsNoTracking().Where(a => a.Cod == alunoCod).Select(a => a.Pessoa.Nome).FirstOrDefaultAsync()
                   ?? throw RegraException.NaoEncontrado("Aluno não encontrado.");

        var matriculas = await db.TurmaAluno.AsNoTracking()
            .Where(ta => ta.CodAluno == alunoCod)
            .ToDictionaryAsync(ta => ta.CodTurma, ta => ta.Cod);
        var turmas = await consultas.TurmasAsync(t => t.Alunos.Any(ta => ta.CodAluno == alunoCod));

        var notas = await db.Boletim.AsNoTracking()
            .Where(b => b.TurmaAluno.CodAluno == alunoCod)
            .Select(b => new { b.CodTurmaAluno, b.CodAvaliacao, b.Nota })
            .ToListAsync();

        var tiposPresenca = await db.TipoPresenca.AsNoTracking().ToDictionaryAsync(t => t.Cod, t => t.Valor);
        var presencas = await db.ListaPresenca.AsNoTracking()
            .Where(l => l.TurmaAluno.CodAluno == alunoCod)
            .Select(l => new { l.CodTurmaAluno, l.DataPresenca, l.CodTipoPresenca, l.Observacao })
            .ToListAsync();

        var boletim = new List<BoletimTurmaResponse>();
        foreach (var turma in turmas)
        {
            var matricula = matriculas[turma.Cod];
            var notaPorAvaliacao = notas.Where(n => n.CodTurmaAluno == matricula)
                .ToDictionary(n => n.CodAvaliacao, n => (double)n.Nota);

            var notasDaTurma = (await consultas.AvaliacoesDaTurmaAsync(turma.Cod))
                .Select(a => new NotaDoAlunoResponse(a.Cod, a.Nome, a.TipoNota, a.DataAvaliacao,
                    notaPorAvaliacao.TryGetValue(a.Cod, out var nota) ? nota : null))
                .ToList();

            var presencasDaTurma = presencas.Where(l => l.CodTurmaAluno == matricula)
                .OrderByDescending(l => l.DataPresenca)
                .Select(l => new PresencaDoAlunoResponse(
                    DateOnly.FromDateTime(l.DataPresenca), tiposPresenca[l.CodTipoPresenca], l.Observacao))
                .ToList();

            boletim.Add(new BoletimTurmaResponse(
                turma,
                notasDaTurma,
                Regras.Media(notasDaTurma.Where(n => n.Nota is not null).Select(n => n.Nota!.Value)),
                presencasDaTurma,
                Regras.Frequencia(presencasDaTurma.Select(x => x.Tipo))));
        }

        return new BoletimAlunoResponse(alunoCod, nome, boletim);
    }
}
