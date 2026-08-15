#nullable enable

using TransactionValidation.Core.Interfaces;
using TransactionValidation.Core.Models;

namespace TransactionValidation.Tests.Integration.TransactionValidation.Api.Support;

internal sealed class AlwaysVerifiedPartnerVerifier : IPartnerVerifier
{
    public Task<bool> VerifyAsync(string partnerId, CancellationToken cancellationToken = default, bool? forceTimeout = null)
        => Task.FromResult(true);
}

internal sealed class ThrowingPartnerVerifier : IPartnerVerifier
{
    private readonly Exception _exception;

    public ThrowingPartnerVerifier(Exception exception)
    {
        _exception = exception;
    }

    public Task<bool> VerifyAsync(string partnerId, CancellationToken cancellationToken = default, bool? forceTimeout = null)
        => Task.FromException<bool>(_exception);
}

internal sealed class NoOpMessagePublisher : IMessagePublisher
{
    public Task PublishAsync(TransactionEnvelope envelope, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

internal sealed class ThrowingMessagePublisher : IMessagePublisher
{
    private readonly Exception _exception;

    public ThrowingMessagePublisher(Exception exception)
    {
        _exception = exception;
    }

    public Task PublishAsync(TransactionEnvelope envelope, CancellationToken cancellationToken = default)
        => Task.FromException(_exception);
}
