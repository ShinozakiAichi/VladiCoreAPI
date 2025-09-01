using FluentValidation;

namespace VladiCore.Api.Models.Staff;

public class CreateBranchRequestValidator : AbstractValidator<CreateBranchRequest>
{
    public CreateBranchRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}
