// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Provides the MitsubishiDeviceMetadata record.</summary>
/// <param name="Symbol">The Symbol parameter.</param>
/// <param name="BinaryCode">The BinaryCode parameter.</param>
/// <param name="AsciiCode">The AsciiCode parameter.</param>
/// <param name="Kind">The Kind parameter.</param>
/// <param name="NumberFormat">The NumberFormat parameter.</param>
public sealed record MitsubishiDeviceMetadata(string Symbol, ushort BinaryCode, ushort AsciiCode, DeviceValueKind Kind, DeviceNumberFormat NumberFormat)
{
    /// <summary>Executes the GetRadix operation.</summary>
    /// <param name="addressNotation">The addressNotation parameter.</param>
    /// <returns>The GetRadix operation result.</returns>
    public int GetRadix(XyAddressNotation addressNotation) => NumberFormat switch
    {
        DeviceNumberFormat.Decimal => 10,
        DeviceNumberFormat.Hexadecimal => 16,
        DeviceNumberFormat.Octal => 8,
        DeviceNumberFormat.XyVariable => addressNotation == XyAddressNotation.Octal ? 8 : 16,
        _ => throw new ArgumentOutOfRangeException(nameof(NumberFormat)),
    };
}
