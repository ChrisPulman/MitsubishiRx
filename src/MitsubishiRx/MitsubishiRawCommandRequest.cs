// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Provides the MitsubishiRawCommandRequest record.</summary>
/// <param name="Command">The Command parameter.</param>
/// <param name="Subcommand">The Subcommand parameter.</param>
/// <param name="Body">The Body parameter.</param>
/// <param name="Description">The Description parameter.</param>
public sealed record MitsubishiRawCommandRequest(ushort Command, ushort Subcommand, IReadOnlyList<byte>? Body = null, string? Description = null)
{
    /// <summary>Gets or sets the ResolvedBody property.</summary>
    public IReadOnlyList<byte> ResolvedBody => Body ?? Array.Empty<byte>();
}
