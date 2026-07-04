// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Provides the MitsubishiTagGroupChange record.</summary>
/// <param name="Name">The Name parameter.</param>
/// <param name="Previous">The Previous parameter.</param>
/// <param name="Current">The Current parameter.</param>
public sealed record MitsubishiTagGroupChange(string Name, MitsubishiTagGroupDefinition? Previous, MitsubishiTagGroupDefinition? Current)
{
    /// <summary>Gets or sets the ChangeKinds property.</summary>
    public MitsubishiSchemaChangeKind ChangeKinds => Previous is null || Current is null ? MitsubishiSchemaChangeKind.StructureChange : MitsubishiSchemaChangeKind.GroupMembershipChange;
}
