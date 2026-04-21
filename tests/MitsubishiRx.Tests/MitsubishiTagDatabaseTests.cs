// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;

namespace MitsubishiRx.Tests;

public sealed class MitsubishiTagDatabaseTests
{
    [Test]
    public async Task CsvImportBuildsTagDatabaseAndPreservesMetadata()
    {
        var csv = """
Name,Address,DataType,Description,Scale,Offset,Notes
MotorSpeed,D100,Word,Main spindle RPM,0.1,0,From commissioning sheet
PumpRunning,M10,Bit,Coolant pump running,1,0,
HeadTemp,D200,Word,Head temperature,1.0,-10,Degrees C
""";

        var database = MitsubishiTagDatabase.FromCsv(csv);
        var speed = database.GetRequired("MotorSpeed");
        var pump = database.GetRequired("PumpRunning");

        await Assert.That(database.Count).IsEqualTo(3);
        await Assert.That(speed.Address).IsEqualTo("D100");
        await Assert.That(speed.DataType).IsEqualTo("Word");
        await Assert.That(speed.Description).IsEqualTo("Main spindle RPM");
        await Assert.That(speed.Scale).IsEqualTo(0.1);
        await Assert.That(speed.Offset).IsEqualTo(0.0);
        await Assert.That(speed.Notes).IsEqualTo("From commissioning sheet");
        await Assert.That(pump.Address).IsEqualTo("M10");
        await Assert.That(pump.DataType).IsEqualTo("Bit");
    }

    [Test]
    public async Task CsvImportNormalizesSupportedDataTypeValues()
    {
        var database = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType
MotorSpeed,D100,word
PumpRunning,M10,bIt
RecipeNumber,D300,dword
""");

        await Assert.That(database.GetRequired("MotorSpeed").DataType).IsEqualTo("Word");
        await Assert.That(database.GetRequired("PumpRunning").DataType).IsEqualTo("Bit");
        await Assert.That(database.GetRequired("RecipeNumber").DataType).IsEqualTo("DWord");
    }

    [Test]
    public async Task CsvImportRejectsUnknownDataTypeValues()
    {
        try
        {
            _ = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType
MotorSpeed,D100,Boolean
""");
            throw new InvalidOperationException("Expected CSV import to reject unknown DataType values.");
        }
        catch (FormatException exception)
        {
            await Assert.That(exception.Message.Contains("DataType", StringComparison.OrdinalIgnoreCase)).IsTrue();
        }
    }

    [Test]
    public async Task ReadWordsByTagAsyncUsesResolvedTagAddress()
    {
        var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x06, 0x00, 0x00, 0x00, 0x34, 0x12, 0x78, 0x56],
        ]);

        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5015,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010);

        var tags = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType,Description
MotorSpeed,D100,Word,Main spindle RPM
""");

        var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = tags,
        };

        var result = await client.ReadWordsByTagAsync("MotorSpeed", 2);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value!.Select(static value => (int)value).ToArray()).IsEquivalentTo(new[] { 0x1234, 0x5678 });
        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        await Assert.That(transport.Requests[0].Description).IsEqualTo("Read words D100");
    }

    [Test]
    public async Task ReadBitsByTagAsyncUsesResolvedTagAddress()
    {
        var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x03, 0x00, 0x00, 0x00, 0x11],
        ]);

        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5016,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010);

        var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType,Description
PumpRunning,M10,Bit,Coolant pump running
"""),
        };

        var result = await client.ReadBitsByTagAsync("PumpRunning", 2);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value!).IsEquivalentTo(new[] { true, true });
        await Assert.That(transport.Requests[0].Description).IsEqualTo("Read bits M10");
    }

    [Test]
    public async Task WriteWordsByTagAsyncUsesResolvedTagAddress()
    {
        var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5017,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010);

        var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType
