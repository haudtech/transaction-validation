using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using TransactionValidation.Api.Idempotency;

using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Api.Idempotency;

public sealed class InMemoryIdempotencyStoreTests
{
    /// <summary>
    /// Scenario: the idempotency TTL is zero.
    /// Expected: construction throws an argument-out-of-range exception.
    /// </summary>
    [Fact]
    public void Constructor_WhenTtlIsNotPositive_ThrowsArgumentOutOfRangeException()
    {
        var action = () => new InMemoryIdempotencyStore(TimeSpan.Zero, NullLogger<InMemoryIdempotencyStore>.Instance);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Scenario: a new idempotency key is acquired.
    /// Expected: the result is Acquired.
    /// </summary>
    [Fact]
    public void TryAcquire_WhenKeyIsNew_ReturnsAcquired()
    {
        var store = new InMemoryIdempotencyStore(TimeSpan.FromMinutes(10), NullLogger<InMemoryIdempotencyStore>.Instance);

        var result = store.TryAcquire("partner|request", "fingerprint", Now);

        result.Should().Be(IdempotencyAcquireResult.Acquired);
    }

    /// <summary>
    /// Scenario: an existing key is submitted with the same fingerprint.
    /// Expected: the result is Duplicate.
    /// </summary>
    [Fact]
    public void TryAcquire_WhenFingerprintMatches_ReturnsDuplicate()
    {
        var store = CreateAcquiredStore();

        var result = store.TryAcquire("partner|request", "fingerprint", Now.AddMinutes(1));

        result.Should().Be(IdempotencyAcquireResult.Duplicate);
    }

    /// <summary>
    /// Scenario: an existing key is submitted with a different fingerprint.
    /// Expected: the result indicates key reuse with a different payload.
    /// </summary>
    [Fact]
    public void TryAcquire_WhenFingerprintDiffers_ReturnsKeyReusedWithDifferentPayload()
    {
        var store = CreateAcquiredStore();

        var result = store.TryAcquire("partner|request", "different", Now.AddMinutes(1));

        result.Should().Be(IdempotencyAcquireResult.KeyReusedWithDifferentPayload);
    }

    /// <summary>
    /// Scenario: an existing idempotency entry has expired.
    /// Expected: the key can be acquired again.
    /// </summary>
    [Fact]
    public void TryAcquire_WhenEntryHasExpired_AcquiresKeyAgain()
    {
        var store = new InMemoryIdempotencyStore(TimeSpan.FromMinutes(10), NullLogger<InMemoryIdempotencyStore>.Instance);
        store.TryAcquire("partner|request", "fingerprint", Now);

        var result = store.TryAcquire("partner|request", "new-fingerprint", Now.AddMinutes(10));

        result.Should().Be(IdempotencyAcquireResult.Acquired);
    }

    /// <summary>
    /// Scenario: an accepted response is cached for a matching request.
    /// Expected: the cached response is returned.
    /// </summary>
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

    /// <summary>
    /// Scenario: the cache is queried with a different fingerprint or unknown key.
    /// Expected: no cached response is returned.
    /// </summary>
    [Fact]
    public void TryGetCachedResponse_WhenResponseIsMissingOrFingerprintDiffers_ReturnsFalse()
    {
        var store = CreateAcquiredStore();
        var expected = new IdempotencyCachedResponse("message-1", "correlation-1", IdempotencyCachedResponseStatus.Accepted);
        store.StoreCachedResponse("partner|request", "fingerprint", Now, expected);

        store.TryGetCachedResponse("partner|request", "different", Now, out _).Should().BeFalse();
        store.TryGetCachedResponse("other|request", "fingerprint", Now, out _).Should().BeFalse();
    }

    /// <summary>
    /// Scenario: an acquired entry has no cached response yet.
    /// Expected: the lookup returns false.
    /// </summary>
    [Fact]
    public void TryGetCachedResponse_WhenEntryHasNoCachedResponse_ReturnsFalse()
    {
        var store = CreateAcquiredStore();

        store.TryGetCachedResponse("partner|request", "fingerprint", Now, out _)
            .Should().BeFalse();
    }

    /// <summary>
    /// Scenario: cache storage receives blank keys, blank fingerprints, or a null response.
    /// Expected: the operation returns without throwing.
    /// </summary>
    [Fact]
    public void StoreCachedResponse_WhenInputsAreBlank_DoesNotThrow()
    {
        var store = CreateAcquiredStore();
        var response = new IdempotencyCachedResponse("message-1", "correlation-1", IdempotencyCachedResponseStatus.Accepted);

        var actions = new Action[]
        {
            () => store.StoreCachedResponse(" ", "fingerprint", Now, response),
            () => store.StoreCachedResponse("key", " ", Now, response),
            () => store.StoreCachedResponse("key", "fingerprint", Now, null!)
        };

        foreach (var action in actions)
        {
            action();
        }
    }

    /// <summary>
    /// Scenario: a cached entry has expired.
    /// Expected: the lookup returns false and removes the expired entry.
    /// </summary>
    [Fact]
    public void TryGetCachedResponse_WhenEntryHasExpired_ReturnsFalse()
    {
        var store = CreateAcquiredStore();
        var response = new IdempotencyCachedResponse("message-1", "correlation-1", IdempotencyCachedResponseStatus.Accepted);
        store.StoreCachedResponse("partner|request", "fingerprint", Now, response);

        store.TryGetCachedResponse("partner|request", "fingerprint", Now.AddMinutes(10), out _)
            .Should().BeFalse();
    }

    /// <summary>
    /// Scenario: a conflicting fingerprint attempts to overwrite an entry.
    /// Expected: the existing entry is preserved and the conflicting response is not returned.
    /// </summary>
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

    /// <summary>
    /// Scenario: an existing idempotency key is released.
    /// Expected: the same key can be acquired again.
    /// </summary>
    [Fact]
    public void Release_RemovesExistingEntry()
    {
        var store = CreateAcquiredStore();

        store.Release("partner|request");

        store.TryAcquire("partner|request", "new-fingerprint", Now.AddMinutes(1))
            .Should().Be(IdempotencyAcquireResult.Acquired);
    }

    /// <summary>
    /// Scenario: the key or request fingerprint is blank.
    /// Expected: acquisition throws an argument exception.
    /// </summary>
    [Fact]
    public void TryAcquire_WhenInputsAreBlank_ThrowsArgumentException()
    {
        var store = new InMemoryIdempotencyStore(TimeSpan.FromMinutes(10), NullLogger<InMemoryIdempotencyStore>.Instance);

        var blankKey = () => store.TryAcquire(" ", "fingerprint", Now);
        var blankFingerprint = () => store.TryAcquire("key", " ", Now);

        blankKey.Should().Throw<ArgumentException>();
        blankFingerprint.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Scenario: multiple callers acquire the same key concurrently.
    /// Expected: exactly one acquisition succeeds and all remaining calls are duplicates.
    /// </summary>
    [Fact]
    public async Task TryAcquire_WhenCalledConcurrently_AllowsOnlyOneAcquisition()
    {
        var store = new InMemoryIdempotencyStore(TimeSpan.FromMinutes(10), NullLogger<InMemoryIdempotencyStore>.Instance);
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
        var store = new InMemoryIdempotencyStore(TimeSpan.FromMinutes(10), NullLogger<InMemoryIdempotencyStore>.Instance);
        store.TryAcquire("partner|request", "fingerprint", Now);
        return store;
    }
}
