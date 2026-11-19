using BoletimPlus.Api.Dominio;

namespace BoletimPlus.Api.Endpoints;

public static class Datas
{
    /// <summary>Parâmetro ?data= do contrato. Formato diferente de YYYY-MM-DD responde 400.</summary>
    public static DateOnly Ler(string? data) =>
        Regras.LerData(data) ?? throw RegraException.Invalido("Data inválida: use o formato YYYY-MM-DD.");
}
