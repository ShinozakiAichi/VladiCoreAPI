using VladiCore.Domain.Enums;

namespace VladiCore.Domain.Entities;

public class Employee
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public long? BranchId { get; set; }
    public UserRole Position { get; set; }
    public DateTime HiredAt { get; set; }
    public bool IsActive { get; set; }
}
