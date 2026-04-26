// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Ports;
using System.Text;
using System.Reactive.Concurrency;

namespace MitsubishiRx.Tests;

public sealed class MitsubishiSerialWriteTests
{
    [Test]
    public async Task WriteWordsAsyncSerial1CFormat1EncodesExpectedRequest()
    {
        var transport = new FakeTransport([BuildAsciiAckResponse(MitsubishiFrameType.OneC, MitsubishiSerialMessageFormat.Format1)]);
        var options = CreateSerialOptions(MitsubishiFrameType.OneC, CommunicationDataCode.Ascii, MitsubishiSerialMessageFormat.Format1);
        var client = new global::MitsubishiRx.MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.WriteWordsAsync("D100", new ushort[] { 0x1234, 0x5678 });

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[0].Payload)).IsEqualTo("\u000500FFWW0D01000212345678D5");
    }

    [Test]
    public async Task WriteWordsAsyncSerial3CFormat1EncodesExpectedRequest()
    {
        var transport = new FakeTransport([BuildAsciiAckResponse(MitsubishiFrameType.ThreeC, MitsubishiSerialMessageFormat.Format1)]);
        var options = CreateSerialOptions(MitsubishiFrameType.ThreeC, CommunicationDataCode.Ascii, MitsubishiSerialMessageFormat.Format1);
        var client = new global::MitsubishiRx.MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.WriteWordsAsync("D100", new ushort[] { 0x1234, 0x5678 });

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[0].Payload)).IsEqualTo("\u0005F90000FF0014010000000064D*000212345678AF");
    }

    [Test]
    public async Task WriteWordsAsyncSerial4CFormat5EncodesExpectedRequest()
    {
        var transport = new FakeTransport([BuildBinaryAckResponse()]);
        var options = CreateSerialOptions(MitsubishiFrameType.FourC, CommunicationDataCode.Binary, MitsubishiSerialMessageFormat.Format5);
        var client = new global::MitsubishiRx.MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.WriteWordsAsync("D100", new ushort[] { 0x1234, 0x5678 });

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        await Assert.That(Convert.ToHexString(transport.Requests[0].Payload)).IsEqualTo("10021600F80000FFFF03000001140000640000A802003412785610033436");
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
