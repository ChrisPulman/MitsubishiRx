// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MitsubishiRx;

/// <summary>
/// Represents a single changed tag definition between two schema versions.
/// </summary>
/// <param name="Name">Tag name.</param>
/// <param name="Previous">Previous tag definition.</param>
/// <param name="Current">Current tag definition.</param>
public sealed record MitsubishiTagChange(string Name, MitsubishiTagDefinition? Previous, MitsubishiTagDefinition? Current)
{
    /// <summary>
    /// Gets the semantic change classification for this tag change.
    /// </summary>
    public MitsubishiSchemaChangeKind ChangeKinds => ClassifyTagChange(Previous, Current);

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

        if (!string.Equals(previous.DataType, current.DataType, StringComparison.OrdinalIgnoreCase) ||
            previous.Length != current.Length ||
            !string.Equals(previous.Encoding, current.Encoding, StringComparison.OrdinalIgnoreCase) ||
            previous.Signed != current.Signed ||
            !string.Equals(previous.ByteOrder, current.ByteOrder, StringComparison.OrdinalIgnoreCase))
        {
            kinds |= MitsubishiSchemaChangeKind.DataTypeChange;
        }

        var metadataChanged = !string.Equals(previous.Description, current.Description, StringComparison.Ordinal) ||
            previous.Scale != current.Scale ||
            previous.Offset != current.Offset ||
            !string.Equals(previous.Units, current.Units, StringComparison.Ordinal) ||
            !string.Equals(previous.Notes, current.Notes, StringComparison.Ordinal);
        if (metadataChanged && kinds == MitsubishiSchemaChangeKind.None)
        {
            kinds |= MitsubishiSchemaChangeKind.MetadataOnly;
        }

        return kinds;
    }
}

/// <summary>
/// Represents a single changed tag group definition between two schema versions.
/// </summary>
/// <param name="Name">Group name.</param>
/// <param name="Previous">Previous group definition.</param>
/// <param name="Current">Current group definition.</param>
public sealed record MitsubishiTagGroupChange(string Name, MitsubishiTagGroupDefinition? Previous, MitsubishiTagGroupDefinition? Current)
{
    /// <summary>
    /// Gets the semantic change classification for this group change.
    /// </summary>
    public MitsubishiSchemaChangeKind ChangeKinds =>
        Previous is null || Current is null
            ? MitsubishiSchemaChangeKind.StructureChange
            : MitsubishiSchemaChangeKind.GroupMembershipChange;
}

/// <summary>
/// Represents a semantic diff between two tag-database schema versions.
/// </summary>
/// <param name="AddedTags">Tags present only in the current schema.</param>
/// <param name="RemovedTags">Tags present only in the previous schema.</param>
/// <param name="ChangedTags">Tags present in both schemas with different definitions.</param>
/// <param name="AddedGroups">Groups present only in the current schema.</param>
/// <param name="RemovedGroups">Groups present only in the previous schema.</param>
/// <param name="ChangedGroups">Groups present in both schemas with different definitions.</param>
public sealed record MitsubishiTagDatabaseDiff(
    IReadOnlyList<MitsubishiTagDefinition> AddedTags,
    IReadOnlyList<MitsubishiTagDefinition> RemovedTags,
    IReadOnlyList<MitsubishiTagChange> ChangedTags,
    IReadOnlyList<MitsubishiTagGroupDefinition> AddedGroups,
    IReadOnlyList<MitsubishiTagGroupDefinition> RemovedGroups,
    IReadOnlyList<MitsubishiTagGroupChange> ChangedGroups)
{
    /// <summary>
    /// Gets the aggregate semantic change classification for this diff.
    /// </summary>
    public MitsubishiSchemaChangeKind ChangeKinds
    {
        get
        {
            var kinds = MitsubishiSchemaChangeKind.None;

            if (AddedTags.Count > 0 || RemovedTags.Count > 0 || AddedGroups.Count > 0 || RemovedGroups.Count > 0)
            {
                kinds |= MitsubishiSchemaChangeKind.StructureChange;
            }

            foreach (var change in ChangedTags)
            {
                kinds |= change.ChangeKinds;
            }

            foreach (var change in ChangedGroups)
            {
                kinds |= change.ChangeKinds;
            }

            return kinds;
        }
    }

    /// <summary>
    /// Gets a value indicating whether any tag or group changes were detected.
    /// </summary>
    public bool HasChanges =>
        AddedTags.Count > 0 ||
        RemovedTags.Count > 0 ||
        ChangedTags.Count > 0 ||
        AddedGroups.Count > 0 ||
        RemovedGroups.Count > 0 ||
        ChangedGroups.Count > 0;

    /// <summary>
    /// Gets the total number of semantic changes in the diff.
    /// </summary>
    public int ChangeCount =>
        AddedTags.Count +
        RemovedTags.Count +
        ChangedTags.Count +
        AddedGroups.Count +
        RemovedGroups.Count +
        ChangedGroups.Count;

    /// <summary>
    /// Gets an empty schema diff.
    /// </summary>
    public static MitsubishiTagDatabaseDiff Empty { get; }
        = new([], [], [], [], [], []);
}
