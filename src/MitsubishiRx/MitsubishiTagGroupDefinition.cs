// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Provides the MitsubishiTagGroupDefinition record.</summary>
/// <param name="Name">The Name parameter.</param>
/// <param name="TagNames">The TagNames parameter.</param>
public sealed record MitsubishiTagGroupDefinition(string Name, IReadOnlyList<string> TagNames)
{
    /// <summary>Gets or sets the ResolvedTagNames property.</summary>
    public IReadOnlyList<string> ResolvedTagNames => TagNames ?? Array.Empty<string>();
}
