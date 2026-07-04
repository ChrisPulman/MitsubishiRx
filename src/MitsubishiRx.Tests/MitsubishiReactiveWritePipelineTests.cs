// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive.Tests;
#else

namespace MitsubishiRx.Tests;
#endif

/// <summary>Provides the MitsubishiReactiveWritePipelineTests type.</summary>
public sealed class MitsubishiReactiveWritePipelineTests
{
    /// <summary>Executes the QueuedReactiveWordWritePipelinePreservesWriteOrder operation.</summary>
    /// <returns>The QueuedReactiveWordWritePipelinePreservesWriteOrder operation result.</returns>
    [Test]
    public async Task QueuedReactiveWordWritePipelinePreservesWriteOrder()
    {
        var scheduler = new TestScheduler();
        var transport = new FakeTransport(
        [
            Ack(),
            Ack(),
            Ack(),
        ]);

        var client = CreateClient(transport, scheduler);
        var pipeline = client.CreateReactiveWordWritePipeline("D100", MitsubishiReactiveWriteMode.Queued);
        var results = new List<MitsubishiReactiveWriteResult>();
        using var subscription = pipeline.Results.Subscribe(results.Add);

        pipeline.Post([(ushort)1]);
        pipeline.Post([(ushort)2]);
        pipeline.Post([(ushort)3]);
        scheduler.AdvanceBy(1);

        await Assert.That(transport.Requests.Count).IsEqualTo(3);
        await Assert.That(results.Count).IsEqualTo(3);

        var baselineTransport = new FakeTransport([Ack(), Ack(), Ack()]);
        var baselineClient = CreateClient(baselineTransport, scheduler);
        await Assert.That((await baselineClient.WriteWordsAsync("D100", [(ushort)1])).IsSucceed).IsTrue();
        await Assert.That((await baselineClient.WriteWordsAsync("D100", [(ushort)2])).IsSucceed).IsTrue();
        await Assert.That((await baselineClient.WriteWordsAsync("D100", [(ushort)3])).IsSucceed).IsTrue();

        await Assert.That(transport.Requests.Select(static request => Convert.ToHexString(request.Payload)).ToArray())
            .IsEquivalentTo(baselineTransport.Requests.Select(static request => Convert.ToHexString(request.Payload)).ToArray());
    }

    /// <summary>Executes the LatestWinsReactiveWordWritePipelineCollapsesBurstToFinalValue operation.</summary>
    /// <returns>The LatestWinsReactiveWordWritePipelineCollapsesBurstToFinalValue operation result.</returns>
    [Test]
    public async Task LatestWinsReactiveWordWritePipelineCollapsesBurstToFinalValue()
    {
        var scheduler = new TestScheduler();
        var transport = new FakeTransport([Ack()]);
        var client = CreateClient(transport, scheduler);
        var pipeline = client.CreateReactiveWordWritePipeline("D100", MitsubishiReactiveWriteMode.LatestWins);
        var results = new List<MitsubishiReactiveWriteResult>();
        using var subscription = pipeline.Results.Subscribe(results.Add);

        pipeline.Post([(ushort)1]);
        pipeline.Post([(ushort)2]);
        pipeline.Post([(ushort)3]);
        scheduler.AdvanceBy(1);

        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        await Assert.That(results.Count).IsEqualTo(1);

        var baselineTransport = new FakeTransport([Ack()]);
        var baselineClient = CreateClient(baselineTransport, scheduler);
        await Assert.That((await baselineClient.WriteWordsAsync("D100", [(ushort)3])).IsSucceed).IsTrue();
        await Assert.That(Convert.ToHexString(transport.Requests[0].Payload)).IsEqualTo(Convert.ToHexString(baselineTransport.Requests[0].Payload));
    }

