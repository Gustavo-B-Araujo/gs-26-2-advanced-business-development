using BoletimPlus.Api.Contratos;
using BoletimPlus.Api.Dados;
using BoletimPlus.Api.Dominio;
using BoletimPlus.Api.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace BoletimPlus.Api.Servicos;

/// <summary>Sala de aula: detalhe da turma, avaliações, notas (Boletim) e chamada (ListaPresenca).</summary>
public class SalaDeAulaServico(BoletimDbContext db, Consultas consultas, TimeProvider relogio)
{
    public async Task<TurmaDetalhe> ObterTurmaAsync(Papeis p, int turmaCod)
    {
        await consultas.ExigirLeituraDaTurmaAsync(p, turmaCod);
        return TurmaDetalhe.De(
            await consultas.TurmaResumoAsync(turmaCod),
            await consultas.AlunosDaTurmaAsync(turmaCod),
            await consultas.AvaliacoesDaTurmaAsync(turmaCod));
    }

    // --- Avaliações --------------------------------------------------------

    public async Task<int> CriarAvaliacaoAsync(Papeis p, int turmaCod, DadosAvaliacaoRequest req)
    {
        await consultas.ExigirProfessorDaTurmaAsync(p, turmaCod);
        var nome = await ValidarAvaliacaoAsync(turmaCod, req, ignorarCod: null);
        var avaliacao = new Avaliacao
        {
            CodTurma = turmaCod,
            CodTipoNota = req.CodTipoNota!.Value,
            Nome = nome,
            DataAvaliacao = req.DataAvaliacao is { } d ? Consultas.ParaDateTime(d) : null,
        };
        db.Avaliacao.Add(avaliacao);
        await db.SaveChangesAsync();
        return avaliacao.Cod;
    }

    public async Task<AvaliacaoDetalhe> ObterAvaliacaoAsync(Papeis p, int avaliacaoCod)
    {
        var avaliacao = await BuscarAvaliacaoAsync(avaliacaoCod);
        await consultas.ExigirLeituraDaTurmaAsync(p, avaliacao.CodTurma);

        var resumo = (await consultas.AvaliacoesDaTurmaAsync(avaliacao.CodTurma)).First(a => a.Cod == avaliacaoCod);
        var notas = await db.Boletim.AsNoTracking()
            .Where(b => b.CodAvaliacao == avaliacaoCod)
            .ToDictionaryAsync(b => b.CodTurmaAluno, b => b.Nota);
        var alunos = await consultas.AlunosDaTurmaAsync(avaliacao.CodTurma);

        return new AvaliacaoDetalhe(
            resumo.Cod, resumo.Nome, resumo.CodTipoNota, resumo.TipoNota, resumo.DataAvaliacao, resumo.NotasLancadas,
            await consultas.TurmaResumoAsync(avaliacao.CodTurma),
            alunos.Select(a => new NotaLancada(
                a.TurmaAlunoCod,
                a.Nome,
                notas.TryGetValue(a.TurmaAlunoCod, out var n) ? n : null)).ToList());
    }

    public async Task AtualizarAvaliacaoAsync(Papeis p, int avaliacaoCod, DadosAvaliacaoRequest req)
    {
        var avaliacao = await db.Avaliacao.FirstOrDefaultAsync(a => a.Cod == avaliacaoCod)
                        ?? throw RegraException.NaoEncontrado("Avaliação não encontrada.");
        await consultas.ExigirProfessorDaTurmaAsync(p, avaliacao.CodTurma);
        avaliacao.Nome = await ValidarAvaliacaoAsync(avaliacao.CodTurma, req, ignorarCod: avaliacaoCod);
        avaliacao.CodTipoNota = req.CodTipoNota!.Value;
        avaliacao.DataAvaliacao = req.DataAvaliacao is { } d ? Consultas.ParaDateTime(d) : null;
        await db.SaveChangesAsync();
    }

    /// <summary>Apaga as notas (Boletim) antes da avaliação, por causa da FK cod_avaliacao. Um SaveChanges = uma transação.</summary>
    public async Task ExcluirAvaliacaoAsync(Papeis p, int avaliacaoCod)
    {
        var avaliacao = await db.Avaliacao.Include(a => a.Notas).FirstOrDefaultAsync(a => a.Cod == avaliacaoCod)
                        ?? throw RegraException.NaoEncontrado("Avaliação não encontrada.");
        await consultas.ExigirProfessorDaTurmaAsync(p, avaliacao.CodTurma);
        db.Boletim.RemoveRange(avaliacao.Notas);
        db.Avaliacao.Remove(avaliacao);
        await db.SaveChangesAsync();
    }

