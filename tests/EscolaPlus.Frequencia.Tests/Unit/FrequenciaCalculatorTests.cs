using EscolaPlus.Frequencia.Api.Domain;
using EscolaPlus.Frequencia.Api.Domain.Services;
using Xunit;

namespace EscolaPlus.Frequencia.Tests.Unit;

public class FrequenciaCalculatorTests
{


    [Fact]
    public void FaltasConsecutivas_ListaVazia_DeveRetornarZero()
    {
        var frequencias = new List<EscolaPlus.Frequencia.Api.Domain.Frequencia>();
        var resultado = FrequenciaCalculator.CalcularFaltasConsecutivas(frequencias, new DateOnly(2026, 11, 1));
        Assert.Equal(0, resultado);
    }

    [Fact]
    public void FaltasConsecutivas_SemFaltas_DeveRetornarZero()
    {
        var frequencias = new List<EscolaPlus.Frequencia.Api.Domain.Frequencia>
        {
            new() { Data = new DateOnly(2026, 11, 1), Status = StatusFrequencia.PRESENTE },
            new() { Data = new DateOnly(2026, 11, 2), Status = StatusFrequencia.ATRASO }
        };
        var resultado = FrequenciaCalculator.CalcularFaltasConsecutivas(frequencias, new DateOnly(2026, 11, 1));
        Assert.Equal(0, resultado);
    }

    [Fact]
    public void FaltasConsecutivas_InterrompidasPorPresenca_DeveContarMaiorSequencia()
    {
        var frequencias = new List<EscolaPlus.Frequencia.Api.Domain.Frequencia>
        {
            new() { Data = new DateOnly(2026, 11, 1), Status = StatusFrequencia.FALTA },
            new() { Data = new DateOnly(2026, 11, 2), Status = StatusFrequencia.FALTA }, 
            new() { Data = new DateOnly(2026, 11, 3), Status = StatusFrequencia.PRESENTE }, 
            new() { Data = new DateOnly(2026, 11, 4), Status = StatusFrequencia.FALTA },
            new() { Data = new DateOnly(2026, 11, 5), Status = StatusFrequencia.FALTA },
            new() { Data = new DateOnly(2026, 11, 6), Status = StatusFrequencia.FALTA } 
        };
        
        var resultado = FrequenciaCalculator.CalcularFaltasConsecutivas(frequencias, new DateOnly(2026, 11, 1));
        Assert.Equal(3, resultado);
    }


    [Fact]
    public void TaxaFrequencia_PeriodoVazio_DeveRetornarZero()
    {
        var frequencias = new List<EscolaPlus.Frequencia.Api.Domain.Frequencia>();
        var resultado = FrequenciaCalculator.CalcularTaxaFrequencia(frequencias, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 5));
        Assert.Equal(0.0, resultado);
    }

    [Fact]
    public void TaxaFrequencia_100PorCento_DeveRetornar100()
    {
        var frequencias = new List<EscolaPlus.Frequencia.Api.Domain.Frequencia>
        {
            new() { Data = new DateOnly(2026, 11, 1), Status = StatusFrequencia.PRESENTE },
            new() { Data = new DateOnly(2026, 11, 2), Status = StatusFrequencia.ATRASO }
        };
        var resultado = FrequenciaCalculator.CalcularTaxaFrequencia(frequencias, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 5));
        Assert.Equal(100.0, resultado);
    }

    [Fact]
    public void TaxaFrequencia_50PorCento_DeveRetornar50()
    {
        var frequencias = new List<EscolaPlus.Frequencia.Api.Domain.Frequencia>
        {
            new() { Data = new DateOnly(2026, 11, 1), Status = StatusFrequencia.PRESENTE },
            new() { Data = new DateOnly(2026, 11, 2), Status = StatusFrequencia.FALTA }
        };
        var resultado = FrequenciaCalculator.CalcularTaxaFrequencia(frequencias, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 5));
        Assert.Equal(50.0, resultado);
    }
}