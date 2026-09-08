using FluentAssertions;

using TransactionValidation.Api.Idempotency;

using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Api.Idempotency;

public sealed class InMemoryIdempotencyStoreTests
{
    [Fact]
    public void Constructor_WhenTtlIsNotPositive_ThrowsArgumentOutOfRangeException()
    {
        var action = () => new InMemoryIdempotencyStore(TimeSpan.Zero);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TryAcquire_WhenKeyIsNew_ReturnsAcquired()
    {
        var store = new InMemoryIdempotencyStore(TimeSpan.FromMinutes(10));

        var result = store.TryAcquire("partner|request", "fingerprint", Now);

        result.Should().Be(IdempotencyAcquireResult.Acquired);
    }

    [Fact]
    public void TryAcquire_WhenFingerprintMatches_ReturnsDuplicate()
    {
        var store = CreateAcquiredStore();

        var result = store.TryAcquire("partner|request", "fingerprint", Now.AddMinutes(1));

        result.Should().Be(IdempotencyAcquireResult.Duplicate);
    }

    [Fact]
    public void TryAcquire_WhenFingerprintDiffers_ReturnsKeyReusedWithDifferentPayload()
    {
        var store = CreateAcquiredStore();

        var result = store.TryAcquire("partner|request", "different", Now.AddMinutes(1));

        result.Should().Be(IdempotencyAcquireResult.KeyReusedWithDifferentPayload);
    }

    [Fact]
    public void TryAcquire_WhenEntryHasExpired_AcquiresKeyAgain()
    {
        var store = new InMemoryIdempotencyStore(TimeSpan.FromMinutes(10));
        store.TryAcquire("partner|request", "fingerprint", Now);

        var result = store.TryAcquire("partner|request", "new-fingerprint", Now.AddMinutes(10));

        result.Should().Be(IdempotencyAcquireResult.Acquired);
    }

    [Fact]
    public void TryGetCachedResponse_WhenResponseWasStored_ReturnsResponse()
    {
        var store = CreateAcquiredStore();
        var expected = new IdempotencyCachedResponse("message-1", "correlation-1", IdempotencyCachedResponseStatus.Accepted);

        store.StoreCachedResponse("partner|request", "fingerprint", Now.AddMinutes(1), expected);

        store.TryGetCachedResponse("partner|request", "fingerprint", Now.AddMinutes(2), out var actual)
            .Should().BeTrue();
        actual.Should().Be(expected);
    }

    [Fact]
    public void TryGetCachedResponse_WhenResponseIsMissingOrFingerprintDiffers_ReturnsFalse()
    {
        var store = CreateAcquiredStore();
        var expected = new IdempotencyCachedResponse("message-1", "correlation-1", IdempotencyCachedResponseStatus.Accepted);
        store.StoreCachedResponse("partner|request", "fingerprint", Now, expected);

        store.TryGetCachedResponse("partner|request", "different", Now, out _).Should().BeFalse();
        store.TryGetCachedResponse("other|request", "fingerprint", Now, out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetCachedResponse_WhenEntryHasExpired_ReturnsFalse()
    {
        var store = CreateAcquiredStore();
        var response = new IdempotencyCachedResponse("message-1", "correlation-1", IdempotencyCachedResponseStatus.Accepted);
        store.StoreCachedResponse("partner|request", "fingerprint", Now, response);

        store.TryGetCachedResponse("partner|request", "fingerprint", Now.AddMinutes(10), out _)
            .Should().BeFalse();
    }

    [Fact]
    public void StoreCachedResponse_WhenFingerprintDiffers_DoesNotReplaceExistingEntry()
    {
        var store = CreateAcquiredStore();
        var response = new IdempotencyCachedResponse("message-1", "correlation-1", IdempotencyCachedResponseStatus.Accepted);
        var conflictingResponse = new IdempotencyCachedResponse("message-2", "correlation-2", IdempotencyCachedResponseStatus.Accepted);

        store.StoreCachedResponse("partner|request", "different", Now, conflictingResponse);

        store.TryGetCachedResponse("partner|request", "fingerprint", Now, out var actual).Should().BeFalse();
        actual.Should().BeNull();
        store.StoreCachedResponse("partner|request", "fingerprint", Now, response);
        store.TryGetCachedResponse("partner|request", "fingerprint", Now, out actual).Should().BeTrue();
        actual.Should().Be(response);
    }

    [Fact]
    public void Release_RemovesExistingEntry()
    {
        var store = CreateAcquiredStore();

        store.Release("partner|request");

        store.TryAcquire("partner|request", "new-fingerprint", Now.AddMinutes(1))
            .Should().Be(IdempotencyAcquireResult.Acquired);
    }

    [Fact]
    public void TryAcquire_WhenInputsAreBlank_ThrowsArgumentException()
    {
        var store = new InMemoryIdempotencyStore(TimeSpan.FromMinutes(10));

        var blankKey = () => store.TryAcquire(" ", "fingerprint", Now);
        var blankFingerprint = () => store.TryAcquire("key", " ", Now);

        blankKey.Should().Throw<ArgumentException>();
        blankFingerprint.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task TryAcquire_WhenCalledConcurrently_AllowsOnlyOneAcquisition()
    {
        var store = new InMemoryIdempotencyStore(TimeSpan.FromMinutes(10));
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = Enumerable.Range(0, 16)
            .Select(_ => Task.Run(async () =>
            {
                await start.Task;
                return store.TryAcquire("partner|request", "fingerprint", Now);
            }))
            .ToArray();

        start.SetResult();
        var results = await Task.WhenAll(attempts);

        results.Count(result => result == IdempotencyAcquireResult.Acquired).Should().Be(1);
        results.Count(result => result == IdempotencyAcquireResult.Duplicate).Should().Be(15);
    }

    private static readonly DateTimeOffset Now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    private static InMemoryIdempotencyStore CreateAcquiredStore()
    {
        var store = new InMemoryIdempotencyStore(TimeSpan.FromMinutes(10));
        store.TryAcquire("partner|request", "fingerprint", Now);
        return store;
    }
}
