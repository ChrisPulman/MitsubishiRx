// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Ports;

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Provides the MitsubishiSerialOptions record.</summary>
/// <param name="PortName">The PortName parameter.</param>
/// <param name="BaudRate">The BaudRate parameter.</param>
/// <param name="DataBits">The DataBits parameter.</param>
/// <param name="Parity">The Parity parameter.</param>
/// <param name="StopBits">The StopBits parameter.</param>
/// <param name="Handshake">The Handshake parameter.</param>
/// <param name="MessageFormat">The MessageFormat parameter.</param>
/// <param name="StationNumber">The StationNumber parameter.</param>
/// <param name="NetworkNumber">The NetworkNumber parameter.</param>
/// <param name="PcNumber">The PcNumber parameter.</param>
/// <param name="RequestDestinationModuleIoNumber">The RequestDestinationModuleIoNumber parameter.</param>
/// <param name="RequestDestinationModuleStationNumber">The RequestDestinationModuleStationNumber parameter.</param>
/// <param name="SelfStationNumber">The SelfStationNumber parameter.</param>
/// <param name="MessageWait">The MessageWait parameter.</param>
/// <param name="ReadBufferSize">The ReadBufferSize parameter.</param>
/// <param name="WriteBufferSize">The WriteBufferSize parameter.</param>
/// <param name="NewLine">The NewLine parameter.</param>
public sealed record MitsubishiSerialOptions(string PortName, int BaudRate = 9600, int DataBits = 7, Parity Parity = Parity.Even, StopBits StopBits = StopBits.One, Handshake Handshake = Handshake.None, MitsubishiSerialMessageFormat MessageFormat = MitsubishiSerialMessageFormat.Format5, byte StationNumber = 0x00, byte NetworkNumber = 0x00, byte PcNumber = 0xFF, ushort RequestDestinationModuleIoNumber = 0x03FF, byte RequestDestinationModuleStationNumber = 0x00, byte SelfStationNumber = 0x00, byte MessageWait = 0x00, int ReadBufferSize = 4096, int WriteBufferSize = 4096, string NewLine = "\r\n")
{
    /// <summary>Gets or sets the Route property.</summary>
    public MitsubishiSerialRoute Route => new(StationNumber, NetworkNumber, PcNumber, RequestDestinationModuleIoNumber, RequestDestinationModuleStationNumber, SelfStationNumber);
}
