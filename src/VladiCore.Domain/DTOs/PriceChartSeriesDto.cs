using System.Collections.Generic;

namespace VladiCore.Domain.DTOs;

public class PriceChartSeriesDto
{
    public string PartType { get; set; } = string.Empty;

    public IList<PricePointDto> Series { get; set; } = new List<PricePointDto>();
}
