namespace VladiCore.Domain.Entities;

public class Motherboard
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public required string Socket { get; set; }

    public required string RamType { get; set; }

    public int RamMaxFreq { get; set; }

    public int M2Slots { get; set; }

    public int PcieSlots { get; set; }

    public required string FormFactor { get; set; }
}