    /// <summary>Upsert por uq_boletim (cod_turma_aluno, cod_avaliacao). Nota nula apaga a linha.</summary>
    public async Task SalvarNotasAsync(Papeis p, int avaliacaoCod, NotasRequest req)
    {
        var avaliacao = await BuscarAvaliacaoAsync(avaliacaoCod);
        await consultas.ExigirProfessorDaTurmaAsync(p, avaliacao.CodTurma);

        var itens = req.Notas ?? [];
        var validos = await TurmaAlunosAsync(avaliacao.CodTurma);
        foreach (var item in itens)
        {
            if (!validos.Contains(item.TurmaAlunoCod)) throw RegraException.Invalido("Aluno fora da turma.");
            RegraException.SeHouver(Regras.ValidarNota(item.Nota));
        }

        var atuais = await db.Boletim.Where(b => b.CodAvaliacao == avaliacaoCod).ToDictionaryAsync(b => b.CodTurmaAluno);
        foreach (var item in itens.GroupBy(i => i.TurmaAlunoCod).Select(g => g.Last()))
        {
            atuais.TryGetValue(item.TurmaAlunoCod, out var atual);
            if (item.Nota is null)
            {
                if (atual is not null) db.Boletim.Remove(atual);
            }
            else if (atual is not null)
            {
                atual.Nota = Regras.ArredondarNota(item.Nota.Value);
            }
            else
            {
                db.Boletim.Add(new Boletim
                {
                    CodAvaliacao = avaliacaoCod,
                    CodTurmaAluno = item.TurmaAlunoCod,
                    Nota = Regras.ArredondarNota(item.Nota.Value),
                });
            }
        }
        await db.SaveChangesAsync();
    }

    // --- Chamada ------------------------------------------------------------

    public async Task<List<ItemChamada>> ObterChamadaAsync(Papeis p, int turmaCod, DateOnly data)
    {
        await consultas.ExigirLeituraDaTurmaAsync(p, turmaCod);
        var dia = Consultas.ParaDateTime(data);
        var registros = await db.ListaPresenca.AsNoTracking()
            .Where(l => l.TurmaAluno.CodTurma == turmaCod && l.DataPresenca == dia)
            .ToDictionaryAsync(l => l.CodTurmaAluno);
        var alunos = await consultas.AlunosDaTurmaAsync(turmaCod);
        return alunos.Select(a =>
        {
            registros.TryGetValue(a.TurmaAlunoCod, out var r);
            return new ItemChamada(a.TurmaAlunoCod, a.Nome, r?.CodTipoPresenca, r?.Observacao);
        }).ToList();
    }

    /// <summary>Upsert por uq_lista_presenca (cod_turma_aluno, data_presenca). Sem data futura (trigger).</summary>
    public async Task SalvarChamadaAsync(Papeis p, int turmaCod, DateOnly data, ChamadaRequest req)
    {
        await consultas.ExigirProfessorDaTurmaAsync(p, turmaCod);
        var hoje = DateOnly.FromDateTime(relogio.GetLocalNow().DateTime);
        RegraException.SeHouver(Regras.ValidarDataPresenca(data, hoje));

        var itens = req.Itens ?? [];
        var validos = await TurmaAlunosAsync(turmaCod);
        var tipos = await db.TipoPresenca.Select(t => t.Cod).ToListAsync();
        foreach (var item in itens)
        {
            if (!validos.Contains(item.TurmaAlunoCod)) throw RegraException.Invalido("Aluno fora da turma.");
            if (!tipos.Contains(item.CodTipoPresenca)) throw RegraException.Invalido("Tipo de presença inválido.");
            RegraException.SeHouver(Regras.ValidarObservacao(item.Observacao));
        }

        var dia = Consultas.ParaDateTime(data);
        var atuais = await db.ListaPresenca
            .Where(l => l.TurmaAluno.CodTurma == turmaCod && l.DataPresenca == dia)
            .ToDictionaryAsync(l => l.CodTurmaAluno);
        foreach (var item in itens.GroupBy(i => i.TurmaAlunoCod).Select(g => g.Last()))
        {
            var observacao = string.IsNullOrWhiteSpace(item.Observacao) ? null : item.Observacao.Trim();
            if (atuais.TryGetValue(item.TurmaAlunoCod, out var atual))
            {
                atual.CodTipoPresenca = item.CodTipoPresenca;
                atual.Observacao = observacao;
            }
            else
            {
                db.ListaPresenca.Add(new ListaPresenca
                {
                    CodTurmaAluno = item.TurmaAlunoCod,
                    CodTipoPresenca = item.CodTipoPresenca,
                    Observacao = observacao,
                    DataPresenca = dia,
                });
            }
        }
        await db.SaveChangesAsync();
    }

    // --- Apoio ---------------------------------------------------------------

    private async Task<Avaliacao> BuscarAvaliacaoAsync(int avaliacaoCod) =>
        await db.Avaliacao.AsNoTracking().FirstOrDefaultAsync(a => a.Cod == avaliacaoCod)
        ?? throw RegraException.NaoEncontrado("Avaliação não encontrada.");

    private async Task<HashSet<int>> TurmaAlunosAsync(int turmaCod) =>
        (await db.TurmaAluno.Where(ta => ta.CodTurma == turmaCod).Select(ta => ta.Cod).ToListAsync()).ToHashSet();

    /// <summary>Nome de 1 a 50, tipo de nota existente e uq_avaliacao (cod_turma, nome).</summary>
    private async Task<string> ValidarAvaliacaoAsync(int turmaCod, DadosAvaliacaoRequest req, int? ignorarCod)
    {
        RegraException.SeHouver(
            Regras.ValidarNomeAvaliacao(req.Nome),
            req.CodTipoNota is null ? "Informe o tipo de nota (codTipoNota)." : null);
        if (!await db.TipoNota.AnyAsync(t => t.Cod == req.CodTipoNota))
            throw RegraException.Invalido("Tipo de nota inválido.");

        var nome = req.Nome!.Trim();
        if (await db.Avaliacao.AnyAsync(a => a.CodTurma == turmaCod && a.Nome == nome && a.Cod != ignorarCod))
            throw RegraException.Conflito("Já existe uma avaliação com este nome nesta turma.");
        return nome;
    }
}
