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

/// <summary>Provides the MitsubishiSerialBitTests type.</summary>
public sealed class MitsubishiSerialBitTests
{
    /// <summary>Executes the ReadBitsAsyncSerial1CFormat1EncodesExpectedRequestAndParsesResponse operation.</summary>
    /// <returns>The ReadBitsAsyncSerial1CFormat1EncodesExpectedRequestAndParsesResponse operation result.</returns>
    [Test]
    public async Task ReadBitsAsyncSerial1CFormat1EncodesExpectedRequestAndParsesResponse()
    {
        var transport = new FakeTransport([BuildAsciiBitReadResponse(MitsubishiFrameType.OneC, MitsubishiSerialMessageFormat.Format1, "1011")]);
        var options = CreateSerialOptions(MitsubishiFrameType.OneC, CommunicationDataCode.Ascii, MitsubishiSerialMessageFormat.Format1);
        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.ReadBitsAsync("M10", 4);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value!).IsEquivalentTo([ true, false, true, true]);
        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[0].Payload)).IsEqualTo("\u000500FFBR0M00100422");
    }

    /// <summary>Executes the WriteBitsAsyncSerial1CFormat1EncodesExpectedRequest operation.</summary>
    /// <returns>The WriteBitsAsyncSerial1CFormat1EncodesExpectedRequest operation result.</returns>
    [Test]
    public async Task WriteBitsAsyncSerial1CFormat1EncodesExpectedRequest()
    {
        var transport = new FakeTransport([BuildAsciiAckResponse(MitsubishiFrameType.OneC, MitsubishiSerialMessageFormat.Format1)]);
        var options = CreateSerialOptions(MitsubishiFrameType.OneC, CommunicationDataCode.Ascii, MitsubishiSerialMessageFormat.Format1);
        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.WriteBitsAsync("M10", [true, false, true, true]);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[0].Payload)).IsEqualTo("\u000500FFBW0M0010041011EA");
    }

    /// <summary>Executes the ReadBitsAsyncSerial3CFormat1EncodesExpectedRequestAndParsesResponse operation.</summary>
    /// <returns>The ReadBitsAsyncSerial3CFormat1EncodesExpectedRequestAndParsesResponse operation result.</returns>
    [Test]
    public async Task ReadBitsAsyncSerial3CFormat1EncodesExpectedRequestAndParsesResponse()
    {
        var transport = new FakeTransport([BuildAsciiBitReadResponse(MitsubishiFrameType.ThreeC, MitsubishiSerialMessageFormat.Format1, "1011")]);
        var options = CreateSerialOptions(MitsubishiFrameType.ThreeC, CommunicationDataCode.Ascii, MitsubishiSerialMessageFormat.Format1);
        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.ReadBitsAsync("M10", 4);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value!).IsEquivalentTo([ true, false, true, true]);
        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[0].Payload)).IsEqualTo("\u0005F90000FF000401000100000AM*04BD");
    }

    /// <summary>Executes the WriteBitsAsyncSerial3CFormat1EncodesExpectedRequest operation.</summary>
    /// <returns>The WriteBitsAsyncSerial3CFormat1EncodesExpectedRequest operation result.</returns>
    [Test]
    public async Task WriteBitsAsyncSerial3CFormat1EncodesExpectedRequest()
    {
        var transport = new FakeTransport([BuildAsciiAckResponse(MitsubishiFrameType.ThreeC, MitsubishiSerialMessageFormat.Format1)]);
        var options = CreateSerialOptions(MitsubishiFrameType.ThreeC, CommunicationDataCode.Ascii, MitsubishiSerialMessageFormat.Format1);
        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.WriteBitsAsync("M10", [true, false, true, true]);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[0].Payload)).IsEqualTo("\u0005F90000FF001401000100000AM*00041011E1");
    }

    /// <summary>Executes the ReadBitsAsyncSerial4CFormat5EncodesExpectedRequestAndParsesResponse operation.</summary>
    /// <returns>The ReadBitsAsyncSerial4CFormat5EncodesExpectedRequestAndParsesResponse operation result.</returns>
    [Test]
    public async Task ReadBitsAsyncSerial4CFormat5EncodesExpectedRequestAndParsesResponse()
    {
        var transport = new FakeTransport([BuildBinaryBitReadResponse()]);
        var options = CreateSerialOptions(MitsubishiFrameType.FourC, CommunicationDataCode.Binary, MitsubishiSerialMessageFormat.Format5);
        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.ReadBitsAsync("M10", 4);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value!).IsEquivalentTo([ true, false, true, true]);
        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        await Assert.That(Convert.ToHexString(transport.Requests[0].Payload)).IsEqualTo("10021200F80000FFFF030000010401000A000090040010034146");
    }

    /// <summary>Executes the WriteBitsAsyncSerial4CFormat5EncodesExpectedRequest operation.</summary>
    /// <returns>The WriteBitsAsyncSerial4CFormat5EncodesExpectedRequest operation result.</returns>
    [Test]
    public async Task WriteBitsAsyncSerial4CFormat5EncodesExpectedRequest()
    {
        var transport = new FakeTransport([BuildBinaryAckResponse()]);
        var options = CreateSerialOptions(MitsubishiFrameType.FourC, CommunicationDataCode.Binary, MitsubishiSerialMessageFormat.Format5);
        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.WriteBitsAsync("M10", [true, false, true, true]);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        await Assert.That(Convert.ToHexString(transport.Requests[0].Payload)).IsEqualTo("10021600F80000FFFF030000011401000A00009004001000101010034633");
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

    /// <summary>Executes the BuildAsciiBitReadResponse operation.</summary>
    /// <param name="frameType">The frameType parameter.</param>
    /// <param name="format">The format parameter.</param>
    /// <param name="payload">The payload parameter.</param>
    /// <returns>The BuildAsciiBitReadResponse operation result.</returns>
    private static byte[] BuildAsciiBitReadResponse(MitsubishiFrameType frameType, MitsubishiSerialMessageFormat format, string payload)
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

    /// <summary>Executes the BuildBinaryBitReadResponse operation.</summary>
    /// <returns>The BuildBinaryBitReadResponse operation result.</returns>
    private static byte[] BuildBinaryBitReadResponse()
        => Convert.FromHexString("10020E00F80000FFFF030000FFFF0000011110033137");

    /// <summary>Executes the BuildBinaryAckResponse operation.</summary>
    /// <returns>The BuildBinaryAckResponse operation result.</returns>
    private static byte[] BuildBinaryAckResponse()
        => Convert.FromHexString("10020C00F80000FFFF030000FFFF000010034137");

    /// <summary>Executes the ComputeChecksum operation.</summary>
    /// <param name="body">The body parameter.</param>
    /// <returns>The ComputeChecksum operation result.</returns>
    private static string ComputeChecksum(string body)
        => (Encoding.ASCII.GetBytes(body).Aggregate(0, static (sum, value) => sum + value) & 0xFF).ToString("X2");
}
