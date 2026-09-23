using System.Collections.Concurrent;
using Moq;
using StackExchange.Redis;

namespace Dzaba.HomeSecurity.TestUtils;

/// <summary>
/// In-memory Moq-backed fake for the handful of IDatabase members
/// RedisCacheClient (and its callers' tests, e.g. key TTL) call - keeps
/// controller tests isolated from a real Redis instance. Expiry is
/// recorded but never counted down: tests use a fresh tenant/user GUID per
/// run, so staleness can't happen within a single test, and
/// KeyTimeToLiveAsync just echoes back whatever KeyExpireAsync was last
/// given for that key.
/// </summary>
public static class FakeRedis
{
    public static IConnectionMultiplexer CreateConnectionMultiplexer()
    {
        var sets = new ConcurrentDictionary<RedisKey, HashSet<RedisValue>>();
        var ttls = new ConcurrentDictionary<RedisKey, TimeSpan>();

        var db = new Mock<IDatabase>();

        db.Setup(d => d.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, CommandFlags flags) => sets.ContainsKey(key));

        db.Setup(d => d.SetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, RedisValue[] values, CommandFlags flags) =>
            {
                var set = sets.GetOrAdd(key, _ => []);
                long added = 0;
                lock (set)
                {
                    foreach (var value in values)
                    {
                        if (set.Add(value))
                        {
                            added++;
                        }
                    }
                }
                return added;
            });

        db.Setup(d => d.SetContainsAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, RedisValue value, CommandFlags flags) =>
                sets.TryGetValue(key, out var set) && set.Contains(value));

        // 2-arg call sites (key, expiry) resolve to this 4-param overload -
        // TimeSpan/ExpireWhen/CommandFlags all fill in via their default
        // values - not the (key, expiry, flags) one.
        db.Setup(d => d.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, TimeSpan? expiry, ExpireWhen when, CommandFlags flags) =>
            {
                if (expiry.HasValue)
                {
                    ttls[key] = expiry.Value;
                }
                return true;
            });

        db.Setup(d => d.KeyTimeToLiveAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, CommandFlags flags) => ttls.TryGetValue(key, out var ttl) ? ttl : null);

        db.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, CommandFlags flags) =>
            {
                ttls.TryRemove(key, out _);
                return sets.TryRemove(key, out _);
            });

        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(db.Object);

        return multiplexer.Object;
    }

    /// <summary>
    /// Simulates a fully unreachable Redis: every IDatabase member
    /// RedisCacheClient calls throws the same RedisConnectionException a
    /// real disconnected multiplexer would raise per-call once
    /// AbortOnConnectFail=false lets Connect() itself succeed - see
    /// RedisCacheClient's translation into CacheUnavailableException.
    /// </summary>
    public static IConnectionMultiplexer CreateUnavailableConnectionMultiplexer()
    {
        var down = new RedisConnectionException(ConnectionFailureType.UnableToConnect, CommandFlags.None, "Simulated Redis outage");

        var db = new Mock<IDatabase>();
        db.Setup(d => d.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ThrowsAsync(down);
        db.Setup(d => d.SetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>())).ThrowsAsync(down);
        db.Setup(d => d.SetContainsAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>())).ThrowsAsync(down);
        db.Setup(d => d.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>())).ThrowsAsync(down);
        db.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ThrowsAsync(down);

        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(db.Object);

        return multiplexer.Object;
    }
}
