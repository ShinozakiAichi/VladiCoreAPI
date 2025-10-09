namespace VladiCore.Domain.Entities;

public class Gpu
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public int LengthMm { get; set; }

    public int Slots { get; set; }

    public int Tdp { get; set; }

    public int PerfScore { get; set; }
}
