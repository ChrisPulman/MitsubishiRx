// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;

namespace MitsubishiRx;

/// <summary>
/// Transport abstraction used by the Mitsubishi reactive client.
/// </summary>
public interface IMitsubishiTransport : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Gets a value indicating whether the transport is connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Connects the transport.
    /// </summary>
    /// <param name="options">Client options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    ValueTask ConnectAsync(MitsubishiClientOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects the transport.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    ValueTask DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Exchanges a request and response with the PLC.
    /// </summary>
    /// <param name="request">Encoded transport request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The raw response bytes.</returns>
    ValueTask<byte[]> ExchangeAsync(MitsubishiTransportRequest request, CancellationToken cancellationToken = default);
}

internal sealed class SocketMitsubishiTransport : IMitsubishiTransport
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Socket? _socket;
    private MitsubishiClientOptions? _options;

    public bool IsConnected => _socket?.Connected ?? false;

    public async ValueTask ConnectAsync(MitsubishiClientOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

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
            await CloseSocketAsync().ConfigureAwait(false);
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

            if (_socket is null)
            {
                throw new InvalidOperationException("Socket transport is not connected.");
            }

            await _socket.SendAsync(request.Payload, SocketFlags.None, cancellationToken).ConfigureAwait(false);
            return _options.TransportKind == MitsubishiTransportKind.Udp
                ? await ReceiveUdpAsync(_socket, cancellationToken).ConfigureAwait(false)
                : await ReceiveTcpAsync(_socket, request.ExpectedResponseLength, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        CloseSocketAsync().AsTask().GetAwaiter().GetResult();
        _gate.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await CloseSocketAsync().ConfigureAwait(false);
        _gate.Dispose();
    }

    private static async ValueTask<byte[]> ReceiveUdpAsync(Socket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        var received = await socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken).ConfigureAwait(false);
        return buffer[..received];
    }

    private async ValueTask<byte[]> ReceiveTcpAsync(Socket socket, int? expectedResponseLength, CancellationToken cancellationToken)
    {
        if (_options is null)
        {
            throw new InvalidOperationException("Transport is not configured.");
        }

        if (expectedResponseLength.HasValue)
        {
            return await ReceiveExactlyAsync(socket, expectedResponseLength.Value, cancellationToken).ConfigureAwait(false);
        }

        if (_options.DataCode == CommunicationDataCode.Ascii)
        {
            return await ReceiveAsciiTcpAsync(socket, _options.FrameType, cancellationToken).ConfigureAwait(false);
        }

        var prefixLength = _options.FrameType == MitsubishiFrameType.FourE ? 15 : 11;
        var prefix = await ReceiveExactlyAsync(socket, prefixLength, cancellationToken).ConfigureAwait(false);
        var totalLength = _options.FrameType switch
        {
            MitsubishiFrameType.ThreeE => prefixLength + BitConverter.ToUInt16(prefix, 7),
            MitsubishiFrameType.FourE => prefixLength + BitConverter.ToUInt16(prefix, 11),
            _ => prefixLength,
        };

        var remaining = totalLength - prefixLength;
        if (remaining <= 0)
        {
            return prefix;
        }

        var payload = await ReceiveExactlyAsync(socket, remaining, cancellationToken).ConfigureAwait(false);
        return prefix.Concat(payload).ToArray();
    }

    private static async ValueTask<byte[]> ReceiveAsciiTcpAsync(Socket socket, MitsubishiFrameType frameType, CancellationToken cancellationToken)
    {
        var prefixLength = frameType == MitsubishiFrameType.FourE ? 30 : 22;
        var prefix = await ReceiveExactlyAsync(socket, prefixLength, cancellationToken).ConfigureAwait(false);
        var prefixText = System.Text.Encoding.ASCII.GetString(prefix);
        var lengthOffset = frameType == MitsubishiFrameType.FourE ? 22 : 14;
        var responseDataLength = Convert.ToInt32(prefixText.Substring(lengthOffset, 4), 16);
        var remainingAsciiChars = Math.Max(0, responseDataLength - 2) * 2;
        if (remainingAsciiChars == 0)
        {
            return prefix;
        }

        var payload = await ReceiveExactlyAsync(socket, remainingAsciiChars, cancellationToken).ConfigureAwait(false);
        return prefix.Concat(payload).ToArray();
    }

    private static async ValueTask<byte[]> ReceiveExactlyAsync(Socket socket, int count, CancellationToken cancellationToken)
    {
        var buffer = new byte[count];
        var read = 0;
        while (read < count)
        {
            var received = await socket.ReceiveAsync(buffer.AsMemory(read, count - read), SocketFlags.None, cancellationToken).ConfigureAwait(false);
            if (received == 0)
            {
                throw new IOException("The PLC connection dropped while waiting for response data.");
            }

            read += received;
        }

        return buffer;
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

        await CloseSocketAsync().ConfigureAwait(false);
        var protocolType = _options.TransportKind == MitsubishiTransportKind.Tcp ? ProtocolType.Tcp : ProtocolType.Udp;
        var socketType = _options.TransportKind == MitsubishiTransportKind.Tcp ? SocketType.Stream : SocketType.Dgram;
        _socket = new Socket(AddressFamily.InterNetwork, socketType, protocolType)
        {
            SendTimeout = (int)_options.ResolvedTimeout.TotalMilliseconds,
            ReceiveTimeout = (int)_options.ResolvedTimeout.TotalMilliseconds,
        };

        var addresses = await Dns.GetHostAddressesAsync(_options.Host, cancellationToken).ConfigureAwait(false);
        var address = addresses.FirstOrDefault(static a => a.AddressFamily == AddressFamily.InterNetwork)
            ?? IPAddress.Parse(_options.Host);
        await _socket.ConnectAsync(new IPEndPoint(address, _options.Port), cancellationToken).ConfigureAwait(false);
    }

    private ValueTask CloseSocketAsync()
    {
        try
        {
            _socket.SafeClose();
            _socket = null;
        }
        catch
        {
            _socket = null;
        }

        return ValueTask.CompletedTask;
    }
}
