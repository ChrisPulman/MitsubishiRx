// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Provides the MitsubishiRx type.</summary>
public sealed partial class MitsubishiRx
{
    /// <summary>Stores the reactiveStreamsGate field.</summary>
    private readonly object _reactiveStreamsGate = new();

    /// <summary>Stores the reactiveStreams field.</summary>
    private readonly Dictionary<string, object> _reactiveStreams = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Executes the ObserveReactiveWords operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="points">The points parameter.</param>
    /// <param name="pollInterval">The pollInterval parameter.</param>
    /// <param name="minimumUpdateSpacing">The minimumUpdateSpacing parameter.</param>
    /// <returns>The ObserveReactiveWords operation result.</returns>
    public IObservable<MitsubishiReactiveValue<ushort[]>> ObserveReactiveWords(string address, int points, TimeSpan pollInterval, TimeSpan? minimumUpdateSpacing = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(points);
        var spacing = minimumUpdateSpacing ?? TimeSpan.Zero;
        var key = $"words|{address}|{points}|{pollInterval.Ticks}|{spacing.Ticks}";
        return GetOrCreateSharedReactiveStream(key, emitInitial => ApplyReactiveSpacing(BuildPollingTrigger(pollInterval, emitInitial).SelectAsyncSequential(async _ => MitsubishiReactiveValue.FromResponse(await ReadWordsAsync(address, points, CancellationToken.None).ConfigureAwait(false), _scheduler.Now, $"Read words {address}")), spacing));
    }

    /// <summary>Executes the ObserveReactiveTag operation.</summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="pollInterval">The pollInterval parameter.</param>
    /// <param name="minimumUpdateSpacing">The minimumUpdateSpacing parameter.</param>
    /// <returns>The ObserveReactiveTag operation result.</returns>
    public IObservable<MitsubishiReactiveValue<T>> ObserveReactiveTag<T>(string tagName, TimeSpan pollInterval, TimeSpan? minimumUpdateSpacing = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);
        var tag = GetRequiredTag(tagName);
        var spacing = minimumUpdateSpacing ?? TimeSpan.Zero;
        if (string.Equals(tag.DataType, "Bit", StringComparison.OrdinalIgnoreCase))
        {
            return ObserveReactiveBits(tag.Address, 1, pollInterval, spacing, emitInitial: true).Select(value => MapReactiveValue(value, $"Tag:{tagName}", bits => CastProjectedValue<T>(tagName, bits[0])));
        }

