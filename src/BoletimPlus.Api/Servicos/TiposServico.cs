using BoletimPlus.Api.Contratos;
using BoletimPlus.Api.Dados;
using BoletimPlus.Api.Dominio;
using Microsoft.EntityFrameworkCore;

namespace BoletimPlus.Api.Servicos;

/// <summary>Enumeradores (TipoPresenca, TipoNota, TipoCurso) e cadastro de curso.</summary>
public class TiposServico(BoletimDbContext db)
{
    public async Task<List<EnumeradorResponse>> ListarAsync<T>() where T : Enumerador =>
        await db.Set<T>().AsNoTracking()
            .OrderBy(e => e.Cod)
            .Select(e => new EnumeradorResponse(e.Cod, e.Valor, e.DataCriacao))
            .ToListAsync();

    /// <summary>
    /// Curso (matéria) vale para a rede toda. UNIQUE (enumerador), e comparando sem acento
    /// nem maiúscula para não ter "Historia" e "História" juntos.
    /// </summary>
    public async Task<EnumeradorResponse> CriarCursoAsync(NovoCursoRequest req)
    {
        RegraException.SeHouver(Regras.ValidarTipoCurso(req.Enumerador));
        var nome = req.Enumerador!.Trim();

        var chave = Regras.ChaveEnum(nome);
        var existentes = await db.TipoCurso.AsNoTracking().Select(t => t.Valor).ToListAsync();
        if (existentes.Any(e => Regras.ChaveEnum(e) == chave))
            throw RegraException.Conflito("Este curso já existe.");

        var curso = new TipoCurso { Valor = nome };
        db.TipoCurso.Add(curso);
        await db.SaveChangesAsync();
        return new EnumeradorResponse(curso.Cod, curso.Valor, curso.DataCriacao);
    }
}
