using Microsoft.Reactive.Testing;
using ReactiveUI.Extensions;
using System.Reactive.Linq;

namespace MitsubishiRx.Tests;

public sealed class MitsubishiReactiveScanRuntimeTests
{
    [Test]
    public async Task ObserveReactiveWordsSharesOneUnderlyingPollForMultipleSubscribers()
    {
        var scheduler = new TestScheduler();
        var transport = new FakeTransport(_ => [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0x34, 0x12]);
        var client = CreateClient(transport, scheduler);

        var first = new List<MitsubishiReactiveValue<ushort[]>>();
        var second = new List<MitsubishiReactiveValue<ushort[]>>();

        using var firstSubscription = client
            .ObserveReactiveWords("D100", 1, TimeSpan.FromSeconds(5))
            .Take(1)
            .Subscribe(first.Add);

        using var secondSubscription = client
            .ObserveReactiveWords("D100", 1, TimeSpan.FromSeconds(5))
            .Take(1)
            .Subscribe(second.Add);

        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(1).Ticks);

        await Assert.That(first.Count).IsEqualTo(1);
        await Assert.That(second.Count).IsEqualTo(1);
        await Assert.That(transport.Requests.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ObserveReactiveWordsReplaysLatestValueToLateSubscriberWithoutExtraPoll()
    {
        var scheduler = new TestScheduler();
        var transport = new FakeTransport(_ => [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0x34, 0x12]);
        var client = CreateClient(transport, scheduler);

        var first = new List<MitsubishiReactiveValue<ushort[]>>();
        using var firstSubscription = client
            .ObserveReactiveWords("D100", 1, TimeSpan.FromSeconds(5))
            .Take(1)
            .Subscribe(first.Add);

        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(1).Ticks);

        var second = new List<MitsubishiReactiveValue<ushort[]>>();
        using var secondSubscription = client
            .ObserveReactiveWords("D100", 1, TimeSpan.FromSeconds(5))
            .Take(1)
            .Subscribe(second.Add);

        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(1).Ticks);

        await Assert.That(first.Count).IsEqualTo(1);
        await Assert.That(second.Count).IsEqualTo(1);
        await Assert.That(second[0].Value).IsEquivalentTo(new ushort[] { 0x1234 });
        await Assert.That(transport.Requests.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ObserveReactiveTagGroupUsesSharedReadPlanAndReturnsTypedValues()
    {
        var scheduler = new TestScheduler();
        var transport = new FakeTransport(_ => [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x06, 0x00, 0x00, 0x00, 0x34, 0x12, 0x78, 0x56]);
        var client = CreateClient(transport, scheduler);
        client.TagDatabase = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition("Recipe", "D100", DataType: "UInt16"),
            new MitsubishiTagDefinition("Setpoint", "D101", DataType: "UInt16"),
        ]);
        client.TagDatabase.AddGroup(new MitsubishiTagGroupDefinition("Line1", ["Recipe", "Setpoint"]));

        var received = new List<MitsubishiReactiveValue<MitsubishiTagGroupSnapshot>>();
        using var subscription = client
            .ObserveReactiveTagGroup("Line1", TimeSpan.FromSeconds(5))
            .Take(1)
            .Subscribe(received.Add);

        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(1).Ticks);

        await Assert.That(received.Count).IsEqualTo(1);
        await Assert.That(received[0].Quality).IsEqualTo(MitsubishiReactiveQuality.Good);
        await Assert.That(received[0].Value!.GetRequired<ushort>("Recipe")).IsEqualTo((ushort)0x1234);
        await Assert.That(received[0].Value!.GetRequired<ushort>("Setpoint")).IsEqualTo((ushort)0x5678);
        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        await Assert.That(transport.Requests[0].Description).IsEqualTo("Reactive scan Line1");
    }

    [Test]
    public async Task ObserveReactiveTagValueUsesTypedTagConversion()
    {
        var scheduler = new TestScheduler();
        var transport = new FakeTransport(_ => [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0x34, 0x12]);
        var client = CreateClient(transport, scheduler);
        client.TagDatabase = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition("Recipe", "D100", DataType: "UInt16"),
        ]);

        var received = new List<MitsubishiReactiveValue<ushort>>();
        using var subscription = client
            .ObserveReactiveTag<ushort>("Recipe", TimeSpan.FromSeconds(5))
            .Take(1)
            .Subscribe(received.Add);

        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(1).Ticks);

        await Assert.That(received.Count).IsEqualTo(1);
        await Assert.That(received[0].Value).IsEqualTo((ushort)0x1234);
        await Assert.That(received[0].Source).IsEqualTo("Tag:Recipe");
    }

    private static MitsubishiRx CreateClient(FakeTransport transport, TestScheduler scheduler)
        => new(
            new MitsubishiClientOptions(
                Host: "127.0.0.1",
                Port: 5003,
                FrameType: MitsubishiFrameType.ThreeE,
                DataCode: CommunicationDataCode.Binary,
                TransportKind: MitsubishiTransportKind.Tcp,
                Route: MitsubishiRoute.Default,
                MonitoringTimer: 0x0010),
            transport,
            scheduler);
}
