using Microsoft.Reactive.Testing;

namespace MitsubishiRx.Tests;

public sealed class MitsubishiReactiveValueTests
{
    [Test]
    public async Task ReactiveValueFromSuccessfulResponseUsesGoodQualityAndMetadata()
    {
        var timestamp = new DateTimeOffset(2026, 4, 21, 12, 0, 0, TimeSpan.Zero);
        var response = new Responce<ushort[]>([(ushort)0x1234]);

        var value = MitsubishiReactiveValue.FromResponse(response, timestamp, "Read words D100");

        await Assert.That(value.TimestampUtc).IsEqualTo(timestamp);
        await Assert.That(value.Quality).IsEqualTo(MitsubishiReactiveQuality.Good);
        await Assert.That(value.Source).IsEqualTo("Read words D100");
        await Assert.That(value.IsHeartbeat).IsFalse();
        await Assert.That(value.IsStale).IsFalse();
        await Assert.That(value.Error).IsEqualTo(string.Empty);
        await Assert.That(value.Value).IsEquivalentTo(new ushort[] { 0x1234 });
    }

    [Test]
    public async Task ReactiveValueFromFailedResponseUsesErrorQualityAndCapturesError()
    {
        var timestamp = new DateTimeOffset(2026, 4, 21, 12, 1, 0, TimeSpan.Zero);
        var response = new Responce<ushort[]>
        {
            IsSucceed = false,
            Err = "boom",
            ErrCode = 7,
        };

        var value = MitsubishiReactiveValue.FromResponse(response, timestamp, "Read words D200");

        await Assert.That(value.TimestampUtc).IsEqualTo(timestamp);
        await Assert.That(value.Quality).IsEqualTo(MitsubishiReactiveQuality.Error);
        await Assert.That(value.Source).IsEqualTo("Read words D200");
        await Assert.That(value.Error).IsEqualTo("boom");
        await Assert.That(value.ErrorCode).IsEqualTo(7);
        await Assert.That(value.Value is null).IsTrue();
    }

    [Test]
    public async Task HeartbeatAndStaleFactoriesStampQualityFlags()
    {
        var timestamp = new DateTimeOffset(2026, 4, 21, 12, 2, 0, TimeSpan.Zero);
        var baseline = new MitsubishiReactiveValue<int>(42, timestamp, MitsubishiReactiveQuality.Good, false, false, "Tag:RecipeNumber", string.Empty, 0);

        var heartbeat = MitsubishiReactiveValue.Heartbeat(baseline, timestamp.AddSeconds(1));
        var stale = MitsubishiReactiveValue.Stale(baseline, timestamp.AddSeconds(2));

        await Assert.That(heartbeat.IsHeartbeat).IsTrue();
        await Assert.That(heartbeat.Quality).IsEqualTo(MitsubishiReactiveQuality.Heartbeat);
        await Assert.That(heartbeat.Value).IsEqualTo(42);

        await Assert.That(stale.IsStale).IsTrue();
        await Assert.That(stale.Quality).IsEqualTo(MitsubishiReactiveQuality.Stale);
        await Assert.That(stale.Value).IsEqualTo(42);
    }
}
