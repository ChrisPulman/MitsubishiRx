// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive.Tests;
#else

namespace MitsubishiRx.Tests;
#endif

/// <summary>Provides the MitsubishiTagDatabaseRolloutPolicyTests type.</summary>
public sealed class MitsubishiTagDatabaseRolloutPolicyTests
{
    /// <summary>Executes the CompareWithClassifiesMetadataAddressDatatypeAndGroupMembershipChanges operation.</summary>
    /// <returns>The CompareWithClassifiesMetadataAddressDatatypeAndGroupMembershipChanges operation result.</returns>
    [Test]
    public async Task CompareWithClassifiesMetadataAddressDatatypeAndGroupMembershipChanges()
    {
        var current = CreatePolicyCurrentDatabase();
        var updated = CreatePolicyUpdatedDatabase();

        var diff = current.CompareWith(updated);

        await Assert.That(diff.ChangeKinds.HasFlag(MitsubishiSchemaChangeKind.MetadataOnly)).IsTrue();
        await Assert.That(diff.ChangeKinds.HasFlag(MitsubishiSchemaChangeKind.AddressChange)).IsTrue();
        await Assert.That(diff.ChangeKinds.HasFlag(MitsubishiSchemaChangeKind.DataTypeChange)).IsTrue();
        await Assert.That(diff.ChangeKinds.HasFlag(MitsubishiSchemaChangeKind.GroupMembershipChange)).IsTrue();

        var metadataChange = diff.ChangedTags.Single(change => change.Name == "OperatorMessage");
        var addressChange = diff.ChangedTags.Single(change => change.Name == "MotorSpeed");
        var dataTypeChange = diff.ChangedTags.Single(change => change.Name == "ProcessValue");

        await Assert.That(metadataChange.ChangeKinds).IsEqualTo(MitsubishiSchemaChangeKind.MetadataOnly);
        await Assert.That(addressChange.ChangeKinds.HasFlag(MitsubishiSchemaChangeKind.AddressChange)).IsTrue();
        await Assert.That(dataTypeChange.ChangeKinds.HasFlag(MitsubishiSchemaChangeKind.DataTypeChange)).IsTrue();
        await Assert.That(diff.ChangedGroups.Single().ChangeKinds).IsEqualTo(MitsubishiSchemaChangeKind.GroupMembershipChange);
    }

