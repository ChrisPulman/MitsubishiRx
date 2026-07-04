// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Provides the MitsubishiTagChange record.</summary>
/// <param name="Name">The Name parameter.</param>
/// <param name="Previous">The Previous parameter.</param>
/// <param name="Current">The Current parameter.</param>
public sealed record MitsubishiTagChange(string Name, MitsubishiTagDefinition? Previous, MitsubishiTagDefinition? Current)
{
    /// <summary>Gets or sets the ChangeKinds property.</summary>
    public MitsubishiSchemaChangeKind ChangeKinds => ClassifyTagChange(Previous, Current);

    /// <summary>Executes the ClassifyTagChange operation.</summary>
    /// <param name="previous">The previous parameter.</param>
    /// <param name="current">The current parameter.</param>
    /// <returns>The ClassifyTagChange operation result.</returns>
    private static MitsubishiSchemaChangeKind ClassifyTagChange(MitsubishiTagDefinition? previous, MitsubishiTagDefinition? current)
    {
        if (previous is null || current is null)
        {
            return MitsubishiSchemaChangeKind.StructureChange;
        }

        var kinds = MitsubishiSchemaChangeKind.None;
        if (!string.Equals(previous.Address, current.Address, StringComparison.OrdinalIgnoreCase))
        {
            kinds |= MitsubishiSchemaChangeKind.AddressChange;
        }

        if (HasDataShapeChange(previous, current))
        {
            kinds |= MitsubishiSchemaChangeKind.DataTypeChange;
        }

        if (HasMetadataChange(previous, current) && kinds == MitsubishiSchemaChangeKind.None)
        {
            kinds |= MitsubishiSchemaChangeKind.MetadataOnly;
        }

        return kinds;
    }

    /// <summary>Executes the HasDataShapeChange operation.</summary>
    /// <param name="previous">The previous parameter.</param>
    /// <param name="current">The current parameter.</param>
    /// <returns>The HasDataShapeChange operation result.</returns>
    private static bool HasDataShapeChange(MitsubishiTagDefinition previous, MitsubishiTagDefinition current) =>
        !string.Equals(previous.DataType, current.DataType, StringComparison.OrdinalIgnoreCase) ||
        previous.Length != current.Length ||
        !string.Equals(previous.Encoding, current.Encoding, StringComparison.OrdinalIgnoreCase) ||
        previous.Signed != current.Signed ||
        !string.Equals(previous.ByteOrder, current.ByteOrder, StringComparison.OrdinalIgnoreCase);

    /// <summary>Executes the HasMetadataChange operation.</summary>
    /// <param name="previous">The previous parameter.</param>
    /// <param name="current">The current parameter.</param>
    /// <returns>The HasMetadataChange operation result.</returns>
    private static bool HasMetadataChange(MitsubishiTagDefinition previous, MitsubishiTagDefinition current) =>
        !string.Equals(previous.Description, current.Description, StringComparison.Ordinal) ||
        previous.Scale != current.Scale ||
        previous.Offset != current.Offset ||
        !string.Equals(previous.Units, current.Units, StringComparison.Ordinal) ||
        !string.Equals(previous.Notes, current.Notes, StringComparison.Ordinal);
}
