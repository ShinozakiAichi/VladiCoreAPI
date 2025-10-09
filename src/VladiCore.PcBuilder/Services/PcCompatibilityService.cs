using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using VladiCore.Data.Contexts;
using VladiCore.Domain.DTOs;
using VladiCore.Domain.Entities;
using VladiCore.Domain.Enums;
using VladiCore.Domain.ValueObjects;

namespace VladiCore.PcBuilder.Services
{
    /// <summary>
    /// Validates PC builds against compatibility rules and power requirements.
    /// </summary>
    public class PcCompatibilityService : IPcCompatibilityService
    {
        private readonly AppDbContext _context;

        public PcCompatibilityService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PcValidateResponse> ValidateAsync(PcValidateRequest request)
        {
            var response = new PcValidateResponse();

            var cpu = request.CpuId.HasValue ? await _context.Cpus.FindAsync(request.CpuId.Value) : null;
            var motherboard = request.MotherboardId.HasValue ? await _context.Motherboards.FindAsync(request.MotherboardId.Value) : null;
            var ram = request.RamId.HasValue ? await _context.Rams.FindAsync(request.RamId.Value) : null;
            var gpu = request.GpuId.HasValue ? await _context.Gpus.FindAsync(request.GpuId.Value) : null;
            var psu = request.PsuId.HasValue ? await _context.Psus.FindAsync(request.PsuId.Value) : null;
            var pcCase = request.CaseId.HasValue ? await _context.Cases.FindAsync(request.CaseId.Value) : null;
            var cooler = request.CoolerId.HasValue ? await _context.Coolers.FindAsync(request.CoolerId.Value) : null;
            var storages = request.StorageIds?.Any() == true
                ? await _context.Storages.Where(s => request.StorageIds.Contains(s.Id)).ToListAsync()
                : new List<Storage>();

            ValidateSockets(cpu, motherboard, response);
            ValidateRam(ram, motherboard, response);
            ValidateGpu(gpu, pcCase, response);
            ValidateCooler(cooler, cpu, pcCase, response);
            ValidatePower(cpu, gpu, psu, response);
            ValidateFormFactor(psu, pcCase, response);
            ValidateSlots(storages, motherboard, gpu, response);

            response.IsCompatible = response.Issues.All(i => !string.Equals(i.Level, "error", StringComparison.OrdinalIgnoreCase));
            return response;
        }

        private static void ValidateSockets(Cpu cpu, Motherboard motherboard, PcValidateResponse response)
        {
            if (cpu == null || motherboard == null)
            {
                return;
            }

            if (!string.Equals(cpu.Socket, motherboard.Socket, StringComparison.OrdinalIgnoreCase))
            {
                response.Issues.Add(new PcValidationIssueDto
                {
                    Level = "error",
                    Code = PcValidationCodes.CpuSocketMismatch,
                    Message = $"CPU socket {cpu.Socket} is incompatible with motherboard socket {motherboard.Socket}."
                });
            }
        }

        private static void ValidateRam(Ram ram, Motherboard motherboard, PcValidateResponse response)
        {
            if (ram == null || motherboard == null)
            {
                return;
            }

            if (!string.Equals(ram.Type, motherboard.RamType, StringComparison.OrdinalIgnoreCase))
            {
                response.Issues.Add(new PcValidationIssueDto
                {
                    Level = "error",
                    Code = PcValidationCodes.RamTypeMismatch,
                    Message = $"RAM type {ram.Type} does not match motherboard requirement {motherboard.RamType}."
                });
            }

            if (ram.Freq > motherboard.RamMaxFreq)
            {
                response.Issues.Add(new PcValidationIssueDto
                {
                    Level = "warn",
                    Code = PcValidationCodes.RamFreqTooHigh,
                    Message = $"RAM frequency {ram.Freq}MHz exceeds motherboard maximum {motherboard.RamMaxFreq}MHz."
                });
            }
        }

        private static void ValidateGpu(Gpu gpu, Domain.Entities.Case pcCase, PcValidateResponse response)
        {
            if (gpu == null || pcCase == null)
            {
                return;
            }

            if (gpu.LengthMm > pcCase.GpuMaxLengthMm)
            {
                response.Issues.Add(new PcValidationIssueDto
                {
                    Level = "error",
                    Code = PcValidationCodes.GpuTooLong,
                    Message = $"GPU length {gpu.LengthMm}mm exceeds case support {pcCase.GpuMaxLengthMm}mm."
                });
            }
        }

