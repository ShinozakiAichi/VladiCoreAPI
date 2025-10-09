using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace VladiCore.Domain.DTOs;

public class PcValidateRequest
{
    public int? CpuId { get; set; }

    public int? MotherboardId { get; set; }

    public int? RamId { get; set; }

    public int? GpuId { get; set; }

    public int? PsuId { get; set; }

    public int? CaseId { get; set; }

    public int? CoolerId { get; set; }

    [Required]
    public IList<int> StorageIds { get; set; } = new List<int>();
}
