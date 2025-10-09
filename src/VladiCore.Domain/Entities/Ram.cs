namespace VladiCore.Domain.Entities;

public class Ram
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public required string Type { get; set; }

    public int Freq { get; set; }

    public int CapacityPerStick { get; set; }

    public int Sticks { get; set; }

    public int PerfScore { get; set; }
}
