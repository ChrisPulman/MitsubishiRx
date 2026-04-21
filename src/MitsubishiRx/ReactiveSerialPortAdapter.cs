// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Ports;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CP.IO.Ports;

namespace MitsubishiRx;

internal sealed class ReactiveSerialPortAdapter : IDisposable
{
    private readonly ISerialPortRx _serialPort;
    private readonly Subject<byte[]> _writes = new();

    public ReactiveSerialPortAdapter(MitsubishiSerialOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _serialPort = new SerialPortRx(
            options.PortName,
            options.BaudRate,
            options.DataBits,
            options.Parity,
            options.StopBits,
            options.Handshake)
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

    public bool IsOpen => _serialPort.IsOpen;

    public IObservable<byte[]> ReceivedBytes => _serialPort.DataReceivedBytes
        .Select(static value => new byte[] { value });

    public IObservable<byte[]> WrittenBytes => _writes.AsObservable();

    public Task OpenAsync() => _serialPort.Open();

    public void Close() => _serialPort.Close();

    public void DiscardInBuffer() => _serialPort.DiscardInBuffer();

    public void DiscardOutBuffer() => _serialPort.DiscardOutBuffer();

    public void Write(byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        _serialPort.Write(buffer, 0, buffer.Length);
        _writes.OnNext(buffer.ToArray());
    }

    public Task<int> ReadAsync(byte[] buffer, int offset, int count)
        => _serialPort.ReadAsync(buffer, offset, count);

    public void Dispose()
    {
        try
        {
            _serialPort.Close();
        }
        catch
        {
        }

        _serialPort.Dispose();
        _writes.OnCompleted();
        _writes.Dispose();
    }
}
