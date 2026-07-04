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

/// <summary>Provides the MitsubishiSerialParityTests type.</summary>
public sealed class MitsubishiSerialParityTests
{
    /// <summary>Executes the ReadTypeNameAsyncSerial3CFormat1EncodesExpectedRequestAndParsesResponse operation.</summary>
    /// <returns>The ReadTypeNameAsyncSerial3CFormat1EncodesExpectedRequestAndParsesResponse operation result.</returns>
    [Test]
    public async Task ReadTypeNameAsyncSerial3CFormat1EncodesExpectedRequestAndParsesResponse()
    {
        var transport = new FakeTransport([BuildAsciiPayloadResponse(MitsubishiFrameType.ThreeC, MitsubishiSerialMessageFormat.Format1, "FX5U0001")]);
        var options = CreateSerialOptions(MitsubishiFrameType.ThreeC, CommunicationDataCode.Ascii, MitsubishiSerialMessageFormat.Format1);
        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.ReadTypeNameAsync();

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value!.ModelName).IsEqualTo("FX5U");
        await Assert.That(result.Value.ModelCode).IsEqualTo((ushort)0x0001);
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[0].Payload)).IsEqualTo("\u0005F90000FF0001010000AD");
    }

    /// <summary>Executes the ReadTypeNameAsyncSerial4CFormat5EncodesExpectedRequestAndParsesResponse operation.</summary>
    /// <returns>The ReadTypeNameAsyncSerial4CFormat5EncodesExpectedRequestAndParsesResponse operation result.</returns>
    [Test]
    public async Task ReadTypeNameAsyncSerial4CFormat5EncodesExpectedRequestAndParsesResponse()
    {
        var transport = new FakeTransport([BuildBinaryDataResponse([.. Encoding.ASCII.GetBytes("FX5U"), 0x01, 0x00])]);
        var options = CreateSerialOptions(MitsubishiFrameType.FourC, CommunicationDataCode.Binary, MitsubishiSerialMessageFormat.Format5);
        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.ReadTypeNameAsync();

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value!.ModelName).IsEqualTo("FX5U");
        await Assert.That(result.Value.ModelCode).IsEqualTo((ushort)0x0001);
        await Assert.That(Convert.ToHexString(transport.Requests[0].Payload)).IsEqualTo("10020C00F80000FFFF0300000101000010033037");
    }

    /// <summary>Executes the LoopbackAsyncSerial3CFormat1EncodesExpectedRequestAndReturnsEchoedPayload operation.</summary>
    /// <returns>The LoopbackAsyncSerial3CFormat1EncodesExpectedRequestAndReturnsEchoedPayload operation result.</returns>
    [Test]
    public async Task LoopbackAsyncSerial3CFormat1EncodesExpectedRequestAndReturnsEchoedPayload()
    {
        var transport = new FakeTransport([BuildAsciiPayloadResponse(MitsubishiFrameType.ThreeC, MitsubishiSerialMessageFormat.Format1, "0004PING")]);
        var options = CreateSerialOptions(MitsubishiFrameType.ThreeC, CommunicationDataCode.Ascii, MitsubishiSerialMessageFormat.Format1);
        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.LoopbackAsync(Encoding.ASCII.GetBytes("PING"));

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(Encoding.ASCII.GetString(result.Value!)).IsEqualTo("PING");
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[0].Payload)).IsEqualTo("\u0005F90000FF00061900000004PINGAD");
    }

    /// <summary>Executes the LoopbackAsyncSerial4CFormat5EncodesExpectedRequestAndReturnsEchoedPayload operation.</summary>
    /// <returns>The LoopbackAsyncSerial4CFormat5EncodesExpectedRequestAndReturnsEchoedPayload operation result.</returns>
    [Test]
    public async Task LoopbackAsyncSerial4CFormat5EncodesExpectedRequestAndReturnsEchoedPayload()
    {
        var transport = new FakeTransport([BuildBinaryDataResponse([0x04, 0x00, .. Encoding.ASCII.GetBytes("PING")])]);
        var options = CreateSerialOptions(MitsubishiFrameType.FourC, CommunicationDataCode.Binary, MitsubishiSerialMessageFormat.Format5);
        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.LoopbackAsync(Encoding.ASCII.GetBytes("PING"));

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(Encoding.ASCII.GetString(result.Value!)).IsEqualTo("PING");
        await Assert.That(Convert.ToHexString(transport.Requests[0].Payload)).IsEqualTo("10021200F80000FFFF03000019060000040050494E4710033543");
    }

    /// <summary>Executes the ReadMemoryAsyncSerial3CFormat1EncodesExpectedRequestAndParsesResponse operation.</summary>
    /// <returns>The ReadMemoryAsyncSerial3CFormat1EncodesExpectedRequestAndParsesResponse operation result.</returns>
    [Test]
    public async Task ReadMemoryAsyncSerial3CFormat1EncodesExpectedRequestAndParsesResponse()
    {
        var transport = new FakeTransport([BuildAsciiPayloadResponse(MitsubishiFrameType.ThreeC, MitsubishiSerialMessageFormat.Format1, "12345678")]);
        var options = CreateSerialOptions(MitsubishiFrameType.ThreeC, CommunicationDataCode.Ascii, MitsubishiSerialMessageFormat.Format1);
        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.ReadMemoryAsync(MitsubishiCommands.MemoryRead, 0x2000, 2);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value!.Select(static value => (int)value).ToArray()).IsEquivalentTo([0x1234, 0x5678]);
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[0].Payload)).IsEqualTo("\u0005F90000FF00061300002000000239");
    }

    /// <summary>Executes the ReadMemoryAsyncSerial4CFormat5EncodesExpectedRequestAndParsesResponse operation.</summary>
    /// <returns>The ReadMemoryAsyncSerial4CFormat5EncodesExpectedRequestAndParsesResponse operation result.</returns>
    [Test]
    public async Task ReadMemoryAsyncSerial4CFormat5EncodesExpectedRequestAndParsesResponse()
    {
        var transport = new FakeTransport([BuildBinaryDataResponse([0x34, 0x12, 0x78, 0x56])]);
        var options = CreateSerialOptions(MitsubishiFrameType.FourC, CommunicationDataCode.Binary, MitsubishiSerialMessageFormat.Format5);
        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.ReadMemoryAsync(MitsubishiCommands.MemoryRead, 0x2000, 2);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value!.Select(static value => (int)value).ToArray()).IsEquivalentTo([0x1234, 0x5678]);
        await Assert.That(Convert.ToHexString(transport.Requests[0].Payload)).IsEqualTo("10021000F80000FFFF030000130600000020020010033434");
    }

    /// <summary>Executes the WriteMemoryAsyncSerial3CFormat1EncodesExpectedRequest operation.</summary>
    /// <returns>The WriteMemoryAsyncSerial3CFormat1EncodesExpectedRequest operation result.</returns>
    [Test]
    public async Task WriteMemoryAsyncSerial3CFormat1EncodesExpectedRequest()
    {
        var transport = new FakeTransport([BuildAsciiAckResponse(MitsubishiFrameType.ThreeC, MitsubishiSerialMessageFormat.Format1)]);
        var options = CreateSerialOptions(MitsubishiFrameType.ThreeC, CommunicationDataCode.Ascii, MitsubishiSerialMessageFormat.Format1);
        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.WriteMemoryAsync(MitsubishiCommands.MemoryWrite, 0x2000, [0x1234, 0x5678]);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[0].Payload)).IsEqualTo("\u0005F90000FF00161300002000000212345678DE");
    }

    /// <summary>Executes the WriteMemoryAsyncSerial4CFormat5EncodesExpectedRequest operation.</summary>
    /// <returns>The WriteMemoryAsyncSerial4CFormat5EncodesExpectedRequest operation result.</returns>
    [Test]
    public async Task WriteMemoryAsyncSerial4CFormat5EncodesExpectedRequest()
    {
        var transport = new FakeTransport([BuildBinaryAckResponse()]);
        var options = CreateSerialOptions(MitsubishiFrameType.FourC, CommunicationDataCode.Binary, MitsubishiSerialMessageFormat.Format5);
        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.WriteMemoryAsync(MitsubishiCommands.MemoryWrite, 0x2000, [0x1234, 0x5678]);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(Convert.ToHexString(transport.Requests[0].Payload)).IsEqualTo("10021400F80000FFFF03000013160000002002003412785610033643");
    }

    /// <summary>Executes the ExecuteRawAsyncSerial3CFormat1EncodesExpectedRequestAndReturnsPayload operation.</summary>
    /// <returns>The ExecuteRawAsyncSerial3CFormat1EncodesExpectedRequestAndReturnsPayload operation result.</returns>
    [Test]
    public async Task ExecuteRawAsyncSerial3CFormat1EncodesExpectedRequestAndReturnsPayload()
    {
        var transport = new FakeTransport([BuildAsciiPayloadResponse(MitsubishiFrameType.ThreeC, MitsubishiSerialMessageFormat.Format1, "CAFE")]);
        var options = CreateSerialOptions(MitsubishiFrameType.ThreeC, CommunicationDataCode.Ascii, MitsubishiSerialMessageFormat.Format1);
        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.ExecuteRawAsync(new MitsubishiRawCommandRequest(0x1234, 0xABCD, Encoding.ASCII.GetBytes("BEEF"), "Custom raw"));

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(Encoding.ASCII.GetString(result.Value!)).IsEqualTo("CAFE");
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[0].Payload)).IsEqualTo("\u0005F90000FF001234ABCDBEEF11");
    }

    /// <summary>Executes the ExecuteRawAsyncSerial4CFormat5EncodesExpectedRequestAndReturnsPayload operation.</summary>
    /// <returns>The ExecuteRawAsyncSerial4CFormat5EncodesExpectedRequestAndReturnsPayload operation result.</returns>
    [Test]
    public async Task ExecuteRawAsyncSerial4CFormat5EncodesExpectedRequestAndReturnsPayload()
    {
        var transport = new FakeTransport([BuildBinaryDataResponse([0xCA, 0xFE, 0xBA, 0xBE])]);
        var options = CreateSerialOptions(MitsubishiFrameType.FourC, CommunicationDataCode.Binary, MitsubishiSerialMessageFormat.Format5);
        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.ExecuteRawAsync(new MitsubishiRawCommandRequest(0x1234, 0xABCD, [0xDE, 0xAD, 0xBE, 0xEF], "Custom raw"));

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(Convert.ToHexString(result.Value!)).IsEqualTo("CAFEBABE");
        await Assert.That(Convert.ToHexString(transport.Requests[0].Payload)).IsEqualTo("10021000F80000FFFF0300003412CDABDEADBEEF10034646");
    }

    /// <summary>Executes the CreateSerialOptions operation.</summary>
    /// <param name="frameType">The frameType parameter.</param>
    /// <param name="dataCode">The dataCode parameter.</param>
    /// <param name="messageFormat">The messageFormat parameter.</param>
    /// <returns>The CreateSerialOptions operation result.</returns>
    private static MitsubishiClientOptions CreateSerialOptions(
        MitsubishiFrameType frameType,
        CommunicationDataCode dataCode,
        MitsubishiSerialMessageFormat messageFormat)
        => new(
            Host: "COM3",
            Port: 0,
            FrameType: frameType,
            DataCode: dataCode,
            TransportKind: MitsubishiTransportKind.Serial,
            Timeout: TimeSpan.FromSeconds(2),
            CpuType: CpuType.Fx5,
            Serial: new MitsubishiSerialOptions(
                PortName: "COM3",
                BaudRate: 9600,
                DataBits: 7,
                Parity: Parity.Even,
                StopBits: StopBits.One,
                Handshake: Handshake.None,
                MessageFormat: messageFormat,
                StationNumber: 0x00,
                NetworkNumber: 0x00,
                PcNumber: 0xFF,
                RequestDestinationModuleIoNumber: 0x03FF,
                RequestDestinationModuleStationNumber: 0x00,
                SelfStationNumber: 0x00,
                MessageWait: 0x00));

    /// <summary>Executes the BuildAsciiPayloadResponse operation.</summary>
    /// <param name="frameType">The frameType parameter.</param>
    /// <param name="format">The format parameter.</param>
    /// <param name="payload">The payload parameter.</param>
    /// <returns>The BuildAsciiPayloadResponse operation result.</returns>
    private static byte[] BuildAsciiPayloadResponse(MitsubishiFrameType frameType, MitsubishiSerialMessageFormat format, string payload)
    {
        var body = frameType switch
        {
            MitsubishiFrameType.OneC => "\u000600" + payload,
            MitsubishiFrameType.ThreeC => "\u0006F900" + payload,
            MitsubishiFrameType.FourC => "\u0006F80000FF03" + payload,
            _ => throw new ArgumentOutOfRangeException(nameof(frameType)),
        };

        var checksum = ComputeChecksum(body);
        return format switch
        {
            MitsubishiSerialMessageFormat.Format1 => Encoding.ASCII.GetBytes(body + checksum),
            MitsubishiSerialMessageFormat.Format4 => Encoding.ASCII.GetBytes("\r\n" + body + checksum + "\r\n"),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
    }

    /// <summary>Executes the BuildAsciiAckResponse operation.</summary>
    /// <param name="frameType">The frameType parameter.</param>
    /// <param name="format">The format parameter.</param>
    /// <returns>The BuildAsciiAckResponse operation result.</returns>
    private static byte[] BuildAsciiAckResponse(MitsubishiFrameType frameType, MitsubishiSerialMessageFormat format)
    {
        var body = frameType switch
        {
            MitsubishiFrameType.OneC => "\u000600FF",
            MitsubishiFrameType.ThreeC => "\u0006F90000FF",
            MitsubishiFrameType.FourC => "\u0006F80000FF03FF00",
            _ => throw new ArgumentOutOfRangeException(nameof(frameType)),
        };

        var checksum = ComputeChecksum(body);
        return format switch
        {
            MitsubishiSerialMessageFormat.Format1 => Encoding.ASCII.GetBytes(body + checksum),
            MitsubishiSerialMessageFormat.Format4 => Encoding.ASCII.GetBytes("\r\n" + body + checksum + "\r\n"),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
    }

    /// <summary>Executes the BuildBinaryAckResponse operation.</summary>
    /// <returns>The BuildBinaryAckResponse operation result.</returns>
    private static byte[] BuildBinaryAckResponse()
        => Convert.FromHexString("10020C00F80000FFFF030000FFFF000010034137");

    /// <summary>Executes the BuildBinaryDataResponse operation.</summary>
    /// <param name="payload">The payload parameter.</param>
    /// <returns>The BuildBinaryDataResponse operation result.</returns>
    private static byte[] BuildBinaryDataResponse(byte[] payload)
    {
        var frame = new List<byte>
        {
            0xF8,
            0x00,
            0x00,
            0xFF,
            0xFF,
            0x03,
            0x00,
            0x00,
            0xFF,
            0xFF,
            0x00,
            0x00,
        };

        frame.AddRange(payload);
        frame.Add(0x10);
        frame.Add(0x03);

        var numberOfDataBytes = checked((ushort)(frame.Count - 2));
        var prefix = new List<byte> { 0x10, 0x02, (byte)(numberOfDataBytes & 0xFF), (byte)(numberOfDataBytes >> 8) };
        prefix.AddRange(frame);
        prefix.AddRange(Encoding.ASCII.GetBytes((prefix.Skip(2).Take(2 + numberOfDataBytes).Aggregate(0, static (sum, value) => sum + value) & 0xFF).ToString("X2")));
        return prefix.ToArray();
    }

    /// <summary>Executes the ComputeChecksum operation.</summary>
    /// <param name="body">The body parameter.</param>
    /// <returns>The ComputeChecksum operation result.</returns>
    private static string ComputeChecksum(string body)
        => (Encoding.ASCII.GetBytes(body).Aggregate(0, static (sum, value) => sum + value) & 0xFF).ToString("X2");
}
