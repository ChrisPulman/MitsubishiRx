// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

namespace MitsubishiRx;

/// <summary>
/// Named tag metadata that maps a human-friendly tag to a Mitsubishi device address.
/// </summary>
/// <param name="Name">Unique tag name.</param>
/// <param name="Address">Raw Mitsubishi device address such as D100 or M10.</param>
/// <param name="DataType">Optional data type hint such as Bit, Word, DWord, Float, or String.</param>
/// <param name="Description">Optional operator-facing description.</param>
/// <param name="Scale">Optional engineering scale factor.</param>
/// <param name="Offset">Optional engineering offset.</param>
/// <param name="Length">Optional logical length for string/array-like tags, expressed in PLC words.</param>
/// <param name="Encoding">Optional string/text encoding hint such as Ascii or Utf8.</param>
/// <param name="Units">Optional engineering units label for documentation/UI use.</param>
/// <param name="Signed">Optional signedness hint for integer word and double-word values.</param>
/// <param name="ByteOrder">Optional byte/word ordering hint such as LittleEndian or BigEndian.</param>
/// <param name="Notes">Optional free-form notes.</param>
public sealed record MitsubishiTagDefinition(
    string Name,
    string Address,
    string? DataType = null,
    string? Description = null,
    double Scale = 1.0,
    double Offset = 0.0,
    int? Length = null,
    string? Encoding = null,
    string? Units = null,
    bool Signed = false,
    string? ByteOrder = null,
    string? Notes = null);

