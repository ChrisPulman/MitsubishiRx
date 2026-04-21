// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;

namespace MitsubishiRx.Tests;

public sealed class MitsubishiTagGroupWriteTests
{
    [Test]
    public async Task ValidateTagGroupWriteReportsUnsupportedValueTypesAndUnknownTags()
    {
        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition("RecipeNumber", "D300", DataType: "UInt16"),
            new MitsubishiTagDefinition("OperatorMessage", "D600", DataType: "String", Length: 2),
        ]);
        database.AddGroup(new MitsubishiTagGroupDefinition("RecipeWrite", ["RecipeNumber", "OperatorMessage"]));

        var client = new MitsubishiRx(new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5040,
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

        var result = client.ValidateTagGroupWrite(
            "RecipeWrite",
            new Dictionary<string, object?>
            {
                ["RecipeNumber"] = "not-a-number",
                ["MissingTag"] = 12,
            });

        await Assert.That(result.IsSucceed).IsFalse();
        await Assert.That(result.ErrList.Any(static err => err.Contains("RecipeNumber", StringComparison.OrdinalIgnoreCase))).IsTrue();
        await Assert.That(result.ErrList.Any(static err => err.Contains("MissingTag", StringComparison.OrdinalIgnoreCase))).IsTrue();
    }

    [Test]
    public async Task WriteTagGroupValuesAsyncWritesOnlyProvidedValuesInGroupOrder()
    {
        var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition("RecipeNumber", "D300", DataType: "UInt16"),
            new MitsubishiTagDefinition("OperatorMessage", "D600", DataType: "String", Length: 2),
            new MitsubishiTagDefinition("PumpRunning", "M10", DataType: "Bit"),
        ]);
        database.AddGroup(new MitsubishiTagGroupDefinition("RecipeWrite", ["RecipeNumber", "OperatorMessage", "PumpRunning"]));

        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5041,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010);

        var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = database,
        };

        var rawTransport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        var rawClient = new MitsubishiRx(options, rawTransport, Scheduler.Immediate);
        var baselineWord = await rawClient.WriteWordsAsync("D300", [(ushort)7]);
        var baselineString = await rawClient.WriteWordsAsync("D600", [0x4B4F, 0x0021]);

        await Assert.That(baselineWord.IsSucceed).IsTrue();
        await Assert.That(baselineString.IsSucceed).IsTrue();

        var result = await client.WriteTagGroupValuesAsync(
            "RecipeWrite",
            new Dictionary<string, object?>
            {
                ["RecipeNumber"] = (ushort)7,
                ["OperatorMessage"] = "OK!",
            });

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests.Count).IsEqualTo(2);
        await Assert.That(transport.Requests[0].Description).IsEqualTo("Write words D300");
        await Assert.That(transport.Requests[1].Description).IsEqualTo("Write words D600");
    }

    [Test]
    public async Task WriteTagGroupSnapshotAsyncWritesTypedSnapshotValues()
    {
        var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition("SignedTemp", "D700", DataType: "Int16", Signed: true),
            new MitsubishiTagDefinition("TotalCount", "D400", DataType: "UInt32"),
            new MitsubishiTagDefinition("OperatorMessage", "D600", DataType: "String", Length: 2),
        ]);
        database.AddGroup(new MitsubishiTagGroupDefinition("Line1Overview", ["SignedTemp", "TotalCount", "OperatorMessage"]));

        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5042,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010);

        var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = database,
        };

        var snapshot = new MitsubishiTagGroupSnapshot(
            "Line1Overview",
            new Dictionary<string, object?>
            {
                ["SignedTemp"] = (short)-100,
                ["TotalCount"] = 0x12345678u,
                ["OperatorMessage"] = "OK!",
            });

        var result = await client.WriteTagGroupSnapshotAsync(snapshot);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests.Count).IsEqualTo(3);
        await Assert.That(transport.Requests.Select(static request => request.Description).ToArray()).IsEquivalentTo(new[]
        {
            "Write words D700",
            "Write words D400",
            "Write words D600",
        });
    }
}
