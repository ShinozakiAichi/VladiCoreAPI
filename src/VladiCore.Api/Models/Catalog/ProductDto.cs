namespace VladiCore.Api.Models.Catalog;

public record ProductDto(Guid Id, string Name, string Slug, string Description, decimal BasePrice, string Currency, bool IsActive, Guid? CategoryId);
