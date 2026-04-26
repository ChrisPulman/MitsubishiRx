// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Ports;
using System.Reactive.Concurrency;
using System.Text;

namespace MitsubishiRx.Tests;

public sealed class MitsubishiSerialRemoteControlTests
{
    [Test]
    public async Task RemoteRunAsyncSerial3CFormat1EncodesExpectedRequest()
    {
        var transport = new FakeTransport([BuildAsciiAckResponse(MitsubishiFrameType.ThreeC, MitsubishiSerialMessageFormat.Format1)]);
        var options = CreateSerialOptions(MitsubishiFrameType.ThreeC, CommunicationDataCode.Ascii, MitsubishiSerialMessageFormat.Format1);
        var client = new global::MitsubishiRx.MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.RemoteRunAsync(force: true, clearMode: true);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[0].Payload)).IsEqualTo("\u0005F90000FF0010010000000100012F");
    }

    [Test]
    public async Task RemoteStopAsyncSerial3CFormat1EncodesExpectedRequest()
    {
        var transport = new FakeTransport([BuildAsciiAckResponse(MitsubishiFrameType.ThreeC, MitsubishiSerialMessageFormat.Format1)]);
        var options = CreateSerialOptions(MitsubishiFrameType.ThreeC, CommunicationDataCode.Ascii, MitsubishiSerialMessageFormat.Format1);
        var client = new global::MitsubishiRx.MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.RemoteStopAsync();

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[0].Payload)).IsEqualTo("\u0005F90000FF0010020000AE");
    }

    [Test]
    public async Task RemotePauseAsyncSerial3CFormat1EncodesExpectedRequest()
    {
        var transport = new FakeTransport([BuildAsciiAckResponse(MitsubishiFrameType.ThreeC, MitsubishiSerialMessageFormat.Format1)]);
        var options = CreateSerialOptions(MitsubishiFrameType.ThreeC, CommunicationDataCode.Ascii, MitsubishiSerialMessageFormat.Format1);
        var client = new global::MitsubishiRx.MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.RemotePauseAsync();

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[0].Payload)).IsEqualTo("\u0005F90000FF0010030000AF");
    }

    [Test]
    public async Task RemoteLatchClearAsyncSerial3CFormat1EncodesExpectedRequest()
    {
        var transport = new FakeTransport([BuildAsciiAckResponse(MitsubishiFrameType.ThreeC, MitsubishiSerialMessageFormat.Format1)]);
        var options = CreateSerialOptions(MitsubishiFrameType.ThreeC, CommunicationDataCode.Ascii, MitsubishiSerialMessageFormat.Format1);
        var client = new global::MitsubishiRx.MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.RemoteLatchClearAsync();

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[0].Payload)).IsEqualTo("\u0005F90000FF0010050000B1");
    }

    [Test]
    public async Task RemoteResetAsyncSerial3CFormat1EncodesExpectedRequest()
    {
        var transport = new FakeTransport([BuildAsciiAckResponse(MitsubishiFrameType.ThreeC, MitsubishiSerialMessageFormat.Format1)]);
        var options = CreateSerialOptions(MitsubishiFrameType.ThreeC, CommunicationDataCode.Ascii, MitsubishiSerialMessageFormat.Format1);
        var client = new global::MitsubishiRx.MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.RemoteResetAsync();

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[0].Payload)).IsEqualTo("\u0005F90000FF0010060000B2");
    }

    [Test]
    public async Task RemoteRunAsyncSerial4CFormat5EncodesExpectedRequest()
    {
        var transport = new FakeTransport([BuildBinaryAckResponse()]);
        var options = CreateSerialOptions(MitsubishiFrameType.FourC, CommunicationDataCode.Binary, MitsubishiSerialMessageFormat.Format5);
        var client = new global::MitsubishiRx.MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.RemoteRunAsync(force: true, clearMode: true);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(Convert.ToHexString(transport.Requests[0].Payload)).IsEqualTo("10021000F80000FFFF030000011000000100010010033143");
    }

    [Test]
    public async Task RemoteStopAsyncSerial4CFormat5EncodesExpectedRequest()
    {
        var transport = new FakeTransport([BuildBinaryAckResponse()]);
        var options = CreateSerialOptions(MitsubishiFrameType.FourC, CommunicationDataCode.Binary, MitsubishiSerialMessageFormat.Format5);
        var client = new global::MitsubishiRx.MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.RemoteStopAsync();

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(Convert.ToHexString(transport.Requests[0].Payload)).IsEqualTo("10020C00F80000FFFF0300000210000010033137");
    }

    [Test]
    public async Task RemotePauseAsyncSerial4CFormat5EncodesExpectedRequest()
    {
        var transport = new FakeTransport([BuildBinaryAckResponse()]);
        var options = CreateSerialOptions(MitsubishiFrameType.FourC, CommunicationDataCode.Binary, MitsubishiSerialMessageFormat.Format5);
        var client = new global::MitsubishiRx.MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.RemotePauseAsync();

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(Convert.ToHexString(transport.Requests[0].Payload)).IsEqualTo("10020C00F80000FFFF0300000310000010033138");
    }

    [Test]
    public async Task RemoteLatchClearAsyncSerial4CFormat5EncodesExpectedRequest()
    {
        var transport = new FakeTransport([BuildBinaryAckResponse()]);
        var options = CreateSerialOptions(MitsubishiFrameType.FourC, CommunicationDataCode.Binary, MitsubishiSerialMessageFormat.Format5);
        var client = new global::MitsubishiRx.MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.RemoteLatchClearAsync();

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(Convert.ToHexString(transport.Requests[0].Payload)).IsEqualTo("10020C00F80000FFFF0300000510000010033141");
    }

    [Test]
    public async Task RemoteResetAsyncSerial4CFormat5EncodesExpectedRequest()
    {
        var transport = new FakeTransport([BuildBinaryAckResponse()]);
        var options = CreateSerialOptions(MitsubishiFrameType.FourC, CommunicationDataCode.Binary, MitsubishiSerialMessageFormat.Format5);
        var client = new global::MitsubishiRx.MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.RemoteResetAsync();

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(Convert.ToHexString(transport.Requests[0].Payload)).IsEqualTo("10020C00F80000FFFF0300000610000010033142");
    }

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

    private static byte[] BuildBinaryAckResponse()
        => Convert.FromHexString("10020C00F80000FFFF030000FFFF000010034137");

    private static string ComputeChecksum(string body)
        => (Encoding.ASCII.GetBytes(body).Aggregate(0, static (sum, value) => sum + value) & 0xFF).ToString("X2");
}
