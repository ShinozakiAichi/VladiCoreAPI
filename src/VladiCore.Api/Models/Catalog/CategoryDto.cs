namespace VladiCore.Api.Models.Catalog;

public record CategoryDto(Guid Id, string Name, string Slug, Guid? ParentId);
