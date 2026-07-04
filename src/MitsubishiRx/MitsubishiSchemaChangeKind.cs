// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Defines the MitsubishiSchemaChangeKind values.</summary>
[Flags]
public enum MitsubishiSchemaChangeKind
{
    /// <summary>Represents the None option.</summary>
    None = 0,
    /// <summary>Represents the MetadataOnly option.</summary>
    MetadataOnly = 1 << 0,
    /// <summary>Represents the AddressChange option.</summary>
    AddressChange = 1 << 1,
    /// <summary>Represents the DataTypeChange option.</summary>
    DataTypeChange = 1 << 2,
    /// <summary>Represents the GroupMembershipChange option.</summary>
    GroupMembershipChange = 1 << 3,
    /// <summary>Represents the StructureChange option.</summary>
    StructureChange = 1 << 4,
}
