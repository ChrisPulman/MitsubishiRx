// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.IO.Compression;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive.Tests;
#else

namespace MitsubishiRx.Tests;
#endif

/// <summary>Provides the MitsubishiGeneratedClientTests type.</summary>
public sealed class MitsubishiGeneratedClientTests
{
    /// <summary>Stores the PackagePackGate field.</summary>
    private static readonly SemaphoreSlim PackagePackGate = new(1, 1);

    /// <summary>Stores the _cachedPackedPackagePath field.</summary>
    private static string? _cachedPackedPackagePath;

    /// <summary>Executes the IncrementalGeneratorEmitsTypedTagAndGroupClientSurface operation.</summary>
    /// <returns>The IncrementalGeneratorEmitsTypedTagAndGroupClientSurface operation result.</returns>
    [Test]
    public async Task IncrementalGeneratorEmitsTypedTagAndGroupClientSurface()
    {
        const string schema = """
        {
          "tags": [
            {
              "name": "MotorSpeed",
              "address": "D100",
              "dataType": "Float",
              "description": "Main spindle speed"
            },
            {
              "name": "Mode",
              "address": "D101",
              "dataType": "UInt16"
            }
          ],
          "groups": [
            {
              "name": "Line1",
              "tagNames": ["MotorSpeed", "Mode"]
            }
          ]
        }
        """;

        var source = $$"""
        using MitsubishiRx;

        /// <summary>Provides the SchemaMarker type.</summary>
        [MitsubishiTagClientSchema({{ToLiteral(schema)}})]
        internal sealed class SchemaMarker { }
        """;

        var generated = RunGenerator(source);

        await Assert.That(generated.Contains("public static class GeneratedMitsubishiTagClientExtensions")).IsTrue();
        await Assert.That(generated.Contains("public static GeneratedMitsubishiTagClient Generated(this global::MitsubishiRx.MitsubishiRx owner) => new(owner);")).IsTrue();
        await Assert.That(generated.Contains("public sealed partial class GeneratedMitsubishiTagClient")).IsTrue();
        await Assert.That(generated.Contains("public TagsClient Tags { get; }")).IsTrue();
        await Assert.That(generated.Contains("public GroupsClient Groups { get; }")).IsTrue();
        await Assert.That(generated.Contains("public MotorSpeedTag MotorSpeed => new(_owner);")).IsTrue();
        await Assert.That(generated.Contains("public Task<Responce<float>> ReadAsync(CancellationToken cancellationToken = default) => _owner.ReadFloatByTagAsync(\"MotorSpeed\", cancellationToken);")).IsTrue();
        await Assert.That(generated.Contains("public Task<Responce> WriteAsync(float value, CancellationToken cancellationToken = default) => _owner.WriteFloatByTagAsync(\"MotorSpeed\", value, cancellationToken);")).IsTrue();
        await Assert.That(generated.Contains("public IObservable<MitsubishiReactiveValue<float>> Observe(TimeSpan pollInterval, TimeSpan? minimumUpdateSpacing = null) => _owner.ObserveReactiveTag<float>(\"MotorSpeed\", pollInterval, minimumUpdateSpacing);")).IsTrue();
        await Assert.That(generated.Contains("public ModeTag Mode => new(_owner);")).IsTrue();
        await Assert.That(generated.Contains("public Task<Responce<ushort>> ReadAsync(CancellationToken cancellationToken = default) => _owner.ReadUInt16ByTagAsync(\"Mode\", cancellationToken);")).IsTrue();
        await Assert.That(generated.Contains("public Task<Responce> WriteAsync(ushort value, CancellationToken cancellationToken = default) => _owner.WriteUInt16ByTagAsync(\"Mode\", value, cancellationToken);")).IsTrue();
        await Assert.That(generated.Contains("public Line1Group Line1 => new(_owner);")).IsTrue();
        await Assert.That(generated.Contains("public sealed partial record Line1Snapshot(float MotorSpeed, ushort Mode)")).IsTrue();
        await Assert.That(generated.Contains("public async Task<Responce<Line1Snapshot>> ReadAsync(CancellationToken cancellationToken = default)")).IsTrue();
        await Assert.That(generated.Contains("return new Responce<Line1Snapshot>(result, Line1Snapshot.FromSnapshot(result.Value));")).IsTrue();
        await Assert.That(generated.Contains("public Task<Responce> WriteAsync(Line1Snapshot value, CancellationToken cancellationToken = default) => _owner.WriteTagGroupSnapshotAsync(value.ToSnapshot(), cancellationToken);")).IsTrue();
        await Assert.That(generated.Contains("public async Task<Responce<Line1Snapshot?>> ReadOptionalAsync(CancellationToken cancellationToken = default)")).IsTrue();
        await Assert.That(generated.Contains("return new Responce<Line1Snapshot?>(result, Line1Snapshot.TryFromSnapshot(result.Value));")).IsTrue();
        await Assert.That(generated.Contains("public IObservable<MitsubishiReactiveValue<Line1Snapshot>> Observe(TimeSpan pollInterval, TimeSpan? minimumUpdateSpacing = null)")).IsTrue();
        await Assert.That(generated.Contains("public IObservable<MitsubishiReactiveValue<Line1Snapshot?>> ObserveOptional(TimeSpan pollInterval, TimeSpan? minimumUpdateSpacing = null)")).IsTrue();
        await Assert.That(generated.Contains("public MitsubishiTagGroupSnapshot ToSnapshot()")).IsTrue();
        await Assert.That(generated.Contains("public static Line1Snapshot? TryFromSnapshot(MitsubishiTagGroupSnapshot? snapshot)")).IsTrue();
        await Assert.That(generated.Contains("catch (KeyNotFoundException)")).IsTrue();
        await Assert.That(generated.Contains("catch (InvalidCastException)")).IsTrue();
        await Assert.That(generated.Contains("snapshot.GetRequired<float>(\"MotorSpeed\")")).IsTrue();
        await Assert.That(generated.Contains("snapshot.GetRequired<ushort>(\"Mode\")")).IsTrue();
    }

