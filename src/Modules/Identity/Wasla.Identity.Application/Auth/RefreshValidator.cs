using FluentValidation;

namespace Wasla.Identity.Application.Auth;

public sealed class RefreshValidator : AbstractValidator<RefreshRequest>
{
    public RefreshValidator()
    {
        RuleFor(request => request.RefreshToken)
            .NotEmpty()
            .MaximumLength(512);
    }
}
