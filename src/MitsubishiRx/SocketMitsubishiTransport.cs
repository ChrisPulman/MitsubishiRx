// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Provides the SocketMitsubishiTransport type.</summary>
internal sealed class SocketMitsubishiTransport : IMitsubishiTransport
{
    /// <summary>Stores the gate field.</summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Stores the socket field.</summary>
    private Socket? _socket;

    /// <summary>Stores the options field.</summary>
    private MitsubishiClientOptions? _options;

    /// <summary>Gets or sets the IsConnected property.</summary>
    public bool IsConnected => _socket?.Connected ?? false;

    /// <summary>Executes the ConnectAsync operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ConnectAsync operation result.</returns>
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
            await CloseSocketAsync().ConfigureAwait(false);
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
            if (_socket is null)
            {
                throw new InvalidOperationException("Socket transport is not connected.");
            }

            await _socket.SendAsync(request.Payload, SocketFlags.None, cancellationToken).ConfigureAwait(false);
            return _options.TransportKind == MitsubishiTransportKind.Udp ? await ReceiveUdpAsync(_socket, cancellationToken).ConfigureAwait(false) : await ReceiveTcpAsync(_socket, request.ExpectedResponseLength, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    /// <summary>Executes the Dispose operation.</summary>
    public void Dispose()
    {
        CloseSocketAsync().AsTask().GetAwaiter().GetResult();
        _gate.Dispose();
    }

    /// <summary>Executes the DisposeAsync operation.</summary>
    /// <returns>The DisposeAsync operation result.</returns>
    public async ValueTask DisposeAsync()
    {
        await CloseSocketAsync().ConfigureAwait(false);
        _gate.Dispose();
    }

    /// <summary>Executes the ReceiveUdpAsync operation.</summary>
    /// <param name="socket">The socket parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReceiveUdpAsync operation result.</returns>
    private static async ValueTask<byte[]> ReceiveUdpAsync(Socket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        var received = await socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken).ConfigureAwait(false);
        return buffer[..received];
    }

    /// <summary>Executes the ReceiveAsciiTcpAsync operation.</summary>
    /// <param name="socket">The socket parameter.</param>
    /// <param name="frameType">The frameType parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReceiveAsciiTcpAsync operation result.</returns>
    private static async ValueTask<byte[]> ReceiveAsciiTcpAsync(Socket socket, MitsubishiFrameType frameType, CancellationToken cancellationToken)
    {
        var prefixLength = frameType == MitsubishiFrameType.FourE ? 30 : 22;
        var prefix = await ReceiveExactlyAsync(socket, prefixLength, cancellationToken).ConfigureAwait(false);
        var prefixText = System.Text.Encoding.ASCII.GetString(prefix);
        var lengthOffset = frameType == MitsubishiFrameType.FourE ? 22 : 14;
        var responseDataLength = Convert.ToInt32(prefixText[lengthOffset..(lengthOffset + 4)], 16);
        var remainingAsciiChars = Math.Max(0, responseDataLength - 2) * 2;
        if (remainingAsciiChars == 0)
        {
            return prefix;
        }

        var payload = await ReceiveExactlyAsync(socket, remainingAsciiChars, cancellationToken).ConfigureAwait(false);
        return prefix.Concat(payload).ToArray();
    }

    /// <summary>Executes the ReceiveExactlyAsync operation.</summary>
    /// <param name="socket">The socket parameter.</param>
    /// <param name="count">The count parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReceiveExactlyAsync operation result.</returns>
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

    /// <summary>Executes the ReceiveTcpAsync operation.</summary>
    /// <param name="socket">The socket parameter.</param>
    /// <param name="expectedResponseLength">The expectedResponseLength parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ReceiveTcpAsync operation result.</returns>
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

        await CloseSocketAsync().ConfigureAwait(false);
        var protocolType = _options.TransportKind == MitsubishiTransportKind.Tcp ? ProtocolType.Tcp : ProtocolType.Udp;
        var socketType = _options.TransportKind == MitsubishiTransportKind.Tcp ? SocketType.Stream : SocketType.Dgram;
        _socket = new(AddressFamily.InterNetwork, socketType, protocolType)
        {
            SendTimeout = (int)_options.ResolvedTimeout.TotalMilliseconds,
            ReceiveTimeout = (int)_options.ResolvedTimeout.TotalMilliseconds,
        };
        var addresses = await Dns.GetHostAddressesAsync(_options.Host, cancellationToken).ConfigureAwait(false);
        var address = addresses.FirstOrDefault(static a => a.AddressFamily == AddressFamily.InterNetwork) ?? IPAddress.Parse(_options.Host);
        await _socket.ConnectAsync(new IPEndPoint(address, _options.Port), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Executes the CloseSocketAsync operation.</summary>
    /// <returns>The CloseSocketAsync operation result.</returns>
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
