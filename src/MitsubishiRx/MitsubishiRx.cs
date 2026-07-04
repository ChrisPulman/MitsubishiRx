// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Provides the MitsubishiRx type.</summary>
public sealed partial class MitsubishiRx : IDisposable, IAsyncDisposable
{
    /// <summary>Stores the connectionStates field.</summary>
    private readonly StateSignal<MitsubishiConnectionState> _connectionStates = new(MitsubishiConnectionState.Disconnected);

    /// <summary>Stores the operationLogs field.</summary>
    private readonly Signal<MitsubishiOperationLog> _operationLogs = new();

    /// <summary>Stores the requestGate field.</summary>
    private readonly SemaphoreSlim _requestGate = new(1, 1);

    /// <summary>Stores the transport field.</summary>
    private readonly IMitsubishiTransport _transport;

    /// <summary>Stores the scheduler field.</summary>
    private readonly IScheduler _scheduler;

    /// <summary>Stores the serialOneCMonitorAddresses field.</summary>
    private IReadOnlyList<string>? _serialOneCMonitorAddresses;

    /// <summary>Stores the disposed field.</summary>
    private bool _disposed;

    /// <summary>Initializes a new instance of the MitsubishiRx class.</summary>
    /// <param name="cpuType">The cpuType parameter.</param>
    /// <param name="ip">The ip parameter.</param>
    /// <param name="port">The port parameter.</param>
    /// <param name="timeout">The timeout parameter.</param>
    public MitsubishiRx(CpuType cpuType, string ip, int port, int timeout = 1500)
        : this(new MitsubishiClientOptions(ip, port, cpuType is CpuType.ASeries or CpuType.Fx3 ? MitsubishiFrameType.OneE : MitsubishiFrameType.ThreeE, CommunicationDataCode.Binary, MitsubishiTransportKind.Tcp, Timeout: TimeSpan.FromMilliseconds(timeout), CpuType: cpuType))
    {
    }

    /// <summary>Initializes a new instance of the MitsubishiRx class.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="transport">The transport parameter.</param>
    /// <param name="scheduler">The scheduler parameter.</param>
    public MitsubishiRx(MitsubishiClientOptions options, IMitsubishiTransport? transport = null, IScheduler? scheduler = null)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        _transport = transport ?? CreateDefaultTransport(options);
        _scheduler = scheduler ?? Scheduler.Default;
        if (options.TransportKind == MitsubishiTransportKind.Serial)
        {
            return;
        }

