// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Provides the MitsubishiTagDefinitionDocument type.</summary>
internal sealed class MitsubishiTagDefinitionDocument
{
    /// <summary>Gets or sets the Name property.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the Address property.</summary>
    public string? Address { get; set; }

    /// <summary>Gets or sets the DataType property.</summary>
    public string? DataType { get; set; }

    /// <summary>Gets or sets the Description property.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the Scale property.</summary>
    public double Scale { get; set; } = 1.0;

    /// <summary>Gets or sets the Offset property.</summary>
    public double Offset { get; set; }

    /// <summary>Gets or sets the Length property.</summary>
    public int? Length { get; set; }

    /// <summary>Gets or sets the Encoding property.</summary>
    public string? Encoding { get; set; }

    /// <summary>Gets or sets the Units property.</summary>
    public string? Units { get; set; }

    /// <summary>Gets or sets the Signed property.</summary>
    public bool Signed { get; set; }

    /// <summary>Gets or sets the ByteOrder property.</summary>
    public string? ByteOrder { get; set; }

    /// <summary>Gets or sets the Notes property.</summary>
    public string? Notes { get; set; }

    /// <summary>Executes the FromModel operation.</summary>
    /// <param name="model">The model parameter.</param>
    /// <returns>The FromModel operation result.</returns>
    public static MitsubishiTagDefinitionDocument FromModel(MitsubishiTagDefinition model) => new()
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

    /// <summary>Executes the ToModel operation.</summary>
    /// <returns>The ToModel operation result.</returns>
    public MitsubishiTagDefinition ToModel() => new(Name ?? string.Empty, Address ?? string.Empty, DataType, Description, Scale, Offset, Length, Encoding, Units, Signed, ByteOrder, Notes);
}
