namespace EscolaPlus.Frequencia.Api.Domain.Services;

public static class FrequenciaCalculator
{
    public static int CalcularFaltasConsecutivas(IEnumerable<EscolaPlus.Frequencia.Api.Domain.Frequencia> frequencias, DateOnly dataInicio)
    {
        var registrosOrdenados = frequencias
            .Where(f => f.Data >= dataInicio)
            .OrderBy(f => f.Data)
            .ToList();

        int faltasConsecutivas = 0;
        int maxFaltasConsecutivas = 0;

        foreach (var registro in registrosOrdenados)
        {
            if (registro.Status == EscolaPlus.Frequencia.Api.Domain.StatusFrequencia.FALTA)
            {
                faltasConsecutivas++;
                if (faltasConsecutivas > maxFaltasConsecutivas)
                    maxFaltasConsecutivas = faltasConsecutivas;
            }
            else
            {
                faltasConsecutivas = 0;
            }
        }

        return maxFaltasConsecutivas;
    }

    public static double CalcularTaxaFrequencia(IEnumerable<EscolaPlus.Frequencia.Api.Domain.Frequencia> frequencias, DateOnly de, DateOnly ate)
    {
        var registros = frequencias.Where(f => f.Data >= de && f.Data <= ate).ToList();
        
        if (!registros.Any()) 
            return 0.0;

        int totalDiasLetivos = registros.Count;
        int presencasEAtrasos = registros.Count(f => 
            f.Status == EscolaPlus.Frequencia.Api.Domain.StatusFrequencia.PRESENTE || 
            f.Status == EscolaPlus.Frequencia.Api.Domain.StatusFrequencia.ATRASO);

        return Math.Round((double)presencasEAtrasos / totalDiasLetivos * 100, 2);
    }
}