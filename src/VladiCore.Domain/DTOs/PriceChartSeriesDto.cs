using System.Collections.Generic;

namespace VladiCore.Domain.DTOs
{
    public class PriceChartSeriesDto
    {
        public string PartType { get; set; }
        public IList<PricePointDto> Series { get; set; }

        public PriceChartSeriesDto()
        {
            Series = new List<PricePointDto>();
        }
    }
}
