using FluentValidation;

namespace Wasla.Customers.Application;

public sealed class AddNoteValidator : AbstractValidator<AddNoteRequest>
{
    public AddNoteValidator()
    {
        RuleFor(request => request.Body)
            .NotEmpty()
            .MaximumLength(4000);
    }
}
