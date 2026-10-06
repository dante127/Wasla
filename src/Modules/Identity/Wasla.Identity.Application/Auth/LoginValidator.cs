using FluentValidation;

namespace Wasla.Identity.Application.Auth;

public sealed class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);

        RuleFor(request => request.Password)
            .NotEmpty()
            .MaximumLength(256);
    }
}
