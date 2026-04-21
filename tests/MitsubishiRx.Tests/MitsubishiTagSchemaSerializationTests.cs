// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MitsubishiRx.Tests;

public sealed class MitsubishiTagSchemaSerializationTests
{
    [Test]
    public async Task ToJsonAndFromJsonRoundTripPreservesTagsAndGroups()
    {
        var database = CreateSchemaDatabase();

        var json = database.ToJson();
        var roundTripped = MitsubishiTagDatabase.FromJson(json);

        await Assert.That(json.Contains("\"groups\"", StringComparison.OrdinalIgnoreCase)).IsTrue();
        await Assert.That(roundTripped.Count).IsEqualTo(3);
        await Assert.That(roundTripped.GroupCount).IsEqualTo(1);

        var signedTemp = roundTripped.GetRequired("SignedTemp");
        await Assert.That(signedTemp.DataType).IsEqualTo("Int16");
        await Assert.That(signedTemp.Signed).IsTrue();

        var operatorMessage = roundTripped.GetRequired("OperatorMessage");
        await Assert.That(operatorMessage.Length).IsEqualTo(2);
        await Assert.That(operatorMessage.Encoding).IsEqualTo("Utf8");
        await Assert.That(operatorMessage.ByteOrder).IsEqualTo("BigEndian");

        var group = roundTripped.GetRequiredGroup("Overview");
        await Assert.That(group.ResolvedTagNames).IsEquivalentTo(new[] { "SignedTemp", "TotalCount", "OperatorMessage" });
    }

    [Test]
    public async Task ToYamlAndFromYamlRoundTripPreservesTagsAndGroups()
    {
        var database = CreateSchemaDatabase();

        var yaml = database.ToYaml();
        var roundTripped = MitsubishiTagDatabase.FromYaml(yaml);

        await Assert.That(yaml.Contains("groups:", StringComparison.OrdinalIgnoreCase)).IsTrue();
        await Assert.That(roundTripped.Count).IsEqualTo(3);
        await Assert.That(roundTripped.GroupCount).IsEqualTo(1);
        await Assert.That(roundTripped.GetRequired("TotalCount").ByteOrder).IsEqualTo("LittleEndian");
        await Assert.That(roundTripped.GetRequiredGroup("Overview").ResolvedTagNames[0]).IsEqualTo("SignedTemp");
    }

    [Test]
    public async Task FromYamlParsesManualSchemaDocumentWithGroups()
    {
        var yaml = """
        tags:
          - name: SignedTemp
            address: D700
            dataType: Int16
            signed: true
            units: °C
          - name: OperatorMessage
            address: D600
            dataType: String
            length: 2
            encoding: Utf8
            byteOrder: BigEndian
        groups:
          - name: Overview
            tagNames:
              - SignedTemp
              - OperatorMessage
        """;

        var database = MitsubishiTagDatabase.FromYaml(yaml);

        await Assert.That(database.Count).IsEqualTo(2);
        await Assert.That(database.GroupCount).IsEqualTo(1);
        await Assert.That(database.GetRequired("SignedTemp").Signed).IsTrue();
        await Assert.That(database.GetRequired("OperatorMessage").Encoding).IsEqualTo("Utf8");
        await Assert.That(database.GetRequiredGroup("Overview").ResolvedTagNames).IsEquivalentTo(new[] { "SignedTemp", "OperatorMessage" });
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
}
