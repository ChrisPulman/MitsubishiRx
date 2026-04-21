// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Serialization;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MitsubishiRx;

internal sealed class MitsubishiTagDatabaseDocument
{
    public List<MitsubishiTagDefinitionDocument>? Tags { get; set; }

    public List<MitsubishiTagGroupDefinitionDocument>? Groups { get; set; }
}

internal sealed class MitsubishiTagDefinitionDocument
{
    public string? Name { get; set; }

    public string? Address { get; set; }

    public string? DataType { get; set; }

    public string? Description { get; set; }

    public double Scale { get; set; } = 1.0;

    public double Offset { get; set; }

    public int? Length { get; set; }

    public string? Encoding { get; set; }

    public string? Units { get; set; }

    public bool Signed { get; set; }

    public string? ByteOrder { get; set; }

    public string? Notes { get; set; }

    public MitsubishiTagDefinition ToModel()
        => new(
            Name ?? string.Empty,
            Address ?? string.Empty,
            DataType,
            Description,
            Scale,
            Offset,
            Length,
            Encoding,
            Units,
            Signed,
            ByteOrder,
            Notes);

    public static MitsubishiTagDefinitionDocument FromModel(MitsubishiTagDefinition model)
        => new()
        {
            Name = model.Name,
            Address = model.Address,
            DataType = model.DataType,
            Description = model.Description,
            Scale = model.Scale,
            Offset = model.Offset,
            Length = model.Length,
            Encoding = model.Encoding,
            Units = model.Units,
            Signed = model.Signed,
            ByteOrder = model.ByteOrder,
            Notes = model.Notes,
        };
}

internal sealed class MitsubishiTagGroupDefinitionDocument
{
    public string? Name { get; set; }

    public List<string>? TagNames { get; set; }

    public MitsubishiTagGroupDefinition ToModel()
        => new(Name ?? string.Empty, TagNames ?? new List<string>());

    public static MitsubishiTagGroupDefinitionDocument FromModel(MitsubishiTagGroupDefinition model)
        => new()
        {
            Name = model.Name,
            TagNames = model.ResolvedTagNames.ToList(),
        };
}

internal static class MitsubishiTagDatabaseSerialization
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly ISerializer YamlSerializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static string ToJson(MitsubishiTagDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        return JsonSerializer.Serialize(ToDocument(database), JsonOptions);
    }

    public static MitsubishiTagDatabase FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var document = JsonSerializer.Deserialize<MitsubishiTagDatabaseDocument>(json, JsonOptions)
            ?? new MitsubishiTagDatabaseDocument();
        return FromDocument(document);
    }

    public static string ToYaml(MitsubishiTagDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        return YamlSerializer.Serialize(ToDocument(database));
    }

    public static MitsubishiTagDatabase FromYaml(string yaml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yaml);
        var document = YamlDeserializer.Deserialize<MitsubishiTagDatabaseDocument>(yaml)
            ?? new MitsubishiTagDatabaseDocument();
        return FromDocument(document);
    }

    private static MitsubishiTagDatabaseDocument ToDocument(MitsubishiTagDatabase database)
        => new()
        {
            Tags = database.Tags.Select(MitsubishiTagDefinitionDocument.FromModel).ToList(),
            Groups = database.Groups.Select(MitsubishiTagGroupDefinitionDocument.FromModel).ToList(),
        };

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