MotorSpeed,D100,Word
"""),
        };

        var rawTransport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        var rawClient = new MitsubishiRx(options, rawTransport, Scheduler.Immediate);
        var baseline = await rawClient.WriteWordsAsync("D100", [0x1234, 0x5678]);
        var result = await client.WriteWordsByTagAsync("MotorSpeed", [0x1234, 0x5678]);

        await Assert.That(baseline.IsSucceed).IsTrue();
        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests[0].Description).IsEqualTo("Write words D100");
        await Assert.That(transport.Requests[0].Payload).IsEquivalentTo(rawTransport.Requests[0].Payload);
    }

    [Test]
    public async Task WriteBitsByTagAsyncUsesResolvedTagAddress()
    {
        var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5018,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010);

        var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType
PumpRunning,M10,Bit
"""),
        };

        var rawTransport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        var rawClient = new MitsubishiRx(options, rawTransport, Scheduler.Immediate);
        var baseline = await rawClient.WriteBitsAsync("M10", [true, false, true, true]);
        var result = await client.WriteBitsByTagAsync("PumpRunning", [true, false, true, true]);

        await Assert.That(baseline.IsSucceed).IsTrue();
        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests[0].Description).IsEqualTo("Write bits M10");
        await Assert.That(transport.Requests[0].Payload).IsEquivalentTo(rawTransport.Requests[0].Payload);
    }

    [Test]
    public async Task RandomReadWordsByTagAsyncUsesResolvedAddressesInRequestOrder()
    {
        var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0x34, 0x12],
        ]);

        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5019,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010);

        var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType
MotorSpeed,D100,Word
RecipeNumber,D300,Word
"""),
        };

        var rawTransport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0x34, 0x12],
        ]);

        var rawClient = new MitsubishiRx(options, rawTransport, Scheduler.Immediate);
        var baseline = await rawClient.RandomReadWordsAsync(["D100", "D300"]);
        var result = await client.RandomReadWordsByTagAsync(["MotorSpeed", "RecipeNumber"]);

        await Assert.That(baseline.IsSucceed).IsTrue();
        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value!.Select(static value => (int)value).ToArray()).IsEquivalentTo(new[] { 0x1234 });
        await Assert.That(transport.Requests[0].Payload).IsEquivalentTo(rawTransport.Requests[0].Payload);
    }

    [Test]
    public async Task RandomWriteWordsByTagAsyncUsesResolvedAddressesInRequestOrder()
    {
        var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5020,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010);

        var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType
MotorSpeed,D100,Word
RecipeNumber,D300,Word
"""),
        };

        var values = new[]
        {
            new KeyValuePair<string, ushort>("MotorSpeed", 0x1234),
            new KeyValuePair<string, ushort>("RecipeNumber", 0x5678),
        };

        var rawTransport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        var rawClient = new MitsubishiRx(options, rawTransport, Scheduler.Immediate);
        var baseline = await rawClient.RandomWriteWordsAsync(
        [
            new KeyValuePair<string, ushort>("D100", 0x1234),
            new KeyValuePair<string, ushort>("D300", 0x5678),
        ]);
        var result = await client.RandomWriteWordsByTagAsync(values);

        await Assert.That(baseline.IsSucceed).IsTrue();
        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests[0].Payload).IsEquivalentTo(rawTransport.Requests[0].Payload);
    }

    [Test]
    public async Task ReadDWordByTagAsyncReadsLittleEndianDoubleWord()
    {
        var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x06, 0x00, 0x00, 0x00, 0x78, 0x56, 0x34, 0x12],
        ]);

        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5021,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010);

        var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType
