// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Serialization;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Provides the MitsubishiTagDatabaseSerialization type.</summary>
internal static class MitsubishiTagDatabaseSerialization
{
    /// <summary>Stores the JsonOptions field.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Stores the YamlSerializer field.</summary>
    private static readonly ISerializer YamlSerializer = new SerializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull).Build();

    /// <summary>Stores the YamlDeserializer field.</summary>
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).IgnoreUnmatchedProperties().Build();

    /// <summary>Executes the ToJson operation.</summary>
    /// <param name="database">The database parameter.</param>
    /// <returns>The ToJson operation result.</returns>
    public static string ToJson(MitsubishiTagDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        return JsonSerializer.Serialize(ToDocument(database), JsonOptions);
    }

    /// <summary>Executes the FromJson operation.</summary>
    /// <param name="json">The json parameter.</param>
    /// <returns>The FromJson operation result.</returns>
    public static MitsubishiTagDatabase FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var document = JsonSerializer.Deserialize<MitsubishiTagDatabaseDocument>(json, JsonOptions) ?? new MitsubishiTagDatabaseDocument();
        return FromDocument(document);
    }

    /// <summary>Executes the ToYaml operation.</summary>
    /// <param name="database">The database parameter.</param>
    /// <returns>The ToYaml operation result.</returns>
    public static string ToYaml(MitsubishiTagDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        return YamlSerializer.Serialize(ToDocument(database));
    }

    /// <summary>Executes the FromYaml operation.</summary>
    /// <param name="yaml">The yaml parameter.</param>
    /// <returns>The FromYaml operation result.</returns>
    public static MitsubishiTagDatabase FromYaml(string yaml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yaml);
        var document = YamlDeserializer.Deserialize<MitsubishiTagDatabaseDocument>(yaml) ?? new MitsubishiTagDatabaseDocument();
        return FromDocument(document);
    }

    /// <summary>Executes the ToDocument operation.</summary>
    /// <param name="database">The database parameter.</param>
    /// <returns>The ToDocument operation result.</returns>
    private static MitsubishiTagDatabaseDocument ToDocument(MitsubishiTagDatabase database) => new()
    {
        Tags = database.Tags.Select(MitsubishiTagDefinitionDocument.FromModel).ToList(),
        Groups = database.Groups.Select(MitsubishiTagGroupDefinitionDocument.FromModel).ToList(),
    };

    /// <summary>Executes the FromDocument operation.</summary>
    /// <param name="document">The document parameter.</param>
    /// <returns>The FromDocument operation result.</returns>
    private static MitsubishiTagDatabase FromDocument(MitsubishiTagDatabaseDocument document)
    {
        var database = new MitsubishiTagDatabase((document.Tags ?? new List<MitsubishiTagDefinitionDocument>()).Select(static tag => tag.ToModel()));
        foreach (var group in document.Groups ?? new List<MitsubishiTagGroupDefinitionDocument>())
        {
            database.AddGroup(group.ToModel());
        }

        return database;
    }
}
