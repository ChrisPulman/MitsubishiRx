// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive.Tests;
#else

namespace MitsubishiRx.Tests;
#endif

/// <summary>Provides the MitsubishiReactiveValueTests type.</summary>
public sealed class MitsubishiReactiveValueTests
{
    /// <summary>Executes the ReactiveValueFromSuccessfulResponseUsesGoodQualityAndMetadata operation.</summary>
    /// <returns>The ReactiveValueFromSuccessfulResponseUsesGoodQualityAndMetadata operation result.</returns>
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
        await Assert.That(value.Value).IsEquivalentTo([(ushort)0x1234]);
    }

    /// <summary>Executes the ReactiveValueFromFailedResponseUsesErrorQualityAndCapturesError operation.</summary>
    /// <returns>The ReactiveValueFromFailedResponseUsesErrorQualityAndCapturesError operation result.</returns>
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

    /// <summary>Executes the HeartbeatAndStaleFactoriesStampQualityFlags operation.</summary>
    /// <returns>The HeartbeatAndStaleFactoriesStampQualityFlags operation result.</returns>
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