TotalCount,D400,DWord
"""),
        };

        var result = await client.ReadDWordByTagAsync("TotalCount");

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value).IsEqualTo(0x12345678u);
    }

    [Test]
    public async Task WriteDWordByTagAsyncEncodesLittleEndianDoubleWord()
    {
        var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5022,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010);

        var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType
TotalCount,D400,DWord
"""),
        };

        var rawTransport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        var rawClient = new MitsubishiRx(options, rawTransport, Scheduler.Immediate);
        var baseline = await rawClient.WriteWordsAsync("D400", [0x5678, 0x1234]);
        var result = await client.WriteDWordByTagAsync("TotalCount", 0x12345678u);

        await Assert.That(baseline.IsSucceed).IsTrue();
        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests[0].Payload).IsEquivalentTo(rawTransport.Requests[0].Payload);
    }

    [Test]
    public async Task ReadFloatByTagAsyncReadsLittleEndianSinglePrecisionValue()
    {
        var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x06, 0x00, 0x00, 0x00, 0x00, 0x00, 0x48, 0x41],
        ]);

        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5023,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010);

        var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType
ProcessValue,D500,Float
"""),
        };

        var result = await client.ReadFloatByTagAsync("ProcessValue");

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value).IsEqualTo(12.5f);
    }

    [Test]
    public async Task WriteFloatByTagAsyncEncodesLittleEndianSinglePrecisionValue()
    {
        var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5024,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010);

        var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType
ProcessValue,D500,Float
"""),
        };

        var rawTransport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        var rawClient = new MitsubishiRx(options, rawTransport, Scheduler.Immediate);
        var baseline = await rawClient.WriteWordsAsync("D500", [0x0000, 0x4148]);
        var result = await client.WriteFloatByTagAsync("ProcessValue", 12.5f);

        await Assert.That(baseline.IsSucceed).IsTrue();
        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests[0].Payload).IsEquivalentTo(rawTransport.Requests[0].Payload);
    }

    [Test]
    public async Task ReadScaledDoubleByTagAsyncAppliesScaleAndOffset()
    {
        var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0xFA, 0x00],
        ]);

        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5025,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010);

        var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType,Scale,Offset
HeadTemp,D200,Word,0.1,-10
"""),
        };

        var result = await client.ReadScaledDoubleByTagAsync("HeadTemp");

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value).IsEqualTo(15.0d);
    }

    [Test]
    public async Task WriteScaledDoubleByTagAsyncAppliesInverseScaleAndOffset()
    {
        var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5026,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010);

        var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType,Scale,Offset
HeadTemp,D200,Word,0.1,-10
"""),
        };

        var rawTransport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        var rawClient = new MitsubishiRx(options, rawTransport, Scheduler.Immediate);
        var baseline = await rawClient.WriteWordsAsync("D200", [250]);
        var result = await client.WriteScaledDoubleByTagAsync("HeadTemp", 15.0d);

        await Assert.That(baseline.IsSucceed).IsTrue();
        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests[0].Payload).IsEquivalentTo(rawTransport.Requests[0].Payload);
    }

    [Test]
    public async Task ReadStringByTagAsyncDecodesPackedAsciiWords()
    {
        var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x06, 0x00, 0x00, 0x00, 0x4F, 0x4B, 0x21, 0x00],
        ]);

        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5027,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010);

        var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType
