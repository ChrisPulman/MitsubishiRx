// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Defines the MitsubishiTransportKind values.</summary>
public enum MitsubishiTransportKind
{
    /// <summary>Represents the Tcp option.</summary>
    Tcp,
    /// <summary>Represents the Udp option.</summary>
    Udp,
    /// <summary>Represents the Serial option.</summary>
    Serial,
}
