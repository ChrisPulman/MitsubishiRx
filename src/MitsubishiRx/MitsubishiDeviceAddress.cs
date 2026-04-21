// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

namespace MitsubishiRx;

/// <summary>
/// Represents a parsed Mitsubishi device address.
/// </summary>
/// <param name="Symbol">Device symbol.</param>
/// <param name="Number">Parsed device number.</param>
/// <param name="Notation">Notation used during parsing.</param>
/// <param name="Original">Original device text.</param>
public sealed partial record MitsubishiDeviceAddress(string Symbol, int Number, XyAddressNotation Notation, string Original)
{
    private static readonly ReadOnlyDictionary<string, MitsubishiDeviceMetadata> s_metadata = new(
        new Dictionary<string, MitsubishiDeviceMetadata>(StringComparer.OrdinalIgnoreCase)
        {
            ["X"] = new("X", 0x9C, 0x5820, DeviceValueKind.Bit, DeviceNumberFormat.XyVariable),
            ["Y"] = new("Y", 0x9D, 0x5920, DeviceValueKind.Bit, DeviceNumberFormat.XyVariable),
            ["M"] = new("M", 0x90, 0x4D20, DeviceValueKind.Bit, DeviceNumberFormat.Decimal),
            ["L"] = new("L", 0x92, 0x4C20, DeviceValueKind.Bit, DeviceNumberFormat.Decimal),
            ["B"] = new("B", 0xA0, 0x4220, DeviceValueKind.Bit, DeviceNumberFormat.Hexadecimal),
            ["D"] = new("D", 0xA8, 0x4420, DeviceValueKind.Word, DeviceNumberFormat.Decimal),
            ["W"] = new("W", 0xB4, 0x5720, DeviceValueKind.Word, DeviceNumberFormat.Hexadecimal),
            ["R"] = new("R", 0xAF, 0x5220, DeviceValueKind.Word, DeviceNumberFormat.Decimal),
            ["ZR"] = new("ZR", 0xB0, 0x5A52, DeviceValueKind.Word, DeviceNumberFormat.Hexadecimal),
            ["TN"] = new("TN", 0xC2, 0x544E, DeviceValueKind.Word, DeviceNumberFormat.Decimal),
            ["TS"] = new("TS", 0xC1, 0x5453, DeviceValueKind.Bit, DeviceNumberFormat.Decimal),
            ["TC"] = new("TC", 0xC0, 0x5443, DeviceValueKind.Bit, DeviceNumberFormat.Decimal),
            ["CN"] = new("CN", 0xC5, 0x434E, DeviceValueKind.Word, DeviceNumberFormat.Decimal),
            ["CS"] = new("CS", 0xC4, 0x4353, DeviceValueKind.Bit, DeviceNumberFormat.Decimal),
            ["CC"] = new("CC", 0xC3, 0x4343, DeviceValueKind.Bit, DeviceNumberFormat.Decimal),
            ["SM"] = new("SM", 0x91, 0x534D, DeviceValueKind.Bit, DeviceNumberFormat.Decimal),
            ["SD"] = new("SD", 0xA9, 0x5344, DeviceValueKind.Word, DeviceNumberFormat.Decimal),
        });

    /// <summary>
    /// Gets the device metadata table used by the library.
    /// </summary>
    public static IReadOnlyDictionary<string, MitsubishiDeviceMetadata> Metadata => s_metadata;

    /// <summary>
    /// Parses a device string.
    /// </summary>
    /// <param name="value">Device text such as D100 or X10.</param>
    /// <param name="xyNotation">How X and Y addresses should be interpreted.</param>
    /// <returns>The parsed address.</returns>
    public static MitsubishiDeviceAddress Parse(string value, XyAddressNotation xyNotation = XyAddressNotation.Octal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var trimmed = value.Trim().ToUpperInvariant();
        var match = DeviceRegex().Match(trimmed);
        if (!match.Success)
        {
            throw new FormatException($"Invalid Mitsubishi device address '{value}'.");
        }

        var symbol = match.Groups[1].Value;
        var numberText = match.Groups[2].Value;
        if (!s_metadata.TryGetValue(symbol, out var metadata))
        {
            throw new NotSupportedException($"Device '{symbol}' is not currently supported.");
        }

        var number = Convert.ToInt32(numberText, metadata.GetRadix(xyNotation));
        return new MitsubishiDeviceAddress(symbol, number, xyNotation, trimmed);
    }

    /// <summary>
    /// Gets the static metadata for the parsed symbol.
    /// </summary>
    public MitsubishiDeviceMetadata Descriptor => s_metadata[Symbol];

    [System.Text.RegularExpressions.GeneratedRegex("^([A-Z]+)([0-9A-F]+)$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex DeviceRegex();
}

/// <summary>
/// Describes a Mitsubishi device family.
/// </summary>
/// <param name="Symbol">Canonical device symbol.</param>
/// <param name="BinaryCode">Binary MC/SLMP device code.</param>
/// <param name="AsciiCode">ASCII MC/SLMP device code encoded as hexadecimal bytes.</param>
/// <param name="Kind">Whether the device is bit or word based.</param>
/// <param name="NumberFormat">Address radix rules.</param>
public sealed record MitsubishiDeviceMetadata(string Symbol, ushort BinaryCode, ushort AsciiCode, DeviceValueKind Kind, DeviceNumberFormat NumberFormat)
{
    /// <summary>
    /// Resolves the numeric radix used for the device.
    /// </summary>
    /// <param name="xyNotation">Configured X/Y notation.</param>
    /// <returns>The radix.</returns>
    public int GetRadix(XyAddressNotation xyNotation) => NumberFormat switch
    {
        DeviceNumberFormat.Decimal => 10,
        DeviceNumberFormat.Hexadecimal => 16,
        DeviceNumberFormat.Octal => 8,
        DeviceNumberFormat.XyVariable => xyNotation == XyAddressNotation.Octal ? 8 : 16,
        _ => throw new ArgumentOutOfRangeException(nameof(NumberFormat)),
    };
}

/// <summary>
/// Defines whether a device stores bits or words.
/// </summary>
public enum DeviceValueKind
{
    /// <summary>
    /// Bit addressable device.
    /// </summary>
    Bit,

    /// <summary>
    /// Word addressable device.
    /// </summary>
    Word,
}

/// <summary>
/// Defines the supported address number formats for a device family.
/// </summary>
public enum DeviceNumberFormat
{
    /// <summary>
    /// Decimal address values.
    /// </summary>
    Decimal,

    /// <summary>
    /// Hexadecimal address values.
    /// </summary>
    Hexadecimal,

    /// <summary>
    /// Octal address values.
    /// </summary>
    Octal,

    /// <summary>
    /// X/Y devices can be octal or hexadecimal depending on configuration.
    /// </summary>
    XyVariable,
}
