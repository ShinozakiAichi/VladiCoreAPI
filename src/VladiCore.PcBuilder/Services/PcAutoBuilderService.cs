using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using VladiCore.Data.Contexts;
using VladiCore.Domain.DTOs;
using VladiCore.Domain.Entities;
using VladiCore.Domain.Enums;
using VladiCore.Recommendations.Services;

namespace VladiCore.PcBuilder.Services
{
    /// <summary>
    /// Builds PC configurations based on greedy perf/price heuristics.
    /// </summary>
    public class PcAutoBuilderService : IPcAutoBuilderService
    {
        private readonly VladiCoreContext _context;
        private readonly IPcCompatibilityService _compatibilityService;
        private readonly IPriceHistoryService _priceHistoryService;

        public PcAutoBuilderService(
            VladiCoreContext context,
            IPcCompatibilityService compatibilityService,
            IPriceHistoryService priceHistoryService)
        {
            _context = context;
            _compatibilityService = compatibilityService;
            _priceHistoryService = priceHistoryService;
        }

        public async Task<AutoBuildResponse> BuildAsync(AutoBuildRequest request)
        {
            var components = LoadComponentProducts();
            var budget = request.Budget;

            var cpuOptions = FilterByPlatform(components["cpu"], request.Platform)
                .OrderByDescending(o => o.Score)
                .Take(8)
                .ToList();
            var gpuOptions = components["gpu"].OrderByDescending(o => o.Score).Take(8).ToList();
            var ramOptions = components["ram"].OrderByDescending(o => o.Score).Take(8).ToList();
            var storageOptions = components["storage"].OrderByDescending(o => o.Score).Take(8).ToList();
            var motherboardOptions = components["motherboard"].OrderByDescending(o => o.Score).Take(8).ToList();
            var psuOptions = components["psu"].OrderByDescending(o => o.Score).Take(8).ToList();
            var caseOptions = components["case"].OrderByDescending(o => o.Score).Take(8).ToList();
            var coolerOptions = components["cooler"].OrderByDescending(o => o.Score).Take(8).ToList();

            var priorities = BuildPriorityWeights(request.Priorities);

            var bestBuild = await EvaluateBuildsAsync(
                cpuOptions,
                gpuOptions,
                ramOptions,
                storageOptions,
                motherboardOptions,
                psuOptions,
                caseOptions,
                coolerOptions,
                budget,
                priorities);

            if (bestBuild == null)
            {
                throw new InvalidOperationException("Cannot build a compatible PC within the provided budget.");
            }

            var validation = await _compatibilityService.ValidateAsync(new PcValidateRequest
            {
                CpuId = bestBuild.Cpu.ComponentId,
                MotherboardId = bestBuild.Motherboard.ComponentId,
                RamId = bestBuild.Ram.ComponentId,
                GpuId = bestBuild.Gpu.ComponentId,
                PsuId = bestBuild.Psu.ComponentId,
                CaseId = bestBuild.Case.ComponentId,
                CoolerId = bestBuild.Cooler?.ComponentId,
                StorageIds = bestBuild.Storages.Select(s => s.ComponentId).ToList()
            });

            var response = new AutoBuildResponse
            {
                Total = bestBuild.TotalPrice,
                RequiredPsuWattage = bestBuild.RequiredWattage,
                Parts = new Dictionary<string, int>
                {
                    { "cpu", bestBuild.Cpu.ProductId },
                    { "motherboard", bestBuild.Motherboard.ProductId },
                    { "ram", bestBuild.Ram.ProductId },
                    { "gpu", bestBuild.Gpu.ProductId },
                    { "psu", bestBuild.Psu.ProductId },
                    { "case", bestBuild.Case.ProductId }
                },
                Rationale = BuildRationale(bestBuild, priorities)
            };

            if (bestBuild.Cooler != null)
            {
                response.Parts.Add("cooler", bestBuild.Cooler.ProductId);
            }

            var storageIndex = 1;
            foreach (var storage in bestBuild.Storages)
            {
                response.Parts.Add($"storage{storageIndex}", storage.ProductId);
                storageIndex++;
            }

            response.PriceCharts = await BuildChartsAsync(bestBuild);

            if (!validation.IsCompatible)
            {
                foreach (var issue in validation.Issues)
                {
                    response.Rationale.Add($"Compatibility issue: [{issue.Level}] {issue.Code} - {issue.Message}");
                }
            }

            return response;
        }

