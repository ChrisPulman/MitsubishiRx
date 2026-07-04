// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Provides the MitsubishiReactiveWritePipeline type.</summary>
/// <typeparam name="TPayload">The TPayload type parameter.</typeparam>
public sealed class MitsubishiReactiveWritePipeline<TPayload> : IDisposable
{
    /// <summary>Stores the gate field.</summary>
    private readonly object _gate = new();

    /// <summary>Stores the scheduler field.</summary>
    private readonly IScheduler _scheduler;

    /// <summary>Stores the target field.</summary>
    private readonly string _target;

    /// <summary>Stores the writer field.</summary>
    private readonly Func<TPayload, Task<Responce>> _writer;

    /// <summary>Stores the coalescingWindow field.</summary>
    private readonly TimeSpan _coalescingWindow;

    /// <summary>Stores the queuedWrites field.</summary>
    private readonly Queue<TPayload> _queuedWrites = new();

    /// <summary>Stores the results field.</summary>
    private readonly Signal<MitsubishiReactiveWriteResult> _results = new();

    /// <summary>Stores the scheduledDrain field.</summary>
    private IDisposable? _scheduledDrain;

    /// <summary>Stores the coalescingTimer field.</summary>
    private IDisposable? _coalescingTimer;

    /// <summary>Stores the pendingLatest field.</summary>
    private TPayload? _pendingLatest;

    /// <summary>Stores the hasPendingLatest field.</summary>
    private bool _hasPendingLatest;

    /// <summary>Stores the disposed field.</summary>
    private bool _disposed;

    /// <summary>Initializes a new instance of the MitsubishiReactiveWritePipeline class.</summary>
    /// <param name="scheduler">The scheduler parameter.</param>
    /// <param name="target">The target parameter.</param>
    /// <param name="mode">The mode parameter.</param>
    /// <param name="writer">The writer parameter.</param>
    /// <param name="coalescingWindow">The coalescingWindow parameter.</param>
    internal MitsubishiReactiveWritePipeline(IScheduler scheduler, string target, MitsubishiReactiveWriteMode mode, Func<TPayload, Task<Responce>> writer, TimeSpan? coalescingWindow)
    {
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _target = target ?? throw new ArgumentNullException(nameof(target));
        Mode = mode;
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _coalescingWindow = coalescingWindow ?? TimeSpan.FromMilliseconds(50);
    }

    /// <summary>Gets or sets the Mode property.</summary>
    public MitsubishiReactiveWriteMode Mode { get; }

    /// <summary>Gets or sets the Results property.</summary>
    public IObservable<MitsubishiReactiveWriteResult> Results => _results.AsObservable();

    /// <summary>Executes the Post operation.</summary>
    /// <param name="payload">The payload parameter.</param>
    public void Post(TPayload payload)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        switch (Mode)
        {
            case MitsubishiReactiveWriteMode.Queued:
            {
                lock (_gate)
                {
                    _queuedWrites.Enqueue(payload);
                    _scheduledDrain ??= ScheduleImmediate(DrainQueued);
                }

                break;
            }

            case MitsubishiReactiveWriteMode.LatestWins:
            {
                lock (_gate)
                {
                    _pendingLatest = payload;
                    _hasPendingLatest = true;
                    _scheduledDrain ??= ScheduleImmediate(DrainLatestWins);
                }

                break;
            }

            case MitsubishiReactiveWriteMode.Coalescing:
            {
                lock (_gate)
                {
                    _pendingLatest = payload;
                    _hasPendingLatest = true;
                    _coalescingTimer?.Dispose();
                    _coalescingTimer = Observable.Timer(_coalescingWindow, _scheduler).Subscribe(_ => FlushCoalesced());
                }

                break;
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(Mode));
        }
    }

    /// <summary>Executes the Dispose operation.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        lock (_gate)
        {
            _scheduledDrain?.Dispose();
            _scheduledDrain = null;
            _coalescingTimer?.Dispose();
            _coalescingTimer = null;
            _queuedWrites.Clear();
            _pendingLatest = default;
            _hasPendingLatest = false;
        }

        _results.OnCompleted();
        _results.Dispose();
    }

    /// <summary>Executes the ScheduleImmediate operation.</summary>
    /// <param name="action">The action parameter.</param>
    /// <returns>The ScheduleImmediate operation result.</returns>
    private IDisposable ScheduleImmediate(Action action) => Observable.Return(Unit.Default, _scheduler).Subscribe(_ => action());

    /// <summary>Executes the DrainQueued operation.</summary>
    private void DrainQueued()
    {
        while (true)
        {
            TPayload payload;
            lock (_gate)
            {
                if (_queuedWrites.Count == 0)
                {
                    _scheduledDrain?.Dispose();
                    _scheduledDrain = null;
                    return;
                }

                payload = _queuedWrites.Dequeue();
            }

            PublishResult(WriteSynchronously(payload));
        }
    }

    /// <summary>Executes the DrainLatestWins operation.</summary>
    private void DrainLatestWins()
    {
        TPayload payload;
        lock (_gate)
        {
            if (!_hasPendingLatest)
            {
                _scheduledDrain?.Dispose();
                _scheduledDrain = null;
                return;
            }

            payload = _pendingLatest!;
            _pendingLatest = default;
            _hasPendingLatest = false;
            _scheduledDrain?.Dispose();
            _scheduledDrain = null;
        }

        PublishResult(WriteSynchronously(payload));
        lock (_gate)
        {
            if (_hasPendingLatest && _scheduledDrain is null)
            {
                _scheduledDrain = ScheduleImmediate(DrainLatestWins);
            }
        }
    }

    /// <summary>Executes the FlushCoalesced operation.</summary>
    private void FlushCoalesced()
    {
        TPayload payload;
        lock (_gate)
        {
            _coalescingTimer?.Dispose();
            _coalescingTimer = null;
            if (!_hasPendingLatest)
            {
                return;
            }

            payload = _pendingLatest!;
            _pendingLatest = default;
            _hasPendingLatest = false;
        }

        PublishResult(WriteSynchronously(payload));
    }

    /// <summary>Executes the WriteSynchronously operation.</summary>
    /// <param name="payload">The payload parameter.</param>
    /// <returns>The WriteSynchronously operation result.</returns>
    private MitsubishiReactiveWriteResult WriteSynchronously(TPayload payload)
    {
        try
        {
            var response = _writer(payload).GetAwaiter().GetResult();
            return new MitsubishiReactiveWriteResult(_target, _scheduler.Now, Mode, response.IsSucceed, response.Err, response.ErrCode, response.Exception);
        }
        catch (Exception ex)
        {
            return new MitsubishiReactiveWriteResult(_target, _scheduler.Now, Mode, false, ex.Message, Exception: ex);
        }
    }

    /// <summary>Executes the PublishResult operation.</summary>
    /// <param name="result">The result parameter.</param>
    private void PublishResult(MitsubishiReactiveWriteResult result)
    {
        if (_disposed)
        {
            return;
        }

        _results.OnNext(result);
    }
}
