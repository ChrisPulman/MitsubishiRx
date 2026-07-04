// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Provides the MitsubishiTagGroupDefinitionDocument type.</summary>
internal sealed class MitsubishiTagGroupDefinitionDocument
{
    /// <summary>Gets or sets the Name property.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the TagNames property.</summary>
    public List<string>? TagNames { get; set; }

    /// <summary>Executes the FromModel operation.</summary>
    /// <param name="model">The model parameter.</param>
    /// <returns>The FromModel operation result.</returns>
    public static MitsubishiTagGroupDefinitionDocument FromModel(MitsubishiTagGroupDefinition model) => new()
    {
        Name = model.Name,
        TagNames = model.ResolvedTagNames.ToList(),
    };

    /// <summary>Executes the ToModel operation.</summary>
    /// <returns>The ToModel operation result.</returns>
    public MitsubishiTagGroupDefinition ToModel() => new(Name ?? string.Empty, TagNames ?? new List<string>());
}
