#nullable enable

using TransactionValidation.Core.Interfaces;
using TransactionValidation.Core.Models;

namespace TransactionValidation.Tests.Integration.TransactionValidation.Api.Support;

/// <summary>
/// Test verifier double that always returns a successful verification result.
/// </summary>
internal sealed class AlwaysVerifiedPartnerVerifier : IPartnerVerifier
{
    public Task<bool> VerifyAsync(string partnerId, CancellationToken cancellationToken = default, bool? forceTimeout = null)
        => Task.FromResult(true);
}

/// <summary>
/// Test verifier double that throws a preconfigured exception for each call.
/// </summary>
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

/// <summary>
/// Test message publisher that completes without side effects.
/// </summary>
internal sealed class NoOpMessagePublisher : IMessagePublisher
{
    public Task PublishAsync(TransactionEnvelope envelope, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

/// <summary>
/// Test message publisher that throws a preconfigured exception for publish calls.
/// </summary>
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
