using System.Collections.Generic;

namespace VladiCore.Domain.DTOs
{
    public class AutoBuildResponse
    {
        public IDictionary<string, int> Parts { get; set; }
        public decimal Total { get; set; }
        public int RequiredPsuWattage { get; set; }
        public IList<string> Rationale { get; set; }
        public IList<PriceChartSeriesDto> PriceCharts { get; set; }

        public AutoBuildResponse()
        {
            Parts = new Dictionary<string, int>();
            Rationale = new List<string>();
            PriceCharts = new List<PriceChartSeriesDto>();
        }
    }
}
