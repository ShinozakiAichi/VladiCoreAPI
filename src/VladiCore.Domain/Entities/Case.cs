namespace VladiCore.Domain.Entities;

public class Case
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public int GpuMaxLengthMm { get; set; }

    public int CoolerMaxHeightMm { get; set; }

    public required string PsuFormFactor { get; set; }
}
