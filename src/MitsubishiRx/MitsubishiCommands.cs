// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Provides the MitsubishiCommands type.</summary>
public static class MitsubishiCommands
{
    /// <summary>Stores the DeviceRead field.</summary>
    public const ushort DeviceRead = 0x0401;

    /// <summary>Stores the DeviceWrite field.</summary>
    public const ushort DeviceWrite = 0x1401;

    /// <summary>Stores the RandomRead field.</summary>
    public const ushort RandomRead = 0x0403;

    /// <summary>Stores the RandomWrite field.</summary>
    public const ushort RandomWrite = 0x1402;

    /// <summary>Stores the BlockRead field.</summary>
    public const ushort BlockRead = 0x0406;

    /// <summary>Stores the BlockWrite field.</summary>
    public const ushort BlockWrite = 0x1406;

    /// <summary>Stores the EntryMonitorDevice field.</summary>
    public const ushort EntryMonitorDevice = 0x0801;

    /// <summary>Stores the ExecuteMonitor field.</summary>
    public const ushort ExecuteMonitor = 0x0802;

    /// <summary>Stores the ExtendUnitRead field.</summary>
    public const ushort ExtendUnitRead = 0x0601;

    /// <summary>Stores the ExtendUnitWrite field.</summary>
    public const ushort ExtendUnitWrite = 0x1601;

    /// <summary>Stores the MemoryRead field.</summary>
    public const ushort MemoryRead = 0x0613;

    /// <summary>Stores the MemoryWrite field.</summary>
    public const ushort MemoryWrite = 0x1613;

    /// <summary>Stores the ReadTypeName field.</summary>
    public const ushort ReadTypeName = 0x0101;

    /// <summary>Stores the RemoteRun field.</summary>
    public const ushort RemoteRun = 0x1001;

    /// <summary>Stores the RemoteStop field.</summary>
    public const ushort RemoteStop = 0x1002;

    /// <summary>Stores the RemotePause field.</summary>
    public const ushort RemotePause = 0x1003;

    /// <summary>Stores the RemoteLatchClear field.</summary>
    public const ushort RemoteLatchClear = 0x1005;

    /// <summary>Stores the RemoteReset field.</summary>
    public const ushort RemoteReset = 0x1006;

    /// <summary>Stores the Unlock field.</summary>
    public const ushort Unlock = 0x1630;

    /// <summary>Stores the Lock field.</summary>
    public const ushort Lock = 0x1631;

    /// <summary>Stores the LoopbackTest field.</summary>
    public const ushort LoopbackTest = 0x0619;

    /// <summary>Stores the ClearError field.</summary>
    public const ushort ClearError = 0x1617;
}