/// <summary>
/// In-memory tag database used to resolve symbolic tag names into Mitsubishi PLC addresses.
/// </summary>
public sealed class MitsubishiTagDatabase
{
    private static readonly IReadOnlyDictionary<string, string> SupportedDataTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Bit"] = "Bit",
        ["Word"] = "Word",
        ["DWord"] = "DWord",
        ["Float"] = "Float",
        ["String"] = "String",
        ["Int16"] = "Int16",
        ["UInt16"] = "UInt16",
        ["Int32"] = "Int32",
        ["UInt32"] = "UInt32",
    };

    private static readonly IReadOnlyDictionary<string, string> SupportedEncodings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Ascii"] = "Ascii",
        ["Utf8"] = "Utf8",
        ["Utf16"] = "Utf16",
    };

    private static readonly IReadOnlyDictionary<string, string> SupportedByteOrders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["LittleEndian"] = "LittleEndian",
        ["BigEndian"] = "BigEndian",
    };

    private readonly Dictionary<string, MitsubishiTagDefinition> _tags;
    private readonly Dictionary<string, MitsubishiTagGroupDefinition> _groups;

    /// <summary>
    /// Initializes a new instance of the <see cref="MitsubishiTagDatabase"/> class.
    /// </summary>
    /// <param name="tags">Initial tag definitions.</param>
    public MitsubishiTagDatabase(IEnumerable<MitsubishiTagDefinition> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        _tags = new Dictionary<string, MitsubishiTagDefinition>(StringComparer.OrdinalIgnoreCase);
        _groups = new Dictionary<string, MitsubishiTagGroupDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in tags)
        {
            Add(tag);
        }
    }

    /// <summary>
    /// Gets the number of tags in the database.
    /// </summary>
    public int Count => _tags.Count;

    /// <summary>
    /// Gets the number of groups in the database.
    /// </summary>
    public int GroupCount => _groups.Count;

    /// <summary>
    /// Serializes the current schema, including groups, to JSON.
    /// </summary>
    /// <returns>JSON schema text.</returns>
    public string ToJson() => MitsubishiTagDatabaseSerialization.ToJson(this);

    /// <summary>
    /// Serializes the current schema, including groups, to YAML.
    /// </summary>
    /// <returns>YAML schema text.</returns>
    public string ToYaml() => MitsubishiTagDatabaseSerialization.ToYaml(this);

    /// <summary>
    /// Saves the current schema to a file using extension-based format detection.
    /// Supported extensions: <c>.csv</c>, <c>.json</c>, <c>.yaml</c>, <c>.yml</c>.
    /// </summary>
    /// <param name="path">Target file path.</param>
    public void Save(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, SerializeByExtension(path));
    }

    /// <summary>
    /// Loads a tag database from a file using extension-based format detection.
    /// Supported extensions: <c>.csv</c>, <c>.json</c>, <c>.yaml</c>, <c>.yml</c>.
    /// </summary>
    /// <param name="path">Source file path.</param>
    /// <returns>Parsed tag database.</returns>
    public static MitsubishiTagDatabase Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var content = File.ReadAllText(path);
        return DeserializeByExtension(path, content);
    }

    /// <summary>
    /// Compares this schema to another schema and returns a semantic diff.
    /// </summary>
    /// <param name="other">Schema to compare against.</param>
    /// <returns>Semantic schema diff.</returns>
    public MitsubishiTagDatabaseDiff CompareWith(MitsubishiTagDatabase other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var addedTags = other.Tags
            .Where(tag => !_tags.ContainsKey(tag.Name))
            .OrderBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var removedTags = Tags
            .Where(tag => !other._tags.ContainsKey(tag.Name))
            .OrderBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var changedTags = Tags
            .Where(tag => other._tags.TryGetValue(tag.Name, out var current) && current != tag)
            .Select(tag => new MitsubishiTagChange(tag.Name, tag, other._tags[tag.Name]))
            .OrderBy(change => change.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var addedGroups = other.Groups
            .Where(group => !_groups.ContainsKey(group.Name))
            .OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var removedGroups = Groups
            .Where(group => !other._groups.ContainsKey(group.Name))
            .OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var changedGroups = Groups
            .Where(group => other._groups.TryGetValue(group.Name, out var current) && current != group)
            .Select(group => new MitsubishiTagGroupChange(group.Name, group, other._groups[group.Name]))
            .OrderBy(change => change.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new MitsubishiTagDatabaseDiff(
            addedTags,
            removedTags,
            changedTags,
            addedGroups,
            removedGroups,
            changedGroups);
    }

    /// <summary>
    /// Gets all tags currently stored in the database.
    /// </summary>
    public IReadOnlyCollection<MitsubishiTagDefinition> Tags => _tags.Values;

    /// <summary>
    /// Gets all groups currently stored in the database.
    /// </summary>
    public IReadOnlyCollection<MitsubishiTagGroupDefinition> Groups => _groups.Values;

    /// <summary>
    /// Builds a tag database from CSV content.
    /// </summary>
    /// <param name="csvContent">CSV text with a header row.</param>
    /// <returns>Parsed tag database.</returns>
    public static MitsubishiTagDatabase FromCsv(string csvContent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(csvContent);
        var lines = csvContent.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
        {
            throw new FormatException("CSV content must include a header row and at least one data row.");
        }

        var headers = ParseCsvLine(lines[0]);
        var index = BuildHeaderIndex(headers);
        var tags = new List<MitsubishiTagDefinition>();
        for (var rowIndex = 1; rowIndex < lines.Length; rowIndex++)
        {
            var line = lines[rowIndex].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var values = ParseCsvLine(line);
            if (values.All(static value => string.IsNullOrWhiteSpace(value)))
            {
                continue;
            }

            string Read(string name, bool required = false)
            {
                if (!index.TryGetValue(name, out var columnIndex) || columnIndex >= values.Count)
                {
                    if (required)
                    {
                        throw new FormatException($"CSV header '{name}' is required.");
                    }

                    return string.Empty;
                }

                var value = values[columnIndex].Trim();
                if (required && value.Length == 0)
                {
                    throw new FormatException($"CSV row {rowIndex + 1} is missing required column '{name}'.");
                }

                return value;
            }

            tags.Add(new MitsubishiTagDefinition(
                Name: Read("Name", required: true),
                Address: Read("Address", required: true),
                DataType: NormalizeDataType(NullIfEmpty(Read("DataType")), $"CSV row {rowIndex + 1}"),
                Description: NullIfEmpty(Read("Description")),
                Scale: ParseDouble(Read("Scale"), defaultValue: 1.0),
                Offset: ParseDouble(Read("Offset"), defaultValue: 0.0),
                Length: ParseNullableInt(Read("Length")),
                Encoding: NormalizeEncoding(NullIfEmpty(Read("Encoding")), $"CSV row {rowIndex + 1}"),
                Units: NullIfEmpty(Read("Units")),
                Signed: ParseBool(Read("Signed"), defaultValue: false),
                ByteOrder: NormalizeByteOrder(NullIfEmpty(Read("ByteOrder")), $"CSV row {rowIndex + 1}"),
                Notes: NullIfEmpty(Read("Notes"))));
        }

        return new MitsubishiTagDatabase(tags);
    }

    /// <summary>
    /// Builds a tag database from JSON schema text.
    /// </summary>
    /// <param name="json">JSON schema text.</param>
    /// <returns>Parsed tag database.</returns>
    public static MitsubishiTagDatabase FromJson(string json) => MitsubishiTagDatabaseSerialization.FromJson(json);

    /// <summary>
    /// Builds a tag database from YAML schema text.
    /// </summary>
    /// <param name="yaml">YAML schema text.</param>
    /// <returns>Parsed tag database.</returns>
    public static MitsubishiTagDatabase FromYaml(string yaml) => MitsubishiTagDatabaseSerialization.FromYaml(yaml);

    /// <summary>
    /// Adds or replaces a tag definition.
    /// </summary>
    /// <param name="tag">Tag definition.</param>
    public void Add(MitsubishiTagDefinition tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        if (string.IsNullOrWhiteSpace(tag.Name))
        {
            throw new ArgumentException("Tag name must not be empty.", nameof(tag));
        }

        if (string.IsNullOrWhiteSpace(tag.Address))
        {
            throw new ArgumentException("Tag address must not be empty.", nameof(tag));
        }

        var normalizedDataType = NormalizeDataType(tag.DataType, nameof(tag));
        var normalizedEncoding = NormalizeEncoding(tag.Encoding, nameof(tag));
        var normalizedByteOrder = NormalizeByteOrder(tag.ByteOrder, nameof(tag));
        if (tag.Length is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tag), "Tag Length must be greater than zero when specified.");
        }

        _tags[tag.Name] = tag with
        {
            DataType = normalizedDataType,
            Encoding = normalizedEncoding,
            ByteOrder = normalizedByteOrder,
        };
    }

    /// <summary>
    /// Tries to resolve a tag definition by name.
    /// </summary>
    /// <param name="name">Tag name.</param>
    /// <param name="tag">Resolved tag when found.</param>
    /// <returns><c>true</c> if the tag exists.</returns>
    public bool TryGet(string name, out MitsubishiTagDefinition tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _tags.TryGetValue(name, out tag!);
    }

    /// <summary>
    /// Resolves a tag definition by name.
    /// </summary>
    /// <param name="name">Tag name.</param>
    /// <returns>The resolved tag definition.</returns>
    public MitsubishiTagDefinition GetRequired(string name)
    {
        if (TryGet(name, out var tag))
        {
            return tag;
        }

        throw new KeyNotFoundException($"Tag '{name}' was not found in the Mitsubishi tag database.");
    }

    /// <summary>
    /// Adds or replaces a named group definition.
    /// </summary>
    /// <param name="group">Group definition.</param>
    public void AddGroup(MitsubishiTagGroupDefinition group)
    {
        ArgumentNullException.ThrowIfNull(group);
        if (string.IsNullOrWhiteSpace(group.Name))
        {
            throw new ArgumentException("Group name must not be empty.", nameof(group));
        }

        if (group.ResolvedTagNames.Count == 0)
        {
            throw new ArgumentException("Group must contain at least one tag name.", nameof(group));
        }

        if (group.ResolvedTagNames.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Group tag names must not be empty.", nameof(group));
        }

        _groups[group.Name] = new MitsubishiTagGroupDefinition(group.Name, group.ResolvedTagNames.ToArray());
    }

    /// <summary>
    /// Tries to resolve a group definition by name.
    /// </summary>
    /// <param name="name">Group name.</param>
    /// <param name="group">Resolved group when found.</param>
    /// <returns><c>true</c> if the group exists.</returns>
    public bool TryGetGroup(string name, out MitsubishiTagGroupDefinition group)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _groups.TryGetValue(name, out group!);
    }

    /// <summary>
    /// Resolves a group definition by name.
    /// </summary>
    /// <param name="name">Group name.</param>
    /// <returns>The resolved group definition.</returns>
    public MitsubishiTagGroupDefinition GetRequiredGroup(string name)
    {
        if (TryGetGroup(name, out var group))
        {
            return group;
        }

        throw new KeyNotFoundException($"Group '{name}' was not found in the Mitsubishi tag database.");
    }

    private static Dictionary<string, int> BuildHeaderIndex(IReadOnlyList<string> headers)
    {
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count; i++)
        {
            index[headers[i].Trim()] = i;
        }

        return index;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        result.Add(current.ToString());
        return result;
    }

    private static double ParseDouble(string value, double defaultValue)
        => string.IsNullOrWhiteSpace(value)
            ? defaultValue
            : double.Parse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture);

    private static int? ParseNullableInt(string value)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);

    private static bool ParseBool(string value, bool defaultValue)
        => string.IsNullOrWhiteSpace(value)
            ? defaultValue
            : bool.Parse(value);

    private static string? NormalizeDataType(string? dataType, string context)
    {
        if (string.IsNullOrWhiteSpace(dataType))
        {
            return null;
        }

        if (SupportedDataTypes.TryGetValue(dataType.Trim(), out var normalized))
        {
            return normalized;
        }

        throw new FormatException($"{context} contains unsupported DataType '{dataType}'. Supported values: {string.Join(", ", SupportedDataTypes.Values)}.");
    }

    private static string? NormalizeEncoding(string? encoding, string context)
    {
        if (string.IsNullOrWhiteSpace(encoding))
        {
            return null;
        }

        if (SupportedEncodings.TryGetValue(encoding.Trim(), out var normalized))
        {
            return normalized;
        }

        throw new FormatException($"{context} contains unsupported Encoding '{encoding}'. Supported values: {string.Join(", ", SupportedEncodings.Values)}.");
    }

    private static string? NormalizeByteOrder(string? byteOrder, string context)
    {
        if (string.IsNullOrWhiteSpace(byteOrder))
        {
            return null;
        }

        if (SupportedByteOrders.TryGetValue(byteOrder.Trim(), out var normalized))
        {
            return normalized;
        }

        throw new FormatException($"{context} contains unsupported ByteOrder '{byteOrder}'. Supported values: {string.Join(", ", SupportedByteOrders.Values)}.");
    }

    private string SerializeByExtension(string path)
        => GetSchemaFormat(path) switch
        {
            MitsubishiTagDatabaseSchemaFormat.Csv => ToCsv(),
            MitsubishiTagDatabaseSchemaFormat.Json => ToJson(),
            MitsubishiTagDatabaseSchemaFormat.Yaml => ToYaml(),
            _ => throw new NotSupportedException($"Schema format for '{path}' is not supported."),
        };

    private static MitsubishiTagDatabase DeserializeByExtension(string path, string content)
        => GetSchemaFormat(path) switch
        {
            MitsubishiTagDatabaseSchemaFormat.Csv => FromCsv(content),
            MitsubishiTagDatabaseSchemaFormat.Json => FromJson(content),
            MitsubishiTagDatabaseSchemaFormat.Yaml => FromYaml(content),
            _ => throw new NotSupportedException($"Schema format for '{path}' is not supported."),
        };

    private string ToCsv()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Name,Address,DataType,Description,Scale,Offset,Length,Encoding,Units,Signed,ByteOrder,Notes");

        foreach (var tag in _tags.Values)
        {
            builder
                .Append(EscapeCsv(tag.Name)).Append(',')
                .Append(EscapeCsv(tag.Address)).Append(',')
                .Append(EscapeCsv(tag.DataType)).Append(',')
                .Append(EscapeCsv(tag.Description)).Append(',')
                .Append(tag.Scale.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(tag.Offset.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(tag.Length?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append(',')
                .Append(EscapeCsv(tag.Encoding)).Append(',')
                .Append(EscapeCsv(tag.Units)).Append(',')
                .Append(tag.Signed.ToString().ToLowerInvariant()).Append(',')
                .Append(EscapeCsv(tag.ByteOrder)).Append(',')
                .Append(EscapeCsv(tag.Notes))
                .AppendLine();
        }

        return builder.ToString();
    }

    private static MitsubishiTagDatabaseSchemaFormat GetSchemaFormat(string path)
    {
        var extension = Path.GetExtension(path);
        if (string.IsNullOrWhiteSpace(extension))
        {
            throw new NotSupportedException($"Schema path '{path}' must use one of: .csv, .json, .yaml, .yml.");
        }

        return extension.ToLowerInvariant() switch
        {
            ".csv" => MitsubishiTagDatabaseSchemaFormat.Csv,
            ".json" => MitsubishiTagDatabaseSchemaFormat.Json,
            ".yaml" or ".yml" => MitsubishiTagDatabaseSchemaFormat.Yaml,
            _ => throw new NotSupportedException($"Schema format '{extension}' is not supported. Use .csv, .json, .yaml, or .yml."),
        };
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.IndexOfAny([',', '"', '\n', '\r']) < 0)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string? NullIfEmpty(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