    /// <summary>Executes the IncrementalGeneratorOutputCompilesAndSupportsGeneratedExtensionUsage operation.</summary>
    /// <returns>The IncrementalGeneratorOutputCompilesAndSupportsGeneratedExtensionUsage operation result.</returns>
    [Test]
    public async Task IncrementalGeneratorOutputCompilesAndSupportsGeneratedExtensionUsage()
    {
        const string schema = """
        {
          "tags": [
            {
              "name": "MotorSpeed",
              "address": "D100",
              "dataType": "Float"
            },
            {
              "name": "Mode",
              "address": "D101",
              "dataType": "UInt16"
            }
          ],
          "groups": [
            {
              "name": "Line1",
              "tagNames": ["MotorSpeed", "Mode"]
            }
          ]
        }
        """;

        var source = $$"""
        using System;
        using System.Threading.Tasks;
        using MitsubishiRx;

        /// <summary>Provides the SchemaMarker type.</summary>
        [MitsubishiTagClientSchema({{ToLiteral(schema)}})]
        internal sealed class SchemaMarker { }

        /// <summary>Provides the Usage type.</summary>
        internal static class Usage
        {
            /// <summary>Executes the ExecuteAsync operation.</summary>
            /// <param name="client">The client parameter.</param>
            /// <returns>The ExecuteAsync operation result.</returns>
            public static async Task ExecuteAsync(global::MitsubishiRx.MitsubishiRx client)
            {
                _ = client.Generated().Tags.MotorSpeed;
                _ = client.Generated().Groups.Line1;
                _ = client.Generated().Tags.MotorSpeed.Observe(TimeSpan.FromMilliseconds(250));
                _ = client.Generated().Groups.Line1.Observe(TimeSpan.FromSeconds(1));
                _ = client.Generated().Groups.Line1.ObserveOptional(TimeSpan.FromSeconds(1));
                var line1 = await client.Generated().Groups.Line1.ReadAsync();
                _ = line1.Value!.Mode;
                var optional = await client.Generated().Groups.Line1.ReadOptionalAsync();
                _ = optional.Value?.Mode;
                if (line1.Value is not null)
                {
                    await client.Generated().Groups.Line1.WriteAsync(line1.Value);
                    _ = line1.Value.ToSnapshot();
                }
                await client.Generated().Tags.Mode.WriteAsync(2);
            }
        }
        """;

        var result = RunGeneratorCompilation(source);
        var errors = result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Select(static diagnostic => diagnostic.ToString())));
        }

        await Assert.That(result.Generated.Contains("public static GeneratedMitsubishiTagClient Generated(this global::MitsubishiRx.MitsubishiRx owner) => new(owner);")).IsTrue();
    }

    /// <summary>Executes the ConsumerProjectReferencingPackedMitsubishiRxPackageBuildsGeneratedClientSurfaceAutomatically operation.</summary>
    /// <returns>The ConsumerProjectReferencingPackedMitsubishiRxPackageBuildsGeneratedClientSurfaceAutomatically operation result.</returns>
    [Test]
    public async Task ConsumerProjectReferencingPackedMitsubishiRxPackageBuildsGeneratedClientSurfaceAutomatically()
    {
        string packagePath = await PackMitsubishiRxPackageAsync();
        string version = Path.GetFileNameWithoutExtension(packagePath)["MitsubishiRx.".Length..];
        string tempDirectory = CreateTemporaryDirectory();

        try
        {
            string consumerDirectory = Path.Combine(tempDirectory, "consumer");
            string packageCacheDirectory = Path.Combine(tempDirectory, "packages");
            _ = Directory.CreateDirectory(consumerDirectory);
            _ = Directory.CreateDirectory(packageCacheDirectory);
            string consumerProjectPath = Path.Combine(consumerDirectory, "Consumer.csproj");
            string programPath = Path.Combine(consumerDirectory, "Program.cs");

            await File.WriteAllTextAsync(
                consumerProjectPath,
                $$"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="MitsubishiRx" Version="{{version}}" />
                  </ItemGroup>
                </Project>
                """);

            await File.WriteAllTextAsync(
                programPath,
                """
                using System.Threading.Tasks;
                using MitsubishiRx;

                /// <summary>Provides the SchemaMarker type.</summary>
                [MitsubishiTagClientSchema(@"{""tags"":[{""name"":""MotorSpeed"",""address"":""D100"",""dataType"":""Float""}],""groups"":[{""name"":""Line1"",""tagNames"":[""MotorSpeed""]}]}")]
                internal sealed class SchemaMarker { }

                /// <summary>Provides the Usage type.</summary>
                internal static class Usage
                {
                    /// <summary>Executes the ExecuteAsync operation.</summary>
                    /// <param name="client">The client parameter.</param>
                    /// <returns>The ExecuteAsync operation result.</returns>
                    public static async Task ExecuteAsync(global::MitsubishiRx.MitsubishiRx client)
                    {
                        _ = client.Generated().Tags.MotorSpeed;
                        _ = await client.Generated().Tags.MotorSpeed.ReadAsync();
                        var line1 = await client.Generated().Groups.Line1.ReadAsync();
                        _ = line1.Value?.MotorSpeed;
                        var optional = await client.Generated().Groups.Line1.ReadOptionalAsync();
                        _ = optional.Value?.MotorSpeed;
                    }
                }
                """);

            var restore = await RunDotNetAsync(
                "restore",
                consumerProjectPath,
                consumerDirectory,
                $"/p:RestorePackagesPath={packageCacheDirectory}",
                $"/p:RestoreAdditionalProjectSources={Path.GetDirectoryName(packagePath)!}");
            if (restore.ExitCode != 0)
            {
                throw new InvalidOperationException(restore.Output);
            }

            var build = await RunDotNetAsync(
                "build",
                consumerProjectPath,
                consumerDirectory,
                "--no-restore",
                $"/p:RestorePackagesPath={packageCacheDirectory}");
            if (build.ExitCode != 0)
            {
                throw new InvalidOperationException(build.Output);
            }

            await Assert.That(build.Output.Contains("Build succeeded.")).IsTrue();
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    /// <summary>Executes the MitsubishiRxPackageShouldContainGeneratorAnalyzerAsset operation.</summary>
    /// <returns>The MitsubishiRxPackageShouldContainGeneratorAnalyzerAsset operation result.</returns>
    [Test]
    public async Task MitsubishiRxPackageShouldContainGeneratorAnalyzerAsset()
    {
        string packagePath = await PackMitsubishiRxPackageAsync();

        await using var package = ZipFile.OpenRead(packagePath);
        bool hasAnalyzer = package.Entries.Any(static entry => entry.FullName.EndsWith("analyzers/dotnet/cs/MitsubishiRx.Generators.dll", StringComparison.OrdinalIgnoreCase));
        await Assert.That(hasAnalyzer).IsTrue();
    }

    /// <summary>Executes the IncrementalGeneratorSanitizesInvalidIdentifiers operation.</summary>
    /// <returns>The IncrementalGeneratorSanitizesInvalidIdentifiers operation result.</returns>
    [Test]
    public async Task IncrementalGeneratorSanitizesInvalidIdentifiers()
    {
        const string schema = """
        {
          "tags": [
            {
              "name": "Motor Speed",
              "address": "D100",
              "dataType": "Float"
            },
            {
              "name": "9Mode",
              "address": "D101",
              "dataType": "UInt16"
            }
          ],
          "groups": [
            {
              "name": "Line 1 Overview",
              "tagNames": ["Motor Speed", "9Mode"]
            }
          ]
        }
        """;

        var source = $$"""
        using MitsubishiRx;

        /// <summary>Provides the SchemaMarker type.</summary>
        [MitsubishiTagClientSchema({{ToLiteral(schema)}})]
        internal sealed class SchemaMarker { }
        """;

        var generated = RunGenerator(source);

        await Assert.That(generated.Contains("public MotorSpeedTag MotorSpeed => new(_owner);")).IsTrue();
        await Assert.That(generated.Contains("public _9ModeTag _9Mode => new(_owner);")).IsTrue();
        await Assert.That(generated.Contains("public Line1OverviewGroup Line1Overview => new(_owner);")).IsTrue();
    }

    /// <summary>Executes the IncrementalGeneratorOptionalSnapshotHelpersReturnNullWhenValuesAreMissingOrWrongType operation.</summary>
    /// <returns>The IncrementalGeneratorOptionalSnapshotHelpersReturnNullWhenValuesAreMissingOrWrongType operation result.</returns>
    [Test]
    public async Task IncrementalGeneratorOptionalSnapshotHelpersReturnNullWhenValuesAreMissingOrWrongType()
    {
        var missingMode = new MitsubishiTagGroupSnapshot(
            "Line1",
            new Dictionary<string, object?>
            {
                ["MotorSpeed"] = 123.4f,
            });
        var wrongMode = new MitsubishiTagGroupSnapshot(
            "Line1",
            new Dictionary<string, object?>
            {
                ["MotorSpeed"] = 123.4f,
                ["Mode"] = "bad-type",
            });

        await Assert.That(missingMode.GetOptional<ushort>("Mode")).IsEqualTo(default(ushort));
        await Assert.That(wrongMode.GetOptional<ushort>("Mode")).IsEqualTo(default(ushort));
    }

    /// <summary>Executes the IncrementalGeneratorReportsDiagnosticForDuplicateTagNames operation.</summary>
    /// <returns>The IncrementalGeneratorReportsDiagnosticForDuplicateTagNames operation result.</returns>
    [Test]
    public async Task IncrementalGeneratorReportsDiagnosticForDuplicateTagNames()
    {
        const string schema = """
        {
          "tags": [
            { "name": "MotorSpeed", "address": "D100", "dataType": "Float" },
            { "name": "MotorSpeed", "address": "D101", "dataType": "UInt16" }
          ],
          "groups": []
        }
        """;

        var source = $$"""
        using MitsubishiRx;

        /// <summary>Provides the SchemaMarker type.</summary>
        [MitsubishiTagClientSchema({{ToLiteral(schema)}})]
        internal sealed class SchemaMarker { }
        """;

        var result = RunGeneratorCompilation(source);
        var diagnostics = result.Diagnostics.Where(static d => d.Id == "MRTXGEN002").ToArray();

        if (diagnostics.Length == 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => diagnostic.ToString())));
        }

        await Assert.That(diagnostics.Any(static d => d.GetMessage().Contains("MotorSpeed", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>Executes the IncrementalGeneratorReportsDiagnosticForUnknownGroupTagReference operation.</summary>
    /// <returns>The IncrementalGeneratorReportsDiagnosticForUnknownGroupTagReference operation result.</returns>
    [Test]
    public async Task IncrementalGeneratorReportsDiagnosticForUnknownGroupTagReference()
    {
        const string schema = """
        {
          "tags": [
            { "name": "MotorSpeed", "address": "D100", "dataType": "Float" }
          ],
          "groups": [
            { "name": "Line1", "tagNames": ["MotorSpeed", "MissingTag"] }
          ]
        }
        """;

        var source = $$"""
        using MitsubishiRx;

        /// <summary>Provides the SchemaMarker type.</summary>
        [MitsubishiTagClientSchema({{ToLiteral(schema)}})]
        internal sealed class SchemaMarker { }
        """;

        var result = RunGeneratorCompilation(source);
        var diagnostics = result.Diagnostics.Where(static d => d.Id == "MRTXGEN003").ToArray();

        if (diagnostics.Length == 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => diagnostic.ToString())));
        }

        await Assert.That(diagnostics.Any(static d => d.GetMessage().Contains("MissingTag", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>Executes the IncrementalGeneratorReportsDiagnosticForUnsupportedDataType operation.</summary>
    /// <returns>The IncrementalGeneratorReportsDiagnosticForUnsupportedDataType operation result.</returns>
    [Test]
    public async Task IncrementalGeneratorReportsDiagnosticForUnsupportedDataType()
    {
        const string schema = """
        {
          "tags": [
            { "name": "MotorSpeed", "address": "D100", "dataType": "Decimal128" }
          ],
          "groups": []
        }
        """;

        var source = $$"""
        using MitsubishiRx;

        /// <summary>Provides the SchemaMarker type.</summary>
        [MitsubishiTagClientSchema({{ToLiteral(schema)}})]
        internal sealed class SchemaMarker { }
        """;

        var result = RunGeneratorCompilation(source);
        var diagnostics = result.Diagnostics.Where(static d => d.Id == "MRTXGEN004").ToArray();

        if (diagnostics.Length == 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => diagnostic.ToString())));
        }

        await Assert.That(diagnostics.Any(static d => d.GetMessage().Contains("Decimal128", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>Executes the IncrementalGeneratorReportsDiagnosticForSanitizedIdentifierCollisions operation.</summary>
    /// <returns>The IncrementalGeneratorReportsDiagnosticForSanitizedIdentifierCollisions operation result.</returns>
    [Test]
    public async Task IncrementalGeneratorReportsDiagnosticForSanitizedIdentifierCollisions()
    {
        const string schema = """
        {
          "tags": [
            { "name": "Motor Speed", "address": "D100", "dataType": "Float" },
            { "name": "Motor-Speed", "address": "D101", "dataType": "UInt16" }
          ],
          "groups": []
        }
        """;

        var source = $$"""
        using MitsubishiRx;

        /// <summary>Provides the SchemaMarker type.</summary>
        [MitsubishiTagClientSchema({{ToLiteral(schema)}})]
        internal sealed class SchemaMarker { }
        """;

        var result = RunGeneratorCompilation(source);
        var diagnostics = result.Diagnostics.Where(static d => d.Id == "MRTXGEN005").ToArray();

        if (diagnostics.Length == 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => diagnostic.ToString())));
        }

        await Assert.That(diagnostics.Any(static d => d.GetMessage().Contains("MotorSpeed", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>Executes the IncrementalGeneratorReportsDiagnosticForEmptyTagName operation.</summary>
    /// <returns>The IncrementalGeneratorReportsDiagnosticForEmptyTagName operation result.</returns>
    [Test]
    public async Task IncrementalGeneratorReportsDiagnosticForEmptyTagName()
    {
        const string schema = """
        {
          "tags": [
            { "name": "", "address": "D100", "dataType": "Float" }
          ],
          "groups": []
        }
        """;

        var source = $$"""
        using MitsubishiRx;

        /// <summary>Provides the SchemaMarker type.</summary>
        [MitsubishiTagClientSchema({{ToLiteral(schema)}})]
        internal sealed class SchemaMarker { }
        """;

        var result = RunGeneratorCompilation(source);
        var diagnostics = result.Diagnostics.Where(static d => d.Id == "MRTXGEN006").ToArray();

        if (diagnostics.Length == 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => diagnostic.ToString())));
        }

        await Assert.That(diagnostics.Any(static d => d.GetMessage().Contains("Tag name", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>Executes the IncrementalGeneratorReportsDiagnosticForEmptyGroupName operation.</summary>
    /// <returns>The IncrementalGeneratorReportsDiagnosticForEmptyGroupName operation result.</returns>
    [Test]
    public async Task IncrementalGeneratorReportsDiagnosticForEmptyGroupName()
    {
        const string schema = """
        {
          "tags": [
            { "name": "MotorSpeed", "address": "D100", "dataType": "Float" }
          ],
          "groups": [
            { "name": "", "tagNames": ["MotorSpeed"] }
          ]
        }
        """;

        var source = $$"""
        using MitsubishiRx;

        /// <summary>Provides the SchemaMarker type.</summary>
        [MitsubishiTagClientSchema({{ToLiteral(schema)}})]
        internal sealed class SchemaMarker { }
        """;

        var result = RunGeneratorCompilation(source);
        var diagnostics = result.Diagnostics.Where(static d => d.Id == "MRTXGEN007").ToArray();

        if (diagnostics.Length == 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => diagnostic.ToString())));
        }

        await Assert.That(diagnostics.Any(static d => d.GetMessage().Contains("Group name", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>Executes the IncrementalGeneratorReportsDiagnosticForEmptyGroupMembership operation.</summary>
    /// <returns>The IncrementalGeneratorReportsDiagnosticForEmptyGroupMembership operation result.</returns>
    [Test]
    public async Task IncrementalGeneratorReportsDiagnosticForEmptyGroupMembership()
    {
        const string schema = """
        {
          "tags": [
            { "name": "MotorSpeed", "address": "D100", "dataType": "Float" }
          ],
          "groups": [
            { "name": "Line1", "tagNames": [] }
          ]
        }
        """;

        var source = $$"""
        using MitsubishiRx;

        /// <summary>Provides the SchemaMarker type.</summary>
        [MitsubishiTagClientSchema({{ToLiteral(schema)}})]
        internal sealed class SchemaMarker { }
        """;

        var result = RunGeneratorCompilation(source);
        var diagnostics = result.Diagnostics.Where(static d => d.Id == "MRTXGEN008").ToArray();

        if (diagnostics.Length == 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => diagnostic.ToString())));
        }

        await Assert.That(diagnostics.Any(static d => d.GetMessage().Contains("Line1", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>Executes the IncrementalGeneratorReportsDiagnosticForDuplicateGroupNames operation.</summary>
    /// <returns>The IncrementalGeneratorReportsDiagnosticForDuplicateGroupNames operation result.</returns>
    [Test]
    public async Task IncrementalGeneratorReportsDiagnosticForDuplicateGroupNames()
    {
        const string schema = """
        {
          "tags": [
            { "name": "MotorSpeed", "address": "D100", "dataType": "Float" }
          ],
          "groups": [
            { "name": "Line1", "tagNames": ["MotorSpeed"] },
            { "name": "Line1", "tagNames": ["MotorSpeed"] }
          ]
        }
        """;

        var source = $$"""
        using MitsubishiRx;

        /// <summary>Provides the SchemaMarker type.</summary>
        [MitsubishiTagClientSchema({{ToLiteral(schema)}})]
        internal sealed class SchemaMarker { }
        """;

        var result = RunGeneratorCompilation(source);
        var diagnostics = result.Diagnostics.Where(static d => d.Id == "MRTXGEN009").ToArray();

        if (diagnostics.Length == 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => diagnostic.ToString())));
        }

        await Assert.That(diagnostics.Any(static d => d.GetMessage().Contains("Line1", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>Executes the IncrementalGeneratorReportsDiagnosticForEmptyGroupTagReference operation.</summary>
    /// <returns>The IncrementalGeneratorReportsDiagnosticForEmptyGroupTagReference operation result.</returns>
    [Test]
    public async Task IncrementalGeneratorReportsDiagnosticForEmptyGroupTagReference()
    {
        const string schema = """
        {
          "tags": [
            { "name": "MotorSpeed", "address": "D100", "dataType": "Float" }
          ],
          "groups": [
            { "name": "Line1", "tagNames": [""] }
          ]
        }
        """;

        var source = $$"""
        using MitsubishiRx;

        /// <summary>Provides the SchemaMarker type.</summary>
        [MitsubishiTagClientSchema({{ToLiteral(schema)}})]
        internal sealed class SchemaMarker { }
        """;

        var result = RunGeneratorCompilation(source);
        var diagnostics = result.Diagnostics.Where(static d => d.Id == "MRTXGEN010").ToArray();

        if (diagnostics.Length == 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => diagnostic.ToString())));
        }

        await Assert.That(diagnostics.Any(static d => d.GetMessage().Contains("Line1", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>Executes the IncrementalGeneratorReportsDiagnosticForDuplicateGroupTagReference operation.</summary>
    /// <returns>The IncrementalGeneratorReportsDiagnosticForDuplicateGroupTagReference operation result.</returns>
    [Test]
    public async Task IncrementalGeneratorReportsDiagnosticForDuplicateGroupTagReference()
    {
        const string schema = """
        {
          "tags": [
            { "name": "MotorSpeed", "address": "D100", "dataType": "Float" }
          ],
          "groups": [
            { "name": "Line1", "tagNames": ["MotorSpeed", "MotorSpeed"] }
          ]
        }
        """;

        var source = $$"""
        using MitsubishiRx;

        /// <summary>Provides the SchemaMarker type.</summary>
        [MitsubishiTagClientSchema({{ToLiteral(schema)}})]
        internal sealed class SchemaMarker { }
        """;

        var result = RunGeneratorCompilation(source);
        var diagnostics = result.Diagnostics.Where(static d => d.Id == "MRTXGEN011").ToArray();

        if (diagnostics.Length == 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => diagnostic.ToString())));
        }

        await Assert.That(diagnostics.Any(static d => d.GetMessage().Contains("MotorSpeed", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>Executes the RunGenerator operation.</summary>
    /// <param name="source">The source parameter.</param>
    /// <returns>The RunGenerator operation result.</returns>
    private static string RunGenerator(string source)
    {
        var result = RunGeneratorCompilation(source);
        var errors = result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Select(static diagnostic => diagnostic.ToString())));
        }

        if (string.IsNullOrWhiteSpace(result.Generated))
        {
            throw new InvalidOperationException("Generator produced no sources.");
        }

        return result.Generated;
    }

    /// <summary>Executes the RunGeneratorCompilation operation.</summary>
    /// <param name="source">The source parameter.</param>
    /// <returns>The RunGeneratorCompilation operation result.</returns>
    private static (string Generated, IReadOnlyList<Diagnostic> Diagnostics) RunGeneratorCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var generatorAssembly = typeof(MitsubishiTagClientGenerator).Assembly;
        var references = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(static assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(static assembly => MetadataReference.CreateFromFile(assembly.Location))
            .Cast<MetadataReference>()
            .ToList();
        AddReference(references, generatorAssembly.Location);
        AddReference(references, typeof(MitsubishiRx).Assembly.Location);
        AddReference(references, typeof(System.Linq.Expressions.Expression).Assembly.Location);
        AddReference(references, typeof(LinqExtensions).Assembly.Location);

        var compilation = CSharpCompilation.Create(
            assemblyName: "GeneratorTests",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        IIncrementalGenerator generator = new MitsubishiTagClientGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics);
        var runResult = driver.GetRunResult();

        var generated = string.Join(
            Environment.NewLine + "// ----" + Environment.NewLine,
            runResult.Results
                .SelectMany(static result => result.GeneratedSources)
                .Select(static generatedSource => generatedSource.SourceText.ToString()));

        var diagnostics = outputCompilation.GetDiagnostics()
            .Concat(generatorDiagnostics)
            .Concat(runResult.Diagnostics)
            .ToArray();

        return (generated, diagnostics);
    }

    /// <summary>Executes the AddReference operation.</summary>
    /// <param name="references">The references parameter.</param>
    /// <param name="assemblyLocation">The assemblyLocation parameter.</param>
    private static void AddReference(List<MetadataReference> references, string assemblyLocation)
    {
        if (references.OfType<PortableExecutableReference>().Any(reference => string.Equals(reference.FilePath, assemblyLocation, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        references.Add(MetadataReference.CreateFromFile(assemblyLocation));
    }

    /// <summary>Executes the ToLiteral operation.</summary>
    /// <param name="value">The value parameter.</param>
    /// <returns>The ToLiteral operation result.</returns>
    private static string ToLiteral(string value)
        => SymbolDisplay.FormatLiteral(value, quote: true);

    /// <summary>Executes the CreateTemporaryDirectory operation.</summary>
    /// <returns>The CreateTemporaryDirectory operation result.</returns>
    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"mitsubishirx-generated-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        return directory;
    }

    /// <summary>Executes the GetRepositoryRoot operation.</summary>
    /// <returns>The GetRepositoryRoot operation result.</returns>
    private static string GetRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "README.md"))
                && File.Exists(Path.Combine(current.FullName, "src", "MitsubishiRx", "MitsubishiRx.csproj")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    /// <summary>Executes the PackMitsubishiRxPackageAsync operation.</summary>
    /// <returns>The PackMitsubishiRxPackageAsync operation result.</returns>
    private static async Task<string> PackMitsubishiRxPackageAsync()
    {
        if (!string.IsNullOrWhiteSpace(_cachedPackedPackagePath) && File.Exists(_cachedPackedPackagePath))
        {
            return _cachedPackedPackagePath;
        }

        await PackagePackGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!string.IsNullOrWhiteSpace(_cachedPackedPackagePath) && File.Exists(_cachedPackedPackagePath))
            {
                return _cachedPackedPackagePath;
            }

            string repoRoot = GetRepositoryRoot();
            string projectPath = Path.Combine(repoRoot, "src", "MitsubishiRx", "MitsubishiRx.csproj");
            string outputDirectory = CreateTemporaryDirectory();

            var pack = await RunDotNetAsync(
                "pack",
                projectPath,
                repoRoot,
                "-c",
                "Release",
                "-o",
                outputDirectory,
                "/p:GeneratePackageOnBuild=false",
                "/p:UseSharedCompilation=false").ConfigureAwait(false);
            if (pack.ExitCode != 0)
            {
                throw new InvalidOperationException(pack.Output);
            }

            _cachedPackedPackagePath = Directory.GetFiles(outputDirectory, "MitsubishiRx.*.nupkg", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(static path => !path.EndsWith(".symbols.nupkg", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("Expected MitsubishiRx package was not created.");

            return _cachedPackedPackagePath;
        }
        finally
        {
            _ = PackagePackGate.Release();
        }
    }

    /// <summary>Executes the RunDotNetAsync operation.</summary>
    /// <param name="command">The command parameter.</param>
    /// <param name="projectPath">The projectPath parameter.</param>
    /// <param name="workingDirectory">The workingDirectory parameter.</param>
    /// <param name="extraArguments">The extraArguments parameter.</param>
    /// <returns>The RunDotNetAsync operation result.</returns>
    private static async Task<(int ExitCode, string Output)> RunDotNetAsync(string command, string projectPath, string workingDirectory, params string[] extraArguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = @"C:\Program Files\dotnet\dotnet.exe",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add(command);
        startInfo.ArgumentList.Add(projectPath);
        foreach (string argument in extraArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start dotnet process.");
        string standardOutput = await process.StandardOutput.ReadToEndAsync();
        string standardError = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, standardOutput + Environment.NewLine + standardError);
    }

    /// <summary>Executes the TryDeleteDirectory operation.</summary>
    /// <param name="path">The path parameter.</param>
    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"Unable to delete temporary directory '{path}'.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException($"Unable to delete temporary directory '{path}'.", ex);
        }
    }
}
