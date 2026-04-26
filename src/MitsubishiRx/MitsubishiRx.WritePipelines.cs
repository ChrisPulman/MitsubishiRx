// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace MitsubishiRx;

public sealed partial class MitsubishiRx
{
    /// <summary>
    /// Creates a reactive write pipeline for raw word writes.
    /// </summary>
    /// <param name="address">Target device address.</param>
    /// <param name="mode">Write behavior.</param>
    /// <param name="coalescingWindow">Optional coalescing window.</param>
    /// <returns>Reactive write pipeline.</returns>
    public MitsubishiReactiveWritePipeline<IReadOnlyList<ushort>> CreateReactiveWordWritePipeline(string address, MitsubishiReactiveWriteMode mode, TimeSpan? coalescingWindow = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        return new MitsubishiReactiveWritePipeline<IReadOnlyList<ushort>>(
            _scheduler,
            $"Words:{address}",
            mode,
            payload => WriteWordsAsync(address, payload, CancellationToken.None),
            coalescingWindow);
    }

    /// <summary>
    /// Creates a reactive write pipeline for tag-based typed writes.
    /// </summary>
    /// <typeparam name="T">Payload type.</typeparam>
    /// <param name="tagName">Configured tag name.</param>
    /// <param name="mode">Write behavior.</param>
    /// <param name="coalescingWindow">Optional coalescing window.</param>
    /// <returns>Reactive write pipeline.</returns>
    public MitsubishiReactiveWritePipeline<T> CreateReactiveTagWritePipeline<T>(string tagName, MitsubishiReactiveWriteMode mode, TimeSpan? coalescingWindow = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);
        _ = GetRequiredTag(tagName);
        return new MitsubishiReactiveWritePipeline<T>(
            _scheduler,
            $"Tag:{tagName}",
            mode,
            payload => WriteTagValueAsync(tagName, payload, CancellationToken.None),
            coalescingWindow);
    }
}

/// <summary>
/// Reactive write pipeline with queued, latest-wins, and coalescing modes.
/// </summary>
/// <typeparam name="TPayload">Payload type.</typeparam>
public sealed class MitsubishiReactiveWritePipeline<TPayload> : IDisposable
{
    private readonly object _gate = new();
    private readonly IScheduler _scheduler;
    private readonly string _target;
    private readonly Func<TPayload, Task<Responce>> _writer;
    private readonly TimeSpan _coalescingWindow;
    private readonly Queue<TPayload> _queuedWrites = new();
    private readonly Subject<MitsubishiReactiveWriteResult> _results = new();
    private IDisposable? _scheduledDrain;
    private IDisposable? _coalescingTimer;
    private TPayload? _pendingLatest;
    private bool _hasPendingLatest;
    private bool _disposed;

    internal MitsubishiReactiveWritePipeline(IScheduler scheduler, string target, MitsubishiReactiveWriteMode mode, Func<TPayload, Task<Responce>> writer, TimeSpan? coalescingWindow)
    {
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _target = target ?? throw new ArgumentNullException(nameof(target));
        Mode = mode;
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _coalescingWindow = coalescingWindow ?? TimeSpan.FromMilliseconds(50);
    }

    /// <summary>
    /// Gets the pipeline mode.
    /// </summary>
    public MitsubishiReactiveWriteMode Mode { get; }

    /// <summary>
    /// Gets the reactive stream of write completion results.
    /// </summary>
    public IObservable<MitsubishiReactiveWriteResult> Results => _results.AsObservable();

    /// <summary>
    /// Posts a payload into the pipeline.
    /// </summary>
    /// <param name="payload">Payload to write.</param>
    public void Post(TPayload payload)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        switch (Mode)
        {
            case MitsubishiReactiveWriteMode.Queued:
                lock (_gate)
                {
                    _queuedWrites.Enqueue(payload);
                    if (_scheduledDrain is null)
                    {
                        _scheduledDrain = ScheduleImmediate(DrainQueued);
                    }
                }

                break;
            case MitsubishiReactiveWriteMode.LatestWins:
                lock (_gate)
                {
                    _pendingLatest = payload;
                    _hasPendingLatest = true;
                    if (_scheduledDrain is null)
                    {
                        _scheduledDrain = ScheduleImmediate(DrainLatestWins);
                    }
                }

                break;
            case MitsubishiReactiveWriteMode.Coalescing:
                lock (_gate)
                {
                    _pendingLatest = payload;
                    _hasPendingLatest = true;
                    _coalescingTimer?.Dispose();
                    _coalescingTimer = Observable.Timer(_coalescingWindow, _scheduler).Subscribe(_ => FlushCoalesced());
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(Mode));
        }
    }

    /// <inheritdoc/>
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

    private IDisposable ScheduleImmediate(Action action)
        => Observable.Return(Unit.Default, _scheduler).Subscribe(_ => action());

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

    private void PublishResult(MitsubishiReactiveWriteResult result)
    {
        if (!_disposed)
        {
            _results.OnNext(result);
        }
    }
}