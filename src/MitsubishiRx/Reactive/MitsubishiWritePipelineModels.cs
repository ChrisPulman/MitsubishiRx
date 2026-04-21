// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MitsubishiRx;

/// <summary>
/// Reactive write pipeline behavior.
/// </summary>
public enum MitsubishiReactiveWriteMode
{
    /// <summary>
    /// Preserve every posted write in order.
    /// </summary>
    Queued,

    /// <summary>
    /// Collapse bursts to the latest posted value.
    /// </summary>
    LatestWins,

    /// <summary>
    /// Delay writes within a window and emit the latest value once the window closes.
    /// </summary>
    Coalescing,
}

/// <summary>
/// Result emitted by a reactive write pipeline.
/// </summary>
/// <param name="Target">Human-readable target.</param>
/// <param name="TimestampUtc">Completion time.</param>
/// <param name="Mode">Applied write mode.</param>
/// <param name="Success">Whether the write succeeded.</param>
/// <param name="Error">Error text when present.</param>
/// <param name="ErrorCode">Error code when present.</param>
/// <param name="Exception">Associated exception when present.</param>
public sealed record MitsubishiReactiveWriteResult(
    string Target,
    DateTimeOffset TimestampUtc,
    MitsubishiReactiveWriteMode Mode,
    bool Success,
    string Error = "",
    int ErrorCode = 0,
    Exception? Exception = null);
