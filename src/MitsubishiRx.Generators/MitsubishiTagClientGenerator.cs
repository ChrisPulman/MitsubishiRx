// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace MitsubishiRx;

[Generator]
public sealed class MitsubishiTagClientGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor GenerationFailureDiagnostic = new(
        id: "MRTXGEN001",
        title: "Failed to generate Mitsubishi tag client",
        messageFormat: "{0}",
        category: "MitsubishiRx.Generators",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateTagDiagnostic = new(
        id: "MRTXGEN002",
        title: "Duplicate generated tag name",
        messageFormat: "Schema contains duplicate tag name '{0}'.",
        category: "MitsubishiRx.Generators",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnknownGroupTagDiagnostic = new(
        id: "MRTXGEN003",
        title: "Unknown generated group tag reference",
        messageFormat: "Group '{0}' references unknown tag '{1}'.",
        category: "MitsubishiRx.Generators",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnsupportedDataTypeDiagnostic = new(
        id: "MRTXGEN004",
        title: "Unsupported generated tag data type",
        messageFormat: "Tag '{0}' uses unsupported data type '{1}'.",
        category: "MitsubishiRx.Generators",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor SanitizedIdentifierCollisionDiagnostic = new(
        id: "MRTXGEN005",
        title: "Generated identifier collision",
        messageFormat: "Generated identifier '{0}' is produced by multiple {1}: {2}.",
        category: "MitsubishiRx.Generators",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor EmptyTagNameDiagnostic = new(
        id: "MRTXGEN006",
        title: "Empty generated tag name",
        messageFormat: "Tag name must not be empty.",
        category: "MitsubishiRx.Generators",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor EmptyGroupNameDiagnostic = new(
        id: "MRTXGEN007",
        title: "Empty generated group name",
        messageFormat: "Group name must not be empty.",
        category: "MitsubishiRx.Generators",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor EmptyGroupMembershipDiagnostic = new(
        id: "MRTXGEN008",
        title: "Empty generated group membership",
        messageFormat: "Group '{0}' must reference at least one tag.",
        category: "MitsubishiRx.Generators",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateGroupDiagnostic = new(
        id: "MRTXGEN009",
        title: "Duplicate generated group name",
        messageFormat: "Schema contains duplicate group name '{0}'.",
        category: "MitsubishiRx.Generators",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor EmptyGroupTagReferenceDiagnostic = new(
        id: "MRTXGEN010",
        title: "Empty generated group tag reference",
        messageFormat: "Group '{0}' contains an empty tag reference.",
        category: "MitsubishiRx.Generators",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateGroupTagReferenceDiagnostic = new(
        id: "MRTXGEN011",
        title: "Duplicate generated group tag reference",
        messageFormat: "Group '{0}' references tag '{1}' more than once.",
        category: "MitsubishiRx.Generators",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly HashSet<string> SupportedDataTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Bit",
        "Word",
        "DWord",
        "Float",
        "String",
        "Int16",
        "UInt16",
        "Int32",
        "UInt32",
    };

    internal sealed class SchemaModel
    {
        public IReadOnlyList<TagModel> Tags { get; }

        public IReadOnlyList<GroupModel> Groups { get; }

        private SchemaModel(IReadOnlyList<TagModel> tags, IReadOnlyList<GroupModel> groups)
        {
            Tags = tags;
            Groups = groups;
        }

        public static SchemaModel Parse(string json)
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var tags = new List<TagModel>();
            if (root.TryGetProperty("tags", out var tagsElement) && tagsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var tag in tagsElement.EnumerateArray())
                {
                    tags.Add(new TagModel(
                        name: tag.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? string.Empty : string.Empty,
                        dataType: tag.TryGetProperty("dataType", out var dataTypeElement) ? dataTypeElement.GetString() : null));
                }
            }

            var groups = new List<GroupModel>();
            if (root.TryGetProperty("groups", out var groupsElement) && groupsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var group in groupsElement.EnumerateArray())
                {
                    var tagNames = new List<string>();
                    if (group.TryGetProperty("tagNames", out var tagNamesElement) && tagNamesElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var tagName in tagNamesElement.EnumerateArray())
                        {
                            tagNames.Add(tagName.GetString() ?? string.Empty);
                        }
                    }

                    groups.Add(new GroupModel(
                        name: group.TryGetProperty("name", out var groupNameElement) ? groupNameElement.GetString() ?? string.Empty : string.Empty,
                        tagNames: tagNames));
                }
            }

            return new SchemaModel(tags, groups);
        }
    }

    internal sealed class TagModel
    {
        public string Name { get; }

        public string? DataType { get; }

        public TagModel(string name, string? dataType)
        {
            Name = name;
            DataType = dataType;
        }
    }

    internal sealed class GroupModel
    {
        public string Name { get; }

        public IReadOnlyList<string> TagNames { get; }

        public GroupModel(string name, IReadOnlyList<string> tagNames)
        {
            Name = name;
            TagNames = tagNames;
        }
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static productionContext =>
            productionContext.AddSource(
                "MitsubishiTagClientSchemaAttribute.g.cs",
                SourceText.From(AttributeSource, Encoding.UTF8)));

        var schemaValues = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is AttributeSyntax attribute && MightBeSchemaAttribute(attribute),
                transform: static (syntaxContext, _) => ExtractSchemaLiteral(syntaxContext))
            .Where(static schema => !string.IsNullOrWhiteSpace(schema))
            .Collect();

        context.RegisterSourceOutput(schemaValues, static (productionContext, schemas) =>
        {
            if (schemas.IsDefaultOrEmpty)
            {
                return;
            }

            try
            {
                var model = SchemaModel.Parse(schemas[0]!);
                if (!ValidateModel(model, productionContext))
                {
                    return;
                }

                var source = MitsubishiTagClientEmitter.Emit(model);
                productionContext.AddSource("MitsubishiTagClient.g.cs", SourceText.From(source, Encoding.UTF8));
            }
            catch (Exception ex)
            {
                productionContext.ReportDiagnostic(Diagnostic.Create(
                    GenerationFailureDiagnostic,
                    Location.None,
                    ex.Message));
            }
        });
    }

    private static bool ValidateModel(SchemaModel model, SourceProductionContext context)
    {
        var isValid = true;
        var seenTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sanitizedTagNames = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var sanitizedGroupNames = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var tag in model.Tags)
        {
            if (string.IsNullOrWhiteSpace(tag.Name))
            {
                context.ReportDiagnostic(Diagnostic.Create(EmptyTagNameDiagnostic, Location.None));
                isValid = false;
                continue;
            }

            if (!seenTags.Add(tag.Name))
            {
                context.ReportDiagnostic(Diagnostic.Create(DuplicateTagDiagnostic, Location.None, tag.Name));
                isValid = false;
            }

            if (!string.IsNullOrWhiteSpace(tag.DataType) && !SupportedDataTypes.Contains(tag.DataType!))
            {
                context.ReportDiagnostic(Diagnostic.Create(UnsupportedDataTypeDiagnostic, Location.None, tag.Name, tag.DataType));
                isValid = false;
            }

            AddSanitizedName(sanitizedTagNames, MitsubishiTagClientEmitter.SanitizeIdentifier(tag.Name), tag.Name);
        }

        var knownTags = new HashSet<string>(model.Tags.Where(static tag => !string.IsNullOrWhiteSpace(tag.Name)).Select(static tag => tag.Name), StringComparer.OrdinalIgnoreCase);
        foreach (var group in model.Groups)
        {
            if (string.IsNullOrWhiteSpace(group.Name))
            {
                context.ReportDiagnostic(Diagnostic.Create(EmptyGroupNameDiagnostic, Location.None));
                isValid = false;
            }
            else
            {
                if (!seenGroups.Add(group.Name))
                {
                    context.ReportDiagnostic(Diagnostic.Create(DuplicateGroupDiagnostic, Location.None, group.Name));
                    isValid = false;
                }

                AddSanitizedName(sanitizedGroupNames, MitsubishiTagClientEmitter.SanitizeIdentifier(group.Name), group.Name);
            }

            if (group.TagNames.Count == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(EmptyGroupMembershipDiagnostic, Location.None, group.Name));
                isValid = false;
            }

            var seenGroupTagNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var tagName in group.TagNames)
            {
                if (string.IsNullOrWhiteSpace(tagName))
                {
                    context.ReportDiagnostic(Diagnostic.Create(EmptyGroupTagReferenceDiagnostic, Location.None, group.Name));
                    isValid = false;
                    continue;
                }

                if (!seenGroupTagNames.Add(tagName))
                {
                    context.ReportDiagnostic(Diagnostic.Create(DuplicateGroupTagReferenceDiagnostic, Location.None, group.Name, tagName));
                    isValid = false;
                    continue;
                }

                if (!knownTags.Contains(tagName))
                {
                    context.ReportDiagnostic(Diagnostic.Create(UnknownGroupTagDiagnostic, Location.None, group.Name, tagName));
                    isValid = false;
                }
            }
        }

        ReportSanitizedCollisions(context, sanitizedTagNames, "tag names", ref isValid);
        ReportSanitizedCollisions(context, sanitizedGroupNames, "group names", ref isValid);

        return isValid;
    }

    private static void AddSanitizedName(Dictionary<string, List<string>> index, string sanitizedName, string originalName)
    {
        if (!index.TryGetValue(sanitizedName, out var originals))
        {
            originals = new List<string>();
            index[sanitizedName] = originals;
        }

        originals.Add(originalName);
    }

    private static void ReportSanitizedCollisions(SourceProductionContext context, Dictionary<string, List<string>> index, string entityKind, ref bool isValid)
    {
        foreach (var pair in index)
        {
            var distinctOriginals = pair.Value.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (distinctOriginals.Length <= 1)
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                SanitizedIdentifierCollisionDiagnostic,
                Location.None,
                pair.Key,
                entityKind,
                string.Join(", ", distinctOriginals)));
            isValid = false;
        }
    }

    private static bool MightBeSchemaAttribute(AttributeSyntax attribute)
    {
        var name = attribute.Name.ToString();
        return name.Contains("MitsubishiTagClientSchema", StringComparison.Ordinal);
    }

    private static string? ExtractSchemaLiteral(GeneratorSyntaxContext syntaxContext)
    {
        if (syntaxContext.Node is not AttributeSyntax attribute || attribute.ArgumentList is null || attribute.ArgumentList.Arguments.Count == 0)
        {
            return null;
        }

        var expression = attribute.ArgumentList.Arguments[0].Expression;
        var constant = syntaxContext.SemanticModel.GetConstantValue(expression);
        if (constant.HasValue && constant.Value is string text)
        {
            return text;
        }

        return expression is LiteralExpressionSyntax literal ? literal.Token.ValueText : null;
    }

    private const string AttributeSource = """
// <auto-generated />
using System;

namespace MitsubishiRx;

[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class MitsubishiTagClientSchemaAttribute : Attribute
{
    public MitsubishiTagClientSchemaAttribute(string schemaJson)
    {
        SchemaJson = schemaJson ?? throw new ArgumentNullException(nameof(schemaJson));
    }

    public string SchemaJson { get; }
}
""";
}
