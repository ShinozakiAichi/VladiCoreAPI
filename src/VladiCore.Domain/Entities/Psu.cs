namespace VladiCore.Domain.Entities;

public class Psu
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public int Wattage { get; set; }

    public required string FormFactor { get; set; }
}
