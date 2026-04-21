// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MitsubishiRx;

/// <summary>
/// Defines the supported Mitsubishi PLC / Ethernet endpoint families.
/// </summary>
public enum CpuType
{
    /// <summary>
    /// No explicit family hint.
    /// </summary>
    None,

    /// <summary>
    /// MELSEC-A / AnS family using A-compatible Ethernet modules.
    /// </summary>
    ASeries,

    /// <summary>
    /// MELSEC-QnA family.
    /// </summary>
    QnaSeries,

    /// <summary>
    /// MELSEC-Q family.
    /// </summary>
    QSeries,

    /// <summary>
    /// MELSEC-L family.
    /// </summary>
    LSeries,

    /// <summary>
    /// MELSEC-FX3 family.
    /// </summary>
    Fx3,

    /// <summary>
    /// MELSEC iQ-F / FX5 family.
    /// </summary>
    Fx5,

    /// <summary>
    /// MELSEC iQ-R family.
    /// </summary>
    IQR,
}
