using System.Collections.Concurrent;

namespace MitsubishiRx.Tests;

internal sealed class FakeTransport : IMitsubishiTransport
{
    private readonly ConcurrentQueue<byte[]> _responses;
    private readonly Func<MitsubishiTransportRequest, byte[]>? _responseFactory;
    private bool _connected;

    public FakeTransport(IEnumerable<byte[]> responses)
    {
        _responses = new ConcurrentQueue<byte[]>(responses);
    }

    public FakeTransport(Func<MitsubishiTransportRequest, byte[]> responseFactory)
    {
        _responses = new ConcurrentQueue<byte[]>();
        _responseFactory = responseFactory;
    }

    public List<MitsubishiTransportRequest> Requests { get; } = new();

    public bool IsConnected => _connected;

    public ValueTask ConnectAsync(MitsubishiClientOptions options, CancellationToken cancellationToken = default)
    {
        _connected = true;
        return ValueTask.CompletedTask;
    }

    public ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _connected = false;
        return ValueTask.CompletedTask;
    }

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

    public void Dispose()
    {
        _connected = false;
    }

    public ValueTask DisposeAsync()
    {
        _connected = false;
        return ValueTask.CompletedTask;
    }
}
