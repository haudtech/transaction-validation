using System.Text.Json;

using FluentAssertions;

using Moq;

using StackExchange.Redis;

using TransactionValidation.Api.Idempotency;

using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Api.Idempotency;

public sealed class RedisIdempotencyStoreTests
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
    private static readonly DateTimeOffset Now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_WhenTtlIsNotPositive_ThrowsArgumentOutOfRangeException()
    {
        var multiplexer = Moq.Mock.Of<IConnectionMultiplexer>();

        var action = () => new RedisIdempotencyStore(multiplexer, TimeSpan.Zero);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TryAcquire_WhenRedisSetsFingerprint_ReturnsAcquired()
    {
        var database = CreateDatabase();
        database.SetReturnsDefault(true);
        var store = CreateStore(database);

        var result = store.TryAcquire("partner|request", " fingerprint ", Now);

        result.Should().Be(IdempotencyAcquireResult.Acquired);
    }

    [Fact]
    public void TryAcquire_WhenFingerprintMatchesExistingValue_ReturnsDuplicate()
    {
        var database = CreateDatabase();
        database
            .Setup(value => value.StringSet(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .Returns(false);
        database
            .Setup(value => value.StringGet(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns((RedisValue)"fingerprint");
        var store = CreateStore(database);

        var result = store.TryAcquire("partner|request", "fingerprint", Now);

        result.Should().Be(IdempotencyAcquireResult.Duplicate);
    }

    [Fact]
    public void TryAcquire_WhenFingerprintDiffers_ReturnsKeyReusedWithDifferentPayload()
    {
        var database = CreateDatabase();
        database
            .Setup(value => value.StringSet(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .Returns(false);
        database
            .Setup(value => value.StringGet(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns((RedisValue)"other-fingerprint");
        var store = CreateStore(database);

        var result = store.TryAcquire("partner|request", "fingerprint", Now);

        result.Should().Be(IdempotencyAcquireResult.KeyReusedWithDifferentPayload);
    }

    [Fact]
    public void TryAcquire_WhenInputsAreBlank_ThrowsArgumentException()
    {
        var store = CreateStore(CreateDatabase());

        var blankKey = () => store.TryAcquire(" ", "fingerprint", Now);
        var blankFingerprint = () => store.TryAcquire("key", " ", Now);

        blankKey.Should().Throw<ArgumentException>();
        blankFingerprint.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TryGetCachedResponse_WhenFingerprintAndResponseExist_ReturnsResponse()
    {
        var database = CreateDatabase();
        var response = new IdempotencyCachedResponse("message-1", "correlation-1", IdempotencyCachedResponseStatus.Accepted);
        database
            .SetupSequence(value => value.StringGet(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns((RedisValue)"fingerprint")
            .Returns((RedisValue)JsonSerializer.Serialize(response));
        var store = CreateStore(database);

        var result = store.TryGetCachedResponse("partner|request", "fingerprint", Now, out var actual);

        result.Should().BeTrue();
        actual.Should().Be(response);
    }

    [Fact]
    public void TryGetCachedResponse_WhenFingerprintOrResponseIsMissing_ReturnsFalse()
    {
        var database = CreateDatabase();
        database
            .Setup(value => value.StringGet(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns(RedisValue.Null);
        var store = CreateStore(database);

        store.TryGetCachedResponse("partner|request", "fingerprint", Now, out _)
            .Should().BeFalse();
    }

    [Fact]
    public void StoreCachedResponse_WhenValuesAreValid_StoresFingerprintAndResponse()
    {
        var database = CreateDatabase();
        database
            .Setup(value => value.StringGet(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns(RedisValue.Null);
        database
            .Setup(value => value.StringSet(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .Returns(true);
        var store = CreateStore(database);
        var response = new IdempotencyCachedResponse("message-1", "correlation-1", IdempotencyCachedResponseStatus.Accepted);

        store.StoreCachedResponse("partner|request", "fingerprint", Now, response);

        database.Verify(value => value.StringSet(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<bool>(),
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()), Times.Exactly(2));
    }

    [Fact]
    public void Release_WhenKeyIsPresent_DeletesFingerprintAndResponse()
    {
        var database = CreateDatabase();
        database
            .Setup(value => value.KeyDelete(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns(true);
        var store = CreateStore(database);

        store.Release("partner|request");

        database.Verify(value => value.KeyDelete(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Exactly(2));
    }

    private static Moq.Mock<IDatabase> CreateDatabase()
    {
        return new Moq.Mock<IDatabase>();
    }

    private static RedisIdempotencyStore CreateStore(Moq.Mock<IDatabase> database)
    {
        var multiplexer = new Moq.Mock<IConnectionMultiplexer>();
        multiplexer
            .Setup(value => value.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database.Object);

        return new RedisIdempotencyStore(multiplexer.Object, Ttl);
    }
}