    /// <summary>Executes the CoalescingReactiveWordWritePipelineEmitsLatestValueAfterWindow operation.</summary>
    /// <returns>The CoalescingReactiveWordWritePipelineEmitsLatestValueAfterWindow operation result.</returns>
    [Test]
    public async Task CoalescingReactiveWordWritePipelineEmitsLatestValueAfterWindow()
    {
        var scheduler = new TestScheduler();
        var transport = new FakeTransport([Ack()]);
        var client = CreateClient(transport, scheduler);
        var pipeline = client.CreateReactiveWordWritePipeline("D100", MitsubishiReactiveWriteMode.Coalescing, TimeSpan.FromMilliseconds(50));
        var results = new List<MitsubishiReactiveWriteResult>();
        using var subscription = pipeline.Results.Subscribe(results.Add);

        pipeline.Post([(ushort)7]);
        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(20).Ticks);
        pipeline.Post([(ushort)8]);
        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(20).Ticks);
        pipeline.Post([(ushort)8]);
        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(50).Ticks + 1);

        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        await Assert.That(results.Count).IsEqualTo(1);

        var baselineTransport = new FakeTransport([Ack()]);
        var baselineClient = CreateClient(baselineTransport, scheduler);
        await Assert.That((await baselineClient.WriteWordsAsync("D100", [(ushort)8])).IsSucceed).IsTrue();
        await Assert.That(Convert.ToHexString(transport.Requests[0].Payload)).IsEqualTo(Convert.ToHexString(baselineTransport.Requests[0].Payload));
    }

    /// <summary>Executes the QueuedReactiveTagWritePipelineDelegatesThroughTypedTagWriter operation.</summary>
    /// <returns>The QueuedReactiveTagWritePipelineDelegatesThroughTypedTagWriter operation result.</returns>
    [Test]
    public async Task QueuedReactiveTagWritePipelineDelegatesThroughTypedTagWriter()
    {
        var scheduler = new TestScheduler();
        var transport = new FakeTransport([Ack()]);
        var client = CreateClient(transport, scheduler);
        client.TagDatabase = new(
        [
            new MitsubishiTagDefinition("RecipeNumber", "D300", DataType: "UInt16"),
        ]);

        var pipeline = client.CreateReactiveTagWritePipeline<ushort>("RecipeNumber", MitsubishiReactiveWriteMode.Queued);
        var results = new List<MitsubishiReactiveWriteResult>();
        using var subscription = pipeline.Results.Subscribe(results.Add);

        pipeline.Post((ushort)7);
        scheduler.AdvanceBy(1);

        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        await Assert.That(results.Count).IsEqualTo(1);
        await Assert.That(results[0].Target).IsEqualTo("Tag:RecipeNumber");
        await Assert.That(results[0].Success).IsTrue();

        var baselineTransport = new FakeTransport([Ack()]);
        var baselineClient = CreateClient(baselineTransport, scheduler);
        baselineClient.TagDatabase = client.TagDatabase;
        await Assert.That((await baselineClient.WriteUInt16ByTagAsync("RecipeNumber", 7)).IsSucceed).IsTrue();
        await Assert.That(Convert.ToHexString(transport.Requests[0].Payload)).IsEqualTo(Convert.ToHexString(baselineTransport.Requests[0].Payload));
    }

    /// <summary>Executes the CreateClient operation.</summary>
    /// <param name="transport">The transport parameter.</param>
    /// <param name="scheduler">The scheduler parameter.</param>
    /// <returns>The CreateClient operation result.</returns>
    private static MitsubishiRx CreateClient(FakeTransport transport, TestScheduler scheduler)
        => new(
            new MitsubishiClientOptions(
                Host: "127.0.0.1",
                Port: 5003,
                FrameType: MitsubishiFrameType.ThreeE,
                DataCode: CommunicationDataCode.Binary,
                TransportKind: MitsubishiTransportKind.Tcp,
                Route: MitsubishiRoute.Default,
                MonitoringTimer: 0x0010),
            transport,
            scheduler);

    /// <summary>Executes the Ack operation.</summary>
    /// <returns>The Ack operation result.</returns>
    private static byte[] Ack()
        => [0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00];
}
