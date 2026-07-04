// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Provides the IMitsubishiTransport contract.</summary>
public interface IMitsubishiTransport : IAsyncDisposable, IDisposable
{
    /// <summary>Gets or sets the IsConnected property.</summary>
    bool IsConnected { get; }

    /// <summary>Executes the ConnectAsync operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ConnectAsync operation result.</returns>
    ValueTask ConnectAsync(MitsubishiClientOptions options, CancellationToken cancellationToken = default);

    /// <summary>Executes the DisconnectAsync operation.</summary>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The DisconnectAsync operation result.</returns>
    ValueTask DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>Executes the ExchangeAsync operation.</summary>
    /// <param name="request">The request parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ExchangeAsync operation result.</returns>
    ValueTask<byte[]> ExchangeAsync(MitsubishiTransportRequest request, CancellationToken cancellationToken = default);
}
