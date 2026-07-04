// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive.Tests;
#else

namespace MitsubishiRx.Tests;
#endif

/// <summary>Provides the MitsubishiReactiveTagGroupTests type.</summary>
public sealed class MitsubishiReactiveTagGroupTests
{
    /// <summary>Executes the ObserveTagGroupHeartbeatEmitsHeartbeatBetweenPolls operation.</summary>
    /// <returns>The ObserveTagGroupHeartbeatEmitsHeartbeatBetweenPolls operation result.</returns>
    [Test]
    public async Task ObserveTagGroupHeartbeatEmitsHeartbeatBetweenPolls()
    {
        var scheduler = new TestScheduler();
        var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0x34, 0x12],
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0x35, 0x12],
        ]);

        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition("RecipeNumber", "D300", DataType: "UInt16"),
        ]);
        database.AddGroup(new MitsubishiTagGroupDefinition("Recipe", ["RecipeNumber"]));

        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5037,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010);

        var client = new MitsubishiRx(options, transport, scheduler)
        {
            TagDatabase = database,
        };

        var received = new List<Heartbeat<Responce<MitsubishiTagGroupSnapshot>>>();

        using var subscription = client
            .ObserveTagGroupHeartbeat("Recipe", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(10))
            .Take(3)
            .Subscribe(received.Add);

        scheduler.AdvanceBy(TimeSpan.FromSeconds(6).Ticks);

        await Assert.That(received.Count).IsEqualTo(3);
        await Assert.That(received[0].IsHeartbeat).IsFalse();
        var firstUpdate = received[0].Update;
        if (firstUpdate is null || firstUpdate.Value is not MitsubishiTagGroupSnapshot firstSnapshot)
        {
            throw new InvalidOperationException("Expected first grouped heartbeat sample to contain a snapshot value.");
        }

        await Assert.That(firstSnapshot.GetRequired<ushort>("RecipeNumber")).IsEqualTo((ushort)0x1234);
        await Assert.That(received[1].IsHeartbeat).IsTrue();
        await Assert.That(received[2].IsHeartbeat).IsTrue();
    }

    /// <summary>Executes the ObserveTagGroupStaleMarksStreamWhenUpdatesGoQuiet operation.</summary>
    /// <returns>The ObserveTagGroupStaleMarksStreamWhenUpdatesGoQuiet operation result.</returns>
    [Test]
    public async Task ObserveTagGroupStaleMarksStreamWhenUpdatesGoQuiet()
    {
        var scheduler = new TestScheduler();
        var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0x34, 0x12],
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0x35, 0x12],
        ]);

        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition("RecipeNumber", "D300", DataType: "UInt16"),
        ]);
        database.AddGroup(new MitsubishiTagGroupDefinition("Recipe", ["RecipeNumber"]));

        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5038,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010);

        var client = new MitsubishiRx(options, transport, scheduler)
        {
            TagDatabase = database,
        };

        var received = new List<Stale<Responce<MitsubishiTagGroupSnapshot>>>();

        using var subscription = client
            .ObserveTagGroupStale("Recipe", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(10))
            .Take(2)
            .Subscribe(received.Add);

        scheduler.AdvanceBy(TimeSpan.FromSeconds(3).Ticks);

        await Assert.That(received.Count).IsEqualTo(2);
        await Assert.That(received[0].IsStale).IsFalse();
        await Assert.That(received[1].IsStale).IsTrue();
    }

    /// <summary>Executes the ObserveTagGroupLatestUsesLatestCompletedSnapshot operation.</summary>
    /// <returns>The ObserveTagGroupLatestUsesLatestCompletedSnapshot operation result.</returns>
    [Test]
    public async Task ObserveTagGroupLatestUsesLatestCompletedSnapshot()
    {
        var scheduler = new TestScheduler();
        var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0x34, 0x12],
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0x35, 0x12],
        ]);

        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition("RecipeNumber", "D300", DataType: "UInt16"),
        ]);
        database.AddGroup(new MitsubishiTagGroupDefinition("Recipe", ["RecipeNumber"]));

        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5039,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010);

        var client = new MitsubishiRx(options, transport, scheduler)
        {
            TagDatabase = database,
        };

        var trigger = new Signal<Unit>();
        var received = new List<Responce<MitsubishiTagGroupSnapshot>>();

        using var subscription = client
            .ObserveTagGroupLatest("Recipe", trigger)
            .Take(2)
            .Subscribe(received.Add);

        trigger.OnNext(Unit.Default);
        trigger.OnNext(Unit.Default);
        scheduler.AdvanceBy(1);

        await Assert.That(received.Count).IsEqualTo(2);
        if (received[0].Value is not MitsubishiTagGroupSnapshot firstLatest)
        {
            throw new InvalidOperationException("Expected first latest-only grouped read to contain a snapshot.");
        }

        if (received[1].Value is not MitsubishiTagGroupSnapshot secondLatest)
        {
            throw new InvalidOperationException("Expected second latest-only grouped read to contain a snapshot.");
        }

        await Assert.That(firstLatest.GetRequired<ushort>("RecipeNumber")).IsEqualTo((ushort)0x1234);
        await Assert.That(secondLatest.GetRequired<ushort>("RecipeNumber")).IsEqualTo((ushort)0x1235);
    }
}
