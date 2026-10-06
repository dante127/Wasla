using FluentValidation;

namespace Wasla.Messages.Application;

public sealed class SendMessageValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageValidator()
    {
        RuleFor(request => request.Body)
            .MaximumLength(8000)
            .When(request => request.Body is not null);

        RuleFor(request => request.MediaFileIds)
            .Must(ids => ids is null || ids.Count <= 10)
            .WithMessage("At most 10 attachments are allowed per message.");

        RuleFor(request => request.IdempotencyKey)
            .MaximumLength(128)
            .When(request => !string.IsNullOrWhiteSpace(request.IdempotencyKey));
    }
}
