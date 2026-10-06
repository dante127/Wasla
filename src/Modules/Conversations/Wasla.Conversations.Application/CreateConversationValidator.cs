using FluentValidation;

namespace Wasla.Conversations.Application;

public sealed class CreateConversationValidator : AbstractValidator<CreateConversationRequest>
{
    public CreateConversationValidator()
    {
        RuleFor(request => request.CustomerId).NotEmpty();
        RuleFor(request => request.ChannelId).NotEmpty();
    }
}
