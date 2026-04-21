using System.Reactive.Concurrency;

namespace MitsubishiRx.Tests;

public sealed class MitsubishiTagGroupTests
{
    [Test]
    public async Task ValidateTagDatabaseFailsForInvalidAddressesAndUnknownGroupMembers()
    {
        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition("BadWord", "BAD100", DataType: "Word"),
            new MitsubishiTagDefinition("OperatorMessage", "D600", DataType: "String"),
        ]);

        database.AddGroup(new MitsubishiTagGroupDefinition("Line1", ["BadWord", "MissingTag", "OperatorMessage"]));

        var client = new MitsubishiRx(new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5034,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010),
            new FakeTransport(Array.Empty<byte[]>()),
            Scheduler.Immediate)
        {
            TagDatabase = database,
        };

        var result = client.ValidateTagDatabase();

        await Assert.That(result.IsSucceed).IsFalse();
        await Assert.That(result.ErrList.Any(static err => err.Contains("BadWord", StringComparison.OrdinalIgnoreCase))).IsTrue();
        await Assert.That(result.ErrList.Any(static err => err.Contains("MissingTag", StringComparison.OrdinalIgnoreCase))).IsTrue();
        await Assert.That(result.ErrList.Any(static err => err.Contains("Length", StringComparison.OrdinalIgnoreCase))).IsTrue();
    }

    [Test]
    public async Task ReadTagGroupSnapshotAsyncReturnsTypedValuesInGroupOrder()
    {
        var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0x9C, 0xFF],
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x06, 0x00, 0x00, 0x00, 0x78, 0x56, 0x34, 0x12],
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x06, 0x00, 0x00, 0x00, 0x4F, 0x4B, 0x21, 0x00],
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x03, 0x00, 0x00, 0x00, 0x11],
        ]);

        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition("SignedTemp", "D700", DataType: "Int16", Signed: true),
            new MitsubishiTagDefinition("TotalCount", "D400", DataType: "UInt32"),
            new MitsubishiTagDefinition("OperatorMessage", "D600", DataType: "String", Length: 2),
            new MitsubishiTagDefinition("PumpRunning", "M10", DataType: "Bit"),
        ]);
        database.AddGroup(new MitsubishiTagGroupDefinition("Line1", ["SignedTemp", "TotalCount", "OperatorMessage", "PumpRunning"]));

        var client = new MitsubishiRx(new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5035,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010),
            transport,
            Scheduler.Immediate)
        {
            TagDatabase = database,
        };

        var result = await client.ReadTagGroupSnapshotAsync("Line1");

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value!.GroupName).IsEqualTo("Line1");
        await Assert.That(result.Value.TagNames).IsEquivalentTo(new[] { "SignedTemp", "TotalCount", "OperatorMessage", "PumpRunning" });
        await Assert.That(result.Value.GetRequired<short>("SignedTemp")).IsEqualTo((short)-100);
        await Assert.That(result.Value.GetRequired<uint>("TotalCount")).IsEqualTo(0x12345678u);
        await Assert.That(result.Value.GetRequired<string>("OperatorMessage")).IsEqualTo("OK!");
        await Assert.That(result.Value.GetRequired<bool>("PumpRunning")).IsEqualTo(true);
        await Assert.That(transport.Requests.Select(static request => request.Description).ToArray()).IsEquivalentTo(new[]
        {
            "Read words D700",
            "Read words D400",
            "Read words D600",
            "Read bits M10",
        });
    }

    [Test]
    public async Task ReadTagGroupSnapshotAsyncReturnsScaledEngineeringValuesWhenConfigured()
    {
        var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0xFA, 0x00],
        ]);

        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition("HeadTemp", "D200", DataType: "Word", Scale: 0.1, Offset: -10, Units: "°C"),
        ]);
        database.AddGroup(new MitsubishiTagGroupDefinition("Thermals", ["HeadTemp"]));

        var client = new MitsubishiRx(new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5036,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010),
            transport,
            Scheduler.Immediate)
        {
            TagDatabase = database,
        };

        var result = await client.ReadTagGroupSnapshotAsync("Thermals");

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value!.GetRequired<double>("HeadTemp")).IsEqualTo(15.0d);
    }
}