        _ = BuildEndPoint(options);
    }

    /// <summary>Gets or sets the Options property.</summary>
    public MitsubishiClientOptions Options { get; }

    /// <summary>Gets or sets the TagDatabase property.</summary>
    public MitsubishiTagDatabase? TagDatabase { get; set; }

    /// <summary>Gets or sets the Connected property.</summary>
    public bool Connected => _transport.IsConnected;

    /// <summary>Gets or sets the ConnectionStates property.</summary>
    public IObservable<MitsubishiConnectionState> ConnectionStates => _connectionStates.AsObservable().DistinctUntilChanged();

    /// <summary>Gets or sets the OperationLogs property.</summary>
    public IObservable<MitsubishiOperationLog> OperationLogs => _operationLogs.AsObservable();

    /// <summary>Executes the ReadGeneratedBitTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadGeneratedBitTagAsync operation result.</returns>
    public async Task<Responce<bool>> ReadGeneratedBitTagAsync(string tagName, CancellationToken cancellationToken = default)
    {
        var raw = await ReadBitsByTagAsync(tagName, 1, cancellationToken).ConfigureAwait(false);
        return !raw.IsSucceed || raw.Value is null ? new Responce<bool>(raw) : new Responce<bool>(raw, raw.Value[0]);
    }

    /// <summary>Executes the WriteGeneratedBitTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteGeneratedBitTagAsync operation result.</returns>
    public Task<Responce> WriteGeneratedBitTagAsync(string tagName, bool value, CancellationToken cancellationToken = default) => WriteBitsByTagAsync(tagName, [value], cancellationToken);

    /// <summary>Executes the ReadWordsByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="points">The points parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadWordsByTagAsync operation result.</returns>
    public Task<Responce<ushort[]>> ReadWordsByTagAsync(string tagName, int points, CancellationToken cancellationToken = default) => ReadWordsAsync(ResolveTagAddress(tagName), points, cancellationToken);

    /// <summary>Executes the ReadBitsByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="points">The points parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadBitsByTagAsync operation result.</returns>
    public Task<Responce<bool[]>> ReadBitsByTagAsync(string tagName, int points, CancellationToken cancellationToken = default) => ReadBitsAsync(ResolveTagAddress(tagName), points, cancellationToken);

    /// <summary>Executes the WriteWordsByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteWordsByTagAsync operation result.</returns>
    public Task<Responce> WriteWordsByTagAsync(string tagName, IReadOnlyList<ushort> values, CancellationToken cancellationToken = default) => WriteWordsAsync(ResolveTagAddress(tagName), values, cancellationToken);

    /// <summary>Executes the WriteBitsByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteBitsByTagAsync operation result.</returns>
    public Task<Responce> WriteBitsByTagAsync(string tagName, IReadOnlyList<bool> values, CancellationToken cancellationToken = default) => WriteBitsAsync(ResolveTagAddress(tagName), values, cancellationToken);

    /// <summary>Executes the RandomReadWordsByTagAsync operation.</summary>
    /// <param name="tagNames">The tagNames parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The RandomReadWordsByTagAsync operation result.</returns>
    public Task<Responce<ushort[]>> RandomReadWordsByTagAsync(IEnumerable<string> tagNames, CancellationToken cancellationToken = default) => RandomReadWordsAsync(ResolveTagAddresses(tagNames), cancellationToken);

    /// <summary>Executes the RandomWriteWordsByTagAsync operation.</summary>
    /// <param name="values">The values parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The RandomWriteWordsByTagAsync operation result.</returns>
    public Task<Responce> RandomWriteWordsByTagAsync(IEnumerable<KeyValuePair<string, ushort>> values, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        return RandomWriteWordsAsync(values.Select(pair => new KeyValuePair<string, ushort>(ResolveTagAddress(pair.Key), pair.Value)), cancellationToken);
    }

    /// <summary>Executes the ReadInt16ByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadInt16ByTagAsync operation result.</returns>
    public async Task<Responce<short>> ReadInt16ByTagAsync(string tagName, CancellationToken cancellationToken = default)
    {
        var raw = await ReadWordsByTagAsync(tagName, 1, cancellationToken).ConfigureAwait(false);
        return ConvertWords(raw, words => unchecked((short)words[0]));
    }

    /// <summary>Executes the WriteInt16ByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteInt16ByTagAsync operation result.</returns>
    public Task<Responce> WriteInt16ByTagAsync(string tagName, short value, CancellationToken cancellationToken = default) => WriteWordsByTagAsync(tagName, [unchecked((ushort)value)], cancellationToken);

    /// <summary>Executes the ReadUInt16ByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadUInt16ByTagAsync operation result.</returns>
    public async Task<Responce<ushort>> ReadUInt16ByTagAsync(string tagName, CancellationToken cancellationToken = default)
    {
        var raw = await ReadWordsByTagAsync(tagName, 1, cancellationToken).ConfigureAwait(false);
        return ConvertWords(raw, words => words[0]);
    }

    /// <summary>Executes the WriteUInt16ByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteUInt16ByTagAsync operation result.</returns>
    public Task<Responce> WriteUInt16ByTagAsync(string tagName, ushort value, CancellationToken cancellationToken = default) => WriteWordsByTagAsync(tagName, [value], cancellationToken);

    /// <summary>Executes the ReadInt32ByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadInt32ByTagAsync operation result.</returns>
    public async Task<Responce<int>> ReadInt32ByTagAsync(string tagName, CancellationToken cancellationToken = default)
    {
        var tag = GetRequiredTag(tagName);
        var raw = await ReadWordsByTagAsync(tagName, 2, cancellationToken).ConfigureAwait(false);
        return ConvertWords(raw, words => ConvertToInt32(words, tag));
    }

    /// <summary>Executes the WriteInt32ByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteInt32ByTagAsync operation result.</returns>
    public Task<Responce> WriteInt32ByTagAsync(string tagName, int value, CancellationToken cancellationToken = default)
    {
        var tag = GetRequiredTag(tagName);
        return WriteWordsByTagAsync(tagName, ConvertFromInt32(value, tag), cancellationToken);
    }

    /// <summary>Executes the ReadDWordByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadDWordByTagAsync operation result.</returns>
    public async Task<Responce<uint>> ReadDWordByTagAsync(string tagName, CancellationToken cancellationToken = default)
    {
        var tag = GetRequiredTag(tagName);
        var raw = await ReadWordsByTagAsync(tagName, 2, cancellationToken).ConfigureAwait(false);
        return ConvertWords(raw, words => ConvertToUInt32(words, tag));
    }

    /// <summary>Executes the WriteDWordByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteDWordByTagAsync operation result.</returns>
    public Task<Responce> WriteDWordByTagAsync(string tagName, uint value, CancellationToken cancellationToken = default)
    {
        var tag = GetRequiredTag(tagName);
        return WriteWordsByTagAsync(tagName, ConvertFromUInt32(value, tag), cancellationToken);
    }

    /// <summary>Executes the ReadFloatByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadFloatByTagAsync operation result.</returns>
    public async Task<Responce<float>> ReadFloatByTagAsync(string tagName, CancellationToken cancellationToken = default)
    {
        var tag = GetRequiredTag(tagName);
        var raw = await ReadWordsByTagAsync(tagName, 2, cancellationToken).ConfigureAwait(false);
        return ConvertWords(raw, words => ConvertToFloat(words, tag));
    }

    /// <summary>Executes the WriteFloatByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteFloatByTagAsync operation result.</returns>
    public Task<Responce> WriteFloatByTagAsync(string tagName, float value, CancellationToken cancellationToken = default)
    {
        var tag = GetRequiredTag(tagName);
        return WriteWordsByTagAsync(tagName, ConvertFromFloat(value, tag), cancellationToken);
    }

    /// <summary>Executes the ReadScaledDoubleByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadScaledDoubleByTagAsync operation result.</returns>
    public async Task<Responce<double>> ReadScaledDoubleByTagAsync(string tagName, CancellationToken cancellationToken = default)
    {
        var tag = GetRequiredTag(tagName);
        var wordCount = GetWordCountForScaledRead(tag);
        var raw = await ReadWordsByTagAsync(tagName, wordCount, cancellationToken).ConfigureAwait(false);
        return ConvertWords(raw, words => ApplyScaleAndOffset(ReadNumericTagValue(tag, words), tag));
    }

    /// <summary>Executes the WriteScaledDoubleByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteScaledDoubleByTagAsync operation result.</returns>
    public Task<Responce> WriteScaledDoubleByTagAsync(string tagName, double value, CancellationToken cancellationToken = default)
    {
        var tag = GetRequiredTag(tagName);
        var rawValue = RemoveScaleAndOffset(value, tag);
        return tag.DataType switch
        {
            null or "Word" => WriteWordsByTagAsync(tagName, [checked((ushort)Math.Round(rawValue, MidpointRounding.AwayFromZero))], cancellationToken),
            "DWord" => WriteDWordByTagAsync(tagName, checked((uint)Math.Round(rawValue, MidpointRounding.AwayFromZero)), cancellationToken),
            "Float" => WriteFloatByTagAsync(tagName, (float)rawValue, cancellationToken),
            _ => Task.FromResult(new Responce().Fail($"Scaled access is not supported for tag '{tagName}' with DataType '{tag.DataType}'.")),
        };
    }

    /// <summary>Executes the ReadStringByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadStringByTagAsync operation result.</returns>
    public Task<Responce<string>> ReadStringByTagAsync(string tagName, CancellationToken cancellationToken = default)
    {
        var tag = GetRequiredTag(tagName);
        var wordLength = tag.Length ?? throw new InvalidOperationException($"Tag '{tagName}' must define Length before ReadStringByTagAsync(tagName) can be used.");
        return ReadStringByTagAsync(tagName, wordLength, cancellationToken);
    }

    /// <summary>Executes the ReadStringByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="wordLength">The wordLength parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadStringByTagAsync operation result.</returns>
    public async Task<Responce<string>> ReadStringByTagAsync(string tagName, int wordLength, CancellationToken cancellationToken = default)
    {
        if (wordLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(wordLength));
        }

        var tag = GetRequiredTag(tagName);
        var raw = await ReadWordsByTagAsync(tagName, wordLength, cancellationToken).ConfigureAwait(false);
        return ConvertWords(raw, words => DecodeStringFromWords(words, tag));
    }

    /// <summary>Executes the WriteStringByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteStringByTagAsync operation result.</returns>
    public Task<Responce> WriteStringByTagAsync(string tagName, string value, CancellationToken cancellationToken = default)
    {
        var tag = GetRequiredTag(tagName);
        var wordLength = tag.Length ?? throw new InvalidOperationException($"Tag '{tagName}' must define Length before WriteStringByTagAsync(tagName, value) can be used.");
        return WriteStringByTagAsync(tagName, value, wordLength, cancellationToken);
    }

    /// <summary>Executes the WriteStringByTagAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="wordLength">The wordLength parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteStringByTagAsync operation result.</returns>
    public Task<Responce> WriteStringByTagAsync(string tagName, string value, int wordLength, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (wordLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(wordLength));
        }

        var tag = GetRequiredTag(tagName);
        return WriteWordsByTagAsync(tagName, EncodeStringWords(value, wordLength, tag), cancellationToken);
    }

    /// <summary>Executes the ValidateTagDatabase operation.</summary>
    /// <returns>The ValidateTagDatabase operation result.</returns>
    public Responce ValidateTagDatabase() => ValidateTagDatabase(TagDatabase);

    /// <summary>Executes the LoadAndValidateTagDatabase operation.</summary>
    /// <param name="path">The path parameter.</param>
    /// <returns>The LoadAndValidateTagDatabase operation result.</returns>
    public Responce<MitsubishiTagDatabase> LoadAndValidateTagDatabase(string path) => LoadAndValidateTagDatabase(path, MitsubishiTagRolloutPolicy.AllowAll);

    /// <summary>Executes the LoadAndValidateTagDatabase operation.</summary>
    /// <param name="path">The path parameter.</param>
    /// <param name="policy">The policy parameter.</param>
    /// <returns>The LoadAndValidateTagDatabase operation result.</returns>
    public Responce<MitsubishiTagDatabase> LoadAndValidateTagDatabase(string path, MitsubishiTagRolloutPolicy policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            var database = MitsubishiTagDatabase.Load(path);
            var validation = ValidateTagDatabase(database);
            if (!validation.IsSucceed)
            {
                return new Responce<MitsubishiTagDatabase>(validation);
            }

            var diff = (TagDatabase ?? new MitsubishiTagDatabase([])).CompareWith(database);
            var policyResult = ValidateRolloutPolicy(diff, policy);
            if (!policyResult.IsSucceed)
            {
                return new Responce<MitsubishiTagDatabase>(policyResult, database);
            }

            TagDatabase = database;
            return new Responce<MitsubishiTagDatabase>(policyResult, database);
        }
        catch (Exception ex)
        {
            return new Responce<MitsubishiTagDatabase>().Fail(ex.Message, exception: ex);
        }
    }

    /// <summary>Executes the PreviewTagDatabaseDiff operation.</summary>
    /// <param name="path">The path parameter.</param>
    /// <returns>The PreviewTagDatabaseDiff operation result.</returns>
    public Responce<MitsubishiTagDatabaseDiff> PreviewTagDatabaseDiff(string path) => PreviewTagDatabaseDiff(path, MitsubishiTagRolloutPolicy.AllowAll);

    /// <summary>Executes the PreviewTagDatabaseDiff operation.</summary>
    /// <param name="path">The path parameter.</param>
    /// <param name="policy">The policy parameter.</param>
    /// <returns>The PreviewTagDatabaseDiff operation result.</returns>
    public Responce<MitsubishiTagDatabaseDiff> PreviewTagDatabaseDiff(string path, MitsubishiTagRolloutPolicy policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            var database = MitsubishiTagDatabase.Load(path);
            var validation = ValidateTagDatabase(database);
            if (!validation.IsSucceed)
            {
                return new Responce<MitsubishiTagDatabaseDiff>(validation);
            }

            var diff = (TagDatabase ?? new MitsubishiTagDatabase([])).CompareWith(database);
            var policyResult = ValidateRolloutPolicy(diff, policy);
            return new Responce<MitsubishiTagDatabaseDiff>(policyResult, diff);
        }
        catch (Exception ex)
        {
            return new Responce<MitsubishiTagDatabaseDiff>().Fail(ex.Message, exception: ex);
        }
    }

    /// <summary>Executes the ObserveTagDatabaseDiff operation.</summary>
    /// <param name="path">The path parameter.</param>
    /// <param name="pollInterval">The pollInterval parameter.</param>
    /// <param name="emitInitial">The emitInitial parameter.</param>
    /// <returns>The ObserveTagDatabaseDiff operation result.</returns>
    public IObservable<Responce<MitsubishiTagDatabaseDiff>> ObserveTagDatabaseDiff(string path, TimeSpan pollInterval, bool emitInitial = true) => ObserveTagDatabaseDiff(path, pollInterval, emitInitial, MitsubishiTagRolloutPolicy.AllowAll);

    /// <summary>Executes the ObserveTagDatabaseDiff operation.</summary>
    /// <param name="path">The path parameter.</param>
    /// <param name="pollInterval">The pollInterval parameter.</param>
    /// <param name="emitInitial">The emitInitial parameter.</param>
    /// <param name="policy">The policy parameter.</param>
    /// <returns>The ObserveTagDatabaseDiff operation result.</returns>
    public IObservable<Responce<MitsubishiTagDatabaseDiff>> ObserveTagDatabaseDiff(string path, TimeSpan pollInterval, bool emitInitial, MitsubishiTagRolloutPolicy policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (pollInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(pollInterval));
        }

        var ticks = Observable.Interval(pollInterval, _scheduler);
        var trigger = emitInitial ? ticks.StartWith(0L) : ticks;
        string? lastFingerprint = null;
        return trigger.Select(_ => GetSchemaFingerprint(path)).Where(fingerprint => !string.Equals(fingerprint, lastFingerprint, StringComparison.Ordinal)).Do(fingerprint => lastFingerprint = fingerprint).Select(_ =>
        {
            var preview = PreviewTagDatabaseDiff(path, policy);
            if (!preview.IsSucceed)
            {
                return preview;
            }

            var load = LoadAndValidateTagDatabase(path, policy);
            return load.IsSucceed && preview.Value is not null ? new Responce<MitsubishiTagDatabaseDiff>(load, preview.Value) : new Responce<MitsubishiTagDatabaseDiff>(preview);
        }).DoOnSubscribe(() => PublishOperation($"Observe tag database diff {path} subscribed", true, Array.Empty<byte>(), Array.Empty<byte>())).DoOnDispose(() => PublishOperation($"Observe tag database diff {path} disposed", true, Array.Empty<byte>(), Array.Empty<byte>()));
    }

    /// <summary>Executes the ObserveTagDatabaseReload operation.</summary>
    /// <param name="path">The path parameter.</param>
    /// <param name="pollInterval">The pollInterval parameter.</param>
    /// <param name="emitInitial">The emitInitial parameter.</param>
    /// <returns>The ObserveTagDatabaseReload operation result.</returns>
    public IObservable<Responce<MitsubishiTagDatabase>> ObserveTagDatabaseReload(string path, TimeSpan pollInterval, bool emitInitial = true) => ObserveTagDatabaseReload(path, pollInterval, emitInitial, MitsubishiTagRolloutPolicy.AllowAll);

    /// <summary>Executes the ObserveTagDatabaseReload operation.</summary>
    /// <param name="path">The path parameter.</param>
    /// <param name="pollInterval">The pollInterval parameter.</param>
    /// <param name="emitInitial">The emitInitial parameter.</param>
    /// <param name="policy">The policy parameter.</param>
    /// <returns>The ObserveTagDatabaseReload operation result.</returns>
    public IObservable<Responce<MitsubishiTagDatabase>> ObserveTagDatabaseReload(string path, TimeSpan pollInterval, bool emitInitial, MitsubishiTagRolloutPolicy policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (pollInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(pollInterval));
        }

        var ticks = Observable.Interval(pollInterval, _scheduler);
        var trigger = emitInitial ? ticks.StartWith(0L) : ticks;
        string? lastFingerprint = null;
        return trigger.Select(_ => GetSchemaFingerprint(path)).Where(fingerprint => !string.Equals(fingerprint, lastFingerprint, StringComparison.Ordinal)).Do(fingerprint => lastFingerprint = fingerprint).Select(_ => LoadAndValidateTagDatabase(path, policy)).DoOnSubscribe(() => PublishOperation($"Observe tag database reload {path} subscribed", true, Array.Empty<byte>(), Array.Empty<byte>())).DoOnDispose(() => PublishOperation($"Observe tag database reload {path} disposed", true, Array.Empty<byte>(), Array.Empty<byte>()));
    }

    /// <summary>Executes the ReadTagGroupSnapshotAsync operation.</summary>
    /// <param name="groupName">The groupName parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadTagGroupSnapshotAsync operation result.</returns>
    public async Task<Responce<MitsubishiTagGroupSnapshot>> ReadTagGroupSnapshotAsync(string groupName, CancellationToken cancellationToken = default)
    {
        var database = TagDatabase ?? throw new InvalidOperationException("TagDatabase must be assigned before tag-based APIs can be used.");
        var group = database.GetRequiredGroup(groupName);
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var tagName in group.ResolvedTagNames)
        {
            var valueResult = await ReadTagValueAsync(tagName, cancellationToken).ConfigureAwait(false);
            if (!valueResult.IsSucceed)
            {
                return new Responce<MitsubishiTagGroupSnapshot>(valueResult);
            }

            values[tagName] = valueResult.Value;
        }

        return new Responce<MitsubishiTagGroupSnapshot>(new MitsubishiTagGroupSnapshot(group.Name, values));
    }

    /// <summary>Executes the ValidateTagGroupWrite operation.</summary>
    /// <param name="groupName">The groupName parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <returns>The ValidateTagGroupWrite operation result.</returns>
    public Responce ValidateTagGroupWrite(string groupName, IReadOnlyDictionary<string, object?> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupName);
        ArgumentNullException.ThrowIfNull(values);
        var result = new Responce();
        var database = TagDatabase;
        if (database is null)
        {
            return result.Fail("TagDatabase must be assigned before grouped writes can be validated.");
        }

        var group = database.GetRequiredGroup(groupName);
        var allowed = new HashSet<string>(group.ResolvedTagNames, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in values)
        {
            if (!allowed.Contains(pair.Key))
            {
                result.IsSucceed = false;
                result.ErrList.Add($"Group '{groupName}' does not contain tag '{pair.Key}'.");
                continue;
            }

            var tag = database.GetRequired(pair.Key);
            if (!CanWriteTagValue(tag, pair.Value, out var error))
            {
                result.IsSucceed = false;
                result.ErrList.Add(error!);
            }
        }

        if (!result.IsSucceed && result.ErrList.Count > 0)
        {
            result.Err = string.Join(Environment.NewLine, result.ErrList);
        }

        return result.EndTime();
    }

    /// <summary>Executes the WriteTagGroupValuesAsync operation.</summary>
    /// <param name="groupName">The groupName parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteTagGroupValuesAsync operation result.</returns>
    public async Task<Responce> WriteTagGroupValuesAsync(string groupName, IReadOnlyDictionary<string, object?> values, CancellationToken cancellationToken = default)
    {
        var validation = ValidateTagGroupWrite(groupName, values);
        if (!validation.IsSucceed)
        {
            return validation;
        }

        var group = GetRequiredTagDatabase().GetRequiredGroup(groupName);
        foreach (var tagName in group.ResolvedTagNames)
        {
            if (!values.TryGetValue(tagName, out var value))
            {
                continue;
            }

            var write = await WriteTagValueAsync(tagName, value, cancellationToken).ConfigureAwait(false);
            if (!write.IsSucceed)
            {
                return write;
            }
        }

        return new Responce().EndTime();
    }

    /// <summary>Executes the WriteTagGroupSnapshotAsync operation.</summary>
    /// <param name="snapshot">The snapshot parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteTagGroupSnapshotAsync operation result.</returns>
    public Task<Responce> WriteTagGroupSnapshotAsync(MitsubishiTagGroupSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return WriteTagGroupValuesAsync(snapshot.GroupName, new Dictionary<string, object?>(snapshot.Values, StringComparer.OrdinalIgnoreCase), cancellationToken);
    }

    /// <summary>Executes the Open operation.</summary>
    /// <returns>The Open operation result.</returns>
    public Responce Open() => OpenAsync().GetAwaiter().GetResult();

    /// <summary>Executes the OpenAsync operation.</summary>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The OpenAsync operation result.</returns>
    public async Task<Responce> OpenAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var result = new Responce();
        try
        {
            await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
            return result.EndTime();
        }
        catch (Exception ex)
        {
            PublishFault("Open transport", Array.Empty<byte>(), Array.Empty<byte>(), ex);
            return result.Fail(ex.Message, exception: ex);
        }
    }

    /// <summary>Executes the Close operation.</summary>
    /// <returns>The Close operation result.</returns>
    public Responce Close() => CloseAsync().GetAwaiter().GetResult();

    /// <summary>Executes the CloseAsync operation.</summary>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The CloseAsync operation result.</returns>
    public async Task<Responce> CloseAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var result = new Responce();
        try
        {
            await _transport.DisconnectAsync(cancellationToken).ConfigureAwait(false);
            PublishState(MitsubishiConnectionState.Disconnected);
            PublishOperation("Close transport", true, Array.Empty<byte>(), Array.Empty<byte>());
            return result.EndTime();
        }
        catch (Exception ex)
        {
            PublishFault("Close transport", Array.Empty<byte>(), Array.Empty<byte>(), ex);
            return result.Fail(ex.Message, exception: ex);
        }
    }

    /// <summary>Executes the SendPackage operation.</summary>
    /// <param name="command">The command parameter.</param>
    /// <param name="receiveCount">The receiveCount parameter.</param>
    /// <returns>The SendPackage operation result.</returns>
    public Responce<byte[]> SendPackage(byte[] command, int receiveCount) => ExecuteEncodedAsync(command, receiveCount, "Legacy raw package").GetAwaiter().GetResult();

    /// <summary>Executes the SendPackageSingle operation.</summary>
    /// <param name="command">The command parameter.</param>
    /// <returns>The SendPackageSingle operation result.</returns>
    public Responce<byte[]> SendPackageSingle(byte[] command) => ExecuteEncodedAsync(command, null, "Legacy raw package single").GetAwaiter().GetResult();

    /// <summary>Executes the SendPackageReliable operation.</summary>
    /// <param name="command">The command parameter.</param>
    /// <returns>The SendPackageReliable operation result.</returns>
    public Responce<byte[]> SendPackageReliable(byte[] command) => ExecuteEncodedAsync(command, null, "Legacy raw package reliable", maxRetries: 2).GetAwaiter().GetResult();

    /// <summary>Executes the ExecuteRawAsync operation.</summary>
    /// <param name="request">The request parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ExecuteRawAsync operation result.</returns>
    public Task<Responce<byte[]>> ExecuteRawAsync(MitsubishiRawCommandRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteObservableAsync(() => EncodeRawRequest(request), GetFixedResponseLength(request), request.Description ?? $"Command {request.Command:X4}", cancellationToken);
    }

    /// <summary>Executes the ReadWordsAsync operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="points">The points parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadWordsAsync operation result.</returns>
    public async Task<Responce<ushort[]>> ReadWordsAsync(string address, int points, CancellationToken cancellationToken = default)
    {
        var parsed = MitsubishiDeviceAddress.Parse(address, Options.XyNotation);
        var raw = await ExecuteObservableAsync(() => EncodeWordReadRequest(parsed, points), GetOneEExpectedLength(2 + (points * 2)), $"Read words {address}", cancellationToken).ConfigureAwait(false);
        return ParseWords(raw, GetSerialExpectedWordCount(points));
    }

    /// <summary>Executes the WriteWordsAsync operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteWordsAsync operation result.</returns>
    public async Task<Responce> WriteWordsAsync(string address, IReadOnlyList<ushort> values, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        var parsed = MitsubishiDeviceAddress.Parse(address, Options.XyNotation);
        var raw = await ExecuteObservableAsync(() => EncodeWordWriteRequest(parsed, values), GetOneEExpectedLength(2), $"Write words {address}", cancellationToken).ConfigureAwait(false);
        return raw.ToBaseResponse();
    }

    /// <summary>Executes the ReadBitsAsync operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="points">The points parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadBitsAsync operation result.</returns>
    public async Task<Responce<bool[]>> ReadBitsAsync(string address, int points, CancellationToken cancellationToken = default)
    {
        var parsed = MitsubishiDeviceAddress.Parse(address, Options.XyNotation);
        var expected = GetOneEExpectedLength(2 + ((points + 1) / 2));
        var raw = await ExecuteObservableAsync(() => EncodeBitReadRequest(parsed, points), expected, $"Read bits {address}", cancellationToken).ConfigureAwait(false);
        return ParseBits(raw, points);
    }

    /// <summary>Executes the WriteBitsAsync operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteBitsAsync operation result.</returns>
    public async Task<Responce> WriteBitsAsync(string address, IReadOnlyList<bool> values, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        var parsed = MitsubishiDeviceAddress.Parse(address, Options.XyNotation);
        var raw = await ExecuteObservableAsync(() => EncodeBitWriteRequest(parsed, values), GetOneEExpectedLength(2), $"Write bits {address}", cancellationToken).ConfigureAwait(false);
        return raw.ToBaseResponse();
    }

    /// <summary>Executes the RandomReadWordsAsync operation.</summary>
    /// <param name="addresses">The addresses parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The RandomReadWordsAsync operation result.</returns>
    public async Task<Responce<ushort[]>> RandomReadWordsAsync(IEnumerable<string> addresses, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        var addressArray = addresses.ToArray();
        if (IsSerialOneC())
        {
            return await RandomReadWordsOneCAsync(addressArray, cancellationToken).ConfigureAwait(false);
        }

        var parsed = addressArray.Select(address => MitsubishiDeviceAddress.Parse(address, Options.XyNotation)).ToArray();
        var raw = await ExecuteObservableAsync(() => Options.TransportKind == MitsubishiTransportKind.Serial ? MitsubishiSerialProtocolEncoding.EncodeRandomReadRequest(Options, parsed) : MitsubishiProtocolEncoding.EncodeRandomRead(Options, parsed), null, "Random read words", cancellationToken).ConfigureAwait(false);
        return ParseWords(raw);
    }

    /// <summary>Executes the RandomWriteWordsAsync operation.</summary>
    /// <param name="values">The values parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The RandomWriteWordsAsync operation result.</returns>
    public async Task<Responce> RandomWriteWordsAsync(IEnumerable<KeyValuePair<string, ushort>> values, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        var valueArray = values.ToArray();
        if (IsSerialOneC())
        {
            return await RandomWriteWordsOneCAsync(valueArray, cancellationToken).ConfigureAwait(false);
        }

        var payload = valueArray.Select(pair => new MitsubishiDeviceValue(MitsubishiDeviceAddress.Parse(pair.Key, Options.XyNotation), pair.Value)).ToArray();
        var raw = await ExecuteObservableAsync(() => Options.TransportKind == MitsubishiTransportKind.Serial ? MitsubishiSerialProtocolEncoding.EncodeRandomWriteRequest(Options, payload) : MitsubishiProtocolEncoding.EncodeRandomWrite(Options, payload), null, "Random write words", cancellationToken).ConfigureAwait(false);
        return raw.ToBaseResponse();
    }

    /// <summary>Executes the RegisterMonitorAsync operation.</summary>
    /// <param name="addresses">The addresses parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The RegisterMonitorAsync operation result.</returns>
    public async Task<Responce> RegisterMonitorAsync(IEnumerable<string> addresses, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        var addressArray = addresses.ToArray();
        if (IsSerialOneC())
        {
            return RegisterMonitorOneC(addressArray);
        }

        var payload = addressArray.Select(address => MitsubishiDeviceAddress.Parse(address, Options.XyNotation)).ToArray();
        var raw = await ExecuteObservableAsync(() => Options.TransportKind == MitsubishiTransportKind.Serial ? MitsubishiSerialProtocolEncoding.EncodeMonitorRegistrationRequest(Options, payload) : MitsubishiProtocolEncoding.EncodeMonitorRegistration(Options, payload), null, "Register monitor", cancellationToken).ConfigureAwait(false);
        return raw.ToBaseResponse();
    }

    /// <summary>Executes the ExecuteMonitorAsync operation.</summary>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ExecuteMonitorAsync operation result.</returns>
    public Task<Responce<byte[]>> ExecuteMonitorAsync(CancellationToken cancellationToken = default) => IsSerialOneC() ? ExecuteMonitorOneCAsync(cancellationToken) : ExecuteObservableAsync(() => Options.TransportKind == MitsubishiTransportKind.Serial ? MitsubishiSerialProtocolEncoding.EncodeExecuteMonitorRequest(Options) : MitsubishiProtocolEncoding.EncodeExecuteMonitor(Options), null, "Execute monitor", cancellationToken);

    /// <summary>Executes the ReadBlocksAsync operation.</summary>
    /// <param name="request">The request parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadBlocksAsync operation result.</returns>
    public Task<Responce<byte[]>> ReadBlocksAsync(MitsubishiBlockRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (IsSerialOneC())
        {
            return ReadBlocksOneCAsync(request, cancellationToken);
        }

        return ExecuteObservableAsync(() => Options.TransportKind == MitsubishiTransportKind.Serial ? MitsubishiSerialProtocolEncoding.EncodeBlockReadRequest(Options, request) : MitsubishiProtocolEncoding.EncodeBlockRead(Options, request), null, "Block read", cancellationToken);
    }

    /// <summary>Executes the WriteBlocksAsync operation.</summary>
    /// <param name="request">The request parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteBlocksAsync operation result.</returns>
    public async Task<Responce> WriteBlocksAsync(MitsubishiBlockRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (IsSerialOneC())
        {
            return await WriteBlocksOneCAsync(request, cancellationToken).ConfigureAwait(false);
        }

        var raw = await ExecuteObservableAsync(() => Options.TransportKind == MitsubishiTransportKind.Serial ? MitsubishiSerialProtocolEncoding.EncodeBlockWriteRequest(Options, request) : MitsubishiProtocolEncoding.EncodeBlockWrite(Options, request), null, "Block write", cancellationToken).ConfigureAwait(false);
        return raw.ToBaseResponse();
    }

    /// <summary>Executes the ReadTypeNameAsync operation.</summary>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadTypeNameAsync operation result.</returns>
    public async Task<Responce<MitsubishiTypeName>> ReadTypeNameAsync(CancellationToken cancellationToken = default)
    {
        var raw = await ExecuteObservableAsync(() => Options.TransportKind == MitsubishiTransportKind.Serial ? MitsubishiSerialProtocolEncoding.EncodeReadTypeNameRequest(Options) : MitsubishiProtocolEncoding.EncodeReadTypeName(Options), null, "Read type name", cancellationToken).ConfigureAwait(false);
        return ParseTypeName(raw);
    }

    /// <summary>Executes the RemoteRunAsync operation.</summary>
    /// <param name="force">The force parameter.</param>
    /// <param name="clearMode">The clearMode parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The RemoteRunAsync operation result.</returns>
    public Task<Responce> RemoteRunAsync(bool force = true, bool clearMode = false, CancellationToken cancellationToken = default) => ExecuteControlAsync(MitsubishiCommands.RemoteRun, cancellationToken, force, clearMode);

    /// <summary>Executes the RemoteStopAsync operation.</summary>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The RemoteStopAsync operation result.</returns>
    public Task<Responce> RemoteStopAsync(CancellationToken cancellationToken = default) => ExecuteControlAsync(MitsubishiCommands.RemoteStop, cancellationToken);

    /// <summary>Executes the RemotePauseAsync operation.</summary>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The RemotePauseAsync operation result.</returns>
    public Task<Responce> RemotePauseAsync(CancellationToken cancellationToken = default) => ExecuteControlAsync(MitsubishiCommands.RemotePause, cancellationToken);

    /// <summary>Executes the RemoteLatchClearAsync operation.</summary>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The RemoteLatchClearAsync operation result.</returns>
    public Task<Responce> RemoteLatchClearAsync(CancellationToken cancellationToken = default) => ExecuteControlAsync(MitsubishiCommands.RemoteLatchClear, cancellationToken);

    /// <summary>Executes the RemoteResetAsync operation.</summary>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The RemoteResetAsync operation result.</returns>
    public Task<Responce> RemoteResetAsync(CancellationToken cancellationToken = default) => ExecuteControlAsync(MitsubishiCommands.RemoteReset, cancellationToken);

    /// <summary>Executes the UnlockAsync operation.</summary>
    /// <param name="password">The password parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The UnlockAsync operation result.</returns>
    public Task<Responce> UnlockAsync(string password, CancellationToken cancellationToken = default) => ExecutePasswordAsync(MitsubishiCommands.Unlock, password, cancellationToken);

    /// <summary>Executes the LockAsync operation.</summary>
    /// <param name="password">The password parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The LockAsync operation result.</returns>
    public Task<Responce> LockAsync(string password, CancellationToken cancellationToken = default) => ExecutePasswordAsync(MitsubishiCommands.Lock, password, cancellationToken);

    /// <summary>Executes the ClearErrorAsync operation.</summary>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ClearErrorAsync operation result.</returns>
    public async Task<Responce> ClearErrorAsync(CancellationToken cancellationToken = default)
    {
        var raw = await ExecuteObservableAsync(() => MitsubishiProtocolEncoding.Encode(Options, new MitsubishiRawCommandRequest(MitsubishiCommands.ClearError, 0x0000, [], "Clear error")), null, "Clear error", cancellationToken).ConfigureAwait(false);
        return raw.ToBaseResponse();
    }

    /// <summary>Executes the LoopbackAsync operation.</summary>
    /// <param name="data">The data parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The LoopbackAsync operation result.</returns>
    public async Task<Responce<byte[]>> LoopbackAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        var raw = await ExecuteObservableAsync(() => EncodeLoopbackRequest(data), GetOneEExpectedLength(4 + data.Length), "Loopback", cancellationToken).ConfigureAwait(false);
        return ParseLoopback(raw);
    }

    /// <summary>Executes the ReadMemoryAsync operation.</summary>
    /// <param name="command">The command parameter.</param>
    /// <param name="address">The address parameter.</param>
    /// <param name="length">The length parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadMemoryAsync operation result.</returns>
    public async Task<Responce<ushort[]>> ReadMemoryAsync(ushort command, ushort address, int length, CancellationToken cancellationToken = default)
    {
        var raw = await ExecuteObservableAsync(() => Options.TransportKind == MitsubishiTransportKind.Serial ? MitsubishiSerialProtocolEncoding.EncodeMemoryAccessRequest(Options, command, address, length, Array.Empty<ushort>()) : MitsubishiProtocolEncoding.EncodeMemoryAccess(Options, command, address, length, Array.Empty<ushort>()), null, $"Read memory {command:X4}", cancellationToken).ConfigureAwait(false);
        return ParseWords(raw, expectedWordCount: length);
    }

    /// <summary>Executes the WriteMemoryAsync operation.</summary>
    /// <param name="command">The command parameter.</param>
    /// <param name="address">The address parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteMemoryAsync operation result.</returns>
    public async Task<Responce> WriteMemoryAsync(ushort command, ushort address, IReadOnlyList<ushort> values, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        var raw = await ExecuteObservableAsync(() => Options.TransportKind == MitsubishiTransportKind.Serial ? MitsubishiSerialProtocolEncoding.EncodeMemoryAccessRequest(Options, command, address, values.Count, values.ToArray()) : MitsubishiProtocolEncoding.EncodeMemoryAccess(Options, command, address, values.Count, values.ToArray()), null, $"Write memory {command:X4}", cancellationToken).ConfigureAwait(false);
        return raw.ToBaseResponse();
    }

    /// <summary>Executes the ObserveWords operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="points">The points parameter.</param>
    /// <param name="pollInterval">The pollInterval parameter.</param>
    /// <param name="minimumUpdateSpacing">The minimumUpdateSpacing parameter.</param>
    /// <param name="pollTimeout">The pollTimeout parameter.</param>
    /// <returns>The ObserveWords operation result.</returns>
    public IObservable<Responce<ushort[]>> ObserveWords(string address, int points, TimeSpan pollInterval, TimeSpan? minimumUpdateSpacing = null, TimeSpan? pollTimeout = null) => BuildPollingTrigger(pollInterval).SelectAsyncSequential(_ => ReadWordsAsync(address, points, CancellationToken.None)).Conflate(minimumUpdateSpacing ?? TimeSpan.FromMilliseconds(10), _scheduler).DoOnSubscribe(() => PublishOperation($"Observe words {address} subscribed", true, Array.Empty<byte>(), Array.Empty<byte>())).DoOnDispose(() => PublishOperation($"Observe words {address} disposed", true, Array.Empty<byte>(), Array.Empty<byte>()));

    /// <summary>Executes the ObserveBits operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="points">The points parameter.</param>
    /// <param name="pollInterval">The pollInterval parameter.</param>
    /// <param name="minimumUpdateSpacing">The minimumUpdateSpacing parameter.</param>
    /// <returns>The ObserveBits operation result.</returns>
    public IObservable<Responce<bool[]>> ObserveBits(string address, int points, TimeSpan pollInterval, TimeSpan? minimumUpdateSpacing = null) => BuildPollingTrigger(pollInterval).SelectAsyncSequential(_ => ReadBitsAsync(address, points, CancellationToken.None)).Conflate(minimumUpdateSpacing ?? TimeSpan.FromMilliseconds(10), _scheduler).DoOnSubscribe(() => PublishOperation($"Observe bits {address} subscribed", true, Array.Empty<byte>(), Array.Empty<byte>())).DoOnDispose(() => PublishOperation($"Observe bits {address} disposed", true, Array.Empty<byte>(), Array.Empty<byte>()));

    /// <summary>Executes the ObserveWordsHeartbeat operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="points">The points parameter.</param>
    /// <param name="pollInterval">The pollInterval parameter.</param>
    /// <param name="heartbeatAfter">The heartbeatAfter parameter.</param>
    /// <param name="minimumUpdateSpacing">The minimumUpdateSpacing parameter.</param>
    /// <param name="pollTimeout">The pollTimeout parameter.</param>
    /// <returns>The ObserveWordsHeartbeat operation result.</returns>
    public IObservable<Heartbeat<Responce<ushort[]>>> ObserveWordsHeartbeat(string address, int points, TimeSpan pollInterval, TimeSpan heartbeatAfter, TimeSpan? minimumUpdateSpacing = null, TimeSpan? pollTimeout = null) => ObserveWords(address, points, pollInterval, minimumUpdateSpacing, pollTimeout).Heartbeat(heartbeatAfter, _scheduler);

    /// <summary>Executes the ObserveWordsStale operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="points">The points parameter.</param>
    /// <param name="pollInterval">The pollInterval parameter.</param>
    /// <param name="staleAfter">The staleAfter parameter.</param>
    /// <param name="minimumUpdateSpacing">The minimumUpdateSpacing parameter.</param>
    /// <returns>The ObserveWordsStale operation result.</returns>
    public IObservable<Stale<Responce<ushort[]>>> ObserveWordsStale(string address, int points, TimeSpan pollInterval, TimeSpan staleAfter, TimeSpan? minimumUpdateSpacing = null) => ObserveWords(address, points, pollInterval, minimumUpdateSpacing).DetectStale(staleAfter, _scheduler);

    /// <summary>Executes the ObserveWordsLatest operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="points">The points parameter.</param>
    /// <param name="trigger">The trigger parameter.</param>
    /// <returns>The ObserveWordsLatest operation result.</returns>
    public IObservable<Responce<ushort[]>> ObserveWordsLatest(string address, int points, IObservable<Unit> trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        return trigger.SelectLatestAsync(_ => ReadWordsAsync(address, points, CancellationToken.None));
    }

    /// <summary>Executes the ObserveTagGroup operation.</summary>
    /// <param name="groupName">The groupName parameter.</param>
    /// <param name="pollInterval">The pollInterval parameter.</param>
    /// <param name="minimumUpdateSpacing">The minimumUpdateSpacing parameter.</param>
    /// <returns>The ObserveTagGroup operation result.</returns>
    public IObservable<Responce<MitsubishiTagGroupSnapshot>> ObserveTagGroup(string groupName, TimeSpan pollInterval, TimeSpan? minimumUpdateSpacing = null) => BuildPollingTrigger(pollInterval).SelectAsyncSequential(_ => ReadTagGroupSnapshotAsync(groupName, CancellationToken.None)).Conflate(minimumUpdateSpacing ?? TimeSpan.FromMilliseconds(10), _scheduler).DoOnSubscribe(() => PublishOperation($"Observe tag group {groupName} subscribed", true, Array.Empty<byte>(), Array.Empty<byte>())).DoOnDispose(() => PublishOperation($"Observe tag group {groupName} disposed", true, Array.Empty<byte>(), Array.Empty<byte>()));

    /// <summary>Executes the ObserveTagGroupHeartbeat operation.</summary>
    /// <param name="groupName">The groupName parameter.</param>
    /// <param name="pollInterval">The pollInterval parameter.</param>
    /// <param name="heartbeatAfter">The heartbeatAfter parameter.</param>
    /// <param name="minimumUpdateSpacing">The minimumUpdateSpacing parameter.</param>
    /// <returns>The ObserveTagGroupHeartbeat operation result.</returns>
    public IObservable<Heartbeat<Responce<MitsubishiTagGroupSnapshot>>> ObserveTagGroupHeartbeat(string groupName, TimeSpan pollInterval, TimeSpan heartbeatAfter, TimeSpan? minimumUpdateSpacing = null) => ObserveTagGroup(groupName, pollInterval, minimumUpdateSpacing).Heartbeat(heartbeatAfter, _scheduler);

    /// <summary>Executes the ObserveTagGroupStale operation.</summary>
    /// <param name="groupName">The groupName parameter.</param>
    /// <param name="pollInterval">The pollInterval parameter.</param>
    /// <param name="staleAfter">The staleAfter parameter.</param>
    /// <param name="minimumUpdateSpacing">The minimumUpdateSpacing parameter.</param>
    /// <returns>The ObserveTagGroupStale operation result.</returns>
    public IObservable<Stale<Responce<MitsubishiTagGroupSnapshot>>> ObserveTagGroupStale(string groupName, TimeSpan pollInterval, TimeSpan staleAfter, TimeSpan? minimumUpdateSpacing = null) => ObserveTagGroup(groupName, pollInterval, minimumUpdateSpacing).DetectStale(staleAfter, _scheduler);

    /// <summary>Executes the ObserveTagGroupLatest operation.</summary>
    /// <param name="groupName">The groupName parameter.</param>
    /// <param name="trigger">The trigger parameter.</param>
    /// <returns>The ObserveTagGroupLatest operation result.</returns>
    public IObservable<Responce<MitsubishiTagGroupSnapshot>> ObserveTagGroupLatest(string groupName, IObservable<Unit> trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        return trigger.SelectLatestAsync(_ => ReadTagGroupSnapshotAsync(groupName, CancellationToken.None));
    }

    /// <summary>Executes the SampleDiagnostics operation.</summary>
    /// <param name="trigger">The trigger parameter.</param>
    /// <returns>The SampleDiagnostics operation result.</returns>
    public IObservable<MitsubishiOperationLog> SampleDiagnostics(IObservable<object> trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        return OperationLogs.SampleLatest(trigger);
    }

    /// <summary>Executes the ObserveConnectionHealth operation.</summary>
    /// <param name="staleAfter">The staleAfter parameter.</param>
    /// <returns>The ObserveConnectionHealth operation result.</returns>
    public IObservable<Stale<MitsubishiConnectionState>> ObserveConnectionHealth(TimeSpan staleAfter) => ConnectionStates.DetectStale(staleAfter, _scheduler);

    /// <summary>Executes the Dispose operation.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _connectionStates.OnCompleted();
        _operationLogs.OnCompleted();
        _connectionStates.Dispose();
        _operationLogs.Dispose();
        DisposeReactiveStreams();
        _transport.Dispose();
        _requestGate.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Executes the DisposeAsync operation.</summary>
    /// <returns>The DisposeAsync operation result.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _connectionStates.OnCompleted();
        _operationLogs.OnCompleted();
        _connectionStates.Dispose();
        _operationLogs.Dispose();
        DisposeReactiveStreams();
        await _transport.DisposeAsync().ConfigureAwait(false);
        _requestGate.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Executes the BuildEndPoint operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <returns>The BuildEndPoint operation result.</returns>
    private static IPEndPoint BuildEndPoint(MitsubishiClientOptions options)
    {
        return IPAddress.TryParse(options.Host, out var ipAddress) ? new IPEndPoint(ipAddress, options.Port) : new IPEndPoint(IPAddress.Any, options.Port);
    }

    /// <summary>Executes the CreateDefaultTransport operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <returns>The CreateDefaultTransport operation result.</returns>
    private static IMitsubishiTransport CreateDefaultTransport(MitsubishiClientOptions options) => options.TransportKind == MitsubishiTransportKind.Serial ? new ReactiveSerialMitsubishiTransport() : new SocketMitsubishiTransport();

    /// <summary>Executes the ParseWordPayload operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="payload">The payload parameter.</param>
    /// <param name="expectedWordCount">The expectedWordCount parameter.</param>
    /// <returns>The ParseWordPayload operation result.</returns>
    private static ushort[] ParseWordPayload(MitsubishiClientOptions options, byte[] payload, int? expectedWordCount = null)
    {
        if (options.DataCode == CommunicationDataCode.Binary)
        {
            var count = expectedWordCount ?? (payload.Length / 2);
            var values = new ushort[count];
            for (var index = 0; index < count; index++)
            {
                values[index] = BitConverter.ToUInt16(payload, index * 2);
            }

            return values;
        }

        var text = System.Text.Encoding.ASCII.GetString(payload);
        var countFromAscii = expectedWordCount ?? (text.Length / 4);
        var valuesFromAscii = new ushort[countFromAscii];
        for (var index = 0; index < countFromAscii; index++)
        {
            valuesFromAscii[index] = Convert.ToUInt16(text.Substring(index * 4, 4), 16);
        }

        return valuesFromAscii;
    }

    /// <summary>Executes the GetTextEncoding operation.</summary>
    /// <param name="tag">The tag parameter.</param>
    /// <returns>The GetTextEncoding operation result.</returns>
    private static Encoding GetTextEncoding(MitsubishiTagDefinition tag) => tag.Encoding switch
    {
        "Utf8" => Encoding.UTF8,
        "Utf16" => Encoding.Unicode,
        _ => Encoding.ASCII,
    };

    /// <summary>Executes the ValidateStringTagLength operation.</summary>
    /// <param name="tag">The tag parameter.</param>
    /// <param name="result">The result parameter.</param>
    private void ValidateStringTagLength(MitsubishiTagDefinition tag, Responce result)
    {
        if (!string.Equals(tag.DataType, "String", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (tag.Length > 0)
        {
            return;
        }

        result.IsSucceed = false;
        result.ErrList.Add($"Tag '{tag.Name}' uses DataType 'String' and must define a positive Length.");
    }

    /// <summary>Executes the GetWordCountForScaledRead operation.</summary>
    /// <param name="tag">The tag parameter.</param>
    /// <returns>The GetWordCountForScaledRead operation result.</returns>
    private int GetWordCountForScaledRead(MitsubishiTagDefinition tag) => tag.DataType switch
    {
        null or "Word" or "Int16" or "UInt16" => 1,
        "DWord" or "Int32" or "UInt32" or "Float" => 2,
        _ => throw new InvalidOperationException($"Scaled access is not supported for DataType '{tag.DataType}'.")
    };

    /// <summary>Executes the BuildPollingTrigger operation.</summary>
    /// <param name="pollInterval">The pollInterval parameter.</param>
    /// <param name="emitInitial">The emitInitial parameter.</param>
    /// <returns>The BuildPollingTrigger operation result.</returns>
    private IObservable<long> BuildPollingTrigger(TimeSpan pollInterval, bool emitInitial = true) => emitInitial ? Observable.Interval(pollInterval, _scheduler).StartWith(0L) : Observable.Interval(pollInterval, _scheduler);

    /// <summary>Executes the ExecuteControlAsync operation.</summary>
    /// <param name="command">The command parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <param name="force">The force parameter.</param>
    /// <param name="clearMode">The clearMode parameter.</param>
    /// <returns>The ExecuteControlAsync operation result.</returns>
    private async Task<Responce> ExecuteControlAsync(ushort command, CancellationToken cancellationToken, bool force = true, bool clearMode = false)
    {
        var raw = await ExecuteObservableAsync(() => Options.TransportKind == MitsubishiTransportKind.Serial ? MitsubishiSerialProtocolEncoding.EncodeRemoteOperationRequest(Options, command, force, clearMode) : MitsubishiProtocolEncoding.EncodeRemoteOperation(Options, command, force, clearMode), null, $"Remote operation {command:X4}", cancellationToken).ConfigureAwait(false);
        return raw.ToBaseResponse();
    }

    /// <summary>Executes the ExecutePasswordAsync operation.</summary>
    /// <param name="command">The command parameter.</param>
    /// <param name="password">The password parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ExecutePasswordAsync operation result.</returns>
    private async Task<Responce> ExecutePasswordAsync(ushort command, string password, CancellationToken cancellationToken)
    {
        var raw = await ExecuteObservableAsync(() => MitsubishiProtocolEncoding.EncodeRemotePassword(Options, command, password), null, command == MitsubishiCommands.Unlock ? "Unlock" : "Lock", cancellationToken).ConfigureAwait(false);
        return raw.ToBaseResponse();
    }

    /// <summary>Executes the IsSerialOneC operation.</summary>
    /// <returns>The IsSerialOneC operation result.</returns>
    private bool IsSerialOneC() => Options.TransportKind == MitsubishiTransportKind.Serial && Options.FrameType == MitsubishiFrameType.OneC;

    /// <summary>Executes the RandomReadWordsOneCAsync operation.</summary>
    /// <param name="addresses">The addresses parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The RandomReadWordsOneCAsync operation result.</returns>
    private async Task<Responce<ushort[]>> RandomReadWordsOneCAsync(IReadOnlyList<string> addresses, CancellationToken cancellationToken)
    {
        if (addresses.Count == 0)
        {
            return new Responce<ushort[]>().Fail("At least one device must be supplied.");
        }

        var values = new List<ushort>(addresses.Count);
        foreach (var address in addresses)
        {
            var read = await ReadWordsAsync(address, 1, cancellationToken).ConfigureAwait(false);
            if (!read.IsSucceed || read.Value is null)
            {
                return new Responce<ushort[]>(read);
            }

            values.Add(read.Value[0]);
        }

        return new Responce<ushort[]>(values.ToArray()).EndTime();
    }

    /// <summary>Executes the RandomWriteWordsOneCAsync operation.</summary>
    /// <param name="values">The values parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The RandomWriteWordsOneCAsync operation result.</returns>
    private async Task<Responce> RandomWriteWordsOneCAsync(IReadOnlyList<KeyValuePair<string, ushort>> values, CancellationToken cancellationToken)
    {
        if (values.Count == 0)
        {
            return new Responce().Fail("At least one device value must be supplied.");
        }

        foreach (var pair in values)
        {
            var write = await WriteWordsAsync(pair.Key, [pair.Value], cancellationToken).ConfigureAwait(false);
            if (!write.IsSucceed)
            {
                return write;
            }
        }

        return new Responce().EndTime();
    }

    /// <summary>Executes the RegisterMonitorOneC operation.</summary>
    /// <param name="addresses">The addresses parameter.</param>
    /// <returns>The RegisterMonitorOneC operation result.</returns>
    private Responce RegisterMonitorOneC(IReadOnlyList<string> addresses)
    {
        if (addresses.Count == 0)
        {
            return new Responce().Fail("At least one device must be supplied.");
        }

        foreach (var address in addresses)
        {
            var parsed = MitsubishiDeviceAddress.Parse(address, Options.XyNotation);
            if (parsed.Descriptor.Kind != DeviceValueKind.Word)
            {
                return new Responce().Fail($"1C monitor emulation supports word devices only; '{address}' is a bit device.");
            }
        }

        _serialOneCMonitorAddresses = addresses.ToArray();
        PublishOperation("Register monitor 1C emulation", true, Array.Empty<byte>(), Array.Empty<byte>());
        return new Responce().EndTime();
    }

    /// <summary>Executes the ExecuteMonitorOneCAsync operation.</summary>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ExecuteMonitorOneCAsync operation result.</returns>
    private async Task<Responce<byte[]>> ExecuteMonitorOneCAsync(CancellationToken cancellationToken)
    {
        if (_serialOneCMonitorAddresses is null || _serialOneCMonitorAddresses.Count == 0)
        {
            return new Responce<byte[]>().Fail("1C monitor execution requires RegisterMonitorAsync to be called first.");
        }

        var read = await RandomReadWordsOneCAsync(_serialOneCMonitorAddresses, cancellationToken).ConfigureAwait(false);
        if (!read.IsSucceed || read.Value is null)
        {
            return new Responce<byte[]>(read);
        }

        var payload = Encoding.ASCII.GetBytes(string.Concat(read.Value.Select(static value => value.ToString("X4", System.Globalization.CultureInfo.InvariantCulture))));
        return new Responce<byte[]>(read, payload);
    }

    /// <summary>Executes the ReadBlocksOneCAsync operation.</summary>
    /// <param name="request">The request parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadBlocksOneCAsync operation result.</returns>
    private async Task<Responce<byte[]>> ReadBlocksOneCAsync(MitsubishiBlockRequest request, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        foreach (var block in request.ResolvedWordBlocks)
        {
            var read = await ReadWordsAsync(block.Address.Original, block.Values.Length, cancellationToken).ConfigureAwait(false);
            if (!read.IsSucceed || read.Value is null)
            {
                return new Responce<byte[]>(read);
            }

            _ = builder.Append(string.Concat(read.Value.Select(static value => value.ToString("X4", System.Globalization.CultureInfo.InvariantCulture))));
        }

        foreach (var block in request.ResolvedBitBlocks)
        {
            var read = await ReadBitsAsync(block.Address.Original, block.Values.Length, cancellationToken).ConfigureAwait(false);
            if (!read.IsSucceed || read.Value is null)
            {
                return new Responce<byte[]>(read);
            }

            _ = builder.Append(string.Concat(read.Value.Select(static value => value ? "10" : "00")));
        }

        return new Responce<byte[]>(Encoding.ASCII.GetBytes(builder.ToString())).EndTime();
    }

    /// <summary>Executes the WriteBlocksOneCAsync operation.</summary>
    /// <param name="request">The request parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteBlocksOneCAsync operation result.</returns>
    private async Task<Responce> WriteBlocksOneCAsync(MitsubishiBlockRequest request, CancellationToken cancellationToken)
    {
        foreach (var block in request.ResolvedWordBlocks)
        {
            var write = await WriteWordsAsync(block.Address.Original, block.Values.ToArray(), cancellationToken).ConfigureAwait(false);
            if (!write.IsSucceed)
            {
                return write;
            }
        }

        foreach (var block in request.ResolvedBitBlocks)
        {
            var write = await WriteBitsAsync(block.Address.Original, block.Values.ToArray(), cancellationToken).ConfigureAwait(false);
            if (!write.IsSucceed)
            {
                return write;
            }
        }

        return new Responce().EndTime();
    }

    /// <summary>Executes the ExecuteEncodedAsync operation.</summary>
    /// <param name="command">The command parameter.</param>
    /// <param name="expectedLength">The expectedLength parameter.</param>
    /// <param name="description">The description parameter.</param>
    /// <param name="maxRetries">The maxRetries parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ExecuteEncodedAsync operation result.</returns>
    private Task<Responce<byte[]>> ExecuteEncodedAsync(byte[] command, int? expectedLength, string description, int maxRetries = 2, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return ExecuteObservableAsync(() => command, expectedLength, description, cancellationToken, maxRetries);
    }

    /// <summary>Executes the ExecuteObservableAsync operation.</summary>
    /// <param name="payloadFactory">The payloadFactory parameter.</param>
    /// <param name="expectedLength">The expectedLength parameter.</param>
    /// <param name="description">The description parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <param name="maxRetries">The maxRetries parameter.</param>
    /// <returns>The ExecuteObservableAsync operation result.</returns>
    private Task<Responce<byte[]>> ExecuteObservableAsync(Func<byte[]> payloadFactory, int? expectedLength, string description, CancellationToken cancellationToken, int maxRetries = 2)
    {
        ArgumentNullException.ThrowIfNull(payloadFactory);
        var observable = Observable.Defer(() => Observable.FromAsync(ct => ExecuteOnceAsync(payloadFactory, expectedLength, description, ct))).RetryWithBackoff(maxRetries, TimeSpan.FromMilliseconds(100), backoffFactor: 2.0, maxDelay: null, scheduler: _scheduler).Catch<Responce<byte[]>, Exception>(ex => Observable.Return(new Responce<byte[]>().Fail(ex.Message, exception: ex)));
        return observable.FirstAsync().ToTask(cancellationToken);
    }

    /// <summary>Executes the ExecuteOnceAsync operation.</summary>
    /// <param name="payloadFactory">The payloadFactory parameter.</param>
    /// <param name="expectedLength">The expectedLength parameter.</param>
    /// <param name="description">The description parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ExecuteOnceAsync operation result.</returns>
    private async Task<Responce<byte[]>> ExecuteOnceAsync(Func<byte[]> payloadFactory, int? expectedLength, string description, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _requestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
            var payload = payloadFactory();
            var request = new MitsubishiTransportRequest(payload, expectedLength, description);
            var response = await _transport.ExchangeAsync(request, cancellationToken).ConfigureAwait(false);
            var decoded = Options.TransportKind == MitsubishiTransportKind.Serial ? MitsubishiSerialProtocolEncoding.Decode(Options, response) : MitsubishiProtocolEncoding.Decode(Options, request, response);
            decoded.Request = Convert.ToHexString(payload);
            decoded.Response = Convert.ToHexString(response);
            PublishOperation(description, decoded.IsSucceed, payload, response, decoded.Exception);
            return decoded;
        }
        catch (Exception ex)
        {
            PublishFault(description, Array.Empty<byte>(), Array.Empty<byte>(), ex);
            await _transport.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        finally
        {
            _ = _requestGate.Release();
        }
    }

    /// <summary>Executes the EnsureConnectedAsync operation.</summary>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The EnsureConnectedAsync operation result.</returns>
    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_transport.IsConnected)
        {
            PublishState(MitsubishiConnectionState.Connected);
            return;
        }

        PublishState(_connectionStates.Value == MitsubishiConnectionState.Disconnected ? MitsubishiConnectionState.Connecting : MitsubishiConnectionState.Reconnecting);
        await _transport.ConnectAsync(Options, cancellationToken).ConfigureAwait(false);
        PublishState(MitsubishiConnectionState.Connected);
    }

    /// <summary>Executes the ParseBits operation.</summary>
    /// <param name="raw">The raw parameter.</param>
    /// <param name="expectedBitCount">The expectedBitCount parameter.</param>
    /// <returns>The ParseBits operation result.</returns>
    private Responce<bool[]> ParseBits(Responce<byte[]> raw, int expectedBitCount)
    {
        if (!raw.IsSucceed || raw.Value is null)
        {
            return new Responce<bool[]>(raw);
        }

        try
        {
            var values = Options.DataCode == CommunicationDataCode.Binary ? ParseBinaryBits(raw.Value, expectedBitCount) : ParseAsciiBits(raw.Value, expectedBitCount);
            return new Responce<bool[]>(raw, values);
        }
        catch (Exception ex)
        {
            return new Responce<bool[]>(raw).Fail(ex.Message, exception: ex);
        }
    }

    /// <summary>Executes the ParseTypeName operation.</summary>
    /// <param name="raw">The raw parameter.</param>
    /// <returns>The ParseTypeName operation result.</returns>
    private Responce<MitsubishiTypeName> ParseTypeName(Responce<byte[]> raw)
    {
        if (!raw.IsSucceed || raw.Value is null)
        {
            return new Responce<MitsubishiTypeName>(raw);
        }

        try
        {
            if (Options.DataCode == CommunicationDataCode.Binary)
            {
                var code = raw.Value.Length >= 2 ? BitConverter.ToUInt16(raw.Value, raw.Value.Length - 2) : (ushort)0;
                var nameLength = Math.Max(0, raw.Value.Length - 2);
                var name = System.Text.Encoding.ASCII.GetString(raw.Value, 0, nameLength).TrimEnd('\0', ' ');
                return new Responce<MitsubishiTypeName>(raw, new MitsubishiTypeName(name, code));
            }

            var ascii = System.Text.Encoding.ASCII.GetString(raw.Value).Trim();
            var modelCode = ascii.Length >= 4 && ushort.TryParse(ascii[^4..], System.Globalization.NumberStyles.HexNumber, null, out var parsed) ? parsed : (ushort)0;
            var modelName = ascii.Length > 4 ? ascii[..^4].Trim() : ascii;
            return new Responce<MitsubishiTypeName>(raw, new MitsubishiTypeName(modelName, modelCode));
        }
        catch (Exception ex)
        {
            return new Responce<MitsubishiTypeName>(raw).Fail(ex.Message, exception: ex);
        }
    }

    /// <summary>Executes the ParseLoopback operation.</summary>
    /// <param name="raw">The raw parameter.</param>
    /// <returns>The ParseLoopback operation result.</returns>
    private Responce<byte[]> ParseLoopback(Responce<byte[]> raw)
    {
        if (!raw.IsSucceed || raw.Value is null)
        {
            return new Responce<byte[]>(raw);
        }

        try
        {
            if (Options.TransportKind != MitsubishiTransportKind.Serial)
            {
                return raw;
            }

            if (Options.DataCode == CommunicationDataCode.Binary)
            {
                if (raw.Value.Length < 2)
                {
                    throw new InvalidOperationException("Loopback payload is missing the returned length field.");
                }

                var length = BitConverter.ToUInt16(raw.Value, 0);
                var available = Math.Max(0, raw.Value.Length - 2);
                var count = Math.Min(length, available);
                return new Responce<byte[]>(raw, raw.Value.Skip(2).Take(count).ToArray());
            }

            var ascii = System.Text.Encoding.ASCII.GetString(raw.Value);
            if (ascii.Length < 4)
            {
                throw new InvalidOperationException("Loopback payload is missing the returned ASCII length field.");
            }

            var lengthValue = ushort.TryParse(ascii[..4], System.Globalization.NumberStyles.HexNumber, null, out var parsedLength) ? parsedLength : (ushort)0;
            var echoed = ascii.Length > 4 ? ascii.Substring(4, Math.Min(lengthValue, ascii.Length - 4)) : string.Empty;
            return new Responce<byte[]>(raw, System.Text.Encoding.ASCII.GetBytes(echoed));
        }
        catch (Exception ex)
        {
            return new Responce<byte[]>(raw).Fail(ex.Message, exception: ex);
        }
    }

    /// <summary>Executes the ParseAsciiBits operation.</summary>
    /// <param name="payload">The payload parameter.</param>
    /// <param name="expectedBitCount">The expectedBitCount parameter.</param>
    /// <returns>The ParseAsciiBits operation result.</returns>
    private bool[] ParseAsciiBits(byte[] payload, int expectedBitCount)
    {
        var text = System.Text.Encoding.ASCII.GetString(payload);
        var bits = new List<bool>(expectedBitCount);
        foreach (var ch in text)
        {
            if (ch is '0' or '1')
            {
                bits.Add(ch == '1');
            }

            if (bits.Count == expectedBitCount)
            {
                break;
            }
        }

        return bits.ToArray();
    }

    /// <summary>Executes the ParseBinaryBits operation.</summary>
    /// <param name="payload">The payload parameter.</param>
    /// <param name="expectedBitCount">The expectedBitCount parameter.</param>
    /// <returns>The ParseBinaryBits operation result.</returns>
    private bool[] ParseBinaryBits(byte[] payload, int expectedBitCount)
    {
        var bits = new List<bool>(expectedBitCount);
        foreach (var value in payload)
        {
            bits.Add((value & 0x0F) != 0);
            if (bits.Count == expectedBitCount)
            {
                break;
            }

            bits.Add((value & 0xF0) != 0);
            if (bits.Count == expectedBitCount)
            {
                break;
            }
        }

        return bits.ToArray();
    }

    /// <summary>Executes the ConvertWords operation.</summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="raw">The raw parameter.</param>
    /// <param name="converter">The converter parameter.</param>
    /// <returns>The ConvertWords operation result.</returns>
    private Responce<T> ConvertWords<T>(Responce<ushort[]> raw, Func<ushort[], T> converter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        if (!raw.IsSucceed || raw.Value is null)
        {
            return new Responce<T>(raw);
        }

        try
        {
            return new Responce<T>(raw, converter(raw.Value));
        }
        catch (Exception ex)
        {
            return new Responce<T>(raw).Fail(ex.Message, exception: ex);
        }
    }

    /// <summary>Executes the ReadNumericTagValue operation.</summary>
    /// <param name="tag">The tag parameter.</param>
    /// <param name="words">The words parameter.</param>
    /// <returns>The ReadNumericTagValue operation result.</returns>
    private double ReadNumericTagValue(MitsubishiTagDefinition tag, ushort[] words) => tag.DataType switch
    {
        null or "Word" => tag.Signed ? unchecked((short)words[0]) : words[0],
        "Int16" => unchecked((short)words[0]),
        "UInt16" => words[0],
        "DWord" => unchecked((uint)(words[0] | (words[1] << 16))),
        "Int32" => ConvertToInt32(words, tag),
        "UInt32" => ConvertToUInt32(words, tag),
        "Float" => ConvertToFloat(words, tag),
        _ => throw new InvalidOperationException($"Numeric conversion is not supported for DataType '{tag.DataType}'.")
    };

    /// <summary>Executes the ParseWords operation.</summary>
    /// <param name="raw">The raw parameter.</param>
    /// <param name="expectedWordCount">The expectedWordCount parameter.</param>
    /// <returns>The ParseWords operation result.</returns>
    private Responce<ushort[]> ParseWords(Responce<byte[]> raw, int? expectedWordCount = null)
    {
        if (!raw.IsSucceed || raw.Value is null)
        {
            return new Responce<ushort[]>(raw);
        }

        try
        {
            return new Responce<ushort[]>(raw, ParseWordPayload(Options, raw.Value, expectedWordCount));
        }
        catch (Exception ex)
        {
            return new Responce<ushort[]>(raw).Fail(ex.Message, exception: ex);
        }
    }

    /// <summary>Executes the GetOneEExpectedLength operation.</summary>
    /// <param name="length">The length parameter.</param>
    /// <returns>The GetOneEExpectedLength operation result.</returns>
    private int? GetOneEExpectedLength(int length)
    {
        if (Options.TransportKind == MitsubishiTransportKind.Serial)
        {
            return null;
        }

        return Options.FrameType == MitsubishiFrameType.OneE ? length : null;
    }

    /// <summary>Executes the GetFixedResponseLength operation.</summary>
    /// <param name="request">The request parameter.</param>
    /// <returns>The GetFixedResponseLength operation result.</returns>
    private int? GetFixedResponseLength(MitsubishiRawCommandRequest request)
    {
        return Options.TransportKind == MitsubishiTransportKind.Serial ? null : MitsubishiProtocolEncoding.GetFixedResponseLength(Options.FrameType, Options.DataCode, request.Command, request.Subcommand, request.ResolvedBody.Count);
    }

    /// <summary>Executes the EncodeRawRequest operation.</summary>
    /// <param name="request">The request parameter.</param>
    /// <returns>The EncodeRawRequest operation result.</returns>
    private byte[] EncodeRawRequest(MitsubishiRawCommandRequest request)
    {
        return Options.TransportKind == MitsubishiTransportKind.Serial ? MitsubishiSerialProtocolEncoding.EncodeRawRequest(Options, request) : MitsubishiProtocolEncoding.Encode(Options, request);
    }

    /// <summary>Executes the EncodeWordReadRequest operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="points">The points parameter.</param>
    /// <returns>The EncodeWordReadRequest operation result.</returns>
    private byte[] EncodeWordReadRequest(MitsubishiDeviceAddress address, int points)
    {
        return Options.TransportKind == MitsubishiTransportKind.Serial ? MitsubishiSerialProtocolEncoding.EncodeWordReadRequest(Options, address, points) : MitsubishiProtocolEncoding.EncodeDeviceBatchRead(Options, address, points, bitUnits: false);
    }

    /// <summary>Executes the EncodeWordWriteRequest operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <returns>The EncodeWordWriteRequest operation result.</returns>
    private byte[] EncodeWordWriteRequest(MitsubishiDeviceAddress address, IReadOnlyList<ushort> values)
    {
        return Options.TransportKind == MitsubishiTransportKind.Serial ? MitsubishiSerialProtocolEncoding.EncodeWordWriteRequest(Options, address, values) : MitsubishiProtocolEncoding.EncodeDeviceBatchWrite(Options, address, values.ToArray(), bitUnits: false);
    }

    /// <summary>Executes the EncodeBitReadRequest operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="points">The points parameter.</param>
    /// <returns>The EncodeBitReadRequest operation result.</returns>
    private byte[] EncodeBitReadRequest(MitsubishiDeviceAddress address, int points)
    {
        return Options.TransportKind == MitsubishiTransportKind.Serial ? MitsubishiSerialProtocolEncoding.EncodeBitReadRequest(Options, address, points) : MitsubishiProtocolEncoding.EncodeDeviceBatchRead(Options, address, points, bitUnits: true);
    }

    /// <summary>Executes the EncodeBitWriteRequest operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <returns>The EncodeBitWriteRequest operation result.</returns>
    private byte[] EncodeBitWriteRequest(MitsubishiDeviceAddress address, IReadOnlyList<bool> values)
    {
        if (Options.TransportKind == MitsubishiTransportKind.Serial)
        {
            return MitsubishiSerialProtocolEncoding.EncodeBitWriteRequest(Options, address, values);
        }

        return MitsubishiProtocolEncoding.EncodeDeviceBatchWrite(Options, address, values.Select(static value => value ? (ushort)1 : (ushort)0).ToArray(), bitUnits: true);
    }

    /// <summary>Executes the EncodeLoopbackRequest operation.</summary>
    /// <param name="data">The data parameter.</param>
    /// <returns>The EncodeLoopbackRequest operation result.</returns>
    private byte[] EncodeLoopbackRequest(byte[] data)
    {
        return Options.TransportKind == MitsubishiTransportKind.Serial ? MitsubishiSerialProtocolEncoding.EncodeLoopbackRequest(Options, data) : MitsubishiProtocolEncoding.EncodeLoopback(Options, data);
    }

    /// <summary>Executes the GetSerialExpectedWordCount operation.</summary>
    /// <param name="wordCount">The wordCount parameter.</param>
    /// <returns>The GetSerialExpectedWordCount operation result.</returns>
    private int? GetSerialExpectedWordCount(int wordCount) => Options.TransportKind == MitsubishiTransportKind.Serial ? wordCount : null;

    /// <summary>Executes the ValidateTagDatabase operation.</summary>
    /// <param name="database">The database parameter.</param>
    /// <returns>The ValidateTagDatabase operation result.</returns>
    private Responce ValidateTagDatabase(MitsubishiTagDatabase? database)
    {
        var result = new Responce();
        if (database is null)
        {
            return result.Fail("TagDatabase must be assigned before validation can run.");
        }

        ValidateTags(database.Tags, result);
        ValidateGroups(database, result);
        ApplyValidationSummary(result);
        return result.EndTime();
    }

    /// <summary>Executes the ValidateTags operation.</summary>
    /// <param name="tags">The tags parameter.</param>
    /// <param name="result">The result parameter.</param>
    private void ValidateTags(IEnumerable<MitsubishiTagDefinition> tags, Responce result)
    {
        foreach (var tag in tags)
        {
            ValidateTagAddress(tag, result);
            ValidateStringTagLength(tag, result);
        }
    }

    /// <summary>Executes the ValidateTagAddress operation.</summary>
    /// <param name="tag">The tag parameter.</param>
    /// <param name="result">The result parameter.</param>
    private void ValidateTagAddress(MitsubishiTagDefinition tag, Responce result)
    {
        try
        {
            _ = MitsubishiDeviceAddress.Parse(tag.Address, Options.XyNotation);
        }
        catch (Exception ex)
        {
            result.IsSucceed = false;
            result.ErrList.Add($"Tag '{tag.Name}' has invalid Address '{tag.Address}': {ex.Message}");
        }
    }

    /// <summary>Executes the ValidateGroups operation.</summary>
    /// <param name="database">The database parameter.</param>
    /// <param name="result">The result parameter.</param>
    private void ValidateGroups(MitsubishiTagDatabase database, Responce result)
    {
        foreach (var group in database.Groups)
        {
            ValidateGroupTags(database, group, result);
        }
    }

    /// <summary>Executes the ValidateGroupTags operation.</summary>
    /// <param name="database">The database parameter.</param>
    /// <param name="group">The group parameter.</param>
    /// <param name="result">The result parameter.</param>
    private void ValidateGroupTags(MitsubishiTagDatabase database, MitsubishiTagGroupDefinition group, Responce result)
    {
        foreach (var tagName in group.ResolvedTagNames)
        {
            if (database.TryGet(tagName, out _))
            {
                continue;
            }

            result.IsSucceed = false;
            result.ErrList.Add($"Group '{group.Name}' references unknown tag '{tagName}'.");
        }
    }

    /// <summary>Executes the ApplyValidationSummary operation.</summary>
    /// <param name="result">The result parameter.</param>
    private void ApplyValidationSummary(Responce result)
    {
        if (result.IsSucceed || result.ErrList.Count == 0)
        {
            return;
        }

        result.Err = string.Join(Environment.NewLine, result.ErrList);
    }

    /// <summary>Executes the GetRequiredTag operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <returns>The GetRequiredTag operation result.</returns>
    private MitsubishiTagDefinition GetRequiredTag(string tagName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);
        var database = TagDatabase ?? throw new InvalidOperationException("TagDatabase must be assigned before tag-based APIs can be used.");
        return database.GetRequired(tagName);
    }

    /// <summary>Executes the ApplyScaleAndOffset operation.</summary>
    /// <param name="rawValue">The rawValue parameter.</param>
    /// <param name="tag">The tag parameter.</param>
    /// <returns>The ApplyScaleAndOffset operation result.</returns>
    private double ApplyScaleAndOffset(double rawValue, MitsubishiTagDefinition tag) => (rawValue * tag.Scale) + tag.Offset;

    /// <summary>Executes the RemoveScaleAndOffset operation.</summary>
    /// <param name="engineeringValue">The engineeringValue parameter.</param>
    /// <param name="tag">The tag parameter.</param>
    /// <returns>The RemoveScaleAndOffset operation result.</returns>
    private double RemoveScaleAndOffset(double engineeringValue, MitsubishiTagDefinition tag)
    {
        if (tag.Scale == 0)
        {
            throw new InvalidOperationException($"Tag '{tag.Name}' has Scale=0 and cannot be used for scaled writes.");
        }

        return (engineeringValue - tag.Offset) / tag.Scale;
    }

    /// <summary>Executes the ConvertToUInt32 operation.</summary>
    /// <param name="words">The words parameter.</param>
    /// <param name="tag">The tag parameter.</param>
    /// <returns>The ConvertToUInt32 operation result.</returns>
    private uint ConvertToUInt32(ushort[] words, MitsubishiTagDefinition tag)
    {
        EnsureWordCount(words, 2, tag.Name);
        return tag.ByteOrder == "BigEndian" ? unchecked((uint)((words[0] << 16) | words[1])) : unchecked((uint)(words[0] | (words[1] << 16)));
    }

    /// <summary>Executes the ConvertToInt32 operation.</summary>
    /// <param name="words">The words parameter.</param>
    /// <param name="tag">The tag parameter.</param>
    /// <returns>The ConvertToInt32 operation result.</returns>
    private int ConvertToInt32(ushort[] words, MitsubishiTagDefinition tag) => unchecked((int)ConvertToUInt32(words, tag));

    /// <summary>Executes the ConvertToFloat operation.</summary>
    /// <param name="words">The words parameter.</param>
    /// <param name="tag">The tag parameter.</param>
    /// <returns>The ConvertToFloat operation result.</returns>
    private float ConvertToFloat(ushort[] words, MitsubishiTagDefinition tag) => BitConverter.Int32BitsToSingle(ConvertToInt32(words, tag));

    /// <summary>Executes the ConvertFromInt32 operation.</summary>
    /// <param name="value">The value parameter.</param>
    /// <param name="tag">The tag parameter.</param>
    /// <returns>The ConvertFromInt32 operation result.</returns>
    private ushort[] ConvertFromInt32(int value, MitsubishiTagDefinition tag)
    {
        var raw = unchecked((uint)value);
        return tag.ByteOrder == "BigEndian" ? [unchecked((ushort)(raw >> 16)), unchecked((ushort)(raw & 0xFFFF))] : [unchecked((ushort)(raw & 0xFFFF)), unchecked((ushort)(raw >> 16))];
    }

    /// <summary>Executes the ConvertFromUInt32 operation.</summary>
    /// <param name="value">The value parameter.</param>
    /// <param name="tag">The tag parameter.</param>
    /// <returns>The ConvertFromUInt32 operation result.</returns>
    private ushort[] ConvertFromUInt32(uint value, MitsubishiTagDefinition tag) => tag.ByteOrder == "BigEndian" ? [unchecked((ushort)(value >> 16)), unchecked((ushort)(value & 0xFFFF))] : [unchecked((ushort)(value & 0xFFFF)), unchecked((ushort)(value >> 16))];

    /// <summary>Executes the ConvertFromFloat operation.</summary>
    /// <param name="value">The value parameter.</param>
    /// <param name="tag">The tag parameter.</param>
    /// <returns>The ConvertFromFloat operation result.</returns>
    private ushort[] ConvertFromFloat(float value, MitsubishiTagDefinition tag) => ConvertFromInt32(BitConverter.SingleToInt32Bits(value), tag);

    /// <summary>Executes the EnsureWordCount operation.</summary>
    /// <param name="words">The words parameter.</param>
    /// <param name="requiredCount">The requiredCount parameter.</param>
    /// <param name="tagName">The tagName parameter.</param>
    private void EnsureWordCount(ushort[] words, int requiredCount, string tagName)
    {
        if (words.Length >= requiredCount)
        {
            return;
        }

        throw new InvalidOperationException($"Tag '{tagName}' requires at least {requiredCount} word(s), but only {words.Length} were read.");
    }

    /// <summary>Executes the DecodeStringFromWords operation.</summary>
    /// <param name="words">The words parameter.</param>
    /// <param name="tag">The tag parameter.</param>
    /// <returns>The DecodeStringFromWords operation result.</returns>
    private string DecodeStringFromWords(ushort[] words, MitsubishiTagDefinition tag)
    {
        var bytes = new byte[words.Length * 2];
        for (var index = 0; index < words.Length; index++)
        {
            var span = bytes.AsSpan(index * 2, 2);
            if (tag.ByteOrder == "BigEndian")
            {
                BinaryPrimitives.WriteUInt16BigEndian(span, words[index]);
            }
            else
            {
                BinaryPrimitives.WriteUInt16LittleEndian(span, words[index]);
            }
        }

        return GetTextEncoding(tag).GetString(bytes).TrimEnd('\0');
    }

    /// <summary>Executes the EncodeStringWords operation.</summary>
    /// <param name="value">The value parameter.</param>
    /// <param name="wordLength">The wordLength parameter.</param>
    /// <param name="tag">The tag parameter.</param>
    /// <returns>The EncodeStringWords operation result.</returns>
    private ushort[] EncodeStringWords(string value, int wordLength, MitsubishiTagDefinition tag)
    {
        var bytes = GetTextEncoding(tag).GetBytes(value);
        var maxBytes = checked(wordLength * 2);
        if (bytes.Length > maxBytes)
        {
            throw new ArgumentException($"String length {bytes.Length} exceeds the requested PLC word capacity of {maxBytes} bytes.", nameof(value));
        }

        var padded = new byte[maxBytes];
        bytes.CopyTo(padded, 0);
        var words = new ushort[wordLength];
        for (var index = 0; index < wordLength; index++)
        {
            var span = padded.AsSpan(index * 2, 2);
            words[index] = tag.ByteOrder == "BigEndian" ? BinaryPrimitives.ReadUInt16BigEndian(span) : BinaryPrimitives.ReadUInt16LittleEndian(span);
        }

        return words;
    }

    /// <summary>Executes the GetRequiredTagDatabase operation.</summary>
    /// <returns>The GetRequiredTagDatabase operation result.</returns>
    private MitsubishiTagDatabase GetRequiredTagDatabase() => TagDatabase ?? throw new InvalidOperationException("TagDatabase must be assigned before tag-based APIs can be used.");

    /// <summary>Executes the ReadTagValueAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReadTagValueAsync operation result.</returns>
    private async Task<Responce<object?>> ReadTagValueAsync(string tagName, CancellationToken cancellationToken)
    {
        var tag = GetRequiredTag(tagName);
        return tag.DataType switch
        {
            "Bit" => await ConvertTagValueAsync(ReadBitsByTagAsync(tagName, 1, cancellationToken), static values => values[0]).ConfigureAwait(false),
            "String" => await ConvertTagValueAsync(ReadStringByTagAsync(tagName, cancellationToken), static value => value).ConfigureAwait(false),
            "Float" => await ConvertTagValueAsync(ReadFloatByTagAsync(tagName, cancellationToken), static value => (object?)value).ConfigureAwait(false),
            "DWord" or "UInt32" => await ConvertTagValueAsync(ReadDWordByTagAsync(tagName, cancellationToken), static value => (object?)value).ConfigureAwait(false),
            "Int32" => await ConvertTagValueAsync(ReadInt32ByTagAsync(tagName, cancellationToken), static value => (object?)value).ConfigureAwait(false),
            "Int16" => await ConvertTagValueAsync(ReadInt16ByTagAsync(tagName, cancellationToken), static value => (object?)value).ConfigureAwait(false),
            "UInt16" => await ConvertTagValueAsync(ReadUInt16ByTagAsync(tagName, cancellationToken), static value => (object?)value).ConfigureAwait(false),
            _ when HasEngineeringMetadata(tag) => await ConvertTagValueAsync(ReadScaledDoubleByTagAsync(tagName, cancellationToken), static value => (object?)value).ConfigureAwait(false),
            _ => await ConvertTagValueAsync(ReadWordsByTagAsync(tagName, 1, cancellationToken), static values => (object?)values[0]).ConfigureAwait(false),
        };
    }

    /// <summary>Executes the WriteTagValueAsync operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The WriteTagValueAsync operation result.</returns>
    private async Task<Responce> WriteTagValueAsync(string tagName, object? value, CancellationToken cancellationToken)
    {
        var tag = GetRequiredTag(tagName);
        try
        {
            var writeTask = CreateWriteTagValueTask(tagName, tag, value, cancellationToken);
            if (writeTask is not null)
            {
                return await writeTask.ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            return new Responce().Fail(ex.Message, exception: ex);
        }

        return new Responce().Fail($"Value for tag '{tagName}' is not compatible with DataType '{tag.DataType ?? "Word"}'.");
    }

    /// <summary>Executes the CreateWriteTagValueTask operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="tag">The tag parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The CreateWriteTagValueTask operation result.</returns>
    private Task<Responce>? CreateWriteTagValueTask(string tagName, MitsubishiTagDefinition tag, object? value, CancellationToken cancellationToken) => tag.DataType switch
    {
        "Bit" => CreateBitWriteTask(tagName, value, cancellationToken),
        "String" => CreateStringWriteTask(tagName, value, cancellationToken),
        "Float" => CreateFloatWriteTask(tagName, value, cancellationToken),
        "DWord" or "UInt32" => CreateDWordWriteTask(tagName, value, cancellationToken),
        "Int32" => CreateInt32WriteTask(tagName, value, cancellationToken),
        "Int16" => CreateInt16WriteTask(tagName, value, cancellationToken),
        "UInt16" => CreateUInt16WriteTask(tagName, value, cancellationToken),
        _ => CreateDefaultWriteTask(tagName, tag, value, cancellationToken),
    };

    /// <summary>Executes the CreateBitWriteTask operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The CreateBitWriteTask operation result.</returns>
    private Task<Responce>? CreateBitWriteTask(string tagName, object? value, CancellationToken cancellationToken) => value is bool bit ? WriteBitsByTagAsync(tagName, [bit], cancellationToken) : null;

    /// <summary>Executes the CreateStringWriteTask operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The CreateStringWriteTask operation result.</returns>
    private Task<Responce>? CreateStringWriteTask(string tagName, object? value, CancellationToken cancellationToken) => value is string text ? WriteStringByTagAsync(tagName, text, cancellationToken) : null;

    /// <summary>Executes the CreateFloatWriteTask operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The CreateFloatWriteTask operation result.</returns>
    private Task<Responce>? CreateFloatWriteTask(string tagName, object? value, CancellationToken cancellationToken) => value is float single ? WriteFloatByTagAsync(tagName, single, cancellationToken) : null;

    /// <summary>Executes the CreateDWordWriteTask operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The CreateDWordWriteTask operation result.</returns>
    private Task<Responce>? CreateDWordWriteTask(string tagName, object? value, CancellationToken cancellationToken) => value is uint uint32 ? WriteDWordByTagAsync(tagName, uint32, cancellationToken) : null;

    /// <summary>Executes the CreateInt32WriteTask operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The CreateInt32WriteTask operation result.</returns>
    private Task<Responce>? CreateInt32WriteTask(string tagName, object? value, CancellationToken cancellationToken) => value is int int32 ? WriteInt32ByTagAsync(tagName, int32, cancellationToken) : null;

    /// <summary>Executes the CreateInt16WriteTask operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The CreateInt16WriteTask operation result.</returns>
    private Task<Responce>? CreateInt16WriteTask(string tagName, object? value, CancellationToken cancellationToken) => value is short int16 ? WriteInt16ByTagAsync(tagName, int16, cancellationToken) : null;

    /// <summary>Executes the CreateUInt16WriteTask operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The CreateUInt16WriteTask operation result.</returns>
    private Task<Responce>? CreateUInt16WriteTask(string tagName, object? value, CancellationToken cancellationToken) => value is ushort uint16 ? WriteUInt16ByTagAsync(tagName, uint16, cancellationToken) : null;

    /// <summary>Executes the CreateDefaultWriteTask operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <param name="tag">The tag parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The CreateDefaultWriteTask operation result.</returns>
    private Task<Responce>? CreateDefaultWriteTask(string tagName, MitsubishiTagDefinition tag, object? value, CancellationToken cancellationToken)
    {
        if (HasEngineeringMetadata(tag) && value is double engineering)
        {
            return WriteScaledDoubleByTagAsync(tagName, engineering, cancellationToken);
        }

        return value is ushort rawWord ? WriteWordsByTagAsync(tagName, [rawWord], cancellationToken) : null;
    }

    /// <summary>Executes the CanWriteTagValue operation.</summary>
    /// <param name="tag">The tag parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="error">The error parameter.</param>
    /// <returns>The CanWriteTagValue operation result.</returns>
    private bool CanWriteTagValue(MitsubishiTagDefinition tag, object? value, out string? error)
    {
        if (value is null)
        {
            error = $"Tag '{tag.Name}' cannot be written with a null value.";
            return false;
        }

        var ok = tag.DataType switch
        {
            "Bit" => value is bool,
            "String" => value is string,
            "Float" => value is float,
            "DWord" or "UInt32" => value is uint,
            "Int32" => value is int,
            "Int16" => value is short,
            "UInt16" => value is ushort,
            _ when HasEngineeringMetadata(tag) => value is double,
            _ => value is ushort,
        };
        error = ok ? null : $"Tag '{tag.Name}' expects a value compatible with DataType '{tag.DataType ?? "Word"}', but received '{value.GetType().Name}'.";
        return ok;
    }

    /// <summary>Executes the ConvertTagValueAsync operation.</summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="task">The task parameter.</param>
    /// <param name="projector">The projector parameter.</param>
    /// <returns>The ConvertTagValueAsync operation result.</returns>
    private async Task<Responce<object?>> ConvertTagValueAsync<T>(Task<Responce<T>> task, Func<T, object?> projector)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(projector);
        var raw = await task.ConfigureAwait(false);
        return !raw.IsSucceed || raw.Value is null ? new Responce<object?>(raw) : new Responce<object?>(raw, projector(raw.Value));
    }

    /// <summary>Executes the HasEngineeringMetadata operation.</summary>
    /// <param name="tag">The tag parameter.</param>
    /// <returns>The HasEngineeringMetadata operation result.</returns>
    private bool HasEngineeringMetadata(MitsubishiTagDefinition tag) => Math.Abs(tag.Scale - 1.0) > double.Epsilon || Math.Abs(tag.Offset) > double.Epsilon;

    /// <summary>Executes the ValidateRolloutPolicy operation.</summary>
    /// <param name="diff">The diff parameter.</param>
    /// <param name="policy">The policy parameter.</param>
    /// <returns>The ValidateRolloutPolicy operation result.</returns>
    private Responce ValidateRolloutPolicy(MitsubishiTagDatabaseDiff diff, MitsubishiTagRolloutPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(diff);
        if (policy == MitsubishiTagRolloutPolicy.AllowAll)
        {
            return new Responce().EndTime();
        }

        if (policy == MitsubishiTagRolloutPolicy.SafeMetadataAndGroups)
        {
            var disallowed = diff.ChangeKinds & (MitsubishiSchemaChangeKind.AddressChange | MitsubishiSchemaChangeKind.DataTypeChange | MitsubishiSchemaChangeKind.StructureChange);
            return disallowed == MitsubishiSchemaChangeKind.None ? new Responce().EndTime() : new Responce().Fail($"Rollout policy '{policy}' rejected schema changes: {disallowed}.");
        }

        return new Responce().Fail($"Unsupported rollout policy '{policy}'.");
    }

    /// <summary>Executes the GetSchemaFingerprint operation.</summary>
    /// <param name="path">The path parameter.</param>
    /// <returns>The GetSchemaFingerprint operation result.</returns>
    private string GetSchemaFingerprint(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists)
        {
            return $"{info.FullName}|missing";
        }

        var content = File.ReadAllBytes(path);
        var hash = Convert.ToHexString(SHA256.HashData(content));
        return $"{info.FullName}|{content.Length}|{hash}";
    }

    /// <summary>Executes the ResolveTagAddress operation.</summary>
    /// <param name="tagName">The tagName parameter.</param>
    /// <returns>The ResolveTagAddress operation result.</returns>
    private string ResolveTagAddress(string tagName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);
        var database = TagDatabase ?? throw new InvalidOperationException("TagDatabase must be assigned before tag-based APIs can be used.");
        return database.GetRequired(tagName).Address;
    }

    /// <summary>Executes the ResolveTagAddresses operation.</summary>
    /// <param name="tagNames">The tagNames parameter.</param>
    /// <returns>The ResolveTagAddresses operation result.</returns>
    private IReadOnlyList<string> ResolveTagAddresses(IEnumerable<string> tagNames)
    {
        ArgumentNullException.ThrowIfNull(tagNames);
        return tagNames.Select(ResolveTagAddress).ToArray();
    }

    /// <summary>Executes the PublishFault operation.</summary>
    /// <param name="description">The description parameter.</param>
    /// <param name="request">The request parameter.</param>
    /// <param name="response">The response parameter.</param>
    /// <param name="exception">The exception parameter.</param>
    private void PublishFault(string description, ReadOnlyMemory<byte> request, ReadOnlyMemory<byte> response, Exception exception)
    {
        PublishState(MitsubishiConnectionState.Faulted);
        PublishOperation(description, false, request, response, exception);
    }

    /// <summary>Executes the PublishOperation operation.</summary>
    /// <param name="description">The description parameter.</param>
    /// <param name="success">The success parameter.</param>
    /// <param name="request">The request parameter.</param>
    /// <param name="response">The response parameter.</param>
    /// <param name="exception">The exception parameter.</param>
    private void PublishOperation(string description, bool success, ReadOnlyMemory<byte> request, ReadOnlyMemory<byte> response, Exception? exception = null)
    {
        _operationLogs.OnNext(new MitsubishiOperationLog(DateTimeOffset.UtcNow, _connectionStates.Value, description, success, request, response, exception));
    }

    /// <summary>Executes the PublishState operation.</summary>
    /// <param name="state">The state parameter.</param>
    private void PublishState(MitsubishiConnectionState state)
    {
        if (_connectionStates.Value == state)
        {
            return;
        }

        _connectionStates.OnNext(state);
    }

    /// <summary>Executes the ThrowIfDisposed operation.</summary>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
