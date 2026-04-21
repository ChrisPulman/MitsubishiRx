// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MitsubishiRx;

/// <summary>
/// Well-known MC protocol / SLMP command identifiers used by Mitsubishi Ethernet PLCs.
/// </summary>
public static class MitsubishiCommands
{
    /// <summary>
    /// Batch device read.
    /// </summary>
    public const ushort DeviceRead = 0x0401;

    /// <summary>
    /// Batch device write.
    /// </summary>
    public const ushort DeviceWrite = 0x1401;

    /// <summary>
    /// Random device read.
    /// </summary>
    public const ushort RandomRead = 0x0403;

    /// <summary>
    /// Random device write.
    /// </summary>
    public const ushort RandomWrite = 0x1402;

    /// <summary>
    /// Block read.
    /// </summary>
    public const ushort BlockRead = 0x0406;

    /// <summary>
    /// Block write.
    /// </summary>
    public const ushort BlockWrite = 0x1406;

    /// <summary>
    /// Monitor registration.
    /// </summary>
    public const ushort EntryMonitorDevice = 0x0801;

    /// <summary>
    /// Monitor execution.
    /// </summary>
    public const ushort ExecuteMonitor = 0x0802;

    /// <summary>
    /// Buffer memory access for intelligent modules.
    /// </summary>
    public const ushort ExtendUnitRead = 0x0601;

    /// <summary>
    /// Buffer memory write for intelligent modules.
    /// </summary>
    public const ushort ExtendUnitWrite = 0x1601;

    /// <summary>
    /// Memory read.
    /// </summary>
    public const ushort MemoryRead = 0x0613;

    /// <summary>
    /// Memory write.
    /// </summary>
    public const ushort MemoryWrite = 0x1613;

    /// <summary>
    /// Read PLC type name.
    /// </summary>
    public const ushort ReadTypeName = 0x0101;

    /// <summary>
    /// Remote run.
    /// </summary>
    public const ushort RemoteRun = 0x1001;

    /// <summary>
    /// Remote stop.
    /// </summary>
    public const ushort RemoteStop = 0x1002;

    /// <summary>
    /// Remote pause.
    /// </summary>
    public const ushort RemotePause = 0x1003;

    /// <summary>
    /// Remote latch clear.
    /// </summary>
    public const ushort RemoteLatchClear = 0x1005;

    /// <summary>
    /// Remote reset.
    /// </summary>
    public const ushort RemoteReset = 0x1006;

    /// <summary>
    /// Unlock with remote password.
    /// </summary>
    public const ushort Unlock = 0x1630;

    /// <summary>
    /// Lock with remote password.
    /// </summary>
    public const ushort Lock = 0x1631;

    /// <summary>
    /// Loopback self test.
    /// </summary>
    public const ushort LoopbackTest = 0x0619;

    /// <summary>
    /// Clear error and LED indication.
    /// </summary>
    public const ushort ClearError = 0x1617;
}
