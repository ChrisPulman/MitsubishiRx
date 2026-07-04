// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Ports;
using System.Text;

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive.Tests;
#else

namespace MitsubishiRx.Tests;
#endif

/// <summary>Provides the MitsubishiSerialOneCCompletionTests type.</summary>
public sealed class MitsubishiSerialOneCCompletionTests
{
    /// <summary>Executes the RandomReadWordsAsyncSerial1CShouldUseBatchReadsAndReturnValues operation.</summary>
    /// <returns>The RandomReadWordsAsyncSerial1CShouldUseBatchReadsAndReturnValues operation result.</returns>
    [Test]
    public async Task RandomReadWordsAsyncSerial1CShouldUseBatchReadsAndReturnValues()
    {
        var transport = new FakeTransport(
        [
            BuildAsciiPayloadResponse("1234"),
            BuildAsciiPayloadResponse("5678"),
        ]);
        var client = new MitsubishiRx(CreateSerialOptions(), transport, Scheduler.Immediate);

        var result = await client.RandomReadWordsAsync(["D100", "D300"]);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value!.Select(static value => (int)value).ToArray()).IsEquivalentTo([0x1234, 0x5678]);
        await Assert.That(transport.Requests.Count).IsEqualTo(2);
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[0].Payload)).IsEqualTo(BuildAsciiRequest("00FFWR0D010001"));
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[1].Payload)).IsEqualTo(BuildAsciiRequest("00FFWR0D030001"));
    }

    /// <summary>Executes the RandomWriteWordsAsyncSerial1CShouldUseBatchWrites operation.</summary>
    /// <returns>The RandomWriteWordsAsyncSerial1CShouldUseBatchWrites operation result.</returns>
    [Test]
    public async Task RandomWriteWordsAsyncSerial1CShouldUseBatchWrites()
    {
        var transport = new FakeTransport(
        [
            BuildAsciiAckResponse(),
            BuildAsciiAckResponse(),
        ]);
        var client = new MitsubishiRx(CreateSerialOptions(), transport, Scheduler.Immediate);

        var result = await client.RandomWriteWordsAsync(
        [
            new KeyValuePair<string, ushort>("D100", 0x1234),
            new KeyValuePair<string, ushort>("D300", 0x5678),
        ]);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests.Count).IsEqualTo(2);
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[0].Payload)).IsEqualTo(BuildAsciiRequest("00FFWW0D0100011234"));
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[1].Payload)).IsEqualTo(BuildAsciiRequest("00FFWW0D0300015678"));
    }

    /// <summary>Executes the ReadBlocksAsyncSerial1CShouldReadEachBlockAndReturnCombinedPayload operation.</summary>
    /// <returns>The ReadBlocksAsyncSerial1CShouldReadEachBlockAndReturnCombinedPayload operation result.</returns>
    [Test]
    public async Task ReadBlocksAsyncSerial1CShouldReadEachBlockAndReturnCombinedPayload()
    {
        var transport = new FakeTransport(
        [
            BuildAsciiPayloadResponse("11223344"),
            BuildAsciiPayloadResponse("101"),
        ]);
        var client = new MitsubishiRx(CreateSerialOptions(), transport, Scheduler.Immediate);

        var result = await client.ReadBlocksAsync(CreateBlockRequest());

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(Encoding.ASCII.GetString(result.Value!)).IsEqualTo("11223344100010");
        await Assert.That(transport.Requests.Count).IsEqualTo(2);
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[0].Payload)).IsEqualTo(BuildAsciiRequest("00FFWR0D010002"));
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[1].Payload)).IsEqualTo(BuildAsciiRequest("00FFBR0M001003"));
    }

    /// <summary>Executes the WriteBlocksAsyncSerial1CShouldWriteEachBlock operation.</summary>
    /// <returns>The WriteBlocksAsyncSerial1CShouldWriteEachBlock operation result.</returns>
    [Test]
    public async Task WriteBlocksAsyncSerial1CShouldWriteEachBlock()
    {
        var transport = new FakeTransport(
        [
            BuildAsciiAckResponse(),
            BuildAsciiAckResponse(),
        ]);
        var client = new MitsubishiRx(CreateSerialOptions(), transport, Scheduler.Immediate);

        var result = await client.WriteBlocksAsync(CreateBlockRequest());

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests.Count).IsEqualTo(2);
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[0].Payload)).IsEqualTo(BuildAsciiRequest("00FFWW0D01000211223344"));
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[1].Payload)).IsEqualTo(BuildAsciiRequest("00FFBW0M001003101"));
    }

    /// <summary>Executes the MonitorAsyncSerial1CShouldRegisterAddressesAndExecuteAsBatchReads operation.</summary>
    /// <returns>The MonitorAsyncSerial1CShouldRegisterAddressesAndExecuteAsBatchReads operation result.</returns>
    [Test]
    public async Task MonitorAsyncSerial1CShouldRegisterAddressesAndExecuteAsBatchReads()
    {
        var transport = new FakeTransport(
        [
            BuildAsciiPayloadResponse("1234"),
            BuildAsciiPayloadResponse("5678"),
        ]);
        var client = new MitsubishiRx(CreateSerialOptions(), transport, Scheduler.Immediate);

        var register = await client.RegisterMonitorAsync(["D100", "D300"]);
        var execute = await client.ExecuteMonitorAsync();

        await Assert.That(register.IsSucceed).IsTrue();
        await Assert.That(execute.IsSucceed).IsTrue();
        await Assert.That(Encoding.ASCII.GetString(execute.Value!)).IsEqualTo("12345678");
        await Assert.That(transport.Requests.Count).IsEqualTo(2);
    }

    /// <summary>Executes the OneCSpecializedSerialRequestsShouldEncodeAndParseResponses operation.</summary>
    /// <returns>The OneCSpecializedSerialRequestsShouldEncodeAndParseResponses operation result.</returns>
    [Test]
    public async Task OneCSpecializedSerialRequestsShouldEncodeAndParseResponses()
    {
        var transport = new FakeTransport(
        [
            BuildAsciiPayloadResponse("FX3U0001"),
            BuildAsciiPayloadResponse("0004PING"),
            BuildAsciiPayloadResponse("12345678"),
            BuildAsciiAckResponse(),
            BuildAsciiAckResponse(),
            BuildAsciiAckResponse(),
            BuildAsciiAckResponse(),
            BuildAsciiAckResponse(),
            BuildAsciiPayloadResponse("CAFE"),
        ]);
        var client = new MitsubishiRx(CreateSerialOptions(), transport, Scheduler.Immediate);

        var type = await client.ReadTypeNameAsync();
        var loopback = await client.LoopbackAsync(Encoding.ASCII.GetBytes("PING"));
        var memory = await client.ReadMemoryAsync(MitsubishiCommands.MemoryRead, 0x2000, 2);
        var run = await client.RemoteRunAsync(force: true, clearMode: true);
        var stop = await client.RemoteStopAsync();
        var pause = await client.RemotePauseAsync();
        var latchClear = await client.RemoteLatchClearAsync();
        var reset = await client.RemoteResetAsync();
        var raw = await client.ExecuteRawAsync(new MitsubishiRawCommandRequest(0x1234, 0xABCD, Encoding.ASCII.GetBytes("BEEF"), "Custom raw"));

        await Assert.That(type.IsSucceed).IsTrue();
        await Assert.That(type.Value!.ModelName).IsEqualTo("FX3U");
        await Assert.That(Encoding.ASCII.GetString(loopback.Value!)).IsEqualTo("PING");
        await Assert.That(memory.Value!.Select(static value => (int)value).ToArray()).IsEquivalentTo([0x1234, 0x5678]);
        await Assert.That(run.IsSucceed).IsTrue();
        await Assert.That(stop.IsSucceed).IsTrue();
        await Assert.That(pause.IsSucceed).IsTrue();
        await Assert.That(latchClear.IsSucceed).IsTrue();
        await Assert.That(reset.IsSucceed).IsTrue();
        await Assert.That(Encoding.ASCII.GetString(raw.Value!)).IsEqualTo("CAFE");
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[0].Payload)).IsEqualTo(BuildAsciiRequest("00FF01010000"));
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[1].Payload)).IsEqualTo(BuildAsciiRequest("00FF061900000004PING"));
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[2].Payload)).IsEqualTo(BuildAsciiRequest("00FF0613000020000002"));
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[3].Payload)).IsEqualTo(BuildAsciiRequest("00FF1001000000010001"));
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[4].Payload)).IsEqualTo(BuildAsciiRequest("00FF10020000"));
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[5].Payload)).IsEqualTo(BuildAsciiRequest("00FF10030000"));
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[6].Payload)).IsEqualTo(BuildAsciiRequest("00FF10050000"));
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[7].Payload)).IsEqualTo(BuildAsciiRequest("00FF10060000"));
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[8].Payload)).IsEqualTo(BuildAsciiRequest("00FF1234ABCDBEEF"));
    }

    /// <summary>Executes the CreateBlockRequest operation.</summary>
    /// <returns>The CreateBlockRequest operation result.</returns>
    private static MitsubishiBlockRequest CreateBlockRequest()
        => new(
            WordBlocks:
            [
                new MitsubishiWordBlock(MitsubishiDeviceAddress.Parse("D100", XyAddressNotation.Octal), new ushort[] { 0x1122, 0x3344 }),
            ],
            BitBlocks:
            [
                new MitsubishiBitBlock(MitsubishiDeviceAddress.Parse("M10", XyAddressNotation.Octal), new bool[] { true, false, true }),
            ]);

    /// <summary>Executes the CreateSerialOptions operation.</summary>
    /// <returns>The CreateSerialOptions operation result.</returns>
    private static MitsubishiClientOptions CreateSerialOptions()
        => new(
            Host: "COM3",
            Port: 0,
            FrameType: MitsubishiFrameType.OneC,
            DataCode: CommunicationDataCode.Ascii,
            TransportKind: MitsubishiTransportKind.Serial,
            Timeout: TimeSpan.FromSeconds(2),
            CpuType: CpuType.Fx3,
            Serial: new MitsubishiSerialOptions(
                PortName: "COM3",
                BaudRate: 9600,
                DataBits: 7,
                Parity: Parity.Even,
                StopBits: StopBits.One,
                Handshake: Handshake.None,
                MessageFormat: MitsubishiSerialMessageFormat.Format1,
                StationNumber: 0x00,
                PcNumber: 0xFF,
                MessageWait: 0x00));

    /// <summary>Executes the BuildAsciiPayloadResponse operation.</summary>
    /// <param name="payload">The payload parameter.</param>
    /// <returns>The BuildAsciiPayloadResponse operation result.</returns>
    private static byte[] BuildAsciiPayloadResponse(string payload)
    {
        var body = "\u000600" + payload;
        return Encoding.ASCII.GetBytes(body + ComputeChecksum(body));
    }

    /// <summary>Executes the BuildAsciiAckResponse operation.</summary>
    /// <returns>The BuildAsciiAckResponse operation result.</returns>
    private static byte[] BuildAsciiAckResponse()
    {
        const string Body = "\u000600FF";
        return Encoding.ASCII.GetBytes(Body + ComputeChecksum(Body));
    }

    /// <summary>Executes the BuildAsciiRequest operation.</summary>
    /// <param name="body">The body parameter.</param>
    /// <returns>The BuildAsciiRequest operation result.</returns>
    private static string BuildAsciiRequest(string body)
        => "\u0005" + body + ComputeChecksum(body);

    /// <summary>Executes the ComputeChecksum operation.</summary>
    /// <param name="body">The body parameter.</param>
    /// <returns>The ComputeChecksum operation result.</returns>
    private static string ComputeChecksum(string body)
        => (Encoding.ASCII.GetBytes(body).Aggregate(0, static (sum, value) => sum + value) & 0xFF).ToString("X2");
}
