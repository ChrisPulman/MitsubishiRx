// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Defines the CpuType values.</summary>
public enum CpuType
{
    /// <summary>Represents the None option.</summary>
    None,
    /// <summary>Represents the ASeries option.</summary>
    ASeries,
    /// <summary>Represents the QnaSeries option.</summary>
    QnaSeries,
    /// <summary>Represents the QSeries option.</summary>
    QSeries,
    /// <summary>Represents the LSeries option.</summary>
    LSeries,
    /// <summary>Represents the Fx3 option.</summary>
    Fx3,
    /// <summary>Represents the Fx5 option.</summary>
    Fx5,
    /// <summary>Represents the IQR option.</summary>
    IQR,
}