OperatorMessage,D600,String
"""),
        };

        var result = await client.ReadStringByTagAsync("OperatorMessage", 2);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value).IsEqualTo("OK!");
    }

    [Test]
    public async Task WriteStringByTagAsyncEncodesPackedAsciiWords()
    {
        var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5028,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010);

        var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType
OperatorMessage,D600,String
"""),
        };

        var rawTransport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        var rawClient = new MitsubishiRx(options, rawTransport, Scheduler.Immediate);
        var baseline = await rawClient.WriteWordsAsync("D600", [0x4B4F, 0x0021]);
        var result = await client.WriteStringByTagAsync("OperatorMessage", "OK!", 2);

        await Assert.That(baseline.IsSucceed).IsTrue();
        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests[0].Payload).IsEquivalentTo(rawTransport.Requests[0].Payload);
    }

    [Test]
    public async Task CsvImportPreservesExtendedSchemaColumnsAndNormalizesValues()
    {
        var database = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType,Length,Encoding,Units,Signed,ByteOrder
SignedTotal,D700,int32,2,utf8,items,true,bigendian
""");

        var tag = database.GetRequired("SignedTotal");

        await Assert.That(tag.DataType).IsEqualTo("Int32");
        await Assert.That(tag.Length).IsEqualTo(2);
        await Assert.That(tag.Encoding).IsEqualTo("Utf8");
        await Assert.That(tag.Units).IsEqualTo("items");
        await Assert.That(tag.Signed).IsEqualTo(true);
        await Assert.That(tag.ByteOrder).IsEqualTo("BigEndian");
    }

    [Test]
    public async Task CsvImportRejectsUnsupportedByteOrderValues()
    {
        try
        {
            _ = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType,ByteOrder
SignedTotal,D700,Int32,MiddleEndian
""");
            throw new InvalidOperationException("Expected CSV import to reject unknown ByteOrder values.");
        }
        catch (FormatException exception)
        {
            await Assert.That(exception.Message.Contains("ByteOrder", StringComparison.OrdinalIgnoreCase)).IsTrue();
        }
    }

    [Test]
    public async Task ReadInt16ByTagAsyncReadsTwosComplementWord()
    {
        var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0x9C, 0xFF],
        ]);

        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5029,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010);

        var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType,Signed
SignedTemp,D700,Int16,true
"""),
        };

        var result = await client.ReadInt16ByTagAsync("SignedTemp");

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value).IsEqualTo((short)-100);
    }

    [Test]
    public async Task WriteInt32ByTagAsyncHonorsBigEndianByteOrder()
    {
        var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5030,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010);

        var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType,ByteOrder
SignedTotal,D700,Int32,BigEndian
"""),
        };

        var rawTransport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        var rawClient = new MitsubishiRx(options, rawTransport, Scheduler.Immediate);
        var baseline = await rawClient.WriteWordsAsync("D700", [0x1234, 0x5678]);
        var result = await client.WriteInt32ByTagAsync("SignedTotal", 0x12345678);

        await Assert.That(baseline.IsSucceed).IsTrue();
        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests[0].Payload).IsEquivalentTo(rawTransport.Requests[0].Payload);
    }

    [Test]
    public async Task ReadStringByTagAsyncWithoutExplicitLengthUsesTagLengthAndEncoding()
    {
        var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x06, 0x00, 0x00, 0x00, 0x41, 0xC3, 0xA9, 0x00],
        ]);

        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5031,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010);

        var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType,Length,Encoding
OperatorMessage,D600,String,2,Utf8
"""),
        };

        var result = await client.ReadStringByTagAsync("OperatorMessage");

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value).IsEqualTo("Aé");
    }

    [Test]
    public async Task WriteStringByTagAsyncWithoutExplicitLengthUsesTagLengthAndBigEndianByteOrder()
    {
        var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5032,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010);

        var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType,Length,Encoding,ByteOrder
OperatorMessage,D600,String,1,Ascii,BigEndian
"""),
        };

        var rawTransport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00],
        ]);

        var rawClient = new MitsubishiRx(options, rawTransport, Scheduler.Immediate);
        var baseline = await rawClient.WriteWordsAsync("D600", [0x4F4B]);
        var result = await client.WriteStringByTagAsync("OperatorMessage", "OK");

        await Assert.That(baseline.IsSucceed).IsTrue();
        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests[0].Payload).IsEquivalentTo(rawTransport.Requests[0].Payload);
    }

    [Test]
    public async Task ReadScaledDoubleByTagAsyncUsesSignedWordMetadata()
    {
        var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0x9C, 0xFF],
        ]);

        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5033,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010);

        var client = new MitsubishiRx(options, transport, Scheduler.Immediate)
        {
            TagDatabase = MitsubishiTagDatabase.FromCsv("""
Name,Address,DataType,Scale,Offset,Signed
SignedTemp,D700,Word,0.1,0,true
"""),
        };

        var result = await client.ReadScaledDoubleByTagAsync("SignedTemp");

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value).IsEqualTo(-10.0d);
    }
}
