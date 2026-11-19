namespace BoletimPlus.Api.Dados;

/// <summary>Toda tabela do DDL tem DATA_CRIACAO. O DbContext preenche no INSERT.</summary>
public interface IComDataCriacao
{
    DateTime DataCriacao { get; set; }
}

/// <summary>Tabelas de enumerador: COD, ENUMERADOR, DATA_CRIACAO.</summary>
public abstract class Enumerador : IComDataCriacao
{
    public int Cod { get; set; }
    public string Valor { get; set; } = "";
    public DateTime DataCriacao { get; set; }
}

public class StatusUsuario : Enumerador;

public class TipoParentesco : Enumerador;

public class TipoCurso : Enumerador;

public class TipoNota : Enumerador;

public class TipoPresenca : Enumerador;

public class Pessoa : IComDataCriacao
{
    public int Cod { get; set; }
    public string Nome { get; set; } = "";
    public string NumDocumento { get; set; } = "";
    public DateTime DataCriacao { get; set; }
}

public class Usuario : IComDataCriacao
{
    public int Cod { get; set; }
    public int CodStatus { get; set; }
    public int CodPessoa { get; set; }
    public string SenhaHash { get; set; } = "";
    public DateTime DataCriacao { get; set; }

    public StatusUsuario Status { get; set; } = null!;
    public Pessoa Pessoa { get; set; } = null!;
}

/// <summary>Papel de uma Pessoa: Diretor, Professor, Guardiao e Aluno têm a mesma forma.</summary>
public abstract class Papel : IComDataCriacao
{
    public int Cod { get; set; }
    public int CodPessoa { get; set; }
    public DateTime DataCriacao { get; set; }

    public Pessoa Pessoa { get; set; } = null!;
}

public class Diretor : Papel;

public class Professor : Papel;

public class Guardiao : Papel;

public class Aluno : Papel;

public class AlunoGuardiao : IComDataCriacao
{
    public int Cod { get; set; }
    public int CodGuardiao { get; set; }
    public int CodAluno { get; set; }
    public int CodTipoParentesco { get; set; }
    public DateTime DataCriacao { get; set; }
}

public class Escola : IComDataCriacao
{
    public int Cod { get; set; }
    public int CodDiretor { get; set; }
    public string Nome { get; set; } = "";
    public DateTime DataCriacao { get; set; }

    public Diretor Diretor { get; set; } = null!;
    public List<Turma> Turmas { get; set; } = [];
}

public class Turma : IComDataCriacao
{
    public int Cod { get; set; }
    public int CodEscola { get; set; }
    public int CodTipoCurso { get; set; }
    public int CodProfessor { get; set; }
    public int SemestreReferencia { get; set; }
    public int AnoReferencia { get; set; }
    public DateTime DataCriacao { get; set; }

    public Escola Escola { get; set; } = null!;
    public TipoCurso TipoCurso { get; set; } = null!;
    public Professor Professor { get; set; } = null!;
    public List<TurmaAluno> Alunos { get; set; } = [];
    public List<Avaliacao> Avaliacoes { get; set; } = [];
}

public class TurmaAluno : IComDataCriacao
{
    public int Cod { get; set; }
    public int CodTurma { get; set; }
    public int CodAluno { get; set; }
    public DateTime DataCriacao { get; set; }

    public Turma Turma { get; set; } = null!;
    public Aluno Aluno { get; set; } = null!;
}

public class Avaliacao : IComDataCriacao
{
    public int Cod { get; set; }
    public int CodTurma { get; set; }
    public int CodTipoNota { get; set; }
    public string Nome { get; set; } = "";
    public DateTime? DataAvaliacao { get; set; }
    public DateTime DataCriacao { get; set; }

    public Turma Turma { get; set; } = null!;
    public TipoNota TipoNota { get; set; } = null!;
    public List<Boletim> Notas { get; set; } = [];
}

public class Boletim : IComDataCriacao
{
    public int Cod { get; set; }
    public int CodTurmaAluno { get; set; }
    public int CodAvaliacao { get; set; }
    public decimal Nota { get; set; }
    public DateTime DataCriacao { get; set; }

    public TurmaAluno TurmaAluno { get; set; } = null!;
    public Avaliacao Avaliacao { get; set; } = null!;
}

public class ListaPresenca : IComDataCriacao
{
    public int Cod { get; set; }
    public int CodTurmaAluno { get; set; }
    public int CodTipoPresenca { get; set; }
    public string? Observacao { get; set; }
    public DateTime DataPresenca { get; set; }
    public DateTime DataCriacao { get; set; }

    public TurmaAluno TurmaAluno { get; set; } = null!;
}
