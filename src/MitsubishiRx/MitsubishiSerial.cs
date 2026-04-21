// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Ports;

namespace MitsubishiRx;

/// <summary>
/// Defines the serial MC protocol message formats supported by the library.
/// </summary>
public enum MitsubishiSerialMessageFormat
{
    /// <summary>
    /// Format 1: ASCII with ENQ/STX/ACK/NAK framing.
    /// </summary>
    Format1,

    /// <summary>
    /// Format 4: ASCII with CR/LF framing.
    /// </summary>
    Format4,

    /// <summary>
    /// Format 5: Binary 4C frame with DLE/STX/ETX framing.
    /// </summary>
    Format5,
}

/// <summary>
/// Reactive serial-port settings for Mitsubishi serial MC protocol communication.
/// </summary>
/// <param name="PortName">Serial port name, for example COM3.</param>
/// <param name="BaudRate">Configured baud rate.</param>
/// <param name="DataBits">Configured data bits.</param>
/// <param name="Parity">Configured parity.</param>
/// <param name="StopBits">Configured stop bits.</param>
/// <param name="Handshake">Configured handshake.</param>
/// <param name="MessageFormat">Configured serial message format.</param>
/// <param name="StationNumber">Target station number.</param>
/// <param name="NetworkNumber">Target network number for 3C/4C routes.</param>
/// <param name="PcNumber">Target PC number.</param>
/// <param name="RequestDestinationModuleIoNumber">Request destination module I/O number for 4C format 5.</param>
/// <param name="RequestDestinationModuleStationNumber">Request destination module station number for 4C format 5.</param>
/// <param name="SelfStationNumber">Self-station number for multidrop topologies.</param>
/// <param name="MessageWait">Message wait in 10 ms units.</param>
/// <param name="ReadBufferSize">Serial read buffer size.</param>
/// <param name="WriteBufferSize">Serial write buffer size.</param>
/// <param name="NewLine">Configured newline sequence for line-oriented modes.</param>
public sealed record MitsubishiSerialOptions(
    string PortName,
    int BaudRate = 9600,
    int DataBits = 7,
    Parity Parity = Parity.Even,
    StopBits StopBits = StopBits.One,
    Handshake Handshake = Handshake.None,
    MitsubishiSerialMessageFormat MessageFormat = MitsubishiSerialMessageFormat.Format5,
    byte StationNumber = 0x00,
    byte NetworkNumber = 0x00,
    byte PcNumber = 0xFF,
    ushort RequestDestinationModuleIoNumber = 0x03FF,
    byte RequestDestinationModuleStationNumber = 0x00,
    byte SelfStationNumber = 0x00,
    byte MessageWait = 0x00,
    int ReadBufferSize = 4096,
    int WriteBufferSize = 4096,
    string NewLine = "\r\n")
{
    /// <summary>
    /// Gets the default direct-connect serial route.
    /// </summary>
    public MitsubishiSerialRoute Route => new(
        StationNumber,
        NetworkNumber,
        PcNumber,
        RequestDestinationModuleIoNumber,
        RequestDestinationModuleStationNumber,
        SelfStationNumber);
}

/// <summary>
/// Serial route metadata used by 1C/3C/4C message builders.
/// </summary>
/// <param name="StationNumber">Target station number.</param>
/// <param name="NetworkNumber">Target network number.</param>
/// <param name="PcNumber">Target PC number.</param>
/// <param name="RequestDestinationModuleIoNumber">Request destination module I/O number.</param>
/// <param name="RequestDestinationModuleStationNumber">Request destination module station number.</param>
/// <param name="SelfStationNumber">Self-station number.</param>
public sealed record MitsubishiSerialRoute(
    byte StationNumber,
    byte NetworkNumber,
    byte PcNumber,
    ushort RequestDestinationModuleIoNumber,
    byte RequestDestinationModuleStationNumber,
    byte SelfStationNumber);
