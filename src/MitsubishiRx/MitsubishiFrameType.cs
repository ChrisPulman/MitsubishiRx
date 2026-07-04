// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Defines the MitsubishiFrameType values.</summary>
public enum MitsubishiFrameType
{
    /// <summary>Represents the OneE option.</summary>
    OneE,
    /// <summary>Represents the ThreeE option.</summary>
    ThreeE,
    /// <summary>Represents the FourE option.</summary>
    FourE,
    /// <summary>Represents the OneC option.</summary>
    OneC,
    /// <summary>Represents the ThreeC option.</summary>
    ThreeC,
    /// <summary>Represents the FourC option.</summary>
    FourC,
}
