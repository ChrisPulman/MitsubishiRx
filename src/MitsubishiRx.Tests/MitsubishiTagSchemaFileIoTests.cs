// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MitsubishiRx.Tests;

public sealed class MitsubishiTagSchemaFileIoTests
{
    [Test]
    public async Task SaveAndLoadJsonRoundTripPreservesTagsAndGroups()
    {
        var database = CreateSchemaDatabase();
        var path = CreateTempPath("json");

        try
        {
            database.Save(path);
            var loaded = MitsubishiTagDatabase.Load(path);

            await Assert.That(File.Exists(path)).IsTrue();
            await Assert.That(loaded.Count).IsEqualTo(3);
            await Assert.That(loaded.GroupCount).IsEqualTo(1);
            await Assert.That(loaded.GetRequired("OperatorMessage").Encoding).IsEqualTo("Utf8");
            await Assert.That(loaded.GetRequiredGroup("Overview").ResolvedTagNames).IsEquivalentTo(new[] { "SignedTemp", "TotalCount", "OperatorMessage" });
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Test]
    public async Task SaveAndLoadYamlRoundTripPreservesTagsAndGroups()
    {
        var database = CreateSchemaDatabase();
        var path = CreateTempPath("yaml");

        try
        {
            database.Save(path);
            var loaded = MitsubishiTagDatabase.Load(path);

            await Assert.That(File.ReadAllText(path).Contains("groups:", StringComparison.OrdinalIgnoreCase)).IsTrue();
            await Assert.That(loaded.Count).IsEqualTo(3);
            await Assert.That(loaded.GroupCount).IsEqualTo(1);
            await Assert.That(loaded.GetRequired("TotalCount").ByteOrder).IsEqualTo("LittleEndian");
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Test]
    public async Task SaveAndLoadYamlWithYmlExtensionUsesYamlFormatDetection()
    {
        var database = CreateSchemaDatabase();
        var path = CreateTempPath("yml");

        try
        {
            database.Save(path);
            var loaded = MitsubishiTagDatabase.Load(path);

            await Assert.That(File.ReadAllText(path).Contains("groups:", StringComparison.OrdinalIgnoreCase)).IsTrue();
            await Assert.That(loaded.Count).IsEqualTo(3);
            await Assert.That(loaded.GroupCount).IsEqualTo(1);
            await Assert.That(loaded.GetRequired("OperatorMessage").ByteOrder).IsEqualTo("BigEndian");
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Test]
    public async Task SaveAndLoadCsvUsesCsvFormatDetection()
    {
        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition("MotorSpeed", "D100", DataType: "Word", Scale: 0.1, Units: "rpm"),
            new MitsubishiTagDefinition("OperatorMessage", "D600", DataType: "String", Length: 4, Encoding: "Utf8", Notes: "Shown on HMI"),
        ]);

        database.AddGroup(new MitsubishiTagGroupDefinition("Overview", ["MotorSpeed", "OperatorMessage"]));
        var path = CreateTempPath("csv");

        try
        {
            database.Save(path);
            var loaded = MitsubishiTagDatabase.Load(path);

            var csv = File.ReadAllText(path);
            await Assert.That(csv.Contains("Name,Address,DataType", StringComparison.Ordinal)).IsTrue();
            await Assert.That(loaded.Count).IsEqualTo(2);
            await Assert.That(loaded.GroupCount).IsEqualTo(0);
            await Assert.That(loaded.GetRequired("MotorSpeed").Units).IsEqualTo("rpm");
            await Assert.That(loaded.GetRequired("OperatorMessage").Length).IsEqualTo(4);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Test]
    public async Task LoadRejectsUnsupportedSchemaExtensions()
    {
        var path = CreateTempPath("txt");

        try
        {
            File.WriteAllText(path, "unsupported");

            var exception = Assert.Throws<NotSupportedException>(() => MitsubishiTagDatabase.Load(path));
            if (exception is null)
            {
                throw new InvalidOperationException("Expected Load to reject unsupported schema extensions.");
            }

            await Assert.That(exception.Message.Contains(".txt", StringComparison.OrdinalIgnoreCase)).IsTrue();
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    private static MitsubishiTagDatabase CreateSchemaDatabase()
    {
        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition("SignedTemp", "D700", DataType: "Int16", Signed: true, Units: "°C"),
            new MitsubishiTagDefinition("TotalCount", "D400", DataType: "UInt32", ByteOrder: "LittleEndian", Units: "items"),
            new MitsubishiTagDefinition("OperatorMessage", "D600", DataType: "String", Length: 2, Encoding: "Utf8", ByteOrder: "BigEndian"),
        ]);

        database.AddGroup(new MitsubishiTagGroupDefinition("Overview", ["SignedTemp", "TotalCount", "OperatorMessage"]));
        return database;
    }

    private static string CreateTempPath(string extension)
        => Path.Combine(Path.GetTempPath(), $"mitsubishirx-schema-{Guid.NewGuid():N}.{extension}");

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
