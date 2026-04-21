// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MitsubishiRx;

/// <summary>
/// Semantic change categories used to classify tag-database rollout risk.
/// </summary>
[Flags]
public enum MitsubishiSchemaChangeKind
{
    /// <summary>
    /// No semantic changes detected.
    /// </summary>
    None = 0,

    /// <summary>
    /// Metadata-only changes such as description, units, notes, scale, or offset.
    /// </summary>
    MetadataOnly = 1 << 0,

    /// <summary>
    /// Device address changed.
    /// </summary>
    AddressChange = 1 << 1,

    /// <summary>
    /// Data type, string shape, signedness, byte order, or encoding changed.
    /// </summary>
    DataTypeChange = 1 << 2,

    /// <summary>
    /// Group membership/order changed.
    /// </summary>
    GroupMembershipChange = 1 << 3,

    /// <summary>
    /// Tag or group added/removed.
    /// </summary>
    StructureChange = 1 << 4,
}

/// <summary>
/// Rollout policy for deciding which schema changes may be applied automatically.
/// </summary>
public enum MitsubishiTagRolloutPolicy
{
    /// <summary>
    /// Allow all validated schema changes.
    /// </summary>
    AllowAll = 0,

    /// <summary>
    /// Allow only metadata and group membership changes; reject address, datatype, and structural changes.
    /// </summary>
    SafeMetadataAndGroups = 1,
}
