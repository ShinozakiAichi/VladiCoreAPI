using System.Collections.Generic;

namespace VladiCore.Domain.DTOs;

public class AutoBuildResponse
{
    public IDictionary<string, int> Parts { get; set; } = new Dictionary<string, int>();

    public decimal Total { get; set; }

    public int RequiredPsuWattage { get; set; }

    public IList<string> Rationale { get; set; } = new List<string>();

    public IList<PriceChartSeriesDto> PriceCharts { get; set; } = new List<PriceChartSeriesDto>();
}
