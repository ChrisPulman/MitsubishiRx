// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Provides the MitsubishiTagDatabaseDiff record.</summary>
/// <param name="AddedTags">The AddedTags parameter.</param>
/// <param name="RemovedTags">The RemovedTags parameter.</param>
/// <param name="ChangedTags">The ChangedTags parameter.</param>
/// <param name="AddedGroups">The AddedGroups parameter.</param>
/// <param name="RemovedGroups">The RemovedGroups parameter.</param>
/// <param name="ChangedGroups">The ChangedGroups parameter.</param>
public sealed record MitsubishiTagDatabaseDiff(IReadOnlyList<MitsubishiTagDefinition> AddedTags, IReadOnlyList<MitsubishiTagDefinition> RemovedTags, IReadOnlyList<MitsubishiTagChange> ChangedTags, IReadOnlyList<MitsubishiTagGroupDefinition> AddedGroups, IReadOnlyList<MitsubishiTagGroupDefinition> RemovedGroups, IReadOnlyList<MitsubishiTagGroupChange> ChangedGroups)
{
    /// <summary>Gets or sets the Empty property.</summary>
    public static MitsubishiTagDatabaseDiff Empty { get; } = new([], [], [], [], [], []);

    /// <summary>Gets or sets the ChangeKinds property.</summary>
    public MitsubishiSchemaChangeKind ChangeKinds
    {
        get
        {
            var kinds = MitsubishiSchemaChangeKind.None;
            if (HasStructureChanges())
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

    /// <summary>Gets or sets the HasChanges property.</summary>
    public bool HasChanges => AddedTags.Count > 0 || RemovedTags.Count > 0 || ChangedTags.Count > 0 || AddedGroups.Count > 0 || RemovedGroups.Count > 0 || ChangedGroups.Count > 0;

    /// <summary>Gets or sets the ChangeCount property.</summary>
    public int ChangeCount => AddedTags.Count + RemovedTags.Count + ChangedTags.Count + AddedGroups.Count + RemovedGroups.Count + ChangedGroups.Count;

    /// <summary>Executes the HasStructureChanges operation.</summary>
    /// <returns>The HasStructureChanges operation result.</returns>
    private bool HasStructureChanges() => AddedTags.Count > 0 || RemovedTags.Count > 0 || AddedGroups.Count > 0 || RemovedGroups.Count > 0;
}
