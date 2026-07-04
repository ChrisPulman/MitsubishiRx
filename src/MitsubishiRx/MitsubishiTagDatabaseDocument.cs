// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Provides the MitsubishiTagDatabaseDocument type.</summary>
internal sealed class MitsubishiTagDatabaseDocument
{
    /// <summary>Gets or sets the Tags property.</summary>
    public List<MitsubishiTagDefinitionDocument>? Tags { get; set; }

    /// <summary>Gets or sets the Groups property.</summary>
    public List<MitsubishiTagGroupDefinitionDocument>? Groups { get; set; }
}
