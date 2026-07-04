// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive.Tests;
#else

namespace MitsubishiRx.Tests;
#endif

/// <summary>Provides test scheduler helpers.</summary>
internal static class TestSchedulerExtensions
{
#if REACTIVE_SHIM
    /// <summary>Extends reactive test schedulers with time-based advancement.</summary>
    /// <param name="scheduler">The scheduler being extended.</param>
    extension(TestScheduler scheduler)
    {
        /// <summary>Advances the scheduler by the specified time span.</summary>
        /// <param name="delay">The delay to advance.</param>
        public void AdvanceBy(TimeSpan delay)
        {
            ArgumentNullException.ThrowIfNull(scheduler);
            scheduler.AdvanceBy(delay.Ticks);
        }
    }
#else
    /// <summary>Extends test schedulers with tick-based advancement.</summary>
    /// <param name="scheduler">The scheduler being extended.</param>
    extension(TestScheduler scheduler)
    {
        /// <summary>Advances the scheduler by the specified ticks.</summary>
        /// <param name="ticks">The tick count to advance.</param>
        public void AdvanceBy(long ticks)
        {
            ArgumentNullException.ThrowIfNull(scheduler);
            scheduler.AdvanceBy(TimeSpan.FromTicks(ticks));
        }
    }
#endif
}
