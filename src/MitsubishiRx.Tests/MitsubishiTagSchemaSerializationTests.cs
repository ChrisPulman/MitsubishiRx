// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive.Tests;
#else

namespace MitsubishiRx.Tests;
#endif

/// <summary>Provides the MitsubishiTagSchemaSerializationTests type.</summary>
public sealed class MitsubishiTagSchemaSerializationTests
{
    /// <summary>Executes the ToJsonAndFromJsonRoundTripPreservesTagsAndGroups operation.</summary>
    /// <returns>The ToJsonAndFromJsonRoundTripPreservesTagsAndGroups operation result.</returns>
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
        await Assert.That(group.ResolvedTagNames).IsEquivalentTo([ "SignedTemp", "TotalCount", "OperatorMessage"]);
    }

    /// <summary>Executes the ToYamlAndFromYamlRoundTripPreservesTagsAndGroups operation.</summary>
    /// <returns>The ToYamlAndFromYamlRoundTripPreservesTagsAndGroups operation result.</returns>
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

    /// <summary>Executes the FromYamlParsesManualSchemaDocumentWithGroups operation.</summary>
    /// <returns>The FromYamlParsesManualSchemaDocumentWithGroups operation result.</returns>
    [Test]
    public async Task FromYamlParsesManualSchemaDocumentWithGroups()
    {
        const string yaml = """
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
        await Assert.That(database.GetRequiredGroup("Overview").ResolvedTagNames).IsEquivalentTo([ "SignedTemp", "OperatorMessage"]);
    }

    /// <summary>Executes the CreateSchemaDatabase operation.</summary>
    /// <returns>The CreateSchemaDatabase operation result.</returns>
    private static MitsubishiTagDatabase CreateSchemaDatabase()
    {
        var database = new MitsubishiTagDatabase(
        [
            new MitsubishiTagDefinition("SignedTemp", "D700", DataType: "Int16", Units: "°C", Signed: true),
            new MitsubishiTagDefinition("TotalCount", "D400", DataType: "UInt32", Units: "items", ByteOrder: "LittleEndian"),
            new MitsubishiTagDefinition("OperatorMessage", "D600", DataType: "String", Length: 2, Encoding: "Utf8", ByteOrder: "BigEndian"),
        ]);

        database.AddGroup(new MitsubishiTagGroupDefinition("Overview", ["SignedTemp", "TotalCount", "OperatorMessage"]));
        return database;
    }
}
