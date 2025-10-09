using VladiCore.Domain.Enums;

namespace VladiCore.Domain.Entities;

public class Storage
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public StorageType Type { get; set; }

    public int CapacityGb { get; set; }

    public int PerfScore { get; set; }
}
