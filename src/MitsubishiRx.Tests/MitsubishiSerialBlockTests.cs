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

/// <summary>Provides the MitsubishiSerialBlockTests type.</summary>
public sealed class MitsubishiSerialBlockTests
{
    /// <summary>Executes the ReadBlocksAsyncSerial3CFormat1EncodesExpectedRequestAndReturnsRawPayload operation.</summary>
    /// <returns>The ReadBlocksAsyncSerial3CFormat1EncodesExpectedRequestAndReturnsRawPayload operation result.</returns>
    [Test]
    public async Task ReadBlocksAsyncSerial3CFormat1EncodesExpectedRequestAndReturnsRawPayload()
    {
        var transport = new FakeTransport([BuildAsciiBlockReadResponse(MitsubishiFrameType.ThreeC, MitsubishiSerialMessageFormat.Format1, "11223344100010")]);
        var options = CreateSerialOptions(MitsubishiFrameType.ThreeC, CommunicationDataCode.Ascii, MitsubishiSerialMessageFormat.Format1);
        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.ReadBlocksAsync(CreateBlockRequest());

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(Encoding.ASCII.GetString(result.Value!)).IsEqualTo("11223344100010");
        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[0].Payload)).IsEqualTo("\u0005F90000FF000406000000010001000064D*000200000AM*0003FC");
    }

    /// <summary>Executes the WriteBlocksAsyncSerial3CFormat1EncodesExpectedRequest operation.</summary>
    /// <returns>The WriteBlocksAsyncSerial3CFormat1EncodesExpectedRequest operation result.</returns>
    [Test]
    public async Task WriteBlocksAsyncSerial3CFormat1EncodesExpectedRequest()
    {
        var transport = new FakeTransport([BuildAsciiAckResponse(MitsubishiFrameType.ThreeC, MitsubishiSerialMessageFormat.Format1)]);
        var options = CreateSerialOptions(MitsubishiFrameType.ThreeC, CommunicationDataCode.Ascii, MitsubishiSerialMessageFormat.Format1);
        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.WriteBlocksAsync(CreateBlockRequest());

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[0].Payload)).IsEqualTo("\u0005F90000FF001406000000010001000064D*00021122334400000AM*0003100010B3");
    }

    /// <summary>Executes the ReadBlocksAsyncSerial4CFormat5EncodesExpectedRequestAndReturnsRawPayload operation.</summary>
    /// <returns>The ReadBlocksAsyncSerial4CFormat5EncodesExpectedRequestAndReturnsRawPayload operation result.</returns>
    [Test]
    public async Task ReadBlocksAsyncSerial4CFormat5EncodesExpectedRequestAndReturnsRawPayload()
    {
        var transport = new FakeTransport([BuildBinaryBlockReadResponse()]);
        var options = CreateSerialOptions(MitsubishiFrameType.FourC, CommunicationDataCode.Binary, MitsubishiSerialMessageFormat.Format5);
        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.ReadBlocksAsync(CreateBlockRequest());

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(Convert.ToHexString(result.Value!)).IsEqualTo("22114433100010");
        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        await Assert.That(Convert.ToHexString(transport.Requests[0].Payload)).IsEqualTo("10021C00F80000FFFF0300000604000001000100640000A802000A000090030010034343");
    }

    /// <summary>Executes the WriteBlocksAsyncSerial4CFormat5EncodesExpectedRequest operation.</summary>
    /// <returns>The WriteBlocksAsyncSerial4CFormat5EncodesExpectedRequest operation result.</returns>
    [Test]
    public async Task WriteBlocksAsyncSerial4CFormat5EncodesExpectedRequest()
    {
        var transport = new FakeTransport([BuildBinaryAckResponse()]);
        var options = CreateSerialOptions(MitsubishiFrameType.FourC, CommunicationDataCode.Binary, MitsubishiSerialMessageFormat.Format5);
        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.WriteBlocksAsync(CreateBlockRequest());

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        await Assert.That(Convert.ToHexString(transport.Requests[0].Payload)).IsEqualTo("10022300F80000FFFF0300000614000001000100640000A80200221144330A000090030010001010034144");
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

    /// <summary>Executes the BuildAsciiBlockReadResponse operation.</summary>
    /// <param name="frameType">The frameType parameter.</param>
    /// <param name="format">The format parameter.</param>
    /// <param name="payload">The payload parameter.</param>
    /// <returns>The BuildAsciiBlockReadResponse operation result.</returns>
    private static byte[] BuildAsciiBlockReadResponse(MitsubishiFrameType frameType, MitsubishiSerialMessageFormat format, string payload)
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

    /// <summary>Executes the BuildBinaryBlockReadResponse operation.</summary>
    /// <returns>The BuildBinaryBlockReadResponse operation result.</returns>
    private static byte[] BuildBinaryBlockReadResponse()
        => Convert.FromHexString("10021300F80000FFFF030000FFFF00002211443310001010034434");

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
