// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Provides the MitsubishiRoute record.</summary>
/// <param name="NetworkNumber">The NetworkNumber parameter.</param>
/// <param name="StationNumber">The StationNumber parameter.</param>
/// <param name="ModuleIoNumber">The ModuleIoNumber parameter.</param>
/// <param name="MultidropStationNumber">The MultidropStationNumber parameter.</param>
/// <param name="ExtensionStationNumber">The ExtensionStationNumber parameter.</param>
public sealed record MitsubishiRoute(byte NetworkNumber, byte StationNumber, ushort ModuleIoNumber, byte MultidropStationNumber, ushort? ExtensionStationNumber = null)
{
    /// <summary>Gets or sets the Default property.</summary>
    public static MitsubishiRoute Default { get; } = new(0x00, 0xFF, 0x03FF, 0x00);
}
