// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Ports;
#if !REACTIVE_SHIM
using CP.IO.Ports;
#endif

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Provides the ReactiveSerialPortAdapter type.</summary>
internal sealed class ReactiveSerialPortAdapter : IDisposable
{
    /// <summary>Stores the serialPort field.</summary>
    private readonly ISerialPortRx _serialPort;

    /// <summary>Stores the writes field.</summary>
    private readonly Signal<byte[]> _writes = new();

    /// <summary>Initializes a new instance of the ReactiveSerialPortAdapter class.</summary>
    /// <param name="options">The options parameter.</param>
    public ReactiveSerialPortAdapter(MitsubishiSerialOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _serialPort = new SerialPortRx(options.PortName, options.BaudRate, options.DataBits, options.Parity, options.StopBits, options.Handshake)
        {
            NewLine = options.NewLine,
            ReadBufferSize = options.ReadBufferSize,
            WriteBufferSize = options.WriteBufferSize,
            ReceivedBytesThreshold = 1,
            EnableAutoDataReceive = true,
            ReadTimeout = SerialPort.InfiniteTimeout,
            WriteTimeout = SerialPort.InfiniteTimeout,
        };
    }

    /// <summary>Gets or sets the IsOpen property.</summary>
    public bool IsOpen => _serialPort.IsOpen;

    /// <summary>Gets or sets the ReceivedBytes property.</summary>
    public IObservable<byte[]> ReceivedBytes => _serialPort.DataReceivedBytes.Select(static value => new byte[] { value });

    /// <summary>Gets or sets the WrittenBytes property.</summary>
    public IObservable<byte[]> WrittenBytes => _writes.AsObservable();

    /// <summary>Executes the OpenAsync operation.</summary>
    /// <returns>The OpenAsync operation result.</returns>
    public Task OpenAsync() => _serialPort.Open();

    /// <summary>Executes the Close operation.</summary>
    public void Close() => _serialPort.Close();

    /// <summary>Executes the DiscardInBuffer operation.</summary>
    public void DiscardInBuffer() => _serialPort.DiscardInBuffer();

    /// <summary>Executes the DiscardOutBuffer operation.</summary>
    public void DiscardOutBuffer() => _serialPort.DiscardOutBuffer();

    /// <summary>Executes the Write operation.</summary>
    /// <param name="buffer">The buffer parameter.</param>
    public void Write(byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        _serialPort.Write(buffer, 0, buffer.Length);
        _writes.OnNext(buffer.ToArray());
    }

    /// <summary>Executes the ReadAsync operation.</summary>
    /// <param name="buffer">The buffer parameter.</param>
    /// <param name="offset">The offset parameter.</param>
    /// <param name="count">The count parameter.</param>
    /// <returns>The ReadAsync operation result.</returns>
    public Task<int> ReadAsync(byte[] buffer, int offset, int count) => _serialPort.ReadAsync(buffer, offset, count);

    /// <summary>Executes the Dispose operation.</summary>
    public void Dispose()
    {
        try
        {
            _serialPort.Close();
        }
        catch (ObjectDisposedException)
        {
        }
        catch (IOException)
        {
        }
        catch (InvalidOperationException)
        {
        }

        _serialPort.Dispose();
        _writes.OnCompleted();
        _writes.Dispose();
    }
}
