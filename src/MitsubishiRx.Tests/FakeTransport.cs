// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Collections.Concurrent;

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive.Tests;
#else

namespace MitsubishiRx.Tests;
#endif

/// <summary>Provides the FakeTransport type.</summary>
internal sealed class FakeTransport : IMitsubishiTransport
{
    /// <summary>Stores the _responses field.</summary>
    private readonly ConcurrentQueue<byte[]> _responses;

    /// <summary>Stores the _responseFactory field.</summary>
    private readonly Func<MitsubishiTransportRequest, byte[]>? _responseFactory;

    /// <summary>Stores the _connected field.</summary>
    private bool _connected;

    /// <summary>Initializes a new instance of the <see cref="FakeTransport"/> class.</summary>
    /// <param name="responses">The queued responses.</param>
    public FakeTransport(IEnumerable<byte[]> responses)
    {
        _responses = new(responses);
    }

    /// <summary>Initializes a new instance of the <see cref="FakeTransport"/> class.</summary>
    /// <param name="responseFactory">The response factory.</param>
    public FakeTransport(Func<MitsubishiTransportRequest, byte[]> responseFactory)
    {
        _responses = new();
        _responseFactory = responseFactory;
    }

    /// <summary>Gets the Requests property.</summary>
    public List<MitsubishiTransportRequest> Requests { get; } = new();

    /// <summary>Gets stores the IsConnected field.</summary>
    public bool IsConnected => _connected;

    /// <summary>Executes the ConnectAsync operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ConnectAsync operation result.</returns>
    public ValueTask ConnectAsync(MitsubishiClientOptions options, CancellationToken cancellationToken = default)
    {
        _connected = true;
        return ValueTask.CompletedTask;
    }

    /// <summary>Executes the DisconnectAsync operation.</summary>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The DisconnectAsync operation result.</returns>
    public ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _connected = false;
        return ValueTask.CompletedTask;
    }

    /// <summary>Executes the ExchangeAsync operation.</summary>
    /// <param name="request">The request parameter.</param>
    /// <param name="cancellationToken">The cancellationToken parameter.</param>
    /// <returns>The ExchangeAsync operation result.</returns>
    public ValueTask<byte[]> ExchangeAsync(MitsubishiTransportRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        if (_responses.TryDequeue(out var response))
        {
            return ValueTask.FromResult(response);
        }

        if (_responseFactory is not null)
        {
            return ValueTask.FromResult(_responseFactory(request));
        }

        throw new InvalidOperationException("No fake response queued.");
    }

    /// <summary>Executes the Dispose operation.</summary>
    public void Dispose()
    {
        _connected = false;
    }

    /// <summary>Executes the DisposeAsync operation.</summary>
    /// <returns>The DisposeAsync operation result.</returns>
    public ValueTask DisposeAsync()
    {
        _connected = false;
        return ValueTask.CompletedTask;
    }
}
