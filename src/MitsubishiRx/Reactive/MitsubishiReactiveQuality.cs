// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Defines the MitsubishiReactiveQuality values.</summary>
public enum MitsubishiReactiveQuality
{
    /// <summary>Represents the Good option.</summary>
    Good,
    /// <summary>Represents the Bad option.</summary>
    Bad,
    /// <summary>Represents the Stale option.</summary>
    Stale,
    /// <summary>Represents the Heartbeat option.</summary>
    Heartbeat,
    /// <summary>Represents the Error option.</summary>
    Error,
}
