using BoletimPlus.Api.Contratos;
using BoletimPlus.Api.Dados;
using BoletimPlus.Api.Dominio;
using BoletimPlus.Api.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace BoletimPlus.Api.Servicos;

/// <summary>
/// Conta: login, cadastro de responsável, troca de senha e de nome. Mesmas regras, mensagens e
/// status do AuthService do Java. A senha é conferida com bcrypt ($2a$, compatível com o
/// BCryptPasswordEncoder do Spring) e o token sai igual ao do Java.
/// </summary>
public class ContaServico(BoletimDbContext db, GeradorToken gerador)
{
    public const int CustoBcrypt = 10; // força padrão do BCryptPasswordEncoder

    /// <summary>CPF ou senha errados: 401. Usuário não Ativo: 403.</summary>
    public async Task<SessaoResponse> EntrarAsync(LoginRequest req)
    {
        RegraException.SeHouver(
            Regras.ValidarCpfFormato(req.NumDocumento),
            Regras.ValidarInformado(req.Senha, "Informe a senha."));

        var usuario = await db.Usuario.AsNoTracking()
            .Include(u => u.Pessoa)
            .Include(u => u.Status)
            .FirstOrDefaultAsync(u => u.Pessoa.NumDocumento == req.NumDocumento);
        if (usuario is null || !SenhaConfere(req.Senha!, usuario.SenhaHash))
            throw new RegraException(StatusCodes.Status401Unauthorized, "CPF ou senha inválidos.");
        if (!EstaAtivo(usuario.Status.Valor))
            throw RegraException.Proibido($"Usuário {usuario.Status.Valor.ToLowerInvariant()}. Procure a secretaria.");

        return await SessaoAsync(usuario.Cod, usuario.Pessoa);
    }

    /// <summary>Uma transação (um SaveChanges): Pessoa + Usuario ATIVO + Guardiao. CPF repetido: 409.</summary>
    public async Task<SessaoResponse> CadastrarAsync(CadastroRequest req)
    {
        RegraException.SeHouver(
            Regras.ValidarNome(req.Nome),
            Regras.ValidarCpfFormato(req.NumDocumento),
            Regras.ValidarSenhaNova(req.Senha, "Informe a senha."));

        if (await db.Pessoa.AnyAsync(p => p.NumDocumento == req.NumDocumento))
            throw RegraException.Conflito("Este CPF já tem cadastro. Entre com sua senha.");

        var pessoa = new Pessoa { Nome = req.Nome!.Trim(), NumDocumento = req.NumDocumento! };
        var usuario = new Usuario
        {
            Pessoa = pessoa,
            CodStatus = await CodStatusAtivoAsync(),
            SenhaHash = BCrypt.Net.BCrypt.HashPassword(req.Senha, workFactor: CustoBcrypt),
        };
        db.Usuario.Add(usuario);
        db.Guardiao.Add(new Guardiao { Pessoa = pessoa });
        await db.SaveChangesAsync();

        return await SessaoAsync(usuario.Cod, pessoa);
    }

    /// <summary>Senha atual errada responde 400, não 401: o app trata 401 como sessão vencida.</summary>
    public async Task AlterarSenhaAsync(Papeis p, AlterarSenhaRequest req)
    {
        RegraException.SeHouver(
            Regras.ValidarInformado(req.SenhaAtual, "Informe a senha atual."),
            Regras.ValidarSenhaNova(req.NovaSenha, "Informe a nova senha."));

        var usuario = await db.Usuario.FirstOrDefaultAsync(u => u.Cod == p.UsuarioCod)
                      ?? throw new RegraException(StatusCodes.Status401Unauthorized, "Sessão expirada. Entre de novo.");
        if (!SenhaConfere(req.SenhaAtual!, usuario.SenhaHash))
            throw RegraException.Invalido("A senha atual não confere.");
        if (req.NovaSenha == req.SenhaAtual)
            throw RegraException.Invalido("A nova senha precisa ser diferente da atual.");

        usuario.SenhaHash = BCrypt.Net.BCrypt.HashPassword(req.NovaSenha, workFactor: CustoBcrypt);
        await db.SaveChangesAsync();
    }

    /// <summary>Só diretor e responsável trocam o próprio nome (UPDATE Pessoa SET nome). O CPF não muda.</summary>
    public async Task<PessoaResponse> AlterarNomeAsync(Papeis p, RenomearRequest req)
    {
        if (p.DiretorCod is null && p.GuardiaoCod is null)
            throw RegraException.Proibido("Só diretor e responsável alteram o próprio nome.");
        RegraException.SeHouver(Regras.ValidarNome(req.Nome));

        var pessoa = await db.Pessoa.FirstOrDefaultAsync(x => x.Cod == p.PessoaCod)
                     ?? throw RegraException.NaoEncontrado("Pessoa não encontrada.");
        pessoa.Nome = req.Nome!.Trim();
        await db.SaveChangesAsync();
        return PessoaResponse.De(pessoa);
    }

    public static bool EstaAtivo(string status) => Regras.ChaveEnum(status) == "ATIVO";

    /// <summary>Hash inválido ou de outro formato conta como senha errada, não como erro 500.</summary>
    private static bool SenhaConfere(string senha, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(senha, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }

    private async Task<int> CodStatusAtivoAsync()
    {
        var status = await db.StatusUsuario.AsNoTracking().ToListAsync();
        return status.FirstOrDefault(s => EstaAtivo(s.Valor))?.Cod
               ?? throw new InvalidOperationException("StatusUsuario 'Ativo' não existe no banco.");
    }

    private async Task<SessaoResponse> SessaoAsync(int usuarioCod, Pessoa pessoa)
    {
        var (token, _) = gerador.Gerar(usuarioCod);
        return new SessaoResponse(token, usuarioCod, PessoaResponse.De(pessoa), await PapeisAsync(pessoa.Cod));
    }

    private async Task<PapeisResponse> PapeisAsync(int pessoaCod) => new(
        await CodDoPapelAsync(db.Diretor, pessoaCod),
        await CodDoPapelAsync(db.Professor, pessoaCod),
        await CodDoPapelAsync(db.Guardiao, pessoaCod),
        await CodDoPapelAsync(db.Aluno, pessoaCod));

    private static Task<int?> CodDoPapelAsync<T>(IQueryable<T> tabela, int pessoaCod) where T : Papel =>
        tabela.AsNoTracking().Where(x => x.CodPessoa == pessoaCod).Select(x => (int?)x.Cod).FirstOrDefaultAsync();
}