    /// <summary>Executes the PreviewTagDatabaseDiffWithSafeMetadataAndGroupsPolicyRejectsAddressAndDatatypeChanges operation.</summary>
    /// <returns>The PreviewTagDatabaseDiffWithSafeMetadataAndGroupsPolicyRejectsAddressAndDatatypeChanges operation result.</returns>
    [Test]
    public async Task PreviewTagDatabaseDiffWithSafeMetadataAndGroupsPolicyRejectsAddressAndDatatypeChanges()
    {
        var path = CreateTempPath("json");
        CreatePolicyUpdatedDatabase().Save(path);

        var client = CreateClient(Scheduler.Immediate);
        client.TagDatabase = CreatePolicyCurrentDatabase();

        try
        {
            var result = client.PreviewTagDatabaseDiff(path, MitsubishiTagRolloutPolicy.SafeMetadataAndGroups);

            await Assert.That(result.IsSucceed).IsFalse();
            await Assert.That(result.Value is not null).IsTrue();
            await Assert.That(result.Value!.ChangeKinds.HasFlag(MitsubishiSchemaChangeKind.AddressChange)).IsTrue();
            await Assert.That(result.Value.ChangeKinds.HasFlag(MitsubishiSchemaChangeKind.DataTypeChange)).IsTrue();
            await Assert.That(result.Err.Contains("AddressChange", StringComparison.OrdinalIgnoreCase)).IsTrue();
            await Assert.That(result.Err.Contains("DataTypeChange", StringComparison.OrdinalIgnoreCase)).IsTrue();
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    /// <summary>Executes the LoadAndValidateTagDatabaseWithSafeMetadataAndGroupsPolicyAppliesAllowedChanges operation.</summary>
    /// <returns>The LoadAndValidateTagDatabaseWithSafeMetadataAndGroupsPolicyAppliesAllowedChanges operation result.</returns>
    [Test]
    public async Task LoadAndValidateTagDatabaseWithSafeMetadataAndGroupsPolicyAppliesAllowedChanges()
    {
        var path = CreateTempPath("json");
        CreateMetadataAndGroupOnlyDatabase().Save(path);

        var client = CreateClient(Scheduler.Immediate);
        client.TagDatabase = CreatePolicyCurrentDatabase();

        try
        {
            var result = client.LoadAndValidateTagDatabase(path, MitsubishiTagRolloutPolicy.SafeMetadataAndGroups);

            await Assert.That(result.IsSucceed).IsTrue();
            await Assert.That(client.TagDatabase!.GetRequired("OperatorMessage").Description).IsEqualTo("Updated HMI text");
            await Assert.That(client.TagDatabase.GetRequiredGroup("Overview").ResolvedTagNames).IsEquivalentTo([ "MotorSpeed", "OperatorMessage"]);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    /// <summary>Executes the ObserveTagDatabaseReloadWithSafeMetadataAndGroupsPolicyRejectsAddressChangeAndPreservesDatabase operation.</summary>
    /// <returns>The ObserveTagDatabaseReloadWithSafeMetadataAndGroupsPolicyRejectsAddressChangeAndPreservesDatabase operation result.</returns>
    [Test]
    public async Task ObserveTagDatabaseReloadWithSafeMetadataAndGroupsPolicyRejectsAddressChangeAndPreservesDatabase()
    {
        var scheduler = new TestScheduler();
        var path = CreateTempPath("json");
        CreatePolicyCurrentDatabase().Save(path);

        var client = CreateClient(scheduler);
        client.TagDatabase = CreatePolicyCurrentDatabase();
        var received = new List<Responce<MitsubishiTagDatabase>>();

        try
        {
            using var subscription = client
                .ObserveTagDatabaseReload(path, TimeSpan.FromSeconds(5), emitInitial: false, policy: MitsubishiTagRolloutPolicy.SafeMetadataAndGroups)
                .Take(1)
                .Subscribe(received.Add);

            CreateAddressOnlyUpdatedDatabase().Save(path);
            scheduler.AdvanceBy(TimeSpan.FromSeconds(5).Ticks + 1);

            await Assert.That(received.Count).IsEqualTo(1);
            await Assert.That(received[0].IsSucceed).IsFalse();
            await Assert.That(received[0].Err.Contains("AddressChange", StringComparison.OrdinalIgnoreCase)).IsTrue();
            await Assert.That(client.TagDatabase!.GetRequired("MotorSpeed").Address).IsEqualTo("D100");
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    /// <summary>Executes the CreateClient operation.</summary>
    /// <param name="scheduler">The scheduler parameter.</param>
    /// <returns>The CreateClient operation result.</returns>
    private static MitsubishiRx CreateClient(IScheduler scheduler)
    {
        var options = new MitsubishiClientOptions(
            Host: "127.0.0.1",
            Port: 5042,
            FrameType: MitsubishiFrameType.ThreeE,
            DataCode: CommunicationDataCode.Binary,
            TransportKind: MitsubishiTransportKind.Tcp,
            Route: MitsubishiRoute.Default,
            MonitoringTimer: 0x0010,
            XyNotation: XyAddressNotation.Octal);

        return new MitsubishiRx(options, new FakeTransport([]), scheduler);
    }

    /// <summary>Executes the CreatePolicyCurrentDatabase operation.</summary>
    /// <returns>The CreatePolicyCurrentDatabase operation result.</returns>
    private static MitsubishiTagDatabase CreatePolicyCurrentDatabase()
    {
        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition("MotorSpeed", "D100", DataType: "Word", Description: "Main spindle RPM", Scale: 0.1, Units: "rpm"),
            new MitsubishiTagDefinition("ProcessValue", "D300", DataType: "Word", Description: "Raw process value"),
            new MitsubishiTagDefinition("OperatorMessage", "D600", DataType: "String", Description: "Current HMI text", Length: 2, Encoding: "Utf8"),
        ]);

        database.AddGroup(new MitsubishiTagGroupDefinition("Overview", ["MotorSpeed", "ProcessValue"]));
        return database;
    }

    /// <summary>Executes the CreatePolicyUpdatedDatabase operation.</summary>
    /// <returns>The CreatePolicyUpdatedDatabase operation result.</returns>
    private static MitsubishiTagDatabase CreatePolicyUpdatedDatabase()
    {
        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition("MotorSpeed", "D101", DataType: "Word", Description: "Main spindle RPM", Scale: 0.1, Units: "rpm"),
            new MitsubishiTagDefinition("ProcessValue", "D300", DataType: "Float", Description: "Engineering process value"),
            new MitsubishiTagDefinition("OperatorMessage", "D600", DataType: "String", Description: "Updated HMI text", Length: 2, Encoding: "Utf8"),
        ]);

        database.AddGroup(new MitsubishiTagGroupDefinition("Overview", ["MotorSpeed", "OperatorMessage"]));
        return database;
    }

    /// <summary>Executes the CreateMetadataAndGroupOnlyDatabase operation.</summary>
    /// <returns>The CreateMetadataAndGroupOnlyDatabase operation result.</returns>
    private static MitsubishiTagDatabase CreateMetadataAndGroupOnlyDatabase()
    {
        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition("MotorSpeed", "D100", DataType: "Word", Description: "Main spindle RPM", Scale: 0.1, Units: "rpm"),
            new MitsubishiTagDefinition("ProcessValue", "D300", DataType: "Word", Description: "Raw process value"),
            new MitsubishiTagDefinition("OperatorMessage", "D600", DataType: "String", Description: "Updated HMI text", Length: 2, Encoding: "Utf8"),
        ]);

        database.AddGroup(new MitsubishiTagGroupDefinition("Overview", ["MotorSpeed", "OperatorMessage"]));
        return database;
    }

    /// <summary>Executes the CreateAddressOnlyUpdatedDatabase operation.</summary>
    /// <returns>The CreateAddressOnlyUpdatedDatabase operation result.</returns>
    private static MitsubishiTagDatabase CreateAddressOnlyUpdatedDatabase()
    {
        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition("MotorSpeed", "D101", DataType: "Word", Description: "Main spindle RPM", Scale: 0.1, Units: "rpm"),
            new MitsubishiTagDefinition("ProcessValue", "D300", DataType: "Word", Description: "Raw process value"),
            new MitsubishiTagDefinition("OperatorMessage", "D600", DataType: "String", Description: "Current HMI text", Length: 2, Encoding: "Utf8"),
        ]);

        database.AddGroup(new MitsubishiTagGroupDefinition("Overview", ["MotorSpeed", "ProcessValue"]));
        return database;
    }

    /// <summary>Executes the CreateTempPath operation.</summary>
    /// <param name="extension">The extension parameter.</param>
    /// <returns>The CreateTempPath operation result.</returns>
    private static string CreateTempPath(string extension)
        => Path.Combine(Path.GetTempPath(), $"mitsubishirx-policy-{Guid.NewGuid():N}.{extension}");

    /// <summary>Executes the DeleteIfExists operation.</summary>
    /// <param name="path">The path parameter.</param>
    private static void DeleteIfExists(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        File.Delete(path);
    }
}
