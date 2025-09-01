using VladiCore.Domain.Enums;

namespace VladiCore.Api.Models.Staff;

public record CreateEmployeeRequest(Guid UserId, UserRole Position, long? BranchId);
