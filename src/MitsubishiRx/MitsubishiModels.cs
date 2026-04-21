// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

namespace MitsubishiRx;

/// <summary>
/// Connection settings for the Mitsubishi reactive client.
/// </summary>
/// <param name="Host">Target host name, IP address, or serial port name.</param>
/// <param name="Port">Target TCP/UDP port configured on the PLC or Ethernet module.</param>
/// <param name="FrameType">Frame family used for communication.</param>
/// <param name="DataCode">ASCII or binary protocol encoding.</param>
/// <param name="TransportKind">TCP, UDP, or serial transport.</param>
/// <param name="Route">SLMP routing metadata for 3E/4E frames.</param>
/// <param name="MonitoringTimer">Monitoring timer in 250 ms units.</param>
/// <param name="Timeout">Transport timeout.</param>
/// <param name="CpuType">Optional family hint.</param>
/// <param name="XyNotation">How X and Y device addresses are parsed.</param>
/// <param name="LegacyPcNumber">PC number used for 1E frames.</param>
/// <param name="SerialNumberProvider">4E serial number generator.</param>
/// <param name="Serial">Optional reactive serial transport settings for 1C/3C/4C communication.</param>
public sealed record MitsubishiClientOptions(
    string Host,
    int Port,
    MitsubishiFrameType FrameType,
    CommunicationDataCode DataCode,
    MitsubishiTransportKind TransportKind,
    MitsubishiRoute? Route = null,
    ushort MonitoringTimer = 0x0010,
    TimeSpan? Timeout = null,
    CpuType CpuType = CpuType.None,
    XyAddressNotation XyNotation = XyAddressNotation.Octal,
    byte LegacyPcNumber = 0xFF,
    Func<ushort>? SerialNumberProvider = null,
    MitsubishiSerialOptions? Serial = null)
{
    /// <summary>
    /// Gets the resolved timeout.
    /// </summary>
    public TimeSpan ResolvedTimeout => Timeout ?? TimeSpan.FromSeconds(4);

    /// <summary>
    /// Gets the resolved route.
    /// </summary>
    public MitsubishiRoute ResolvedRoute => Route ?? MitsubishiRoute.Default;

    /// <summary>
    /// Gets the configured serial options or throws when serial transport is selected without them.
    /// </summary>
    public MitsubishiSerialOptions ResolvedSerial => Serial ?? throw new InvalidOperationException("Serial transport requires Serial options.");

    /// <summary>
    /// Gets the next serial number for 4E packets.
    /// </summary>
    /// <returns>The next serial number.</returns>
    public ushort GetNextSerialNumber() => SerialNumberProvider?.Invoke() ?? 0;
}

/// <summary>
/// Represents a 3E / 4E access route.
/// </summary>
/// <param name="NetworkNumber">Destination network number.</param>
/// <param name="StationNumber">Destination station number.</param>
/// <param name="ModuleIoNumber">Destination module I/O number.</param>
/// <param name="MultidropStationNumber">Destination multidrop station number.</param>
/// <param name="ExtensionStationNumber">Optional extension station number for TSN / station extension frames.</param>
public sealed record MitsubishiRoute(
    byte NetworkNumber,
    byte StationNumber,
    ushort ModuleIoNumber,
    byte MultidropStationNumber,
    ushort? ExtensionStationNumber = null)
{
    /// <summary>
    /// Gets the standard direct-connect route for the own station CPU.
    /// </summary>
    public static MitsubishiRoute Default { get; } = new(0x00, 0xFF, 0x03FF, 0x00);
}

/// <summary>
/// Carries a low-level encoded MC protocol command request.
/// </summary>
/// <param name="Command">Command number.</param>
/// <param name="Subcommand">Subcommand number.</param>
/// <param name="Body">Encoded request body.</param>
/// <param name="Description">Human readable description.</param>
public sealed record MitsubishiRawCommandRequest(ushort Command, ushort Subcommand, IReadOnlyList<byte>? Body = null, string? Description = null)
{
    /// <summary>
    /// Gets the request body as a read-only list.
    /// </summary>
    public IReadOnlyList<byte> ResolvedBody => Body ?? Array.Empty<byte>();
}

/// <summary>
/// Represents an encoded transport exchange.
/// </summary>
/// <param name="Payload">Bytes to send.</param>
/// <param name="ExpectedResponseLength">Expected response length for fixed-size frame types.</param>
/// <param name="Description">Human readable description.</param>
public sealed record MitsubishiTransportRequest(byte[] Payload, int? ExpectedResponseLength, string Description);

/// <summary>
/// Structured operation log entry emitted by the reactive client.
/// </summary>
/// <param name="TimestampUtc">Event time in UTC.</param>
/// <param name="State">Current connection state.</param>
/// <param name="Description">Operation description.</param>
/// <param name="Success">Whether the step succeeded.</param>
/// <param name="RequestPayload">Encoded request payload when available.</param>
/// <param name="ResponsePayload">Encoded response payload when available.</param>
/// <param name="Exception">Associated exception, if any.</param>
public sealed record MitsubishiOperationLog(
    DateTimeOffset TimestampUtc,
    MitsubishiConnectionState State,
    string Description,
    bool Success,
    ReadOnlyMemory<byte> RequestPayload,
    ReadOnlyMemory<byte> ResponsePayload,
    Exception? Exception = null);

/// <summary>
/// Rich PLC metadata returned by read-type operations.
/// </summary>
/// <param name="ModelName">Reported model name.</param>
/// <param name="ModelCode">Reported model code.</param>
public sealed record MitsubishiTypeName(string ModelName, ushort ModelCode);

/// <summary>
/// Represents a single device write value.
/// </summary>
/// <param name="Address">Target device.</param>
/// <param name="Value">Value to write.</param>
public sealed record MitsubishiDeviceValue(MitsubishiDeviceAddress Address, ushort Value);

/// <summary>
/// Represents a word device block for multiple block operations.
/// </summary>
/// <param name="Address">Start address.</param>
/// <param name="Values">Words in the block.</param>
public sealed record MitsubishiWordBlock(MitsubishiDeviceAddress Address, ReadOnlyMemory<ushort> Values);

/// <summary>
/// Represents a bit device block for multiple block operations.
/// </summary>
/// <param name="Address">Start address.</param>
/// <param name="Values">Bit values in the block.</param>
public sealed record MitsubishiBitBlock(MitsubishiDeviceAddress Address, ReadOnlyMemory<bool> Values);

/// <summary>
/// Multiple block read/write request payload.
/// </summary>
/// <param name="WordBlocks">Word blocks.</param>
/// <param name="BitBlocks">Bit blocks.</param>
public sealed record MitsubishiBlockRequest(IReadOnlyList<MitsubishiWordBlock>? WordBlocks = null, IReadOnlyList<MitsubishiBitBlock>? BitBlocks = null)
{
    /// <summary>
    /// Gets the resolved word blocks.
    /// </summary>
    public IReadOnlyList<MitsubishiWordBlock> ResolvedWordBlocks => WordBlocks ?? Array.Empty<MitsubishiWordBlock>();

    /// <summary>
    /// Gets the resolved bit blocks.
    /// </summary>
    public IReadOnlyList<MitsubishiBitBlock> ResolvedBitBlocks => BitBlocks ?? Array.Empty<MitsubishiBitBlock>();
}