        private static void ValidateCooler(Cooler cooler, Cpu cpu, Domain.Entities.Case pcCase, PcValidateResponse response)
        {
            if (cooler == null)
            {
                return;
            }

            if (pcCase != null && cooler.HeightMm > pcCase.CoolerMaxHeightMm)
            {
                response.Issues.Add(new PcValidationIssueDto
                {
                    Level = "error",
                    Code = PcValidationCodes.CoolerTooTall,
                    Message = $"Cooler height {cooler.HeightMm}mm exceeds case limit {pcCase.CoolerMaxHeightMm}mm."
                });
            }

            if (cpu != null && !IsSocketSupported(cooler.SocketSupport, cpu.Socket))
            {
                response.Issues.Add(new PcValidationIssueDto
                {
                    Level = "error",
                    Code = PcValidationCodes.CpuSocketMismatch,
                    Message = $"Cooler does not support CPU socket {cpu.Socket}."
                });
            }
        }

        private static void ValidatePower(Cpu cpu, Gpu gpu, Psu psu, PcValidateResponse response)
        {
            if (psu == null)
            {
                return;
            }

            int cpuTdp = cpu?.Tdp ?? 0;
            int gpuTdp = gpu?.Tdp ?? 0;
            var sumTdp = cpuTdp + gpuTdp + 50;
            var required = (int)Math.Ceiling(sumTdp * 1.5);
            if (psu.Wattage < required)
            {
                response.Issues.Add(new PcValidationIssueDto
                {
                    Level = "error",
                    Code = PcValidationCodes.PsuTooWeak,
                    Message = $"PSU wattage {psu.Wattage}W is below required {required}W." 
                });
            }
        }

        private static void ValidateFormFactor(Psu psu, Domain.Entities.Case pcCase, PcValidateResponse response)
        {
            if (psu == null || pcCase == null)
            {
                return;
            }

            if (!string.Equals(psu.FormFactor, pcCase.PsuFormFactor, StringComparison.OrdinalIgnoreCase))
            {
                response.Issues.Add(new PcValidationIssueDto
                {
                    Level = "error",
                    Code = PcValidationCodes.PsuFormFactorMismatch,
                    Message = $"PSU form factor {psu.FormFactor} does not match case requirement {pcCase.PsuFormFactor}."
                });
            }
        }

        private static void ValidateSlots(IEnumerable<Storage> storages, Motherboard motherboard, Gpu gpu, PcValidateResponse response)
        {
            if (motherboard == null)
            {
                return;
            }

            var nvmeCount = storages.Count(s => s != null && s.Type == StorageType.Nvme);
            if (nvmeCount > motherboard.M2Slots)
            {
                response.Issues.Add(new PcValidationIssueDto
                {
                    Level = "error",
                    Code = PcValidationCodes.M2SlotsExceeded,
                    Message = $"Selected {nvmeCount} NVMe drives, but motherboard supports only {motherboard.M2Slots}."
                });
            }

            if (gpu != null && gpu.Slots > motherboard.PcieSlots)
            {
                response.Issues.Add(new PcValidationIssueDto
                {
                    Level = "error",
                    Code = PcValidationCodes.PcieSlotsExceeded,
                    Message = $"GPU requires {gpu.Slots} PCIe slots, motherboard has {motherboard.PcieSlots}."
                });
            }
        }

        private static bool IsSocketSupported(string? socketSupportJson, string socket)
        {
            if (string.IsNullOrWhiteSpace(socketSupportJson) || string.IsNullOrWhiteSpace(socket))
            {
                return true;
            }

            try
            {
                var supported = JsonConvert.DeserializeObject<List<string>>(socketSupportJson);
                return supported?.Any(s => string.Equals(s, socket, StringComparison.OrdinalIgnoreCase)) ?? true;
            }
            catch (JsonException)
            {
                // When data is malformed we optimistically allow and rely on data cleanup.
                return true;
            }
        }
    }
}
