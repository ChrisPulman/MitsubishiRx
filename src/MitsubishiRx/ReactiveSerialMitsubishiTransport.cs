// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Provides the ReactiveSerialMitsubishiTransport type.</summary>
internal sealed class ReactiveSerialMitsubishiTransport : IMitsubishiTransport
{
    /// <summary>Stores the gate field.</summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Stores the serialPort field.</summary>
    private ReactiveSerialPortAdapter? _serialPort;

    /// <summary>Stores the options field.</summary>
    private MitsubishiClientOptions? _options;

    /// <summary>Gets or sets the IsConnected property.</summary>
    public bool IsConnected => _serialPort?.IsOpen ?? false;

    /// <summary>Executes the ConnectAsync operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ConnectAsync operation result.</returns>
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
            _ = _gate.Release();
        }
    }

    /// <summary>Executes the DisconnectAsync operation.</summary>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The DisconnectAsync operation result.</returns>
    public async ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ClosePort();
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    /// <summary>Executes the ExchangeAsync operation.</summary>
    /// <param name="request">The request parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ExchangeAsync operation result.</returns>
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
            var received = new List<byte>();
            return await _serialPort.ReceivedBytes.Select(chunk =>
            {
                received.AddRange(chunk);
                return received.ToArray();
            }).Where(buffer => MitsubishiSerialProtocolEncoding.IsExpectedFrameComplete(_options, buffer)).Timeout(timeout).FirstAsync().ToTask(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    /// <summary>Executes the Dispose operation.</summary>
    public void Dispose()
    {
        ClosePort();
        _gate.Dispose();
    }

    /// <summary>Executes the DisposeAsync operation.</summary>
    /// <returns>The DisposeAsync operation result.</returns>
    public ValueTask DisposeAsync()
    {
        ClosePort();
        _gate.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>Executes the EnsureConnectedCoreAsync operation.</summary>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The EnsureConnectedCoreAsync operation result.</returns>
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
        _serialPort = new(_options.ResolvedSerial);
        await _serialPort.OpenAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Executes the ClosePort operation.</summary>
    private void ClosePort()
    {
        try
        {
            _serialPort?.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            _serialPort = null;
        }
    }
}
