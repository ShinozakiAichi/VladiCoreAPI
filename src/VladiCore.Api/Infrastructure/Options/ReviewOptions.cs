namespace VladiCore.Api.Infrastructure.Options;

public class ReviewOptions
{
    public bool RequireAuthentication { get; set; }

    public int UserEditWindowHours { get; set; } = 24;
}
