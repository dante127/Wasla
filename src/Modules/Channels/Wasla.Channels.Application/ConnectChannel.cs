using FluentValidation;
using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Application.ChannelAdapters;
using Wasla.BuildingBlocks.Domain;
using Wasla.Channels.Application.Abstractions;
using Wasla.Channels.Domain;

namespace Wasla.Channels.Application;

/// <summary>Connect (or re-connect) a channel with provider credentials.</summary>
public sealed record ConnectChannelRequest(
    string AccessToken,
    string? AppSecret,
    string? VerifyToken,
    string? PhoneNumberId);

/// <summary>
/// Connect flow (docs/channels.md §3): verify credentials against the provider, store
/// them encrypted, discover the external account id and activate the channel.
/// </summary>
public sealed class ConnectChannelHandler(
    ITenantContext tenantContext,
    IChannelRepository channels,
    IChannelCredentialStore credentials,
    IChannelConnectionVerifierRegistry verifiers,
    IChannelWebhookRegistrarRegistry registrars,
    IChannelsUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<ChannelListItem>> HandleAsync(
        Guid channelId,
        ConnectChannelRequest request,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<ChannelListItem>(
                new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        var channel = await channels.GetByIdAsync(tenantId, new ChannelId(channelId), cancellationToken);

        if (channel is null)
        {
            return Result.Failure<ChannelListItem>(new Error("channels.not_found", "Channel not found."));
        }

        var verifier = verifiers.Resolve(channel.Type);

        if (verifier is null)
        {
            return Result.Failure<ChannelListItem>(
                new Error("channels.adapter_unavailable", $"No connection verifier for channel type '{channel.Type}'."));
        }

        var values = new Dictionary<string, string>
        {
            [ChannelCredentialKeys.AccessToken] = request.AccessToken.Trim(),
        };

        if (!string.IsNullOrWhiteSpace(request.AppSecret))
        {
            values[ChannelCredentialKeys.AppSecret] = request.AppSecret.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.VerifyToken))
        {
            values[ChannelCredentialKeys.VerifyToken] = request.VerifyToken.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.PhoneNumberId))
        {
            values[ChannelCredentialKeys.PhoneNumberId] = request.PhoneNumberId.Trim();
        }

        var verification = await verifier.VerifyAndDiscoverAsync(new ChannelCredentialDraft(values), cancellationToken);

        if (!verification.IsValid)
        {
            return Result.Failure<ChannelListItem>(
                new Error("channels.connection_failed", verification.Error ?? "Credential verification failed."));
        }

        var registrar = registrars.Resolve(channel.Type);

        if (registrar is not null)
        {
            try
            {
                await registrar.RegisterAsync(tenantId, channelId, new ChannelCredentialDraft(values), cancellationToken);
            }
            catch (Exception exception)
            {
                return Result.Failure<ChannelListItem>(
                    new Error("channels.webhook_registration_failed", exception.Message));
            }
        }

        foreach (var (key, value) in values)
        {
            await credentials.SetAsync(tenantId, channelId, key, value, cancellationToken);
        }

        var externalAccountId = verification.ExternalAccountId ?? request.PhoneNumberId?.Trim();

        if (!string.IsNullOrWhiteSpace(externalAccountId))
        {
            channel.SetExternalAccountId(externalAccountId);
        }

        channel.Activate();
        channel.RecordHealthCheck(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new ChannelListItem(
            channel.Id.Value,
            channel.Type.ToString(),
            channel.DisplayName,
            channel.Status.ToString()));
    }
}

public sealed class ConnectChannelValidator : AbstractValidator<ConnectChannelRequest>
{
    public ConnectChannelValidator()
    {
        RuleFor(request => request.AccessToken)
            .NotEmpty()
            .MaximumLength(2048);

        RuleFor(request => request.AppSecret)
            .MaximumLength(512)
            .When(request => request.AppSecret is not null);

        RuleFor(request => request.VerifyToken)
            .MaximumLength(512)
            .When(request => request.VerifyToken is not null);

        RuleFor(request => request.PhoneNumberId)
            .MaximumLength(64)
            .When(request => request.PhoneNumberId is not null);
    }
}
