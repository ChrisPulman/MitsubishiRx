// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Provides the MitsubishiTagDefinition record.</summary>
/// <param name="Name">The Name parameter.</param>
/// <param name="Address">The Address parameter.</param>
/// <param name="DataType">The DataType parameter.</param>
/// <param name="Description">The Description parameter.</param>
/// <param name="Scale">The Scale parameter.</param>
/// <param name="Offset">The Offset parameter.</param>
/// <param name="Length">The Length parameter.</param>
/// <param name="Encoding">The Encoding parameter.</param>
/// <param name="Units">The Units parameter.</param>
/// <param name="Signed">The Signed parameter.</param>
/// <param name="ByteOrder">The ByteOrder parameter.</param>
/// <param name="Notes">The Notes parameter.</param>
public sealed record MitsubishiTagDefinition(string Name, string Address, string? DataType = null, string? Description = null, double Scale = 1.0, double Offset = 0.0, int? Length = null, string? Encoding = null, string? Units = null, bool Signed = false, string? ByteOrder = null, string? Notes = null);
