using FluentValidation;

namespace VladiCore.Api.Models.Staff;

public class CreateEmployeeRequestValidator : AbstractValidator<CreateEmployeeRequest>
{
    public CreateEmployeeRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Position).IsInEnum();
    }
}
