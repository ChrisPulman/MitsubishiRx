// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;

namespace MitsubishiRx;

internal sealed class ReactiveSerialMitsubishiTransport : IMitsubishiTransport
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ReactiveSerialPortAdapter? _serialPort;
    private MitsubishiClientOptions? _options;

    public bool IsConnected => _serialPort?.IsOpen ?? false;

    public async ValueTask ConnectAsync(MitsubishiClientOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.TransportKind != MitsubishiTransportKind.Serial)
        {
            throw new InvalidOperationException("Reactive serial transport requires TransportKind.Serial.");
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _options = options;
            await EnsureConnectedCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ClosePort();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<byte[]> ExchangeAsync(MitsubishiTransportRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_options is null)
        {
            throw new InvalidOperationException("Transport is not configured.");
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureConnectedCoreAsync(cancellationToken).ConfigureAwait(false);
            if (_serialPort is null)
            {
                throw new InvalidOperationException("Serial transport is not connected.");
            }

            _serialPort.DiscardInBuffer();
            _serialPort.DiscardOutBuffer();
            _serialPort.Write(request.Payload);

            var timeout = _options.ResolvedTimeout;
            return await _serialPort.ReceivedBytes
                .Scan(new List<byte>(), static (state, chunk) =>
                {
                    state.AddRange(chunk);
                    return state;
                })
                .Where(buffer => MitsubishiSerialProtocolEncoding.IsExpectedFrameComplete(_options, buffer.ToArray()))
                .Select(static buffer => buffer.ToArray())
                .Timeout(timeout)
                .FirstAsync()
                .ToTask(cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        ClosePort();
        _gate.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        ClosePort();
        _gate.Dispose();
        return ValueTask.CompletedTask;
    }

    private async ValueTask EnsureConnectedCoreAsync(CancellationToken cancellationToken)
    {
        if (IsConnected)
        {
            return;
        }

        if (_options is null)
        {
            throw new InvalidOperationException("Transport is not configured.");
        }

        ClosePort();
        _serialPort = new ReactiveSerialPortAdapter(_options.ResolvedSerial);
        await _serialPort.OpenAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private void ClosePort()
    {
        try
        {
            _serialPort?.Dispose();
        }
        catch
        {
        }
        finally
        {
            _serialPort = null;
        }
    }
}
