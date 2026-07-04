// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Provides the MitsubishiBlockRequest record.</summary>
/// <param name="WordBlocks">The WordBlocks parameter.</param>
/// <param name="BitBlocks">The BitBlocks parameter.</param>
public sealed record MitsubishiBlockRequest(IReadOnlyList<MitsubishiWordBlock>? WordBlocks = null, IReadOnlyList<MitsubishiBitBlock>? BitBlocks = null)
{
    /// <summary>Gets or sets the ResolvedWordBlocks property.</summary>
    public IReadOnlyList<MitsubishiWordBlock> ResolvedWordBlocks => WordBlocks ?? Array.Empty<MitsubishiWordBlock>();

    /// <summary>Gets or sets the ResolvedBitBlocks property.</summary>
    public IReadOnlyList<MitsubishiBitBlock> ResolvedBitBlocks => BitBlocks ?? Array.Empty<MitsubishiBitBlock>();
}
