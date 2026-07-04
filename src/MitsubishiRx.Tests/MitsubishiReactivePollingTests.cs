// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive.Tests;
#else

namespace MitsubishiRx.Tests;
#endif

/// <summary>Provides the MitsubishiReactivePollingTests type.</summary>
public sealed class MitsubishiReactivePollingTests
{
    /// <summary>Executes the ObserveWordsHeartbeatEmitsHeartbeatBetweenPolls operation.</summary>
    /// <returns>The ObserveWordsHeartbeatEmitsHeartbeatBetweenPolls operation result.</returns>
    [Test]
    public async Task ObserveWordsHeartbeatEmitsHeartbeatBetweenPolls()
    {
        var scheduler = new TestScheduler();
        var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0x34, 0x12],
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0x35, 0x12],
        ]);

        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5003,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010);

        var client = new MitsubishiRx(options, transport, scheduler);
        var received = new List<Heartbeat<Responce<ushort[]>>>();

        using var subscription = client
            .ObserveWordsHeartbeat("D100", 1, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(30))
            .Take(3)
            .Subscribe(received.Add);

        scheduler.AdvanceBy(TimeSpan.FromSeconds(6).Ticks);

        await Assert.That(received.Count).IsEqualTo(3);
        await Assert.That(received[0].IsHeartbeat).IsFalse();
        await Assert.That(received[1].IsHeartbeat).IsTrue();
        await Assert.That(received[2].IsHeartbeat).IsTrue();
    }
}