        private async Task<IList<PriceChartSeriesDto>> BuildChartsAsync(BestBuild build)
        {
            var now = DateTime.UtcNow;
            var from = now.AddDays(-30);
            var charts = new List<PriceChartSeriesDto>();

            foreach (var part in new[]
            {
                ("cpu", build.Cpu.ProductId),
                ("gpu", build.Gpu.ProductId),
                ("ram", build.Ram.ProductId)
            })
            {
                var series = await _priceHistoryService.GetSeriesAsync(part.Item2, from, now, "day");
                charts.Add(new PriceChartSeriesDto { PartType = part.Item1, Series = series.ToList() });
            }

            return charts;
        }

        private async Task<BestBuild> EvaluateBuildsAsync(
            IList<ComponentOption> cpuOptions,
            IList<ComponentOption> gpuOptions,
            IList<ComponentOption> ramOptions,
            IList<ComponentOption> storageOptions,
            IList<ComponentOption> motherboardOptions,
            IList<ComponentOption> psuOptions,
            IList<ComponentOption> caseOptions,
            IList<ComponentOption> coolerOptions,
            int budget,
            IDictionary<string, double> priorities)
        {
            BestBuild best = null;

            foreach (var cpu in cpuOptions)
            {
                var compatibleMotherboards = motherboardOptions
                    .Where(m => string.Equals(((Motherboard)m.Entity).Socket, ((Cpu)cpu.Entity).Socket, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var motherboard in compatibleMotherboards)
                {
                    foreach (var gpu in gpuOptions)
                    {
                        foreach (var ram in ramOptions)
                        {
                            var storages = storageOptions.Take(2).ToList();
                            var psu = SelectBestPsu(psuOptions, cpu, gpu);
                            var pcCase = SelectBestCase(caseOptions, gpu);
                            var cooler = SelectCooler(coolerOptions, cpu, pcCase);

                            var total = cpu.Price + motherboard.Price + gpu.Price + ram.Price + storages.Sum(s => s.Price) + psu.Price + pcCase.Price + (cooler?.Price ?? 0m);
                            if (total > budget)
                            {
                                continue;
                            }

                            var request = new PcValidateRequest
                            {
                                CpuId = cpu.ComponentId,
                                MotherboardId = motherboard.ComponentId,
                                RamId = ram.ComponentId,
                                GpuId = gpu.ComponentId,
                                PsuId = psu.ComponentId,
                                CaseId = pcCase.ComponentId,
                                CoolerId = cooler?.ComponentId,
                                StorageIds = storages.Select(s => s.ComponentId).ToList()
                            };

                            var validation = await _compatibilityService.ValidateAsync(request);
                            if (!validation.IsCompatible)
                            {
                                continue;
                            }

                            var score = ComputeScore(cpu, gpu, ram, storages, priorities);
                            var requiredWattage = EstimatePowerRequirement((Cpu)cpu.Entity, (Gpu)gpu.Entity);

                            if (best == null || score > best.Score)
                            {
                                best = new BestBuild
                                {
                                    Cpu = cpu,
                                    Motherboard = motherboard,
                                    Gpu = gpu,
                                    Ram = ram,
                                    Storages = storages,
                                    Psu = psu,
                                    Case = pcCase,
                                    Cooler = cooler,
                                    TotalPrice = total,
                                    Score = score,
                                    RequiredWattage = requiredWattage
                                };
                            }
                        }
                    }
                }
            }

            return best;
        }

        private static List<string> BuildRationale(BestBuild build, IDictionary<string, double> priorities)
        {
            return new List<string>
            {
                $"Total price: {build.TotalPrice:C}",
                $"Composite performance score: {build.Score:F2}",
                $"Required PSU wattage: {build.RequiredWattage}W",
                $"Applied priority weights: {string.Join(", ", priorities.Select(p => $"{p.Key}={p.Value:F1}"))}"
            };
        }

        private static ComponentOption SelectBestCase(IList<ComponentOption> cases, ComponentOption gpu)
        {
            foreach (var candidate in cases)
            {
                var caseEntity = (Domain.Entities.Case)candidate.Entity;
                var gpuEntity = (Gpu)gpu.Entity;
                if (gpuEntity.LengthMm <= caseEntity.GpuMaxLengthMm)
                {
                    return candidate;
                }
            }

            return cases.First();
        }

        private static ComponentOption SelectCooler(IList<ComponentOption> coolers, ComponentOption cpu, ComponentOption pcCase)
        {
            var caseEntity = (Domain.Entities.Case)pcCase.Entity;
            var cpuEntity = (Cpu)cpu.Entity;
            foreach (var option in coolers)
            {
                var cooler = (Cooler)option.Entity;
                if (cooler.HeightMm > caseEntity.CoolerMaxHeightMm)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(cooler.SocketSupport))
                {
                    var sockets = JsonConvert.DeserializeObject<List<string>>(cooler.SocketSupport) ?? new List<string>();
                    if (!sockets.Any(s => string.Equals(s, cpuEntity.Socket, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }
                }

                return option;
            }

            return coolers.FirstOrDefault();
        }

        private static ComponentOption SelectBestPsu(IList<ComponentOption> psus, ComponentOption cpu, ComponentOption gpu)
        {
            var required = EstimatePowerRequirement((Cpu)cpu.Entity, (Gpu)gpu.Entity);
            return psus
                .OrderBy(p => ((Psu)p.Entity).Wattage >= required ? p.Price : decimal.MaxValue)
                .ThenBy(p => Math.Abs(((Psu)p.Entity).Wattage - required))
                .First();
        }

        private static double ComputeScore(ComponentOption cpu, ComponentOption gpu, ComponentOption ram, IList<ComponentOption> storages, IDictionary<string, double> priorities)
        {
            return cpu.Score * GetWeight(priorities, "cpu")
                   + gpu.Score * GetWeight(priorities, "gpu")
                   + ram.Score * GetWeight(priorities, "ram")
                   + storages.Sum(s => s.Score) * GetWeight(priorities, "storage");
        }

        private static double GetWeight(IDictionary<string, double> weights, string key)
        {
            return weights.TryGetValue(key, out var value) ? value : 1d;
        }

        private static int EstimatePowerRequirement(Cpu cpu, Gpu gpu)
        {
            var sumTdp = (cpu?.Tdp ?? 0) + (gpu?.Tdp ?? 0) + 50;
            return (int)Math.Ceiling(sumTdp * 1.5);
        }

        private static IDictionary<string, double> BuildPriorityWeights(IList<string> priorities)
        {
            var weights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                { "gpu", 5d },
                { "cpu", 4d },
                { "ram", 2d },
                { "storage", 2d }
            };

            foreach (var priority in priorities ?? Array.Empty<string>())
            {
                switch (priority?.ToLowerInvariant())
                {
                    case "gaming":
                        weights["gpu"] += 2;
                        weights["cpu"] += 1;
                        break;
                    case "office":
                        weights["cpu"] += 2;
                        weights["storage"] += 1;
                        break;
                    case "silent":
                        weights["gpu"] -= 1;
                        weights["cpu"] -= 0.5;
                        break;
                }
            }

            return weights;
        }

        private Dictionary<string, List<ComponentOption>> LoadComponentProducts()
        {
            var products = _context.Products.ToList();
            var map = new Dictionary<string, List<ComponentOption>>(StringComparer.OrdinalIgnoreCase)
            {
                { "cpu", new List<ComponentOption>() },
                { "motherboard", new List<ComponentOption>() },
                { "ram", new List<ComponentOption>() },
                { "gpu", new List<ComponentOption>() },
                { "psu", new List<ComponentOption>() },
                { "case", new List<ComponentOption>() },
                { "cooler", new List<ComponentOption>() },
                { "storage", new List<ComponentOption>() }
            };

            foreach (var product in products)
            {
                if (string.IsNullOrWhiteSpace(product.Attributes))
                {
                    continue;
                }

                try
                {
                    var attributes = JsonConvert.DeserializeObject<ComponentAttributes>(product.Attributes);
                    if (attributes?.ComponentType == null)
                    {
                        continue;
                    }

                    var option = CreateOption(product, attributes);
                    if (option != null)
                    {
                        map[attributes.ComponentType.ToLowerInvariant()].Add(option);
                    }
                }
                catch (JsonException)
                {
                    // Ignore malformed attributes to keep the builder resilient to dirty data.
                }
            }

            return map;
        }

        private ComponentOption CreateOption(Product product, ComponentAttributes attributes)
        {
            switch (attributes.ComponentType?.ToLowerInvariant())
            {
                case "cpu":
                    var cpu = _context.Cpus.Find(attributes.ComponentId);
                    return cpu == null ? null : new ComponentOption(product, attributes.ComponentId, attributes.ComponentType, cpu.PerfScore, cpu);
                case "motherboard":
                    var motherboard = _context.Motherboards.Find(attributes.ComponentId);
                    return motherboard == null ? null : new ComponentOption(product, attributes.ComponentId, attributes.ComponentType, 1, motherboard);
                case "ram":
                    var ram = _context.Rams.Find(attributes.ComponentId);
                    return ram == null ? null : new ComponentOption(product, attributes.ComponentId, attributes.ComponentType, ram.PerfScore, ram);
                case "gpu":
                    var gpu = _context.Gpus.Find(attributes.ComponentId);
                    return gpu == null ? null : new ComponentOption(product, attributes.ComponentId, attributes.ComponentType, gpu.PerfScore, gpu);
                case "psu":
                    var psu = _context.Psus.Find(attributes.ComponentId);
                    return psu == null ? null : new ComponentOption(product, attributes.ComponentId, attributes.ComponentType, psu.Wattage, psu);
                case "case":
                    var pcCase = _context.Cases.Find(attributes.ComponentId);
                    return pcCase == null ? null : new ComponentOption(product, attributes.ComponentId, attributes.ComponentType, pcCase.GpuMaxLengthMm, pcCase);
                case "cooler":
                    var cooler = _context.Coolers.Find(attributes.ComponentId);
                    return cooler == null ? null : new ComponentOption(product, attributes.ComponentId, attributes.ComponentType, cooler.HeightMm, cooler);
                case "storage":
                    var storage = _context.Storages.Find(attributes.ComponentId);
                    if (storage == null)
                    {
                        return null;
                    }

                    var storageScore = storage.PerfScore + (storage.Type == StorageType.Nvme ? 10 : 0);
                    return new ComponentOption(product, attributes.ComponentId, attributes.ComponentType, storageScore, storage);
                default:
                    return null;
            }
        }

        private static IList<ComponentOption> FilterByPlatform(IEnumerable<ComponentOption> options, string platform)
        {
            if (string.IsNullOrWhiteSpace(platform))
            {
                return options.ToList();
            }

            platform = platform.ToLowerInvariant();
            return options.Where(o =>
            {
                var cpu = o.Entity as Cpu;
                if (cpu == null)
                {
                    return false;
                }

                return platform == "intel"
                    ? cpu.Socket.StartsWith("LGA", StringComparison.OrdinalIgnoreCase)
                    : cpu.Socket.StartsWith("AM", StringComparison.OrdinalIgnoreCase);
            }).ToList();
        }

        private class ComponentOption
        {
            public ComponentOption(Product product, int componentId, string partType, int perfScore, object entity)
            {
                ProductId = product.Id;
                ComponentId = componentId;
                PartType = partType;
                Entity = entity;
                Price = product.Price;
                Score = perfScore / (double)Math.Max(1m, product.Price);
            }

            public int ProductId { get; }
            public int ComponentId { get; }
            public string PartType { get; }
            public object Entity { get; }
            public decimal Price { get; }
            public double Score { get; }
        }

        private class ComponentAttributes
        {
            [JsonProperty("componentType")]
            public string ComponentType { get; set; }

            [JsonProperty("componentId")]
            public int ComponentId { get; set; }
        }

        private class BestBuild
        {
            public ComponentOption Cpu { get; set; }
            public ComponentOption Motherboard { get; set; }
            public ComponentOption Ram { get; set; }
            public ComponentOption Gpu { get; set; }
            public ComponentOption Psu { get; set; }
            public ComponentOption Case { get; set; }
            public ComponentOption Cooler { get; set; }
            public IList<ComponentOption> Storages { get; set; }
            public decimal TotalPrice { get; set; }
            public double Score { get; set; }
            public int RequiredWattage { get; set; }
        }
    }
}
