// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive.Tests;
#else

namespace MitsubishiRx.Tests;
#endif

/// <summary>Provides the MitsubishiUdpTests type.</summary>
public sealed class MitsubishiUdpTests
{
    /// <summary>Executes the ReadWordsAsyncAsciiUdpRoundTripsThroughDynamicResponse operation.</summary>
    /// <returns>The ReadWordsAsyncAsciiUdpRoundTripsThroughDynamicResponse operation result.</returns>
    [Test]
    public async Task ReadWordsAsyncAsciiUdpRoundTripsThroughDynamicResponse()
    {
        var transport = new FakeTransport(request =>
        {
            _ = System.Text.Encoding.ASCII.GetString(request.Payload);
            return System.Text.Encoding.ASCII.GetBytes("D00000FF03FF000006000000425678");
        });

        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5014,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Ascii,
            TransportKind: MitsubishiTransportKind.Udp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010);

        var client = new MitsubishiRx(options, transport, Scheduler.Immediate);
        var result = await client.ReadWordsAsync("D100", 2);
        if (!result.IsSucceed)
        {
            throw new InvalidOperationException(result.Err);
        }

        await Assert.That(result.IsSucceed).IsTrue();
        await Assert.That(result.Value!.Select(static value => (int)value).ToArray()).IsEquivalentTo([0x0042, 0x5678]);
        await Assert.That(transport.Requests[0].Description).IsEqualTo("Read words D100");
    }
}
