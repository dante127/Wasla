using FluentValidation;

namespace Wasla.Customers.Application;

public sealed class CreateCustomerValidator : AbstractValidator<CreateCustomerRequest>
{
    public CreateCustomerValidator()
    {
        RuleFor(request => request.DisplayName)
            .NotEmpty()
            .MaximumLength(400);

        RuleFor(request => request.Email)
            .EmailAddress()
            .MaximumLength(320)
            .When(request => !string.IsNullOrWhiteSpace(request.Email));

        RuleFor(request => request.Phone)
            .MaximumLength(40)
            .When(request => !string.IsNullOrWhiteSpace(request.Phone));
    }
}
