// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using Microsoft.Reactive.Testing;
using ReactiveUI.Extensions;

namespace MitsubishiRx.Tests;

public sealed class MitsubishiReactivePollingTests
{
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
        var received = new List<IHeartbeat<Responce<ushort[]>>>();

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
