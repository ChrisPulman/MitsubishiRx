// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;

namespace MitsubishiRx.Tests;

public sealed class MitsubishiHardeningTests
{
    [Test]
    public async Task RemoteRunAsyncEncodesAscii3EForceAndClearMode()
    {
        var transport = new FakeTransport(
        [
            System.Text.Encoding.ASCII.GetBytes("D00000FF03FF0000020000"),
        ]);

        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5010,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Ascii,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010);

        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);
        var result = await client.RemoteRunAsync(force: true, clearMode: true);
        if (!result.IsSucceed)
        {
            throw new InvalidOperationException(result.Err);
        }

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(System.Text.Encoding.ASCII.GetString(transport.Requests[0].Payload)).IsEqualTo("500000FF03FF00000E00101001000000010001");
    }

    [Test]
    public async Task RegisterMonitorAsyncEncodesAscii3EAddresses()
    {
        var transport = new FakeTransport(
        [
            System.Text.Encoding.ASCII.GetBytes("D00000FF03FF0000020000"),
        ]);

        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5011,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Ascii,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010);

        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);
        var result = await client.RegisterMonitorAsync(["D100", "D101"]);
        if (!result.IsSucceed)
        {
            throw new InvalidOperationException(result.Err);
        }

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(System.Text.Encoding.ASCII.GetString(transport.Requests[0].Payload)).IsEqualTo("500000FF03FF00001A0010080100000200000064D*000065D*");
    }

    [Test]
    public async Task RandomReadWordsAsyncPreservesHexadecimalXyNotation()
    {
        var transport = new FakeTransport(
        [
            [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x06, 0x00, 0x00, 0x00, 0x34, 0x12, 0x78, 0x56],
        ]);

        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5012,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010,
            XyNotation: XyAddressNotation.Hexadecimal);

        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);
        var result = await client.RandomReadWordsAsync(["X10", "Y1F"]);

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(transport.Requests[0].Payload.Skip(15).Take(8).Select(static value => (int)value).ToArray()).IsEquivalentTo(
        [0x02, 0x00, 0x10, 0x00, 0x00, 0x9C, 0x1F, 0x00]);
    }

    [Test]
    public async Task ReadBlocksAsyncEncodesAscii3EBlockCounts()
    {
        var transport = new FakeTransport(
        [
            System.Text.Encoding.ASCII.GetBytes("D00000FF03FF0000020000"),
        ]);

        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5013,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Ascii,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010);

        var request = new MitsubishiBlockRequest(
            WordBlocks:
            [
                new MitsubishiWordBlock(MitsubishiDeviceAddress.Parse("D100", XyAddressNotation.Octal), new ushort[2]),
            ],
            BitBlocks:
            [
                new MitsubishiBitBlock(MitsubishiDeviceAddress.Parse("M10", XyAddressNotation.Octal), new bool[3]),
            ]);

        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);
        var result = await client.ReadBlocksAsync(request);
        if (!result.IsSucceed)
        {
            throw new InvalidOperationException(result.Err);
        }

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(System.Text.Encoding.ASCII.GetString(transport.Requests[0].Payload)).IsEqualTo("500000FF03FF00002600100406000000010001000064D*000200000AM*0003");
    }
}
