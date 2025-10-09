namespace VladiCore.Domain.Entities;

public class Cooler
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public int HeightMm { get; set; }

    public required string SocketSupport { get; set; }
}