        var wordCount = GetReactiveWordCount(tag);
        return ObserveReactiveWords(tag.Address, wordCount, pollInterval, spacing).Select(value => MapReactiveValue(value, $"Tag:{tagName}", words => CastProjectedValue<T>(tagName, ConvertTagWordsToObject(tag, words))));
    }

    /// <summary>Executes the ObserveReactiveTagGroup operation.</summary>
    /// <param name="groupName">The groupName parameter.</param>
    /// <param name="pollInterval">The pollInterval parameter.</param>
    /// <param name="minimumUpdateSpacing">The minimumUpdateSpacing parameter.</param>
    /// <returns>The ObserveReactiveTagGroup operation result.</returns>
    public IObservable<MitsubishiReactiveValue<MitsubishiTagGroupSnapshot>> ObserveReactiveTagGroup(string groupName, TimeSpan pollInterval, TimeSpan? minimumUpdateSpacing = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupName);
        var spacing = minimumUpdateSpacing ?? TimeSpan.Zero;
        var key = $"group|{groupName}|{pollInterval.Ticks}|{spacing.Ticks}";
        return TryCreateContiguousWordGroupPlan(groupName, out var plan) ? GetOrCreateSharedReactiveStream(key, emitInitial => ApplyReactiveSpacing(
                BuildPollingTrigger(pollInterval, emitInitial).SelectAsyncSequential(async _ =>
            {
                var raw = await ExecuteObservableAsync(() => EncodeWordReadRequest(plan.StartAddress, plan.TotalWords), GetOneEExpectedLength(2 + (plan.TotalWords * 2)), $"Reactive scan {groupName}", CancellationToken.None).ConfigureAwait(false);
                var words = ParseWords(raw, GetSerialExpectedWordCount(plan.TotalWords));
                if (!words.IsSucceed || words.Value is null)
                {
                    return MitsubishiReactiveValue.FromResponse(new Responce<MitsubishiTagGroupSnapshot>(words), _scheduler.Now, $"Group:{groupName}");
                }

                try
                {
                    var snapshot = BuildContiguousWordGroupSnapshot(plan, words.Value);
                    return MitsubishiReactiveValue.FromResponse(new Responce<MitsubishiTagGroupSnapshot>(words, snapshot), _scheduler.Now, $"Group:{groupName}");
                }
                catch (Exception ex)
                {
                    return new MitsubishiReactiveValue<MitsubishiTagGroupSnapshot>(null, _scheduler.Now, MitsubishiReactiveQuality.Error, Source: $"Group:{groupName}", Error: ex.Message, Exception: ex);
                }
            }),
                spacing)) : GetOrCreateSharedReactiveStream(key, emitInitial => ApplyReactiveSpacing(BuildPollingTrigger(pollInterval, emitInitial).SelectAsyncSequential(async _ => MitsubishiReactiveValue.FromResponse(await ReadTagGroupSnapshotAsync(groupName, CancellationToken.None).ConfigureAwait(false), _scheduler.Now, $"Group:{groupName}")), spacing));
    }

    /// <summary>Executes the BuildContiguousWordGroupSnapshot operation.</summary>
    /// <param name="plan">The plan parameter.</param>
    /// <param name="words">The words parameter.</param>
    /// <returns>The BuildContiguousWordGroupSnapshot operation result.</returns>
    private MitsubishiTagGroupSnapshot BuildContiguousWordGroupSnapshot(ReactiveWordGroupPlan plan, ushort[] words)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in plan.Items)
        {
            if (words.Length < item.WordOffset + item.WordCount)
            {
                throw new InvalidOperationException($"Reactive scan for group '{plan.GroupName}' did not return enough words for tag '{item.TagName}'.");
            }

            var slice = words.Skip(item.WordOffset).Take(item.WordCount).ToArray();
            values[item.TagName] = ConvertTagWordsToObject(item.Tag, slice);
        }

        return new MitsubishiTagGroupSnapshot(plan.GroupName, values);
    }

    /// <summary>Executes the ConvertTagWordsToObject operation.</summary>
    /// <param name="tag">The tag parameter.</param>
    /// <param name="words">The words parameter.</param>
    /// <returns>The ConvertTagWordsToObject operation result.</returns>
    private object? ConvertTagWordsToObject(MitsubishiTagDefinition tag, ushort[] words) => tag.DataType switch
    {
        "String" => DecodeStringFromWords(words, tag),
        "Float" => ConvertToFloat(words, tag),
        "DWord" or "UInt32" => ConvertToUInt32(words, tag),
        "Int32" => ConvertToInt32(words, tag),
        "Int16" => unchecked((short)words[0]),
        "UInt16" => words[0],
        _ when HasEngineeringMetadata(tag) => ApplyScaleAndOffset(ReadNumericTagValue(tag, words), tag),
        null or "Word" => tag.Signed ? unchecked((short)words[0]) : words[0],
        _ => words[0],
    };

    /// <summary>Executes the GetReactiveWordCount operation.</summary>
    /// <param name="tag">The tag parameter.</param>
    /// <returns>The GetReactiveWordCount operation result.</returns>
    private int GetReactiveWordCount(MitsubishiTagDefinition tag) => tag.DataType switch
    {
        "String" => tag.Length ?? throw new InvalidOperationException($"Tag '{tag.Name}' must define Length before reactive string observation can be used."),
        "Float" or "DWord" or "UInt32" or "Int32" => 2,
        "Bit" => 1,
        _ when HasEngineeringMetadata(tag) => GetWordCountForScaledRead(tag),
        _ => 1,
    };

    /// <summary>Executes the CastProjectedValue operation.</summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <returns>The CastProjectedValue operation result.</returns>
    private T CastProjectedValue<T>(string tagName, object? value)
    {
        if (value is T typed)
        {
            return typed;
        }

        throw new InvalidCastException($"Reactive tag '{tagName}' produced '{value?.GetType().Name ?? "null"}' which is not assignable to '{nameof(T)}'.");
    }

    /// <summary>Executes the ObserveReactiveBits operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="points">The points parameter.</param>
    /// <param name="pollInterval">The pollInterval parameter.</param>
    /// <param name="minimumUpdateSpacing">The minimumUpdateSpacing parameter.</param>
    /// <param name="emitInitial">The emitInitial parameter.</param>
    /// <returns>The ObserveReactiveBits operation result.</returns>
    private IObservable<MitsubishiReactiveValue<bool[]>> ObserveReactiveBits(string address, int points, TimeSpan pollInterval, TimeSpan minimumUpdateSpacing, bool emitInitial)
    {
        var key = $"bits|{address}|{points}|{pollInterval.Ticks}|{minimumUpdateSpacing.Ticks}";
        return GetOrCreateSharedReactiveStream(key, initial => ApplyReactiveSpacing(BuildPollingTrigger(pollInterval, initial).SelectAsyncSequential(async _ => MitsubishiReactiveValue.FromResponse(await ReadBitsAsync(address, points, CancellationToken.None).ConfigureAwait(false), _scheduler.Now, $"Read bits {address}")), minimumUpdateSpacing));
    }

    /// <summary>Executes the GetOrCreateSharedReactiveStream operation.</summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="key">The key parameter.</param>
    /// <param name="streamFactory">The streamFactory parameter.</param>
    /// <returns>The GetOrCreateSharedReactiveStream operation result.</returns>
    private IObservable<MitsubishiReactiveValue<T>> GetOrCreateSharedReactiveStream<T>(string key, Func<bool, IObservable<MitsubishiReactiveValue<T>>> streamFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(streamFactory);
        lock (_reactiveStreamsGate)
        {
            if (_reactiveStreams.TryGetValue(key, out var existing))
            {
                return ((SharedReactiveStream<T>)existing).Stream;
            }

            var created = new SharedReactiveStream<T>(streamFactory);
            _reactiveStreams[key] = created;
            return created.Stream;
        }
    }

    /// <summary>Executes the DisposeReactiveStreams operation.</summary>
    private void DisposeReactiveStreams()
    {
        lock (_reactiveStreamsGate)
        {
            foreach (var stream in _reactiveStreams.Values.OfType<IDisposable>())
            {
                stream.Dispose();
            }

            _reactiveStreams.Clear();
        }
    }

    /// <summary>Executes the TryCreateContiguousWordGroupPlan operation.</summary>
    /// <param name="groupName">The groupName parameter.</param>
    /// <param name="plan">The plan parameter.</param>
    /// <returns>The TryCreateContiguousWordGroupPlan operation result.</returns>
    private bool TryCreateContiguousWordGroupPlan(string groupName, out ReactiveWordGroupPlan plan)
    {
        var database = GetRequiredTagDatabase();
        var group = database.GetRequiredGroup(groupName);
        var sortable = new List<(string TagName, MitsubishiTagDefinition Tag, MitsubishiDeviceAddress Address, int WordCount)>();
        foreach (var tagName in group.ResolvedTagNames)
        {
            var tag = database.GetRequired(tagName);
            if (string.Equals(tag.DataType, "Bit", StringComparison.OrdinalIgnoreCase))
            {
                plan = default!;
                return false;
            }

            var parsed = MitsubishiDeviceAddress.Parse(tag.Address, Options.XyNotation);
            if (parsed.Descriptor.Kind != DeviceValueKind.Word)
            {
                plan = default!;
                return false;
            }

            sortable.Add((tagName, tag, parsed, GetReactiveWordCount(tag)));
        }

        if (sortable.Count == 0)
        {
            plan = default!;
            return false;
        }

        var ordered = sortable.OrderBy(static item => item.Address.Descriptor.Symbol, StringComparer.OrdinalIgnoreCase).ThenBy(static item => item.Address.Number).ToArray();
        var first = ordered[0];
        if (ordered.Any(item => !string.Equals(item.Address.Descriptor.Symbol, first.Address.Descriptor.Symbol, StringComparison.OrdinalIgnoreCase) || item.Address.Descriptor.BinaryCode != first.Address.Descriptor.BinaryCode))
        {
            plan = default!;
            return false;
        }

        var expectedNumber = first.Address.Number;
        var offsets = new Dictionary<string, ReactiveWordGroupItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in ordered)
        {
            if (item.Address.Number != expectedNumber)
            {
                plan = default!;
                return false;
            }

            offsets[item.TagName] = new(item.TagName, item.Tag, item.Address.Number - first.Address.Number, item.WordCount);
            expectedNumber += item.WordCount;
        }

        var items = group.ResolvedTagNames.Select(tagName => offsets[tagName]).ToArray();
        plan = new(group.Name, first.Address, expectedNumber - first.Address.Number, items);
        return true;
    }

    /// <summary>Executes the MapReactiveValue operation.</summary>
    /// <typeparam name="TInput">The TInput type parameter.</typeparam>
    /// <typeparam name="TOutput">The TOutput type parameter.</typeparam>
    /// <param name="value">The value parameter.</param>
    /// <param name="source">The source parameter.</param>
    /// <param name="projector">The projector parameter.</param>
    /// <returns>The MapReactiveValue operation result.</returns>
    private MitsubishiReactiveValue<TOutput> MapReactiveValue<TInput, TOutput>(MitsubishiReactiveValue<TInput> value, string source, Func<TInput, TOutput> projector)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(projector);
        if (value.Quality == MitsubishiReactiveQuality.Error)
        {
            return new MitsubishiReactiveValue<TOutput>(default, value.TimestampUtc, value.Quality, value.IsHeartbeat, value.IsStale, source, value.Error, value.ErrorCode, value.Exception);
        }

        if (value.Value is null)
        {
            return new MitsubishiReactiveValue<TOutput>(default, value.TimestampUtc, MitsubishiReactiveQuality.Error, Source: source, Error: $"Reactive source '{source}' produced a null payload.");
        }

        try
        {
            return new MitsubishiReactiveValue<TOutput>(projector(value.Value), value.TimestampUtc, value.Quality, value.IsHeartbeat, value.IsStale, source, value.Error, value.ErrorCode, value.Exception);
        }
        catch (Exception ex)
        {
            return new MitsubishiReactiveValue<TOutput>(default, value.TimestampUtc, MitsubishiReactiveQuality.Error, Source: source, Error: ex.Message, Exception: ex);
        }
    }

    /// <summary>Executes the ApplyReactiveSpacing operation.</summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="source">The source parameter.</param>
    /// <param name="spacing">The spacing parameter.</param>
    /// <returns>The ApplyReactiveSpacing operation result.</returns>
    private IObservable<MitsubishiReactiveValue<T>> ApplyReactiveSpacing<T>(IObservable<MitsubishiReactiveValue<T>> source, TimeSpan spacing)
    {
        ArgumentNullException.ThrowIfNull(source);
        return spacing > TimeSpan.Zero ? source.Conflate(spacing, _scheduler) : source;
    }

    /// <summary>Provides the SharedReactiveStream type.</summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    private sealed class SharedReactiveStream<T> : IDisposable
    {
        /// <summary>Stores the gate field.</summary>
        private readonly object _gate = new();

        /// <summary>Stores the subject field.</summary>
        private readonly ReplaySignal<MitsubishiReactiveValue<T>> _subject = new(1);

        /// <summary>Stores the streamFactory field.</summary>
        private readonly Func<bool, IObservable<MitsubishiReactiveValue<T>>> _streamFactory;

        /// <summary>Stores the connection field.</summary>
        private IDisposable? _connection;

        /// <summary>Stores the subscriberCount field.</summary>
        private int _subscriberCount;

        /// <summary>Stores the hasCachedValue field.</summary>
        private bool _hasCachedValue;

        /// <summary>Stores the disposed field.</summary>
        private bool _disposed;

        /// <summary>Initializes a new instance of the SharedReactiveStream class.</summary>
        /// <param name="streamFactory">The streamFactory parameter.</param>
        public SharedReactiveStream(Func<bool, IObservable<MitsubishiReactiveValue<T>>> streamFactory)
        {
            _streamFactory = streamFactory;
            Stream = Observable.Create<MitsubishiReactiveValue<T>>(observer =>
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                var subscription = _subject.Subscribe(observer);
                StartIfNeeded();
                return Disposable.Create(() =>
                {
                    subscription.Dispose();
                    StopIfUnused();
                });
            });
        }

        /// <summary>Gets or sets the Stream property.</summary>
        public IObservable<MitsubishiReactiveValue<T>> Stream { get; }

        /// <summary>Executes the Dispose operation.</summary>
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _connection?.Dispose();
                _connection = null;
                _subscriberCount = 0;
            }

            _subject.OnCompleted();
            _subject.Dispose();
        }

        /// <summary>Executes the StartIfNeeded operation.</summary>
        private void StartIfNeeded()
        {
            bool emitInitial = false;
            bool shouldStart = false;
            lock (_gate)
            {
                _subscriberCount++;
                if (_connection is null)
                {
                    emitInitial = !_hasCachedValue;
                    shouldStart = true;
                }
            }

            if (!shouldStart)
            {
                return;
            }

            var connection = _streamFactory(emitInitial).Do(_ =>
            {
                lock (_gate)
                {
                    _hasCachedValue = true;
                }
            }).Subscribe(_subject);
            lock (_gate)
            {
                if (_disposed || _connection is not null)
                {
                    connection.Dispose();
                    return;
                }

                _connection = connection;
            }
        }

        /// <summary>Executes the StopIfUnused operation.</summary>
        private void StopIfUnused()
        {
            lock (_gate)
            {
                _subscriberCount--;
                if (_subscriberCount <= 0)
                {
                    _subscriberCount = 0;
                    _connection?.Dispose();
                    _connection = null;
                }
            }
        }
    }

    /// <summary>Provides the ReactiveWordGroupPlan record.</summary>
    /// <param name="GroupName">The GroupName parameter.</param>
    /// <param name="StartAddress">The StartAddress parameter.</param>
    /// <param name="TotalWords">The TotalWords parameter.</param>
    /// <param name="Items">The Items parameter.</param>
    private sealed record ReactiveWordGroupPlan(string GroupName, MitsubishiDeviceAddress StartAddress, int TotalWords, IReadOnlyList<ReactiveWordGroupItem> Items);

    /// <summary>Provides the ReactiveWordGroupItem record.</summary>
    /// <param name="TagName">The TagName parameter.</param>
    /// <param name="Tag">The Tag parameter.</param>
    /// <param name="WordOffset">The WordOffset parameter.</param>
    /// <param name="WordCount">The WordCount parameter.</param>
    private sealed record ReactiveWordGroupItem(string TagName, MitsubishiTagDefinition Tag, int WordOffset, int WordCount);
}
