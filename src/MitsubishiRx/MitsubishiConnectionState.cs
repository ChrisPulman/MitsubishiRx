// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Defines the MitsubishiConnectionState values.</summary>
public enum MitsubishiConnectionState
{
    /// <summary>Represents the Disconnected option.</summary>
    Disconnected,
    /// <summary>Represents the Connecting option.</summary>
    Connecting,
    /// <summary>Represents the Connected option.</summary>
    Connected,
    /// <summary>Represents the Reconnecting option.</summary>
    Reconnecting,
    /// <summary>Represents the Faulted option.</summary>
    Faulted,
}
