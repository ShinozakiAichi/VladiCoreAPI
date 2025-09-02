using System.Text.Json;
using VladiCore.Domain.Enums;

namespace VladiCore.Domain.Entities;

public class Payment
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public Currency Currency { get; set; }
    public PaymentStatus Status { get; set; }
    public string? ProviderRef { get; set; }
    public JsonDocument? Payload { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Order? Order { get; set; }
}
