// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Microsoft.Reactive.Testing;

namespace MitsubishiRx.Tests;

public sealed class MitsubishiTagDatabaseDiffTests
{
    [Test]
    public async Task CompareToReportsAddedRemovedAndChangedTagsAndGroups()
    {
        var current = CreateCurrentDatabase();
        var updated = CreateUpdatedDatabase();

        var diff = current.CompareWith(updated);

        await Assert.That(diff.HasChanges).IsTrue();
        await Assert.That(diff.AddedTags.Count).IsEqualTo(1);
        await Assert.That(diff.RemovedTags.Count).IsEqualTo(1);
        await Assert.That(diff.ChangedTags.Count).IsEqualTo(1);
        await Assert.That(diff.AddedGroups.Count).IsEqualTo(1);
        await Assert.That(diff.RemovedGroups.Count).IsEqualTo(1);
        await Assert.That(diff.ChangedGroups.Count).IsEqualTo(1);

        await Assert.That(diff.AddedTags[0].Name).IsEqualTo("OperatorMessage");
        await Assert.That(diff.RemovedTags[0].Name).IsEqualTo("LegacyText");
        await Assert.That(diff.ChangedTags[0].Name).IsEqualTo("MotorSpeed");
        await Assert.That(diff.ChangedTags[0].Previous!.Address).IsEqualTo("D100");
        await Assert.That(diff.ChangedTags[0].Current!.Address).IsEqualTo("D101");

        await Assert.That(diff.AddedGroups[0].Name).IsEqualTo("Diagnostics");
        await Assert.That(diff.RemovedGroups[0].Name).IsEqualTo("Legacy");
        await Assert.That(diff.ChangedGroups[0].Name).IsEqualTo("Overview");
    }

    [Test]
    public async Task PreviewTagDatabaseDiffReturnsChangesWithoutReplacingCurrentDatabase()
    {
        var path = CreateTempPath("json");
        CreateUpdatedDatabase().Save(path);

        var client = CreateClient(Scheduler.Immediate);
        client.TagDatabase = CreateCurrentDatabase();

        try
        {
            var result = client.PreviewTagDatabaseDiff(path);

            await Assert.That(result.IsSucceed).IsTrue();
            await Assert.That(result.Value is not null).IsTrue();
            await Assert.That(result.Value!.ChangedTags.Count).IsEqualTo(1);
            await Assert.That(result.Value.AddedTags[0].Name).IsEqualTo("OperatorMessage");
            await Assert.That(client.TagDatabase!.GetRequired("MotorSpeed").Address).IsEqualTo("D100");
            await Assert.That(client.TagDatabase.TryGet("OperatorMessage", out _)).IsFalse();
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Test]
    public async Task ObserveTagDatabaseDiffEmitsSemanticChangesAndAppliesSuccessfulReloads()
    {
        var scheduler = new TestScheduler();
        var path = CreateTempPath("json");
        CreateCurrentDatabase().Save(path);

        var client = CreateClient(scheduler);
        client.TagDatabase = CreateCurrentDatabase();
        var received = new List<Responce<MitsubishiTagDatabaseDiff>>();

        try
        {
            using var subscription = client
                .ObserveTagDatabaseDiff(path, TimeSpan.FromSeconds(5), emitInitial: false)
                .Take(1)
                .Subscribe(received.Add);

            CreateUpdatedDatabase().Save(path);
            scheduler.AdvanceBy(TimeSpan.FromSeconds(5).Ticks + 1);

            await Assert.That(received.Count).IsEqualTo(1);
            await Assert.That(received[0].IsSucceed).IsTrue();
            await Assert.That(received[0].Value is not null).IsTrue();
            await Assert.That(received[0].Value!.ChangedTags.Count).IsEqualTo(1);
            await Assert.That(received[0].Value!.AddedTags[0].Name).IsEqualTo("OperatorMessage");
            await Assert.That(received[0].Value!.RemovedTags[0].Name).IsEqualTo("LegacyText");
            await Assert.That(client.TagDatabase!.GetRequired("MotorSpeed").Address).IsEqualTo("D101");
            await Assert.That(client.TagDatabase.TryGet("OperatorMessage", out _)).IsTrue();
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Test]
    public async Task ObserveTagDatabaseDiffEmitsFailureAndPreservesLastValidDatabaseOnInvalidReload()
    {
        var scheduler = new TestScheduler();
        var path = CreateTempPath("json");
        CreateCurrentDatabase().Save(path);

        var client = CreateClient(scheduler);
        client.TagDatabase = CreateCurrentDatabase();
        var received = new List<Responce<MitsubishiTagDatabaseDiff>>();

        try
        {
            using var subscription = client
                .ObserveTagDatabaseDiff(path, TimeSpan.FromSeconds(5), emitInitial: false)
                .Take(1)
                .Subscribe(received.Add);

            File.WriteAllText(path, """
            {
              "tags": [
                {
                  "name": "BrokenText",
                  "address": "D600",
                  "dataType": "String"
                }
              ]
            }
            """);
            scheduler.AdvanceBy(TimeSpan.FromSeconds(5).Ticks + 1);

            await Assert.That(received.Count).IsEqualTo(1);
            await Assert.That(received[0].IsSucceed).IsFalse();
            await Assert.That(received[0].Err.Contains("must define a positive Length", StringComparison.OrdinalIgnoreCase)).IsTrue();
            await Assert.That(client.TagDatabase!.GetRequired("MotorSpeed").Address).IsEqualTo("D100");
            await Assert.That(client.TagDatabase.TryGet("OperatorMessage", out _)).IsFalse();
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    private static MitsubishiRx CreateClient(IScheduler scheduler)
    {
        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5041,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010,
            XyNotation: XyAddressNotation.Octal);

        return new MitsubishiRx(options, new FakeTransport([]), scheduler);
    }

    private static MitsubishiTagDatabase CreateCurrentDatabase()
    {
        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition("MotorSpeed", "D100", DataType: "Word", Scale: 0.1, Units: "rpm"),
            new MitsubishiTagDefinition("LegacyText", "D200", DataType: "String", Length: 2, Encoding: "Ascii"),
        ]);

        database.AddGroup(new MitsubishiTagGroupDefinition("Overview", ["MotorSpeed", "LegacyText"]));
        database.AddGroup(new MitsubishiTagGroupDefinition("Legacy", ["LegacyText"]));
        return database;
    }

    private static MitsubishiTagDatabase CreateUpdatedDatabase()
    {
        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition("MotorSpeed", "D101", DataType: "Word", Scale: 0.1, Units: "rpm"),
            new MitsubishiTagDefinition("OperatorMessage", "D600", DataType: "String", Length: 2, Encoding: "Utf8"),
        ]);

        database.AddGroup(new MitsubishiTagGroupDefinition("Overview", ["MotorSpeed", "OperatorMessage"]));
        database.AddGroup(new MitsubishiTagGroupDefinition("Diagnostics", ["OperatorMessage"]));
        return database;
    }

    private static string CreateTempPath(string extension)
        => Path.Combine(Path.GetTempPath(), $"mitsubishirx-diff-{Guid.NewGuid():N}.{extension}");

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
