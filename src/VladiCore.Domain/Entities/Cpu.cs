namespace VladiCore.Domain.Entities;

public class Cpu
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public required string Socket { get; set; }

    public int Tdp { get; set; }

    public int PerfScore { get; set; }
}
