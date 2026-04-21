// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Text;
using ReactiveUI.Extensions;

namespace MitsubishiRx;

/// <summary>
/// Reactive Mitsubishi Ethernet client supporting 1E, 3E, and 4E MC protocol / SLMP communication.
/// </summary>
public sealed class MitsubishiRx : IDisposable, IAsyncDisposable
{
    private readonly BehaviorSubject<MitsubishiConnectionState> _connectionStates = new(MitsubishiConnectionState.Disconnected);
    private readonly Subject<MitsubishiOperationLog> _operationLogs = new();
    private readonly SemaphoreSlim _requestGate = new(1, 1);
    private readonly IMitsubishiTransport _transport;
    private readonly IScheduler _scheduler;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MitsubishiRx"/> class using the legacy constructor shape.
    /// </summary>
    /// <param name="cpuType">CPU family hint.</param>
    /// <param name="ip">Target PLC IP address or host name.</param>
    /// <param name="port">Configured Ethernet port.</param>
    /// <param name="timeout">Transport timeout in milliseconds.</param>
    public MitsubishiRx(CpuType cpuType, string ip, int port, int timeout = 1500)
        : this(new MitsubishiClientOptions(
            ip,
            port,
            cpuType is CpuType.ASeries or CpuType.Fx3 ? MitsubishiFrameType.OneE : MitsubishiFrameType.ThreeE,
            CommunicationDataCode.Binary,
            MitsubishiTransportKind.Tcp,
            Timeout: TimeSpan.FromMilliseconds(timeout),
            CpuType: cpuType))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MitsubishiRx"/> class.
    /// </summary>
    /// <param name="options">Client options.</param>
    /// <param name="transport">Optional transport override.</param>
    /// <param name="scheduler">Optional scheduler override for reactive streams.</param>
    public MitsubishiRx(MitsubishiClientOptions options, IMitsubishiTransport? transport = null, IScheduler? scheduler = null)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        _transport = transport ?? CreateDefaultTransport(options);
        _scheduler = scheduler ?? Scheduler.Default;
        if (options.TransportKind != MitsubishiTransportKind.Serial)
        {
            _ = BuildEndPoint(options);
        }
    }

    /// <summary>
    /// Gets the configured options.
    /// </summary>
    public MitsubishiClientOptions Options { get; }

    /// <summary>
    /// Gets or sets the optional symbolic tag database used to resolve human-friendly tag names.
    /// </summary>
    public MitsubishiTagDatabase? TagDatabase { get; set; }

    /// <summary>
    /// Reads word devices using a configured tag name instead of a raw PLC address.
    /// </summary>
    /// <param name="tagName">Configured tag name.</param>
    /// <param name="points">Number of words to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Read words.</returns>
    public Task<Responce<ushort[]>> ReadWordsByTagAsync(string tagName, int points, CancellationToken cancellationToken = default)
        => ReadWordsAsync(ResolveTagAddress(tagName), points, cancellationToken);

    /// <summary>
    /// Reads bit devices using a configured tag name instead of a raw PLC address.
    /// </summary>
    /// <param name="tagName">Configured tag name.</param>
    /// <param name="points">Number of bits to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Read bit values.</returns>
    public Task<Responce<bool[]>> ReadBitsByTagAsync(string tagName, int points, CancellationToken cancellationToken = default)
        => ReadBitsAsync(ResolveTagAddress(tagName), points, cancellationToken);

    /// <summary>
    /// Writes word devices using a configured tag name instead of a raw PLC address.
    /// </summary>
    /// <param name="tagName">Configured tag name.</param>
    /// <param name="values">Values to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Operation result.</returns>
    public Task<Responce> WriteWordsByTagAsync(string tagName, IReadOnlyList<ushort> values, CancellationToken cancellationToken = default)
        => WriteWordsAsync(ResolveTagAddress(tagName), values, cancellationToken);

    /// <summary>
    /// Writes bit devices using a configured tag name instead of a raw PLC address.
    /// </summary>
    /// <param name="tagName">Configured tag name.</param>
    /// <param name="values">Bit values to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Operation result.</returns>
    public Task<Responce> WriteBitsByTagAsync(string tagName, IReadOnlyList<bool> values, CancellationToken cancellationToken = default)
        => WriteBitsAsync(ResolveTagAddress(tagName), values, cancellationToken);

    /// <summary>
    /// Executes a random word read using configured tag names instead of raw PLC addresses.
    /// </summary>
    /// <param name="tagNames">Configured tag names in request order.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Read values in tag order.</returns>
    public Task<Responce<ushort[]>> RandomReadWordsByTagAsync(IEnumerable<string> tagNames, CancellationToken cancellationToken = default)
        => RandomReadWordsAsync(ResolveTagAddresses(tagNames), cancellationToken);

    /// <summary>
    /// Executes a random word write using configured tag names instead of raw PLC addresses.
    /// </summary>
    /// <param name="values">Tag/value pairs keyed by configured tag name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Operation result.</returns>
    public Task<Responce> RandomWriteWordsByTagAsync(IEnumerable<KeyValuePair<string, ushort>> values, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        return RandomWriteWordsAsync(values.Select(pair => new KeyValuePair<string, ushort>(ResolveTagAddress(pair.Key), pair.Value)), cancellationToken);
    }

    /// <summary>
    /// Reads a signed 16-bit integer using a configured tag name.
    /// </summary>
    /// <param name="tagName">Configured tag name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Read signed 16-bit value.</returns>
    public async Task<Responce<short>> ReadInt16ByTagAsync(string tagName, CancellationToken cancellationToken = default)
    {
        var raw = await ReadWordsByTagAsync(tagName, 1, cancellationToken).ConfigureAwait(false);
        return ConvertWords(raw, words => unchecked((short)words[0]));
    }

    /// <summary>
    /// Writes a signed 16-bit integer using a configured tag name.
    /// </summary>
    /// <param name="tagName">Configured tag name.</param>
    /// <param name="value">Value to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Operation result.</returns>
    public Task<Responce> WriteInt16ByTagAsync(string tagName, short value, CancellationToken cancellationToken = default)
        => WriteWordsByTagAsync(tagName, [unchecked((ushort)value)], cancellationToken);

    /// <summary>
    /// Reads an unsigned 16-bit integer using a configured tag name.
    /// </summary>
    /// <param name="tagName">Configured tag name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Read unsigned 16-bit value.</returns>
    public async Task<Responce<ushort>> ReadUInt16ByTagAsync(string tagName, CancellationToken cancellationToken = default)
    {
        var raw = await ReadWordsByTagAsync(tagName, 1, cancellationToken).ConfigureAwait(false);
        return ConvertWords(raw, words => words[0]);
    }

    /// <summary>
    /// Writes an unsigned 16-bit integer using a configured tag name.
    /// </summary>
    /// <param name="tagName">Configured tag name.</param>
    /// <param name="value">Value to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Operation result.</returns>
    public Task<Responce> WriteUInt16ByTagAsync(string tagName, ushort value, CancellationToken cancellationToken = default)
        => WriteWordsByTagAsync(tagName, [value], cancellationToken);

    /// <summary>
    /// Reads a signed 32-bit integer using a configured tag name.
    /// </summary>
    /// <param name="tagName">Configured tag name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Read signed 32-bit value.</returns>
    public async Task<Responce<int>> ReadInt32ByTagAsync(string tagName, CancellationToken cancellationToken = default)
    {
        var tag = GetRequiredTag(tagName);
        var raw = await ReadWordsByTagAsync(tagName, 2, cancellationToken).ConfigureAwait(false);
        return ConvertWords(raw, words => ConvertToInt32(words, tag));
    }

    /// <summary>
    /// Writes a signed 32-bit integer using a configured tag name.
    /// </summary>
    /// <param name="tagName">Configured tag name.</param>
    /// <param name="value">Value to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Operation result.</returns>
    public Task<Responce> WriteInt32ByTagAsync(string tagName, int value, CancellationToken cancellationToken = default)
    {
        var tag = GetRequiredTag(tagName);
        return WriteWordsByTagAsync(tagName, ConvertFromInt32(value, tag), cancellationToken);
    }

    /// <summary>
    /// Reads a 32-bit unsigned value using a configured tag name.
    /// </summary>
    /// <param name="tagName">Configured tag name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Read double-word value.</returns>
    public async Task<Responce<uint>> ReadDWordByTagAsync(string tagName, CancellationToken cancellationToken = default)
    {
        var tag = GetRequiredTag(tagName);
        var raw = await ReadWordsByTagAsync(tagName, 2, cancellationToken).ConfigureAwait(false);
        return ConvertWords(raw, words => ConvertToUInt32(words, tag));
    }

    /// <summary>
    /// Writes a 32-bit unsigned value using a configured tag name.
    /// </summary>
    /// <param name="tagName">Configured tag name.</param>
    /// <param name="value">Value to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Operation result.</returns>
    public Task<Responce> WriteDWordByTagAsync(string tagName, uint value, CancellationToken cancellationToken = default)
    {
        var tag = GetRequiredTag(tagName);
        return WriteWordsByTagAsync(tagName, ConvertFromUInt32(value, tag), cancellationToken);
    }

    /// <summary>
    /// Reads a 32-bit single-precision floating-point value using a configured tag name.
    /// </summary>
    /// <param name="tagName">Configured tag name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Read floating-point value.</returns>
    public async Task<Responce<float>> ReadFloatByTagAsync(string tagName, CancellationToken cancellationToken = default)
    {
        var tag = GetRequiredTag(tagName);
        var raw = await ReadWordsByTagAsync(tagName, 2, cancellationToken).ConfigureAwait(false);
        return ConvertWords(raw, words => ConvertToFloat(words, tag));
    }

    /// <summary>
    /// Writes a 32-bit single-precision floating-point value using a configured tag name.
    /// </summary>
    /// <param name="tagName">Configured tag name.</param>
    /// <param name="value">Value to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Operation result.</returns>
    public Task<Responce> WriteFloatByTagAsync(string tagName, float value, CancellationToken cancellationToken = default)
    {
        var tag = GetRequiredTag(tagName);
        return WriteWordsByTagAsync(tagName, ConvertFromFloat(value, tag), cancellationToken);
    }

    /// <summary>
    /// Reads a scaled engineering value using the configured tag Scale and Offset metadata.
    /// </summary>
    /// <param name="tagName">Configured tag name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Scaled engineering value.</returns>
    public async Task<Responce<double>> ReadScaledDoubleByTagAsync(string tagName, CancellationToken cancellationToken = default)
    {
        var tag = GetRequiredTag(tagName);
        var wordCount = GetWordCountForScaledRead(tag);
        var raw = await ReadWordsByTagAsync(tagName, wordCount, cancellationToken).ConfigureAwait(false);
        return ConvertWords(raw, words => ApplyScaleAndOffset(ReadNumericTagValue(tag, words), tag));
    }

    /// <summary>
    /// Writes a scaled engineering value using the configured tag Scale and Offset metadata.
    /// </summary>
    /// <param name="tagName">Configured tag name.</param>
    /// <param name="value">Engineering value to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Operation result.</returns>
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

    /// <summary>
    /// Reads a PLC string stored across word devices using packed text bytes and the tag-configured length when available.
    /// </summary>
    /// <param name="tagName">Configured tag name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Decoded string.</returns>
    public Task<Responce<string>> ReadStringByTagAsync(string tagName, CancellationToken cancellationToken = default)
    {
        var tag = GetRequiredTag(tagName);
        var wordLength = tag.Length ?? throw new InvalidOperationException($"Tag '{tagName}' must define Length before ReadStringByTagAsync(tagName) can be used.");
        return ReadStringByTagAsync(tagName, wordLength, cancellationToken);
    }

    /// <summary>
    /// Reads a PLC string stored across word devices using packed ASCII bytes.
    /// </summary>
    /// <param name="tagName">Configured tag name.</param>
    /// <param name="wordLength">Number of PLC words to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Decoded string.</returns>
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

    /// <summary>
    /// Writes a PLC string across word devices using the tag-configured length when available.
    /// </summary>
    /// <param name="tagName">Configured tag name.</param>
    /// <param name="value">String to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Operation result.</returns>
    public Task<Responce> WriteStringByTagAsync(string tagName, string value, CancellationToken cancellationToken = default)
    {
        var tag = GetRequiredTag(tagName);
        var wordLength = tag.Length ?? throw new InvalidOperationException($"Tag '{tagName}' must define Length before WriteStringByTagAsync(tagName, value) can be used.");
        return WriteStringByTagAsync(tagName, value, wordLength, cancellationToken);
    }

    /// <summary>
    /// Writes a PLC string across word devices using packed ASCII bytes.
    /// </summary>
    /// <param name="tagName">Configured tag name.</param>
    /// <param name="value">String to write.</param>
    /// <param name="wordLength">Number of PLC words to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Operation result.</returns>
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

    /// <summary>
    /// Validates the configured tag database and reports tag/group schema issues.
    /// </summary>
    /// <returns>Validation result.</returns>
    public Responce ValidateTagDatabase()
        => ValidateTagDatabase(TagDatabase);

    /// <summary>
    /// Loads a tag database from a schema file, validates it against the current client options, and assigns it when valid.
    /// </summary>
    /// <param name="path">Schema file path.</param>
    /// <returns>Validation result with the loaded database when successful.</returns>
    public Responce<MitsubishiTagDatabase> LoadAndValidateTagDatabase(string path)
        => LoadAndValidateTagDatabase(path, MitsubishiTagRolloutPolicy.AllowAll);

    /// <summary>
    /// Loads a tag database from a schema file, validates it, enforces rollout policy, and assigns it when valid.
    /// </summary>
    /// <param name="path">Schema file path.</param>
    /// <param name="policy">Rollout policy.</param>
    /// <returns>Validation result with the loaded database when successful.</returns>
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

            var diff = (TagDatabase ?? new MitsubishiTagDatabase(Array.Empty<MitsubishiTagDefinition>())).CompareWith(database);
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

    /// <summary>
    /// Loads a tag database from a schema file, validates it, and compares it to the current active schema without applying it.
    /// </summary>
    /// <param name="path">Schema file path.</param>
    /// <returns>Semantic schema diff.</returns>
    public Responce<MitsubishiTagDatabaseDiff> PreviewTagDatabaseDiff(string path)
        => PreviewTagDatabaseDiff(path, MitsubishiTagRolloutPolicy.AllowAll);

    /// <summary>
    /// Loads a tag database from a schema file, validates it, compares it to the current active schema, and evaluates rollout policy without applying it.
    /// </summary>
    /// <param name="path">Schema file path.</param>
    /// <param name="policy">Rollout policy.</param>
    /// <returns>Semantic schema diff.</returns>
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

            var diff = (TagDatabase ?? new MitsubishiTagDatabase(Array.Empty<MitsubishiTagDefinition>())).CompareWith(database);
            var policyResult = ValidateRolloutPolicy(diff, policy);
            return new Responce<MitsubishiTagDatabaseDiff>(policyResult, diff);
        }
        catch (Exception ex)
        {
            return new Responce<MitsubishiTagDatabaseDiff>().Fail(ex.Message, exception: ex);
        }
    }

    /// <summary>
    /// Periodically reloads a schema file, validates it, and emits semantic diffs against the last active schema.
    /// Successful reloads replace <see cref="TagDatabase"/>; failed reloads leave the current database unchanged.
    /// </summary>
    /// <param name="path">Schema file path.</param>
    /// <param name="pollInterval">Polling interval for change detection.</param>
    /// <param name="emitInitial">If true, emits immediately before waiting for the first interval.</param>
    /// <returns>Observable diff results.</returns>
    public IObservable<Responce<MitsubishiTagDatabaseDiff>> ObserveTagDatabaseDiff(string path, TimeSpan pollInterval, bool emitInitial = true)
        => ObserveTagDatabaseDiff(path, pollInterval, emitInitial, MitsubishiTagRolloutPolicy.AllowAll);

    /// <summary>
    /// Periodically reloads a schema file, validates it, and emits semantic diffs against the last active schema.
    /// Successful reloads replace <see cref="TagDatabase"/> when permitted by the rollout policy.
    /// </summary>
    /// <param name="path">Schema file path.</param>
    /// <param name="pollInterval">Polling interval for change detection.</param>
    /// <param name="emitInitial">If true, emits immediately before waiting for the first interval.</param>
    /// <param name="policy">Rollout policy.</param>
    /// <returns>Observable diff results.</returns>
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

        return trigger
            .Select(_ => GetSchemaFingerprint(path))
            .Where(fingerprint => !string.Equals(fingerprint, lastFingerprint, StringComparison.Ordinal))
            .Do(fingerprint => lastFingerprint = fingerprint)
            .Select(_ =>
            {
                var preview = PreviewTagDatabaseDiff(path, policy);
                if (!preview.IsSucceed)
                {
                    return preview;
                }

                var load = LoadAndValidateTagDatabase(path, policy);
                return load.IsSucceed && preview.Value is not null
                    ? new Responce<MitsubishiTagDatabaseDiff>(load, preview.Value)
                    : new Responce<MitsubishiTagDatabaseDiff>(preview);
            })
            .DoOnSubscribe(() => PublishOperation($"Observe tag database diff {path} subscribed", true, Array.Empty<byte>(), Array.Empty<byte>()))
            .DoOnDispose(() => PublishOperation($"Observe tag database diff {path} disposed", true, Array.Empty<byte>(), Array.Empty<byte>()));
    }

    /// <summary>
    /// Periodically reloads a schema file, validates it, and emits the latest load result.
    /// Successful reloads replace <see cref="TagDatabase"/>; failed reloads leave the current database unchanged.
    /// </summary>
    /// <param name="path">Schema file path.</param>
    /// <param name="pollInterval">Polling interval for change detection.</param>
    /// <param name="emitInitial">If true, emits immediately before waiting for the first interval.</param>
    /// <returns>Observable reload results.</returns>
    public IObservable<Responce<MitsubishiTagDatabase>> ObserveTagDatabaseReload(string path, TimeSpan pollInterval, bool emitInitial = true)
        => ObserveTagDatabaseReload(path, pollInterval, emitInitial, MitsubishiTagRolloutPolicy.AllowAll);

    /// <summary>
    /// Periodically reloads a schema file, validates it, and emits the latest load result subject to rollout policy.
    /// Successful reloads replace <see cref="TagDatabase"/> when permitted; failed reloads leave the current database unchanged.
    /// </summary>
    /// <param name="path">Schema file path.</param>
    /// <param name="pollInterval">Polling interval for change detection.</param>
    /// <param name="emitInitial">If true, emits immediately before waiting for the first interval.</param>
    /// <param name="policy">Rollout policy.</param>
    /// <returns>Observable reload results.</returns>
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

        return trigger
            .Select(_ => GetSchemaFingerprint(path))
            .Where(fingerprint => !string.Equals(fingerprint, lastFingerprint, StringComparison.Ordinal))
            .Do(fingerprint => lastFingerprint = fingerprint)
            .Select(_ => LoadAndValidateTagDatabase(path, policy))
            .DoOnSubscribe(() => PublishOperation($"Observe tag database reload {path} subscribed", true, Array.Empty<byte>(), Array.Empty<byte>()))
            .DoOnDispose(() => PublishOperation($"Observe tag database reload {path} disposed", true, Array.Empty<byte>(), Array.Empty<byte>()));
    }

    private Responce ValidateTagDatabase(MitsubishiTagDatabase? database)
    {
        var result = new Responce();
        if (database is null)
        {
            return result.Fail("TagDatabase must be assigned before validation can run.");
        }

        foreach (var tag in database.Tags)
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

            if (string.Equals(tag.DataType, "String", StringComparison.OrdinalIgnoreCase) && (!tag.Length.HasValue || tag.Length.Value <= 0))
            {
                result.IsSucceed = false;
                result.ErrList.Add($"Tag '{tag.Name}' uses DataType 'String' and must define a positive Length.");
            }
        }

        foreach (var group in database.Groups)
        {
            foreach (var tagName in group.ResolvedTagNames)
            {
                if (!database.TryGet(tagName, out _))
                {
                    result.IsSucceed = false;
                    result.ErrList.Add($"Group '{group.Name}' references unknown tag '{tagName}'.");
                }
            }
        }

        if (!result.IsSucceed && result.ErrList.Count > 0)
        {
            result.Err = string.Join(Environment.NewLine, result.ErrList);
        }

        return result.EndTime();
    }

    /// <summary>
    /// Reads a heterogeneous snapshot for all tags in a configured group.
    /// </summary>
    /// <param name="groupName">Configured group name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Grouped snapshot values.</returns>
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

    /// <summary>
    /// Validates a pending grouped write payload against the configured tag schema.
    /// </summary>
    /// <param name="groupName">Configured group name.</param>
    /// <param name="values">Pending values keyed by tag name.</param>
    /// <returns>Validation result.</returns>
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

    /// <summary>
    /// Writes the provided subset of values for a configured group.
    /// </summary>
    /// <param name="groupName">Configured group name.</param>
    /// <param name="values">Values keyed by tag name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Operation result.</returns>
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

    /// <summary>
    /// Writes all values contained in a grouped snapshot.
    /// </summary>
    /// <param name="snapshot">Grouped snapshot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Operation result.</returns>
    public Task<Responce> WriteTagGroupSnapshotAsync(MitsubishiTagGroupSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return WriteTagGroupValuesAsync(snapshot.GroupName, new Dictionary<string, object?>(snapshot.Values, StringComparer.OrdinalIgnoreCase), cancellationToken);
    }

    /// <summary>
    /// Gets a value indicating whether the underlying transport is connected.
    /// </summary>
    public bool Connected => _transport.IsConnected;

    /// <summary>
    /// Gets a reactive stream of connection state transitions.
    /// </summary>
    public IObservable<MitsubishiConnectionState> ConnectionStates => _connectionStates.AsObservable().DistinctUntilChanged();

    /// <summary>
    /// Gets a reactive stream of operation logs.
    /// </summary>
    public IObservable<MitsubishiOperationLog> OperationLogs => _operationLogs.AsObservable();

    /// <summary>
    /// Opens the underlying transport.
    /// </summary>
    /// <returns>Operation result.</returns>
    public Responce Open() => OpenAsync().GetAwaiter().GetResult();

    /// <summary>
    /// Opens the underlying transport asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Operation result.</returns>
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

    /// <summary>
    /// Closes the underlying transport.
    /// </summary>
    /// <returns>Operation result.</returns>
    public Responce Close() => CloseAsync().GetAwaiter().GetResult();

    /// <summary>
    /// Closes the underlying transport asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Operation result.</returns>
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

    /// <summary>
    /// Sends an already encoded request and reads a fixed-size response.
    /// </summary>
    /// <param name="command">Encoded request bytes.</param>
    /// <param name="receiveCount">Expected response length.</param>
    /// <returns>Raw response bytes.</returns>
    public Responce<byte[]> SendPackage(byte[] command, int receiveCount)
        => ExecuteEncodedAsync(command, receiveCount, "Legacy raw package").GetAwaiter().GetResult();

    /// <summary>
    /// Sends an already encoded request and reads a frame-sized response.
    /// </summary>
    /// <param name="command">Encoded request bytes.</param>
    /// <returns>Raw response bytes.</returns>
    public Responce<byte[]> SendPackageSingle(byte[] command)
        => ExecuteEncodedAsync(command, null, "Legacy raw package single").GetAwaiter().GetResult();

    /// <summary>
    /// Sends an already encoded request and retries transient transport failures with backoff.
    /// </summary>
    /// <param name="command">Encoded request bytes.</param>
    /// <returns>Raw response bytes.</returns>
    public Responce<byte[]> SendPackageReliable(byte[] command)
        => ExecuteEncodedAsync(command, null, "Legacy raw package reliable", maxRetries: 2).GetAwaiter().GetResult();

    /// <summary>
    /// Executes a low-level MC protocol / SLMP command.
    /// </summary>
    /// <param name="request">Command request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Raw decoded response payload.</returns>
    public Task<Responce<byte[]>> ExecuteRawAsync(MitsubishiRawCommandRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteObservableAsync(
            () => Options.TransportKind == MitsubishiTransportKind.Serial
                ? throw new NotSupportedException("Raw serial execution is not yet implemented for 1C/3C/4C commands.")
                : MitsubishiProtocolEncoding.Encode(Options, request),
            Options.TransportKind == MitsubishiTransportKind.Serial
                ? null
                : MitsubishiProtocolEncoding.GetFixedResponseLength(Options.FrameType, Options.DataCode, request.Command, request.Subcommand, request.ResolvedBody.Count),
            request.Description ?? $"Command {request.Command:X4}",
            cancellationToken);
    }

    /// <summary>
    /// Reads word devices using batch access.
    /// </summary>
    /// <param name="address">Device address.</param>
    /// <param name="points">Number of words to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Read words.</returns>
    public async Task<Responce<ushort[]>> ReadWordsAsync(string address, int points, CancellationToken cancellationToken = default)
    {
        var parsed = MitsubishiDeviceAddress.Parse(address, Options.XyNotation);
        var raw = await ExecuteObservableAsync(
            () => Options.TransportKind == MitsubishiTransportKind.Serial
                ? MitsubishiSerialProtocolEncoding.EncodeWordReadRequest(Options, parsed, points)
                : MitsubishiProtocolEncoding.EncodeDeviceBatchRead(Options, parsed, points, bitUnits: false),
            Options.TransportKind == MitsubishiTransportKind.Serial
                ? null
                : Options.FrameType == MitsubishiFrameType.OneE ? 2 + (points * 2) : null,
            $"Read words {address}",
            cancellationToken).ConfigureAwait(false);
        return ParseWords(raw, Options.TransportKind == MitsubishiTransportKind.Serial ? points : null);
    }

    /// <summary>
    /// Writes word devices using batch access.
    /// </summary>
    /// <param name="address">Device address.</param>
    /// <param name="values">Values to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Operation result.</returns>
    public async Task<Responce> WriteWordsAsync(string address, IReadOnlyList<ushort> values, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        var parsed = MitsubishiDeviceAddress.Parse(address, Options.XyNotation);
        var raw = await ExecuteObservableAsync(
            () => MitsubishiProtocolEncoding.EncodeDeviceBatchWrite(Options, parsed, values.ToArray(), bitUnits: false),
            Options.FrameType == MitsubishiFrameType.OneE ? 2 : null,
            $"Write words {address}",
            cancellationToken).ConfigureAwait(false);
        return raw.ToBaseResponse();
    }

    /// <summary>
    /// Reads bit devices using batch access.
    /// </summary>
    /// <param name="address">Device address.</param>
    /// <param name="points">Number of bit points to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Read bit values.</returns>
    public async Task<Responce<bool[]>> ReadBitsAsync(string address, int points, CancellationToken cancellationToken = default)
    {
        var parsed = MitsubishiDeviceAddress.Parse(address, Options.XyNotation);
        int? expected = Options.FrameType == MitsubishiFrameType.OneE ? 2 + ((points + 1) / 2) : null;
        var raw = await ExecuteObservableAsync(
            () => MitsubishiProtocolEncoding.EncodeDeviceBatchRead(Options, parsed, points, bitUnits: true),
            expected,
            $"Read bits {address}",
            cancellationToken).ConfigureAwait(false);
        return ParseBits(raw, points);
    }

    /// <summary>
    /// Writes bit devices using batch access.
    /// </summary>
    /// <param name="address">Device address.</param>
    /// <param name="values">Bit values to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Operation result.</returns>
    public async Task<Responce> WriteBitsAsync(string address, IReadOnlyList<bool> values, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        var parsed = MitsubishiDeviceAddress.Parse(address, Options.XyNotation);
        var raw = await ExecuteObservableAsync(
            () => MitsubishiProtocolEncoding.EncodeDeviceBatchWrite(Options, parsed, values.Select(static v => v ? (ushort)1 : (ushort)0).ToArray(), bitUnits: true),
            Options.FrameType == MitsubishiFrameType.OneE ? 2 : null,
            $"Write bits {address}",
            cancellationToken).ConfigureAwait(false);
        return raw.ToBaseResponse();
    }

    /// <summary>
    /// Executes a random read for word devices.
    /// </summary>
    /// <param name="addresses">Addresses to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Read values in request order.</returns>
    public async Task<Responce<ushort[]>> RandomReadWordsAsync(IEnumerable<string> addresses, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        var parsed = addresses.Select(address => MitsubishiDeviceAddress.Parse(address, Options.XyNotation)).ToArray();
        var raw = await ExecuteObservableAsync(
            () => MitsubishiProtocolEncoding.EncodeRandomRead(Options, parsed),
            null,
            "Random read words",
            cancellationToken).ConfigureAwait(false);
        return ParseWords(raw);
    }

    /// <summary>
    /// Executes a random write for word devices.
    /// </summary>
    /// <param name="values">Values to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Operation result.</returns>
    public async Task<Responce> RandomWriteWordsAsync(IEnumerable<KeyValuePair<string, ushort>> values, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        var payload = values.Select(pair => new MitsubishiDeviceValue(MitsubishiDeviceAddress.Parse(pair.Key, Options.XyNotation), pair.Value)).ToArray();
        var raw = await ExecuteObservableAsync(
            () => MitsubishiProtocolEncoding.EncodeRandomWrite(Options, payload),
            null,
            "Random write words",
            cancellationToken).ConfigureAwait(false);
        return raw.ToBaseResponse();
    }

    /// <summary>
    /// Registers monitor devices for subsequent execute-monitor calls.
    /// </summary>
    /// <param name="addresses">Word device addresses to monitor.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Operation result.</returns>
    public async Task<Responce> RegisterMonitorAsync(IEnumerable<string> addresses, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        var payload = addresses.Select(address => MitsubishiDeviceAddress.Parse(address, Options.XyNotation)).ToArray();
        var raw = await ExecuteObservableAsync(
            () => MitsubishiProtocolEncoding.EncodeMonitorRegistration(Options, payload),
            null,
            "Register monitor",
            cancellationToken).ConfigureAwait(false);
        return raw.ToBaseResponse();
    }

    /// <summary>
    /// Executes a previously registered monitor operation.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Raw monitor payload.</returns>
    public Task<Responce<byte[]>> ExecuteMonitorAsync(CancellationToken cancellationToken = default)
        => ExecuteObservableAsync(
            () => MitsubishiProtocolEncoding.EncodeExecuteMonitor(Options),
            null,
            "Execute monitor",
            cancellationToken);

    /// <summary>
    /// Executes a block read request.
    /// </summary>
    /// <param name="request">Block request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Raw block response bytes.</returns>
    public Task<Responce<byte[]>> ReadBlocksAsync(MitsubishiBlockRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteObservableAsync(
            () => MitsubishiProtocolEncoding.EncodeBlockRead(Options, request),
            null,
            "Block read",
            cancellationToken);
    }

    /// <summary>
    /// Executes a block write request.
    /// </summary>
    /// <param name="request">Block request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Operation result.</returns>
    public async Task<Responce> WriteBlocksAsync(MitsubishiBlockRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var raw = await ExecuteObservableAsync(
            () => MitsubishiProtocolEncoding.EncodeBlockWrite(Options, request),
            null,
            "Block write",
            cancellationToken).ConfigureAwait(false);
        return raw.ToBaseResponse();
    }

    /// <summary>
    /// Reads the PLC type name and model code.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Type information.</returns>
    public async Task<Responce<MitsubishiTypeName>> ReadTypeNameAsync(CancellationToken cancellationToken = default)
    {
        var raw = await ExecuteObservableAsync(
            () => MitsubishiProtocolEncoding.EncodeReadTypeName(Options),
            null,
            "Read type name",
            cancellationToken).ConfigureAwait(false);
        return ParseTypeName(raw);
    }

    /// <summary>
    /// Performs a remote RUN request.
    /// </summary>
    /// <param name="force">Whether forced mode should be requested.</param>
    /// <param name="clearMode">Whether clear mode should be requested.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Operation result.</returns>
    public Task<Responce> RemoteRunAsync(bool force = true, bool clearMode = false, CancellationToken cancellationToken = default)
        => ExecuteControlAsync(MitsubishiCommands.RemoteRun, cancellationToken, force, clearMode);

    /// <summary>
    /// Performs a remote STOP request.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Operation result.</returns>
    public Task<Responce> RemoteStopAsync(CancellationToken cancellationToken = default)
        => ExecuteControlAsync(MitsubishiCommands.RemoteStop, cancellationToken);

    /// <summary>
    /// Performs a remote PAUSE request.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Operation result.</returns>
    public Task<Responce> RemotePauseAsync(CancellationToken cancellationToken = default)
        => ExecuteControlAsync(MitsubishiCommands.RemotePause, cancellationToken);

    /// <summary>
    /// Performs a remote latch clear request.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Operation result.</returns>
    public Task<Responce> RemoteLatchClearAsync(CancellationToken cancellationToken = default)
        => ExecuteControlAsync(MitsubishiCommands.RemoteLatchClear, cancellationToken);

    /// <summary>
    /// Performs a remote reset request.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Operation result.</returns>
    public Task<Responce> RemoteResetAsync(CancellationToken cancellationToken = default)
        => ExecuteControlAsync(MitsubishiCommands.RemoteReset, cancellationToken);

    /// <summary>
    /// Unlocks the PLC using the configured remote password facility.
    /// </summary>
    /// <param name="password">Remote password.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Operation result.</returns>
    public Task<Responce> UnlockAsync(string password, CancellationToken cancellationToken = default)
        => ExecutePasswordAsync(MitsubishiCommands.Unlock, password, cancellationToken);

    /// <summary>
    /// Locks the PLC using the configured remote password facility.
    /// </summary>
    /// <param name="password">Remote password.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Operation result.</returns>
    public Task<Responce> LockAsync(string password, CancellationToken cancellationToken = default)
        => ExecutePasswordAsync(MitsubishiCommands.Lock, password, cancellationToken);

    /// <summary>
    /// Clears the error state on the target PLC or Ethernet module.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Operation result.</returns>
    public async Task<Responce> ClearErrorAsync(CancellationToken cancellationToken = default)
    {
        var raw = await ExecuteObservableAsync(
            () => MitsubishiProtocolEncoding.Encode(Options, new MitsubishiRawCommandRequest(MitsubishiCommands.ClearError, 0x0000, Array.Empty<byte>(), "Clear error")),
            null,
            "Clear error",
            cancellationToken).ConfigureAwait(false);
        return raw.ToBaseResponse();
    }

    /// <summary>
    /// Executes a loopback self-test against the PLC or Ethernet module.
    /// </summary>
    /// <param name="data">Loopback data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Looped-back payload.</returns>
    public async Task<Responce<byte[]>> LoopbackAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        int? expected = Options.FrameType == MitsubishiFrameType.OneE ? 4 + data.Length : null;
        var raw = await ExecuteObservableAsync(
            () => MitsubishiProtocolEncoding.EncodeLoopback(Options, data),
            expected,
            "Loopback",
            cancellationToken).ConfigureAwait(false);
        return raw;
    }

    /// <summary>
    /// Reads raw memory / buffer data from the own station or an intelligent module.
    /// </summary>
    /// <param name="command">Read command to execute.</param>
    /// <param name="address">Start address.</param>
    /// <param name="length">Number of words.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Read words.</returns>
    public async Task<Responce<ushort[]>> ReadMemoryAsync(ushort command, ushort address, int length, CancellationToken cancellationToken = default)
    {
        var raw = await ExecuteObservableAsync(
            () => MitsubishiProtocolEncoding.EncodeMemoryAccess(Options, command, address, length, Array.Empty<ushort>()),
            null,
            $"Read memory {command:X4}",
            cancellationToken).ConfigureAwait(false);
        return ParseWords(raw, expectedWordCount: length);
    }

    /// <summary>
    /// Writes raw memory / buffer data to the own station or an intelligent module.
    /// </summary>
    /// <param name="command">Write command to execute.</param>
    /// <param name="address">Start address.</param>
    /// <param name="values">Values to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Operation result.</returns>
    public async Task<Responce> WriteMemoryAsync(ushort command, ushort address, IReadOnlyList<ushort> values, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        var raw = await ExecuteObservableAsync(
            () => MitsubishiProtocolEncoding.EncodeMemoryAccess(Options, command, address, values.Count, values.ToArray()),
            null,
            $"Write memory {command:X4}",
            cancellationToken).ConfigureAwait(false);
        return raw.ToBaseResponse();
    }

    /// <summary>
    /// Continuously polls a word range and emits sequential results.
    /// </summary>
    /// <param name="address">Device address.</param>
    /// <param name="points">Number of words.</param>
    /// <param name="pollInterval">Polling interval.</param>
    /// <param name="minimumUpdateSpacing">Minimum spacing between notifications.</param>
    /// <param name="pollTimeout">Per-poll timeout.</param>
    /// <returns>Observable read results.</returns>
    public IObservable<Responce<ushort[]>> ObserveWords(string address, int points, TimeSpan pollInterval, TimeSpan? minimumUpdateSpacing = null, TimeSpan? pollTimeout = null)
        => BuildPollingTrigger(pollInterval)
            .SelectAsyncSequential(_ => ReadWordsAsync(address, points, CancellationToken.None))
            .Conflate(minimumUpdateSpacing ?? TimeSpan.FromMilliseconds(10), _scheduler)
            .DoOnSubscribe(() => PublishOperation($"Observe words {address} subscribed", true, Array.Empty<byte>(), Array.Empty<byte>()))
            .DoOnDispose(() => PublishOperation($"Observe words {address} disposed", true, Array.Empty<byte>(), Array.Empty<byte>()));

    /// <summary>
    /// Continuously polls a bit range and emits sequential results.
    /// </summary>
    /// <param name="address">Device address.</param>
    /// <param name="points">Number of bits.</param>
    /// <param name="pollInterval">Polling interval.</param>
    /// <param name="minimumUpdateSpacing">Minimum spacing between notifications.</param>
    /// <returns>Observable bit results.</returns>
    public IObservable<Responce<bool[]>> ObserveBits(string address, int points, TimeSpan pollInterval, TimeSpan? minimumUpdateSpacing = null)
        => BuildPollingTrigger(pollInterval)
            .SelectAsyncSequential(_ => ReadBitsAsync(address, points, CancellationToken.None))
            .Conflate(minimumUpdateSpacing ?? TimeSpan.FromMilliseconds(10), _scheduler)
            .DoOnSubscribe(() => PublishOperation($"Observe bits {address} subscribed", true, Array.Empty<byte>(), Array.Empty<byte>()))
            .DoOnDispose(() => PublishOperation($"Observe bits {address} disposed", true, Array.Empty<byte>(), Array.Empty<byte>()));

    /// <summary>
    /// Polls a word range and injects heartbeat items when no new results arrive.
    /// </summary>
    /// <param name="address">Device address.</param>
    /// <param name="points">Number of words.</param>
    /// <param name="pollInterval">Polling interval.</param>
    /// <param name="heartbeatAfter">Heartbeat interval.</param>
    /// <param name="minimumUpdateSpacing">Minimum spacing between notifications.</param>
    /// <param name="pollTimeout">Per-poll timeout.</param>
    /// <returns>Observable heartbeat stream.</returns>
    public IObservable<IHeartbeat<Responce<ushort[]>>> ObserveWordsHeartbeat(string address, int points, TimeSpan pollInterval, TimeSpan heartbeatAfter, TimeSpan? minimumUpdateSpacing = null, TimeSpan? pollTimeout = null)
        => ObserveWords(address, points, pollInterval, minimumUpdateSpacing, pollTimeout).Heartbeat(heartbeatAfter, _scheduler);

    /// <summary>
    /// Polls a word range and emits stale markers when the stream goes quiet.
    /// </summary>
    /// <param name="address">Device address.</param>
    /// <param name="points">Number of words.</param>
    /// <param name="pollInterval">Polling interval.</param>
    /// <param name="staleAfter">Staleness threshold.</param>
    /// <param name="minimumUpdateSpacing">Minimum spacing between notifications.</param>
    /// <returns>Observable stale-aware stream.</returns>
    public IObservable<IStale<Responce<ushort[]>>> ObserveWordsStale(string address, int points, TimeSpan pollInterval, TimeSpan staleAfter, TimeSpan? minimumUpdateSpacing = null)
        => ObserveWords(address, points, pollInterval, minimumUpdateSpacing).DetectStale(staleAfter, _scheduler);

    /// <summary>
    /// Polls a word range using an external trigger and emits only the latest completed read.
    /// </summary>
    /// <param name="address">Device address.</param>
    /// <param name="points">Number of words.</param>
    /// <param name="trigger">Trigger stream.</param>
    /// <returns>Observable latest-result stream.</returns>
    public IObservable<Responce<ushort[]>> ObserveWordsLatest(string address, int points, IObservable<Unit> trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        return trigger.SelectLatestAsync(_ => ReadWordsAsync(address, points, CancellationToken.None));
    }

    /// <summary>
    /// Continuously polls a configured tag group and emits sequential grouped snapshots.
    /// </summary>
    /// <param name="groupName">Configured group name.</param>
    /// <param name="pollInterval">Polling interval.</param>
    /// <param name="minimumUpdateSpacing">Minimum spacing between notifications.</param>
    /// <returns>Observable grouped snapshots.</returns>
    public IObservable<Responce<MitsubishiTagGroupSnapshot>> ObserveTagGroup(string groupName, TimeSpan pollInterval, TimeSpan? minimumUpdateSpacing = null)
        => BuildPollingTrigger(pollInterval)
            .SelectAsyncSequential(_ => ReadTagGroupSnapshotAsync(groupName, CancellationToken.None))
            .Conflate(minimumUpdateSpacing ?? TimeSpan.FromMilliseconds(10), _scheduler)
            .DoOnSubscribe(() => PublishOperation($"Observe tag group {groupName} subscribed", true, Array.Empty<byte>(), Array.Empty<byte>()))
            .DoOnDispose(() => PublishOperation($"Observe tag group {groupName} disposed", true, Array.Empty<byte>(), Array.Empty<byte>()));

    /// <summary>
    /// Polls a configured tag group and injects heartbeat items when no new grouped snapshots arrive.
    /// </summary>
    /// <param name="groupName">Configured group name.</param>
    /// <param name="pollInterval">Polling interval.</param>
    /// <param name="heartbeatAfter">Heartbeat interval.</param>
    /// <param name="minimumUpdateSpacing">Minimum spacing between notifications.</param>
    /// <returns>Observable heartbeat stream.</returns>
    public IObservable<IHeartbeat<Responce<MitsubishiTagGroupSnapshot>>> ObserveTagGroupHeartbeat(string groupName, TimeSpan pollInterval, TimeSpan heartbeatAfter, TimeSpan? minimumUpdateSpacing = null)
        => ObserveTagGroup(groupName, pollInterval, minimumUpdateSpacing).Heartbeat(heartbeatAfter, _scheduler);

    /// <summary>
    /// Polls a configured tag group and emits stale markers when the stream goes quiet.
    /// </summary>
    /// <param name="groupName">Configured group name.</param>
    /// <param name="pollInterval">Polling interval.</param>
    /// <param name="staleAfter">Staleness threshold.</param>
    /// <param name="minimumUpdateSpacing">Minimum spacing between notifications.</param>
    /// <returns>Observable stale-aware stream.</returns>
    public IObservable<IStale<Responce<MitsubishiTagGroupSnapshot>>> ObserveTagGroupStale(string groupName, TimeSpan pollInterval, TimeSpan staleAfter, TimeSpan? minimumUpdateSpacing = null)
        => ObserveTagGroup(groupName, pollInterval, minimumUpdateSpacing).DetectStale(staleAfter, _scheduler);

    /// <summary>
    /// Polls a configured tag group using an external trigger and emits only the latest completed grouped snapshot.
    /// </summary>
    /// <param name="groupName">Configured group name.</param>
    /// <param name="trigger">Trigger stream.</param>
    /// <returns>Observable latest-result stream.</returns>
    public IObservable<Responce<MitsubishiTagGroupSnapshot>> ObserveTagGroupLatest(string groupName, IObservable<Unit> trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        return trigger.SelectLatestAsync(_ => ReadTagGroupSnapshotAsync(groupName, CancellationToken.None));
    }

    /// <summary>
    /// Samples the latest operation log whenever a trigger fires.
    /// </summary>
    /// <param name="trigger">Trigger stream.</param>
    /// <returns>Sampled diagnostics stream.</returns>
    public IObservable<MitsubishiOperationLog> SampleDiagnostics(IObservable<object> trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        return OperationLogs.SampleLatest(trigger);
    }

    /// <summary>
    /// Observes connection health using stale detection over state changes.
    /// </summary>
    /// <param name="staleAfter">Staleness threshold.</param>
    /// <returns>Observable health states.</returns>
    public IObservable<IStale<MitsubishiConnectionState>> ObserveConnectionHealth(TimeSpan staleAfter)
        => ConnectionStates.DetectStale(staleAfter, _scheduler);

    /// <inheritdoc/>
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
        _transport.Dispose();
        _requestGate.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
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
        await _transport.DisposeAsync().ConfigureAwait(false);
        _requestGate.Dispose();
        GC.SuppressFinalize(this);
    }

    private static IPEndPoint BuildEndPoint(MitsubishiClientOptions options)
    {
        if (IPAddress.TryParse(options.Host, out var ipAddress))
        {
            return new IPEndPoint(ipAddress, options.Port);
        }

        return new IPEndPoint(IPAddress.Any, options.Port);
    }

    private static IMitsubishiTransport CreateDefaultTransport(MitsubishiClientOptions options)
        => options.TransportKind == MitsubishiTransportKind.Serial
            ? new ReactiveSerialMitsubishiTransport()
            : new SocketMitsubishiTransport();

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

    private IObservable<long> BuildPollingTrigger(TimeSpan pollInterval)
        => Observable.Interval(pollInterval, _scheduler).StartWith(0L);

    private async Task<Responce> ExecuteControlAsync(ushort command, CancellationToken cancellationToken, bool force = true, bool clearMode = false)
    {
        var raw = await ExecuteObservableAsync(
            () => MitsubishiProtocolEncoding.EncodeRemoteOperation(Options, command, force, clearMode),
            null,
            $"Remote operation {command:X4}",
            cancellationToken).ConfigureAwait(false);
        return raw.ToBaseResponse();
    }

    private async Task<Responce> ExecutePasswordAsync(ushort command, string password, CancellationToken cancellationToken)
    {
        var raw = await ExecuteObservableAsync(
            () => MitsubishiProtocolEncoding.EncodeRemotePassword(Options, command, password),
            null,
            command == MitsubishiCommands.Unlock ? "Unlock" : "Lock",
            cancellationToken).ConfigureAwait(false);
        return raw.ToBaseResponse();
    }

    private async Task<Responce<byte[]>> ExecuteEncodedAsync(byte[] command, int? expectedLength, string description, int maxRetries = 2, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return await ExecuteObservableAsync(() => command, expectedLength, description, cancellationToken, maxRetries).ConfigureAwait(false);
    }

    private async Task<Responce<byte[]>> ExecuteObservableAsync(Func<byte[]> payloadFactory, int? expectedLength, string description, CancellationToken cancellationToken, int maxRetries = 2)
    {
        ArgumentNullException.ThrowIfNull(payloadFactory);
        var observable = Observable.Defer(() => Observable.FromAsync(ct => ExecuteOnceAsync(payloadFactory, expectedLength, description, ct)))
            .RetryWithBackoff(maxRetries, TimeSpan.FromMilliseconds(100), scheduler: _scheduler)
            .Catch<Responce<byte[]>, Exception>(ex => Observable.Return(new Responce<byte[]>().Fail(ex.Message, exception: ex)));

        return await observable.FirstAsync().ToTask(cancellationToken).ConfigureAwait(false);
    }

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
            var decoded = Options.TransportKind == MitsubishiTransportKind.Serial
                ? MitsubishiSerialProtocolEncoding.Decode(Options, response)
                : MitsubishiProtocolEncoding.Decode(Options, request, response);
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
            _requestGate.Release();
        }
    }

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

    private Responce<bool[]> ParseBits(Responce<byte[]> raw, int expectedBitCount)
    {
        if (!raw.IsSucceed || raw.Value is null)
        {
            return new Responce<bool[]>(raw);
        }

        try
        {
            var values = Options.DataCode == CommunicationDataCode.Binary
                ? ParseBinaryBits(raw.Value, expectedBitCount)
                : ParseAsciiBits(raw.Value, expectedBitCount);
            return new Responce<bool[]>(raw, values);
        }
        catch (Exception ex)
        {
            return new Responce<bool[]>(raw).Fail(ex.Message, exception: ex);
        }
    }

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
            var modelCode = ascii.Length >= 4 && ushort.TryParse(ascii[^4..], System.Globalization.NumberStyles.HexNumber, null, out var parsed)
                ? parsed
                : (ushort)0;
            var modelName = ascii.Length > 4 ? ascii[..^4].Trim() : ascii;
            return new Responce<MitsubishiTypeName>(raw, new MitsubishiTypeName(modelName, modelCode));
        }
        catch (Exception ex)
        {
            return new Responce<MitsubishiTypeName>(raw).Fail(ex.Message, exception: ex);
        }
    }

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

    private static bool[] ParseAsciiBits(byte[] payload, int expectedBitCount)
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

    private static bool[] ParseBinaryBits(byte[] payload, int expectedBitCount)
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

    private MitsubishiTagDefinition GetRequiredTag(string tagName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);
        var database = TagDatabase ?? throw new InvalidOperationException("TagDatabase must be assigned before tag-based APIs can be used.");
        return database.GetRequired(tagName);
    }

    private static Responce<T> ConvertWords<T>(Responce<ushort[]> raw, Func<ushort[], T> converter)
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

    private static double ReadNumericTagValue(MitsubishiTagDefinition tag, ushort[] words)
        => tag.DataType switch
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

    private static int GetWordCountForScaledRead(MitsubishiTagDefinition tag)
        => tag.DataType switch
        {
            null or "Word" or "Int16" or "UInt16" => 1,
            "DWord" or "Int32" or "UInt32" or "Float" => 2,
            _ => throw new InvalidOperationException($"Scaled access is not supported for DataType '{tag.DataType}'.")
        };

    private static double ApplyScaleAndOffset(double rawValue, MitsubishiTagDefinition tag)
        => (rawValue * tag.Scale) + tag.Offset;

    private static double RemoveScaleAndOffset(double engineeringValue, MitsubishiTagDefinition tag)
    {
        if (tag.Scale == 0)
        {
            throw new InvalidOperationException($"Tag '{tag.Name}' has Scale=0 and cannot be used for scaled writes.");
        }

        return (engineeringValue - tag.Offset) / tag.Scale;
    }

    private static uint ConvertToUInt32(ushort[] words, MitsubishiTagDefinition tag)
    {
        EnsureWordCount(words, 2, tag.Name);
        return tag.ByteOrder == "BigEndian"
            ? unchecked((uint)((words[0] << 16) | words[1]))
            : unchecked((uint)(words[0] | (words[1] << 16)));
    }

    private static int ConvertToInt32(ushort[] words, MitsubishiTagDefinition tag)
        => unchecked((int)ConvertToUInt32(words, tag));

    private static float ConvertToFloat(ushort[] words, MitsubishiTagDefinition tag)
        => BitConverter.Int32BitsToSingle(ConvertToInt32(words, tag));

    private static ushort[] ConvertFromInt32(int value, MitsubishiTagDefinition tag)
    {
        var raw = unchecked((uint)value);
        return tag.ByteOrder == "BigEndian"
            ? [unchecked((ushort)(raw >> 16)), unchecked((ushort)(raw & 0xFFFF))]
            : [unchecked((ushort)(raw & 0xFFFF)), unchecked((ushort)(raw >> 16))];
    }

    private static ushort[] ConvertFromUInt32(uint value, MitsubishiTagDefinition tag)
        => tag.ByteOrder == "BigEndian"
            ? [unchecked((ushort)(value >> 16)), unchecked((ushort)(value & 0xFFFF))]
            : [unchecked((ushort)(value & 0xFFFF)), unchecked((ushort)(value >> 16))];

    private static ushort[] ConvertFromFloat(float value, MitsubishiTagDefinition tag)
        => ConvertFromInt32(BitConverter.SingleToInt32Bits(value), tag);

    private static void EnsureWordCount(ushort[] words, int requiredCount, string tagName)
    {
        if (words.Length < requiredCount)
        {
            throw new InvalidOperationException($"Tag '{tagName}' requires at least {requiredCount} word(s), but only {words.Length} were read.");
        }
    }

    private static string DecodeStringFromWords(ushort[] words, MitsubishiTagDefinition tag)
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

    private static ushort[] EncodeStringWords(string value, int wordLength, MitsubishiTagDefinition tag)
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
            words[index] = tag.ByteOrder == "BigEndian"
                ? BinaryPrimitives.ReadUInt16BigEndian(span)
                : BinaryPrimitives.ReadUInt16LittleEndian(span);
        }

        return words;
    }

    private static Encoding GetTextEncoding(MitsubishiTagDefinition tag)
        => tag.Encoding switch
        {
            "Utf8" => Encoding.UTF8,
            "Utf16" => Encoding.Unicode,
            _ => Encoding.ASCII,
        };

    private MitsubishiTagDatabase GetRequiredTagDatabase()
        => TagDatabase ?? throw new InvalidOperationException("TagDatabase must be assigned before tag-based APIs can be used.");

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

    private async Task<Responce> WriteTagValueAsync(string tagName, object? value, CancellationToken cancellationToken)
    {
        var tag = GetRequiredTag(tagName);
        try
        {
            switch (tag.DataType)
            {
                case "Bit":
                    if (value is bool bit)
                    {
                        return await WriteBitsByTagAsync(tagName, [bit], cancellationToken).ConfigureAwait(false);
                    }

                    break;
                case "String":
                    if (value is string text)
                    {
                        return await WriteStringByTagAsync(tagName, text, cancellationToken).ConfigureAwait(false);
                    }

                    break;
                case "Float":
                    if (value is float single)
                    {
                        return await WriteFloatByTagAsync(tagName, single, cancellationToken).ConfigureAwait(false);
                    }

                    break;
                case "DWord":
                case "UInt32":
                    if (value is uint uint32)
                    {
                        return await WriteDWordByTagAsync(tagName, uint32, cancellationToken).ConfigureAwait(false);
                    }

                    break;
                case "Int32":
                    if (value is int int32)
                    {
                        return await WriteInt32ByTagAsync(tagName, int32, cancellationToken).ConfigureAwait(false);
                    }

                    break;
                case "Int16":
                    if (value is short int16)
                    {
                        return await WriteInt16ByTagAsync(tagName, int16, cancellationToken).ConfigureAwait(false);
                    }

                    break;
                case "UInt16":
                    if (value is ushort uint16)
                    {
                        return await WriteUInt16ByTagAsync(tagName, uint16, cancellationToken).ConfigureAwait(false);
                    }

                    break;
                default:
                    if (HasEngineeringMetadata(tag) && value is double engineering)
                    {
                        return await WriteScaledDoubleByTagAsync(tagName, engineering, cancellationToken).ConfigureAwait(false);
                    }

                    if (value is ushort rawWord)
                    {
                        return await WriteWordsByTagAsync(tagName, [rawWord], cancellationToken).ConfigureAwait(false);
                    }

                    break;
            }
        }
        catch (Exception ex)
        {
            return new Responce().Fail(ex.Message, exception: ex);
        }

        return new Responce().Fail($"Value for tag '{tagName}' is not compatible with DataType '{tag.DataType ?? "Word"}'.");
    }

    private static bool CanWriteTagValue(MitsubishiTagDefinition tag, object? value, out string? error)
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

    private static async Task<Responce<object?>> ConvertTagValueAsync<T>(Task<Responce<T>> task, Func<T, object?> projector)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(projector);
        var raw = await task.ConfigureAwait(false);
        if (!raw.IsSucceed || raw.Value is null)
        {
            return new Responce<object?>(raw);
        }

        return new Responce<object?>(raw, projector(raw.Value));
    }

    private static bool HasEngineeringMetadata(MitsubishiTagDefinition tag)
        => Math.Abs(tag.Scale - 1.0) > double.Epsilon || Math.Abs(tag.Offset) > double.Epsilon;

    private static Responce ValidateRolloutPolicy(MitsubishiTagDatabaseDiff diff, MitsubishiTagRolloutPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(diff);

        if (policy == MitsubishiTagRolloutPolicy.AllowAll)
        {
            return new Responce().EndTime();
        }

        if (policy == MitsubishiTagRolloutPolicy.SafeMetadataAndGroups)
        {
            var disallowed = diff.ChangeKinds & (MitsubishiSchemaChangeKind.AddressChange | MitsubishiSchemaChangeKind.DataTypeChange | MitsubishiSchemaChangeKind.StructureChange);
            if (disallowed == MitsubishiSchemaChangeKind.None)
            {
                return new Responce().EndTime();
            }

            return new Responce().Fail($"Rollout policy '{policy}' rejected schema changes: {disallowed}.");
        }

        return new Responce().Fail($"Unsupported rollout policy '{policy}'.");
    }

    private static string GetSchemaFingerprint(string path)
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

    private string ResolveTagAddress(string tagName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);
        var database = TagDatabase ?? throw new InvalidOperationException("TagDatabase must be assigned before tag-based APIs can be used.");
        return database.GetRequired(tagName).Address;
    }

    private IReadOnlyList<string> ResolveTagAddresses(IEnumerable<string> tagNames)
    {
        ArgumentNullException.ThrowIfNull(tagNames);
        return tagNames.Select(ResolveTagAddress).ToArray();
    }

    private void PublishFault(string description, ReadOnlyMemory<byte> request, ReadOnlyMemory<byte> response, Exception exception)
    {
        PublishState(MitsubishiConnectionState.Faulted);
        PublishOperation(description, false, request, response, exception);
    }

    private void PublishOperation(string description, bool success, ReadOnlyMemory<byte> request, ReadOnlyMemory<byte> response, Exception? exception = null)
    {
        _operationLogs.OnNext(new MitsubishiOperationLog(
            DateTimeOffset.UtcNow,
            _connectionStates.Value,
            description,
            success,
            request,
            response,
            exception));
    }

    private void PublishState(MitsubishiConnectionState state)
    {
        if (_connectionStates.Value != state)
        {
            _connectionStates.OnNext(state);
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
