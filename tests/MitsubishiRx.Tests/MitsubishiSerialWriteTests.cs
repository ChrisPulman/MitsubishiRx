// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Ports;
using System.Reactive.Concurrency;
using System.Text;

namespace MitsubishiRx.Tests;

public sealed class MitsubishiSerialWriteTests
{
    [Test]
    public async Task WriteWordsAsyncSupportsSerial1CFormat1()
    {
        var options = CreateOptions(MitsubishiFrameType.OneC, CommunicationDataCode.Ascii, MitsubishiSerialMessageFormat.Format1);
        var transport = new CapturingTransport(BuildAsciiAckResponse(MitsubishiFrameType.OneC, MitsubishiSerialMessageFormat.Format1));
        var client = new MitsubishiRx.MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.WriteWordsAsync("D100", new ushort[] { 0x1234, 0x5678 });

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[0].Payload)).IsEqualTo("0500FFWW0D010002123456787E");
    }

    [Test]
    public async Task WriteWordsAsyncSupportsSerial3CFormat1()
    {
        var options = CreateOptions(MitsubishiFrameType.ThreeC, CommunicationDataCode.Ascii, MitsubishiSerialMessageFormat.Format1);
        var transport = new CapturingTransport(BuildAsciiAckResponse(MitsubishiFrameType.ThreeC, MitsubishiSerialMessageFormat.Format1));
        var client = new MitsubishiRx.MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.WriteWordsAsync("D100", new ushort[] { 0x1234, 0x5678 });

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        await Assert.That(Encoding.ASCII.GetString(transport.Requests[0].Payload)).IsEqualTo("05F90000FF000140100000064D*0002123456788C");
    }

    [Test]
    public async Task WriteWordsAsyncSupportsSerial4CFormat5Binary()
    {
        var options = CreateOptions(MitsubishiFrameType.FourC, CommunicationDataCode.Binary, MitsubishiSerialMessageFormat.Format5);
        var transport = new CapturingTransport(BuildBinaryAckResponse());
        var client = new MitsubishiRx.MitsubishiRx(options, transport, Scheduler.Immediate);

        var result = await client.WriteWordsAsync("D100", new ushort[] { 0x1234, 0x5678 });

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        await Assert.That(Convert.ToHexString(transport.Requests[0].Payload)).IsEqualTo("10021400F80000FFFF0300000114010000640000A802003412785610034346");
    }

    private static MitsubishiClientOptions CreateOptions(
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
            MitsubishiFrameType.OneC => "0600FF",
            MitsubishiFrameType.ThreeC => "06F90000FF",
            MitsubishiFrameType.FourC => "06F80000FF03FF00",
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

    private sealed class CapturingTransport : IMitsubishiTransport
    {
        private readonly byte[] _response;

        public CapturingTransport(byte[] response)
        {
            _response = response;
        }

        public List<MitsubishiTransportRequest> Requests { get; } = new();

        public bool IsConnected { get; private set; }

        public ValueTask ConnectAsync(MitsubishiClientOptions options, CancellationToken cancellationToken = default)
        {
            IsConnected = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
        {
            IsConnected = false;
            return ValueTask.CompletedTask;
        }

        public ValueTask<byte[]> ExchangeAsync(MitsubishiTransportRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return ValueTask.FromResult(_response);
        }

        public void Dispose()
        {
            IsConnected = false;
        }

        public ValueTask DisposeAsync()
        {
            IsConnected = false;
            return ValueTask.CompletedTask;
        }
    }
}
