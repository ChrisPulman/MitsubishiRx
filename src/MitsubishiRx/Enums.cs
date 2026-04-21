// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MitsubishiRx;

/// <summary>
/// Defines the Mitsubishi MC protocol frame families supported by the client.
/// </summary>
public enum MitsubishiFrameType
{
    /// <summary>
    /// A-compatible 1E Ethernet frame.
    /// </summary>
    OneE,

    /// <summary>
    /// QnA-compatible 3E Ethernet frame.
    /// </summary>
    ThreeE,

    /// <summary>
    /// QnA-compatible 4E Ethernet frame with serial correlation.
    /// </summary>
    FourE,

    /// <summary>
    /// A-compatible 1C serial frame.
    /// </summary>
    OneC,

    /// <summary>
    /// QnA-compatible 3C serial frame.
    /// </summary>
    ThreeC,

    /// <summary>
    /// QnA-compatible 4C serial frame.
    /// </summary>
    FourC,
}

/// <summary>
/// Defines the data encoding used by MC protocol / SLMP packets.
/// </summary>
public enum CommunicationDataCode
{
    /// <summary>
    /// Packed binary protocol representation.
    /// </summary>
    Binary,

    /// <summary>
    /// ASCII hexadecimal protocol representation.
    /// </summary>
    Ascii,
}

/// <summary>
/// Defines the transport used to exchange Mitsubishi MC protocol frames.
/// </summary>
public enum MitsubishiTransportKind
{
    /// <summary>
    /// TCP request/response transport.
    /// </summary>
    Tcp,

    /// <summary>
    /// UDP datagram transport.
    /// </summary>
    Udp,

    /// <summary>
    /// Serial transport using a reactive SerialPortRx implementation.
    /// </summary>
    Serial,
}

/// <summary>
/// Defines how X and Y addresses should be parsed.
/// </summary>
public enum XyAddressNotation
{
    /// <summary>
    /// Parse X/Y device numbers as octal values.
    /// </summary>
    Octal,

    /// <summary>
    /// Parse X/Y device numbers as hexadecimal values.
    /// </summary>
    Hexadecimal,
}

/// <summary>
/// Defines high-level Mitsubishi connection states published by the client.
/// </summary>
public enum MitsubishiConnectionState
{
    /// <summary>
    /// The transport is disconnected.
    /// </summary>
    Disconnected,

    /// <summary>
    /// The client is establishing a transport connection.
    /// </summary>
    Connecting,

    /// <summary>
    /// The transport is connected and ready.
    /// </summary>
    Connected,

    /// <summary>
    /// The client is recovering after a fault.
    /// </summary>
    Reconnecting,

    /// <summary>
    /// The client observed a terminal transport or protocol failure.
    /// </summary>
    Faulted,
}
