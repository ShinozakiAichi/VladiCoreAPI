namespace VladiCore.App.Options;

public class JwtOptions
{
    public string Key { get; set; } = string.Empty;
    public int AccessMinutes { get; set; }
    public int RefreshDays { get; set; }
}
