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

/// <summary>Provides the MitsubishiSerialRemoteControlTests type.</summary>
public sealed class MitsubishiSerialRemoteControlTests
{
    /// <summary>Executes the RemoteRunAsyncSerial3CFormat1EncodesExpectedRequest operation.</summary>
    /// <returns>The RemoteRunAsyncSerial3CFormat1EncodesExpectedRequest operation result.</returns>
    [Test]
    public async Task RemoteRunAsyncSerial3CFormat1EncodesExpectedRequest()
    {
        var transport = new FakeTransport([BuildAsciiAckResponse(MitsubishiFrameType.ThreeC, MitsubishiSerialMessageFormat.Format1)]);
        var options = CreateSerialOptions(MitsubishiFrameType.ThreeC, CommunicationDataCode.Ascii, MitsubishiSerialMessageFormat.Format1);
        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.RemoteRunAsync(force: true, clearMode: true);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[0].Payload)).IsEqualTo("\u0005F90000FF0010010000000100012F");
    }

    /// <summary>Executes the RemoteStopAsyncSerial3CFormat1EncodesExpectedRequest operation.</summary>
    /// <returns>The RemoteStopAsyncSerial3CFormat1EncodesExpectedRequest operation result.</returns>
    [Test]
    public async Task RemoteStopAsyncSerial3CFormat1EncodesExpectedRequest()
    {
        var transport = new FakeTransport([BuildAsciiAckResponse(MitsubishiFrameType.ThreeC, MitsubishiSerialMessageFormat.Format1)]);
        var options = CreateSerialOptions(MitsubishiFrameType.ThreeC, CommunicationDataCode.Ascii, MitsubishiSerialMessageFormat.Format1);
        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.RemoteStopAsync();

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[0].Payload)).IsEqualTo("\u0005F90000FF0010020000AE");
    }

    /// <summary>Executes the RemotePauseAsyncSerial3CFormat1EncodesExpectedRequest operation.</summary>
    /// <returns>The RemotePauseAsyncSerial3CFormat1EncodesExpectedRequest operation result.</returns>
    [Test]
    public async Task RemotePauseAsyncSerial3CFormat1EncodesExpectedRequest()
    {
        var transport = new FakeTransport([BuildAsciiAckResponse(MitsubishiFrameType.ThreeC, MitsubishiSerialMessageFormat.Format1)]);
        var options = CreateSerialOptions(MitsubishiFrameType.ThreeC, CommunicationDataCode.Ascii, MitsubishiSerialMessageFormat.Format1);
        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.RemotePauseAsync();

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[0].Payload)).IsEqualTo("\u0005F90000FF0010030000AF");
    }

    /// <summary>Executes the RemoteLatchClearAsyncSerial3CFormat1EncodesExpectedRequest operation.</summary>
    /// <returns>The RemoteLatchClearAsyncSerial3CFormat1EncodesExpectedRequest operation result.</returns>
    [Test]
    public async Task RemoteLatchClearAsyncSerial3CFormat1EncodesExpectedRequest()
    {
        var transport = new FakeTransport([BuildAsciiAckResponse(MitsubishiFrameType.ThreeC, MitsubishiSerialMessageFormat.Format1)]);
        var options = CreateSerialOptions(MitsubishiFrameType.ThreeC, CommunicationDataCode.Ascii, MitsubishiSerialMessageFormat.Format1);
        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.RemoteLatchClearAsync();

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[0].Payload)).IsEqualTo("\u0005F90000FF0010050000B1");
    }

    /// <summary>Executes the RemoteResetAsyncSerial3CFormat1EncodesExpectedRequest operation.</summary>
    /// <returns>The RemoteResetAsyncSerial3CFormat1EncodesExpectedRequest operation result.</returns>
    [Test]
    public async Task RemoteResetAsyncSerial3CFormat1EncodesExpectedRequest()
    {
        var transport = new FakeTransport([BuildAsciiAckResponse(MitsubishiFrameType.ThreeC, MitsubishiSerialMessageFormat.Format1)]);
        var options = CreateSerialOptions(MitsubishiFrameType.ThreeC, CommunicationDataCode.Ascii, MitsubishiSerialMessageFormat.Format1);
        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.RemoteResetAsync();

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[0].Payload)).IsEqualTo("\u0005F90000FF0010060000B2");
    }

    /// <summary>Executes the RemoteRunAsyncSerial4CFormat5EncodesExpectedRequest operation.</summary>
    /// <returns>The RemoteRunAsyncSerial4CFormat5EncodesExpectedRequest operation result.</returns>
    [Test]
    public async Task RemoteRunAsyncSerial4CFormat5EncodesExpectedRequest()
    {
        var transport = new FakeTransport([BuildBinaryAckResponse()]);
        var options = CreateSerialOptions(MitsubishiFrameType.FourC, CommunicationDataCode.Binary, MitsubishiSerialMessageFormat.Format5);
        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.RemoteRunAsync(force: true, clearMode: true);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(Convert.ToHexString(transport.Requests[0].Payload)).IsEqualTo("10021000F80000FFFF030000011000000100010010033143");
    }

    /// <summary>Executes the RemoteStopAsyncSerial4CFormat5EncodesExpectedRequest operation.</summary>
    /// <returns>The RemoteStopAsyncSerial4CFormat5EncodesExpectedRequest operation result.</returns>
    [Test]
    public async Task RemoteStopAsyncSerial4CFormat5EncodesExpectedRequest()
    {
        var transport = new FakeTransport([BuildBinaryAckResponse()]);
        var options = CreateSerialOptions(MitsubishiFrameType.FourC, CommunicationDataCode.Binary, MitsubishiSerialMessageFormat.Format5);
        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.RemoteStopAsync();

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(Convert.ToHexString(transport.Requests[0].Payload)).IsEqualTo("10020C00F80000FFFF0300000210000010033137");
    }

    /// <summary>Executes the RemotePauseAsyncSerial4CFormat5EncodesExpectedRequest operation.</summary>
    /// <returns>The RemotePauseAsyncSerial4CFormat5EncodesExpectedRequest operation result.</returns>
    [Test]
    public async Task RemotePauseAsyncSerial4CFormat5EncodesExpectedRequest()
    {
        var transport = new FakeTransport([BuildBinaryAckResponse()]);
        var options = CreateSerialOptions(MitsubishiFrameType.FourC, CommunicationDataCode.Binary, MitsubishiSerialMessageFormat.Format5);
        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.RemotePauseAsync();

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(Convert.ToHexString(transport.Requests[0].Payload)).IsEqualTo("10020C00F80000FFFF0300000310000010033138");
    }

    /// <summary>Executes the RemoteLatchClearAsyncSerial4CFormat5EncodesExpectedRequest operation.</summary>
    /// <returns>The RemoteLatchClearAsyncSerial4CFormat5EncodesExpectedRequest operation result.</returns>
    [Test]
    public async Task RemoteLatchClearAsyncSerial4CFormat5EncodesExpectedRequest()
    {
        var transport = new FakeTransport([BuildBinaryAckResponse()]);
        var options = CreateSerialOptions(MitsubishiFrameType.FourC, CommunicationDataCode.Binary, MitsubishiSerialMessageFormat.Format5);
        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.RemoteLatchClearAsync();

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(Convert.ToHexString(transport.Requests[0].Payload)).IsEqualTo("10020C00F80000FFFF0300000510000010033141");
    }

    /// <summary>Executes the RemoteResetAsyncSerial4CFormat5EncodesExpectedRequest operation.</summary>
    /// <returns>The RemoteResetAsyncSerial4CFormat5EncodesExpectedRequest operation result.</returns>
    [Test]
    public async Task RemoteResetAsyncSerial4CFormat5EncodesExpectedRequest()
    {
        var transport = new FakeTransport([BuildBinaryAckResponse()]);
        var options = CreateSerialOptions(MitsubishiFrameType.FourC, CommunicationDataCode.Binary, MitsubishiSerialMessageFormat.Format5);
        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.RemoteResetAsync();

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(Convert.ToHexString(transport.Requests[0].Payload)).IsEqualTo("10020C00F80000FFFF0300000610000010033142");
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

    /// <summary>Executes the ComputeChecksum operation.</summary>
    /// <param name="body">The body parameter.</param>
    /// <returns>The ComputeChecksum operation result.</returns>
    private static string ComputeChecksum(string body)
        => (Encoding.ASCII.GetBytes(body).Aggregate(0, static (sum, value) => sum + value) & 0xFF).ToString("X2");
}
