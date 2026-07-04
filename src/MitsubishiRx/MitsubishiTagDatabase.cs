// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Provides the MitsubishiTagDatabase type.</summary>
public sealed class MitsubishiTagDatabase
{
    /// <summary>Stores the SupportedDataTypes field.</summary>
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

    /// <summary>Stores the SupportedEncodings field.</summary>
    private static readonly IReadOnlyDictionary<string, string> SupportedEncodings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Ascii"] = "Ascii",
        ["Utf8"] = "Utf8",
        ["Utf16"] = "Utf16",
    };

    /// <summary>Stores the SupportedByteOrders field.</summary>
    private static readonly IReadOnlyDictionary<string, string> SupportedByteOrders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["LittleEndian"] = "LittleEndian",
        ["BigEndian"] = "BigEndian",
    };

    /// <summary>Stores the tags field.</summary>
    private readonly Dictionary<string, MitsubishiTagDefinition> _tags;

    /// <summary>Stores the groups field.</summary>
    private readonly Dictionary<string, MitsubishiTagGroupDefinition> _groups;

    /// <summary>Initializes a new instance of the MitsubishiTagDatabase class.</summary>
    /// <param name="tags">The tags parameter.</param>
    public MitsubishiTagDatabase(IEnumerable<MitsubishiTagDefinition> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        _tags = new(StringComparer.OrdinalIgnoreCase);
        _groups = new(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in tags)
        {
            Add(tag);
        }
    }

    /// <summary>Gets or sets the Count property.</summary>
    public int Count => _tags.Count;

    /// <summary>Gets or sets the GroupCount property.</summary>
    public int GroupCount => _groups.Count;

    /// <summary>Gets or sets the Tags property.</summary>
    public IReadOnlyCollection<MitsubishiTagDefinition> Tags => _tags.Values;

    /// <summary>Gets or sets the Groups property.</summary>
    public IReadOnlyCollection<MitsubishiTagGroupDefinition> Groups => _groups.Values;

    /// <summary>Executes the Load operation.</summary>
    /// <param name="path">The path parameter.</param>
    /// <returns>The Load operation result.</returns>
    public static MitsubishiTagDatabase Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var content = File.ReadAllText(path);
        return DeserializeByExtension(path, content);
    }

    /// <summary>Executes the FromCsv operation.</summary>
    /// <param name="csvContent">The csvContent parameter.</param>
    /// <returns>The FromCsv operation result.</returns>
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

            tags.Add(new MitsubishiTagDefinition(Name: Read("Name", required: true), Address: Read("Address", required: true), DataType: NormalizeDataType(NullIfEmpty(Read("DataType")), $"CSV row {rowIndex + 1}"), Description: NullIfEmpty(Read("Description")), Scale: ParseDouble(Read("Scale"), defaultValue: 1.0), Offset: ParseDouble(Read("Offset"), defaultValue: 0.0), Length: ParseNullableInt(Read("Length")), Encoding: NormalizeEncoding(NullIfEmpty(Read("Encoding")), $"CSV row {rowIndex + 1}"), Units: NullIfEmpty(Read("Units")), Signed: ParseBool(Read("Signed"), defaultValue: false), ByteOrder: NormalizeByteOrder(NullIfEmpty(Read("ByteOrder")), $"CSV row {rowIndex + 1}"), Notes: NullIfEmpty(Read("Notes"))));
        }

        return new MitsubishiTagDatabase(tags);
    }

    /// <summary>Executes the FromJson operation.</summary>
    /// <param name="json">The json parameter.</param>
    /// <returns>The FromJson operation result.</returns>
    public static MitsubishiTagDatabase FromJson(string json) => MitsubishiTagDatabaseSerialization.FromJson(json);

    /// <summary>Executes the FromYaml operation.</summary>
    /// <param name="yaml">The yaml parameter.</param>
    /// <returns>The FromYaml operation result.</returns>
    public static MitsubishiTagDatabase FromYaml(string yaml) => MitsubishiTagDatabaseSerialization.FromYaml(yaml);

    /// <summary>Executes the ToJson operation.</summary>
    /// <returns>The ToJson operation result.</returns>
    public string ToJson() => MitsubishiTagDatabaseSerialization.ToJson(this);

    /// <summary>Executes the ToYaml operation.</summary>
    /// <returns>The ToYaml operation result.</returns>
    public string ToYaml() => MitsubishiTagDatabaseSerialization.ToYaml(this);

    /// <summary>Executes the Save operation.</summary>
    /// <param name="path">The path parameter.</param>
    public void Save(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, SerializeByExtension(path));
    }

    /// <summary>Executes the CompareWith operation.</summary>
    /// <param name="other">The other parameter.</param>
    /// <returns>The CompareWith operation result.</returns>
    public MitsubishiTagDatabaseDiff CompareWith(MitsubishiTagDatabase other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var addedTags = other.Tags.Where(tag => !_tags.ContainsKey(tag.Name)).OrderBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var removedTags = Tags.Where(tag => !other._tags.ContainsKey(tag.Name)).OrderBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var changedTags = Tags.Where(tag => other._tags.TryGetValue(tag.Name, out var current) && current != tag).Select(tag => new MitsubishiTagChange(tag.Name, tag, other._tags[tag.Name])).OrderBy(change => change.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var addedGroups = other.Groups.Where(group => !_groups.ContainsKey(group.Name)).OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var removedGroups = Groups.Where(group => !other._groups.ContainsKey(group.Name)).OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var changedGroups = Groups.Where(group => other._groups.TryGetValue(group.Name, out var current) && current != group).Select(group => new MitsubishiTagGroupChange(group.Name, group, other._groups[group.Name])).OrderBy(change => change.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        return new MitsubishiTagDatabaseDiff(addedTags, removedTags, changedTags, addedGroups, removedGroups, changedGroups);
    }

    /// <summary>Executes the Add operation.</summary>
    /// <param name="tag">The tag parameter.</param>
    public void Add(MitsubishiTagDefinition tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag.Address);
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

    /// <summary>Executes the TryGet operation.</summary>
    /// <param name="name">The name parameter.</param>
    /// <param name="tag">The tag parameter.</param>
    /// <returns>The TryGet operation result.</returns>
    public bool TryGet(string name, out MitsubishiTagDefinition tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _tags.TryGetValue(name, out tag!);
    }

    /// <summary>Executes the GetRequired operation.</summary>
    /// <param name="name">The name parameter.</param>
    /// <returns>The GetRequired operation result.</returns>
    public MitsubishiTagDefinition GetRequired(string name)
    {
        if (TryGet(name, out var tag))
        {
            return tag;
        }

        throw new KeyNotFoundException($"Tag '{name}' was not found in the Mitsubishi tag database.");
    }

    /// <summary>Executes the AddGroup operation.</summary>
    /// <param name="group">The group parameter.</param>
    public void AddGroup(MitsubishiTagGroupDefinition group)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentException.ThrowIfNullOrWhiteSpace(group.Name);
        if (group.ResolvedTagNames.Count == 0)
        {
            throw new ArgumentException("Group must contain at least one tag name.", nameof(group));
        }

        if (group.ResolvedTagNames.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Group tag names must not be empty.", nameof(group));
        }

        _groups[group.Name] = new(group.Name, group.ResolvedTagNames.ToArray());
    }

    /// <summary>Executes the TryGetGroup operation.</summary>
    /// <param name="name">The name parameter.</param>
    /// <param name="group">The group parameter.</param>
    /// <returns>The TryGetGroup operation result.</returns>
    public bool TryGetGroup(string name, out MitsubishiTagGroupDefinition group)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _groups.TryGetValue(name, out group!);
    }

    /// <summary>Executes the GetRequiredGroup operation.</summary>
    /// <param name="name">The name parameter.</param>
    /// <returns>The GetRequiredGroup operation result.</returns>
    public MitsubishiTagGroupDefinition GetRequiredGroup(string name)
    {
        if (TryGetGroup(name, out var group))
        {
            return group;
        }

        throw new KeyNotFoundException($"Group '{name}' was not found in the Mitsubishi tag database.");
    }

    /// <summary>Executes the BuildHeaderIndex operation.</summary>
    /// <param name="headers">The headers parameter.</param>
    /// <returns>The BuildHeaderIndex operation result.</returns>
    private static Dictionary<string, int> BuildHeaderIndex(IReadOnlyList<string> headers)
    {
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count; i++)
        {
            index[headers[i].Trim()] = i;
        }

        return index;
    }

    /// <summary>Executes the ParseCsvLine operation.</summary>
    /// <param name="line">The line parameter.</param>
    /// <returns>The ParseCsvLine operation result.</returns>
    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    _ = current.Append('"');
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
                _ = current.Clear();
                continue;
            }

            _ = current.Append(ch);
        }

        result.Add(current.ToString());
        return result;
    }

    /// <summary>Executes the ParseDouble operation.</summary>
    /// <param name="value">The value parameter.</param>
    /// <param name="defaultValue">The defaultValue parameter.</param>
    /// <returns>The ParseDouble operation result.</returns>
    private static double ParseDouble(string value, double defaultValue) => string.IsNullOrWhiteSpace(value) ? defaultValue : double.Parse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture);

    /// <summary>Executes the ParseNullableInt operation.</summary>
    /// <param name="value">The value parameter.</param>
    /// <returns>The ParseNullableInt operation result.</returns>
    private static int? ParseNullableInt(string value) => string.IsNullOrWhiteSpace(value) ? null : int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);

    /// <summary>Executes the ParseBool operation.</summary>
    /// <param name="value">The value parameter.</param>
    /// <param name="defaultValue">The defaultValue parameter.</param>
    /// <returns>The ParseBool operation result.</returns>
    private static bool ParseBool(string value, bool defaultValue) => string.IsNullOrWhiteSpace(value) ? defaultValue : bool.Parse(value);

    /// <summary>Executes the NormalizeDataType operation.</summary>
    /// <param name="dataType">The dataType parameter.</param>
    /// <param name="context">The context parameter.</param>
    /// <returns>The NormalizeDataType operation result.</returns>
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

    /// <summary>Executes the NormalizeEncoding operation.</summary>
    /// <param name="encoding">The encoding parameter.</param>
    /// <param name="context">The context parameter.</param>
    /// <returns>The NormalizeEncoding operation result.</returns>
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

    /// <summary>Executes the NormalizeByteOrder operation.</summary>
    /// <param name="byteOrder">The byteOrder parameter.</param>
    /// <param name="context">The context parameter.</param>
    /// <returns>The NormalizeByteOrder operation result.</returns>
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

    /// <summary>Executes the DeserializeByExtension operation.</summary>
    /// <param name="path">The path parameter.</param>
    /// <param name="content">The content parameter.</param>
    /// <returns>The DeserializeByExtension operation result.</returns>
    private static MitsubishiTagDatabase DeserializeByExtension(string path, string content) => GetSchemaFormat(path) switch
    {
        MitsubishiTagDatabaseSchemaFormat.Csv => FromCsv(content),
        MitsubishiTagDatabaseSchemaFormat.Json => FromJson(content),
        MitsubishiTagDatabaseSchemaFormat.Yaml => FromYaml(content),
        _ => throw new NotSupportedException($"Schema format for '{path}' is not supported."),
    };

    /// <summary>Executes the GetSchemaFormat operation.</summary>
    /// <param name="path">The path parameter.</param>
    /// <returns>The GetSchemaFormat operation result.</returns>
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

    /// <summary>Executes the EscapeCsv operation.</summary>
    /// <param name="value">The value parameter.</param>
    /// <returns>The EscapeCsv operation result.</returns>
    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.IndexOfAny([',', '"', '\n', '\r']) < 0 ? value : $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    /// <summary>Executes the NullIfEmpty operation.</summary>
    /// <param name="value">The value parameter.</param>
    /// <returns>The NullIfEmpty operation result.</returns>
    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>Executes the SerializeByExtension operation.</summary>
    /// <param name="path">The path parameter.</param>
    /// <returns>The SerializeByExtension operation result.</returns>
    private string SerializeByExtension(string path) => GetSchemaFormat(path) switch
    {
        MitsubishiTagDatabaseSchemaFormat.Csv => ToCsv(),
        MitsubishiTagDatabaseSchemaFormat.Json => ToJson(),
        MitsubishiTagDatabaseSchemaFormat.Yaml => ToYaml(),
        _ => throw new NotSupportedException($"Schema format for '{path}' is not supported."),
    };

    /// <summary>Executes the ToCsv operation.</summary>
    /// <returns>The ToCsv operation result.</returns>
    private string ToCsv()
    {
        var builder = new StringBuilder();
        _ = builder.AppendLine("Name,Address,DataType,Description,Scale,Offset,Length,Encoding,Units,Signed,ByteOrder,Notes");
        foreach (var tag in _tags.Values)
        {
            _ = builder.Append(EscapeCsv(tag.Name)).Append(',').Append(EscapeCsv(tag.Address)).Append(',').Append(EscapeCsv(tag.DataType)).Append(',').Append(EscapeCsv(tag.Description)).Append(',').Append(tag.Scale.ToString(CultureInfo.InvariantCulture)).Append(',').Append(tag.Offset.ToString(CultureInfo.InvariantCulture)).Append(',').Append(tag.Length?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append(',').Append(EscapeCsv(tag.Encoding)).Append(',').Append(EscapeCsv(tag.Units)).Append(',').Append(tag.Signed.ToString().ToLowerInvariant()).Append(',').Append(EscapeCsv(tag.ByteOrder)).Append(',').AppendLine(EscapeCsv(tag.Notes));
        }

        return builder.ToString();
    }
}
