namespace VladiCore.Domain.Entities;

public class Branch
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
}
