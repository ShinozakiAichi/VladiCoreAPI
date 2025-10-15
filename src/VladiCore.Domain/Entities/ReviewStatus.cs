namespace VladiCore.Domain.Entities;

public enum ReviewStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    RemovedByAdmin = 3,
    DeletedByAuthor = 4
}
