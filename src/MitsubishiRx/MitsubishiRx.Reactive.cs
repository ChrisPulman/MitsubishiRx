// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ReactiveUI.Extensions;

namespace MitsubishiRx;

public sealed partial class MitsubishiRx
{
    private readonly object _reactiveStreamsGate = new();
    private readonly Dictionary<string, object> _reactiveStreams = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Observes a PLC word range through a shared hot scan stream.
    /// </summary>
    /// <param name="address">Device address.</param>
    /// <param name="points">Number of words.</param>
    /// <param name="pollInterval">Polling interval.</param>
    /// <param name="minimumUpdateSpacing">Minimum spacing between notifications.</param>
    /// <returns>Shared hot value stream.</returns>
    public IObservable<MitsubishiReactiveValue<ushort[]>> ObserveReactiveWords(string address, int points, TimeSpan pollInterval, TimeSpan? minimumUpdateSpacing = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(points);
        var spacing = minimumUpdateSpacing ?? TimeSpan.Zero;
        var key = $"words|{address}|{points}|{pollInterval.Ticks}|{spacing.Ticks}";

        return GetOrCreateSharedReactiveStream(key, emitInitial => ApplyReactiveSpacing(BuildPollingTrigger(pollInterval, emitInitial)
            .SelectAsyncSequential(async _ => MitsubishiReactiveValue.FromResponse(
                await ReadWordsAsync(address, points, CancellationToken.None).ConfigureAwait(false),
                _scheduler.Now,
                $"Read words {address}")), spacing));
    }

    /// <summary>
    /// Observes a configured tag through the shared reactive scan layer.
    /// </summary>
    /// <typeparam name="T">Projected value type.</typeparam>
    /// <param name="tagName">Configured tag name.</param>
    /// <param name="pollInterval">Polling interval.</param>
    /// <param name="minimumUpdateSpacing">Minimum spacing between notifications.</param>
    /// <returns>Shared hot typed stream.</returns>
    public IObservable<MitsubishiReactiveValue<T>> ObserveReactiveTag<T>(string tagName, TimeSpan pollInterval, TimeSpan? minimumUpdateSpacing = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);
        var tag = GetRequiredTag(tagName);
        var spacing = minimumUpdateSpacing ?? TimeSpan.Zero;

        if (string.Equals(tag.DataType, "Bit", StringComparison.OrdinalIgnoreCase))
        {
            return ObserveReactiveBits(tag.Address, 1, pollInterval, spacing, emitInitial: true)
                .Select(value => MapReactiveValue(value, $"Tag:{tagName}", bits => CastProjectedValue<T>(tagName, bits[0])));
        }

