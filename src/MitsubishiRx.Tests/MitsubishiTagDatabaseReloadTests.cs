// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Microsoft.Reactive.Testing;

namespace MitsubishiRx.Tests;

public sealed class MitsubishiTagDatabaseReloadTests
{
    [Test]
    public async Task LoadAndValidateAppliesLoadedDatabaseWhenSchemaIsValid()
    {
        var path = CreateTempPath("json");
        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition("MotorSpeed", "D100", DataType: "Word", Scale: 0.1, Units: "rpm"),
            new MitsubishiTagDefinition("OperatorMessage", "D600", DataType: "String", Length: 2, Encoding: "Utf8"),
        ]);
        database.AddGroup(new MitsubishiTagGroupDefinition("Overview", ["MotorSpeed", "OperatorMessage"]));
        database.Save(path);

        var client = CreateClient(Scheduler.Immediate);

        try
        {
            var result = client.LoadAndValidateTagDatabase(path);

            await Assert.That(result.IsSucceed).IsTrue();
            await Assert.That(client.TagDatabase is not null).IsTrue();
            await Assert.That(client.TagDatabase!.Count).IsEqualTo(2);
            await Assert.That(client.TagDatabase.GroupCount).IsEqualTo(1);
            await Assert.That(client.TagDatabase.GetRequired("OperatorMessage").Encoding).IsEqualTo("Utf8");
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Test]
    public async Task LoadAndValidateReturnsErrorsAndDoesNotReplaceExistingDatabaseWhenSchemaIsInvalid()
    {
        var path = CreateTempPath("json");
        File.WriteAllText(path, """
        {
          "tags": [
            {
              "name": "BadString",
              "address": "D100",
              "dataType": "String"
            }
          ]
        }
        """);

        var existing = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition("ExistingTag", "D200", DataType: "UInt16"),
        ]);

        var client = CreateClient(Scheduler.Immediate);
        client.TagDatabase = existing;

        try
        {
            var result = client.LoadAndValidateTagDatabase(path);

            await Assert.That(result.IsSucceed).IsFalse();
            await Assert.That(result.Err.Contains("must define a positive Length", StringComparison.OrdinalIgnoreCase)).IsTrue();
            await Assert.That(ReferenceEquals(client.TagDatabase, existing)).IsTrue();
            await Assert.That(client.TagDatabase!.GetRequired("ExistingTag").Address).IsEqualTo("D200");
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Test]
    public async Task ObserveTagDatabaseReloadEmitsSuccessfulReloadsWhenSchemaChanges()
    {
        var scheduler = new TestScheduler();
        var path = CreateTempPath("json");
        CreateDatabase("MotorSpeed", "D100").Save(path);

        var client = CreateClient(scheduler);
        var received = new List<Responce<MitsubishiTagDatabase>>();

        try
        {
            using var subscription = client
                .ObserveTagDatabaseReload(path, TimeSpan.FromSeconds(5), emitInitial: true)
                .Take(2)
                .Subscribe(received.Add);

            scheduler.AdvanceBy(1);
            CreateDatabase("MotorSpeed", "D101").Save(path);
            scheduler.AdvanceBy(TimeSpan.FromSeconds(5).Ticks + 1);

            await Assert.That(received.Count).IsEqualTo(2);
            await Assert.That(received[0].IsSucceed).IsTrue();
            await Assert.That(received[1].IsSucceed).IsTrue();
            await Assert.That(received[0].Value!.GetRequired("MotorSpeed").Address).IsEqualTo("D100");
            await Assert.That(received[1].Value!.GetRequired("MotorSpeed").Address).IsEqualTo("D101");
            await Assert.That(client.TagDatabase!.GetRequired("MotorSpeed").Address).IsEqualTo("D101");
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Test]
    public async Task ObserveTagDatabaseReloadEmitsFailureForInvalidUpdateAndPreservesLastValidDatabase()
    {
        var scheduler = new TestScheduler();
        var path = CreateTempPath("json");
        CreateDatabase("MotorSpeed", "D100").Save(path);

        var client = CreateClient(scheduler);
        var received = new List<Responce<MitsubishiTagDatabase>>();

        try
        {
            using var subscription = client
                .ObserveTagDatabaseReload(path, TimeSpan.FromSeconds(5), emitInitial: true)
                .Take(2)
                .Subscribe(received.Add);

            scheduler.AdvanceBy(1);
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

            await Assert.That(received.Count).IsEqualTo(2);
            await Assert.That(received[0].IsSucceed).IsTrue();
            await Assert.That(received[1].IsSucceed).IsFalse();
            await Assert.That(received[1].Err.Contains("must define a positive Length", StringComparison.OrdinalIgnoreCase)).IsTrue();
            await Assert.That(client.TagDatabase!.GetRequired("MotorSpeed").Address).IsEqualTo("D100");
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
            Port: 5040,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010,
            XyNotation: XyAddressNotation.Octal);

        return new MitsubishiRx(options, new FakeTransport([]), scheduler);
    }

    private static MitsubishiTagDatabase CreateDatabase(string tagName, string address)
    {
        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition(tagName, address, DataType: "Word", Scale: 0.1, Units: "rpm"),
        ]);
        database.AddGroup(new MitsubishiTagGroupDefinition("Overview", [tagName]));
        return database;
    }

    private static string CreateTempPath(string extension)
        => Path.Combine(Path.GetTempPath(), $"mitsubishirx-reload-{Guid.NewGuid():N}.{extension}");

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
