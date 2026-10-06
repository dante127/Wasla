using FluentValidation;

namespace Wasla.Identity.Application.Users;

public sealed class CreateUserValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);

        RuleFor(request => request.DisplayName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(request => request.Password)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(256);

        RuleFor(request => request.RoleIds)
            .NotNull();
    }
}