        var wordCount = GetReactiveWordCount(tag);
        return ObserveReactiveWords(tag.Address, wordCount, pollInterval, spacing)
            .Select(value => MapReactiveValue(value, $"Tag:{tagName}", words => CastProjectedValue<T>(tagName, ConvertTagWordsToObject(tag, words))));
    }

    /// <summary>
    /// Observes a configured tag group through a shared scan plan when possible.
    /// </summary>
    /// <param name="groupName">Configured group name.</param>
    /// <param name="pollInterval">Polling interval.</param>
    /// <param name="minimumUpdateSpacing">Minimum spacing between notifications.</param>
    /// <returns>Shared hot grouped snapshot stream.</returns>
    public IObservable<MitsubishiReactiveValue<MitsubishiTagGroupSnapshot>> ObserveReactiveTagGroup(string groupName, TimeSpan pollInterval, TimeSpan? minimumUpdateSpacing = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupName);
        var spacing = minimumUpdateSpacing ?? TimeSpan.Zero;
        var key = $"group|{groupName}|{pollInterval.Ticks}|{spacing.Ticks}";

        if (TryCreateContiguousWordGroupPlan(groupName, out var plan))
        {
            return GetOrCreateSharedReactiveStream(key, emitInitial => ApplyReactiveSpacing(BuildPollingTrigger(pollInterval, emitInitial)
                .SelectAsyncSequential(async _ =>
                {
                    var raw = await ExecuteObservableAsync(
                        () => Options.TransportKind == MitsubishiTransportKind.Serial
                            ? MitsubishiSerialProtocolEncoding.EncodeWordReadRequest(Options, plan.StartAddress, plan.TotalWords)
                            : MitsubishiProtocolEncoding.EncodeDeviceBatchRead(Options, plan.StartAddress, plan.TotalWords, bitUnits: false),
                        Options.TransportKind == MitsubishiTransportKind.Serial
                            ? null
                            : Options.FrameType == MitsubishiFrameType.OneE ? 2 + (plan.TotalWords * 2) : null,
                        $"Reactive scan {groupName}",
                        CancellationToken.None).ConfigureAwait(false);

                    var words = ParseWords(raw, Options.TransportKind == MitsubishiTransportKind.Serial ? plan.TotalWords : null);
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
                }), spacing));
        }

        return GetOrCreateSharedReactiveStream(key, emitInitial => ApplyReactiveSpacing(BuildPollingTrigger(pollInterval, emitInitial)
            .SelectAsyncSequential(async _ => MitsubishiReactiveValue.FromResponse(
                await ReadTagGroupSnapshotAsync(groupName, CancellationToken.None).ConfigureAwait(false),
                _scheduler.Now,
                $"Group:{groupName}")), spacing));
    }

    private IObservable<MitsubishiReactiveValue<bool[]>> ObserveReactiveBits(string address, int points, TimeSpan pollInterval, TimeSpan minimumUpdateSpacing, bool emitInitial)
    {
        var key = $"bits|{address}|{points}|{pollInterval.Ticks}|{minimumUpdateSpacing.Ticks}";
        return GetOrCreateSharedReactiveStream(key, initial => ApplyReactiveSpacing(BuildPollingTrigger(pollInterval, initial)
            .SelectAsyncSequential(async _ => MitsubishiReactiveValue.FromResponse(
                await ReadBitsAsync(address, points, CancellationToken.None).ConfigureAwait(false),
                _scheduler.Now,
                $"Read bits {address}")), minimumUpdateSpacing));
    }

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

        var ordered = sortable
            .OrderBy(static item => item.Address.Descriptor.Symbol, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static item => item.Address.Number)
            .ToArray();

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

            offsets[item.TagName] = new ReactiveWordGroupItem(item.TagName, item.Tag, item.Address.Number - first.Address.Number, item.WordCount);
            expectedNumber += item.WordCount;
        }

        var items = group.ResolvedTagNames.Select(tagName => offsets[tagName]).ToArray();
        plan = new ReactiveWordGroupPlan(group.Name, first.Address, expectedNumber - first.Address.Number, items);
        return true;
    }

    private static MitsubishiTagGroupSnapshot BuildContiguousWordGroupSnapshot(ReactiveWordGroupPlan plan, ushort[] words)
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

    private static object? ConvertTagWordsToObject(MitsubishiTagDefinition tag, ushort[] words)
        => tag.DataType switch
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

    private static int GetReactiveWordCount(MitsubishiTagDefinition tag)
        => tag.DataType switch
        {
            "String" => tag.Length ?? throw new InvalidOperationException($"Tag '{tag.Name}' must define Length before reactive string observation can be used."),
            "Float" or "DWord" or "UInt32" or "Int32" => 2,
            "Bit" => 1,
            _ when HasEngineeringMetadata(tag) => GetWordCountForScaledRead(tag),
            _ => 1,
        };

    private static T CastProjectedValue<T>(string tagName, object? value)
    {
        if (value is T typed)
        {
            return typed;
        }

        throw new InvalidCastException($"Reactive tag '{tagName}' produced '{value?.GetType().Name ?? "null"}' which is not assignable to '{typeof(T).Name}'.");
    }

    private static MitsubishiReactiveValue<TOutput> MapReactiveValue<TInput, TOutput>(MitsubishiReactiveValue<TInput> value, string source, Func<TInput, TOutput> projector)
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

    private IObservable<MitsubishiReactiveValue<T>> ApplyReactiveSpacing<T>(IObservable<MitsubishiReactiveValue<T>> source, TimeSpan spacing)
    {
        ArgumentNullException.ThrowIfNull(source);
        return spacing > TimeSpan.Zero
            ? source.Conflate(spacing, _scheduler)
            : source;
    }

    private sealed record ReactiveWordGroupPlan(string GroupName, MitsubishiDeviceAddress StartAddress, int TotalWords, IReadOnlyList<ReactiveWordGroupItem> Items);

    private sealed record ReactiveWordGroupItem(string TagName, MitsubishiTagDefinition Tag, int WordOffset, int WordCount);

    private sealed class SharedReactiveStream<T> : IDisposable
    {
        private readonly object _gate = new();
        private readonly ReplaySubject<MitsubishiReactiveValue<T>> _subject = new(1);
        private readonly Func<bool, IObservable<MitsubishiReactiveValue<T>>> _streamFactory;
        private IDisposable? _connection;
        private int _subscriberCount;
        private bool _hasCachedValue;
        private bool _disposed;

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

        public IObservable<MitsubishiReactiveValue<T>> Stream { get; }

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

            var connection = _streamFactory(emitInitial)
                .Do(_ =>
                {
                    lock (_gate)
                    {
                        _hasCachedValue = true;
                    }
                })
                .Subscribe(_subject);

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
}