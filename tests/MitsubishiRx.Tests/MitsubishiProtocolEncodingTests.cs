// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;

namespace MitsubishiRx.Tests;

public sealed class MitsubishiProtocolEncodingTests
{
    [Test]
    public async Task ReadWordsAsyncEncodesBinary3ERequest()
    {
        var transport = new FakeTransport(
        [
            [
                0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x06, 0x00, 0x00, 0x00, 0x34, 0x12, 0x78, 0x56,
            ],
        ]);

        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5000,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010);

        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);
        var result = await client.ReadWordsAsync("D100", 2);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value!.Select(static value => (int)value).ToArray()).IsEquivalentTo([0x1234, 0x5678]);
        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        await Assert.That(transport.Requests[0].Payload.Select(static value => (int)value).ToArray()).IsEquivalentTo(
        [
            0x50, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x0C, 0x00, 0x10, 0x00, 0x01, 0x04, 0x00, 0x00, 0x64, 0x00, 0x00, 0xA8, 0x02, 0x00,
        ]);
    }

    [Test]
    public async Task ExecuteRawAsyncEncodesBinary4ERequestWithSerialNumber()
    {
        var transport = new FakeTransport(
        [
            [
                0xD4, 0x00, 0x34, 0x12, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00,
            ],
        ]);

        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5001,
            FrameType: MitsubishiFrameType.FourE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010,
            SerialNumberProvider: () => 0x1234);

        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);
        var result = await client.ExecuteRawAsync(new MitsubishiRawCommandRequest(0x0101, 0x0000));

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        await Assert.That(transport.Requests[0].Payload.Select(static value => (int)value).ToArray()).IsEquivalentTo(
        [
            0x54, 0x00, 0x34, 0x12, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x06, 0x00, 0x10, 0x00, 0x01, 0x01, 0x00, 0x00,
        ]);
    }

    [Test]
    public async Task ReadWordsAsyncEncodesBinary1ERequest()
    {
        var transport = new FakeTransport(
        [
            [0x81, 0x00, 0x34, 0x12, 0x78, 0x56],
        ]);

        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5002,
            FrameType: MitsubishiFrameType.OneE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            MonitoringTimer: 0x0010,
            LegacyPcNumber: 0xFF);

        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);
        var result = await client.ReadWordsAsync("D100", 2);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value!.Select(static value => (int)value).ToArray()).IsEquivalentTo([0x1234, 0x5678]);
        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        await Assert.That(transport.Requests[0].Payload.Select(static value => (int)value).ToArray()).IsEquivalentTo(
        [
            0x01, 0xFF, 0x10, 0x00, 0x64, 0x00, 0x00, 0x00, 0x20, 0x44, 0x02, 0x00,
        ]);
        await Assert.That(transport.Requests[0].ExpectedResponseLength).IsEqualTo(6);
    }

    [Test]
    public async Task ReadWordsAsyncParsesAscii3EResponse()
    {
        var asciiResponse = System.Text.Encoding.ASCII.GetBytes("D00000FF03FF000006000012345678");
        var transport = new FakeTransport([asciiResponse]);
        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5004,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Ascii,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010);

        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);
        var result = await client.ReadWordsAsync("D100", 2);
        if (!result.IsSucceed)
        {
            throw new InvalidOperationException(result.Err);
        }

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value!.Select(static value => (int)value).ToArray()).IsEquivalentTo([0x1234, 0x5678]);
        await Assert.That(System.Text.Encoding.ASCII.GetString(transport.Requests[0].Payload)).IsEqualTo("500000FF03FF000012001004010000000064D*0002");
    }

    [Test]
    public async Task ReadMemoryAsyncEncodesRequestedLength()
    {
        var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x08, 0x00, 0x00, 0x00, 0x34, 0x12, 0x78, 0x56, 0xBC, 0x9A],
        ]);

        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5005,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010);

        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);
        var result = await client.ReadMemoryAsync(MitsubishiCommands.MemoryRead, 0x2000, 3);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value!.Select(static value => (int)value).ToArray()).IsEquivalentTo([0x1234, 0x5678, 0x9ABC]);
        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        var payload = transport.Requests[0].Payload;
        await Assert.That(payload[15]).IsEqualTo((byte)0x00);
        await Assert.That(payload[16]).IsEqualTo((byte)0x20);
        await Assert.That(payload[17]).IsEqualTo((byte)0x03);
        await Assert.That(payload[18]).IsEqualTo((byte)0x00);
    }
}
