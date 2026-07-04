// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Provides the MitsubishiClientOptions record.</summary>
/// <param name="Host">The Host parameter.</param>
/// <param name="Port">The Port parameter.</param>
/// <param name="FrameType">The FrameType parameter.</param>
/// <param name="DataCode">The DataCode parameter.</param>
/// <param name="TransportKind">The TransportKind parameter.</param>
/// <param name="Route">The Route parameter.</param>
/// <param name="MonitoringTimer">The MonitoringTimer parameter.</param>
/// <param name="Timeout">The Timeout parameter.</param>
/// <param name="CpuType">The CpuType parameter.</param>
/// <param name="XyNotation">The XYNotation parameter.</param>
/// <param name="LegacyPcNumber">The LegacyPcNumber parameter.</param>
/// <param name="SerialNumberProvider">The SerialNumberProvider parameter.</param>
/// <param name="Serial">The Serial parameter.</param>
public sealed record MitsubishiClientOptions(string Host, int Port, MitsubishiFrameType FrameType, CommunicationDataCode DataCode, MitsubishiTransportKind TransportKind, MitsubishiRoute? Route = null, ushort MonitoringTimer = 0x0010, TimeSpan? Timeout = null, CpuType CpuType = CpuType.None, XyAddressNotation XyNotation = XyAddressNotation.Octal, byte LegacyPcNumber = 0xFF, Func<ushort>? SerialNumberProvider = null, MitsubishiSerialOptions? Serial = null)
{
    /// <summary>Gets or sets the ResolvedTimeout property.</summary>
    public TimeSpan ResolvedTimeout => Timeout ?? TimeSpan.FromSeconds(4);

    /// <summary>Gets or sets the ResolvedRoute property.</summary>
    public MitsubishiRoute ResolvedRoute => Route ?? MitsubishiRoute.Default;

    /// <summary>Gets or sets the ResolvedSerial property.</summary>
    public MitsubishiSerialOptions ResolvedSerial => Serial ?? throw new InvalidOperationException("Serial transport requires Serial options.");

    /// <summary>Executes the GetNextSerialNumber operation.</summary>
    /// <returns>The GetNextSerialNumber operation result.</returns>
    public ushort GetNextSerialNumber() => SerialNumberProvider?.Invoke() ?? 0;
}
