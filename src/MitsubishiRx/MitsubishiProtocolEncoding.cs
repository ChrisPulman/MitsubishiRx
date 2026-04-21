// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace MitsubishiRx;

internal static class MitsubishiProtocolEncoding
{
    public static byte[] Encode(MitsubishiClientOptions options, MitsubishiRawCommandRequest request)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(request);

        return options.FrameType switch
        {
            MitsubishiFrameType.OneE => Encode1E(options, request),
            MitsubishiFrameType.ThreeE => Encode3E(options, request),
            MitsubishiFrameType.FourE => Encode4E(options, request),
            _ => throw new ArgumentOutOfRangeException(nameof(options.FrameType)),
        };
    }

    public static Responce<byte[]> Decode(MitsubishiClientOptions options, MitsubishiTransportRequest request, byte[] response)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);

        return options.DataCode switch
        {
            CommunicationDataCode.Ascii => options.FrameType == MitsubishiFrameType.OneE ? Decode1EAscii(response) : Decode3EOr4EAscii(response),
            _ => options.FrameType switch
            {
                MitsubishiFrameType.OneE => Decode1E(response),
                MitsubishiFrameType.ThreeE or MitsubishiFrameType.FourE => Decode3EOr4E(response),
                _ => throw new ArgumentOutOfRangeException(nameof(options.FrameType)),
            },
        };
    }

    public static int? GetFixedResponseLength(MitsubishiFrameType frameType, CommunicationDataCode dataCode, ushort command, ushort subcommand, int requestBodyLength, int? explicitLength = null)
    {
        if (explicitLength.HasValue)
        {
            return explicitLength;
        }

        return frameType switch
        {
            MitsubishiFrameType.OneE => GetFixedResponseLength1E(command, requestBodyLength, dataCode),
            MitsubishiFrameType.ThreeE or MitsubishiFrameType.FourE => null,
            _ => null,
        };
    }

    public static byte[] EncodeDeviceBatchRead(MitsubishiClientOptions options, MitsubishiDeviceAddress address, int points, bool bitUnits)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(points);
        var subcommand = options.FrameType == MitsubishiFrameType.OneE
            ? (ushort)(bitUnits ? 0x0000 : 0x0001)
            : (ushort)(bitUnits ? 0x0001 : 0x0000);
        var body = options.FrameType == MitsubishiFrameType.OneE
            ? Encode1EDeviceBody(address, points, options)
            : Encode3EDeviceBody(address, points, options);

        return Encode(options, new MitsubishiRawCommandRequest(MitsubishiCommands.DeviceRead, subcommand, body, $"Read {address.Original}"));
    }

    public static byte[] EncodeDeviceBatchWrite(MitsubishiClientOptions options, MitsubishiDeviceAddress address, ReadOnlySpan<ushort> values, bool bitUnits)
    {
        if (values.IsEmpty)
        {
            throw new ArgumentException("At least one value must be supplied.", nameof(values));
        }

        var subcommand = options.FrameType == MitsubishiFrameType.OneE
            ? (ushort)(bitUnits ? 0x0002 : 0x0003)
            : (ushort)(bitUnits ? 0x0001 : 0x0000);
        var body = options.FrameType == MitsubishiFrameType.OneE
            ? Encode1EDeviceWriteBody(address, values, options, bitUnits)
            : Encode3EDeviceWriteBody(address, values, options, bitUnits);

        return Encode(options, new MitsubishiRawCommandRequest(MitsubishiCommands.DeviceWrite, subcommand, body, $"Write {address.Original}"));
    }

    public static byte[] EncodeRandomRead(MitsubishiClientOptions options, IReadOnlyList<MitsubishiDeviceAddress> wordDevices)
    {
        ArgumentNullException.ThrowIfNull(wordDevices);
        if (wordDevices.Count == 0)
        {
            throw new ArgumentException("At least one device must be supplied.", nameof(wordDevices));
        }

        var body = options.FrameType == MitsubishiFrameType.OneE
            ? throw new NotSupportedException("1E random read is not implemented in this release.")
            : Encode3ERandomReadBody(wordDevices, options);

        return Encode(options, new MitsubishiRawCommandRequest(MitsubishiCommands.RandomRead, 0x0000, body, "Random read"));
    }

    public static byte[] EncodeRandomWrite(MitsubishiClientOptions options, IReadOnlyList<MitsubishiDeviceValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            throw new ArgumentException("At least one device value must be supplied.", nameof(values));
        }

        var body = options.FrameType == MitsubishiFrameType.OneE
            ? throw new NotSupportedException("1E random write is not implemented in this release.")
            : Encode3ERandomWriteBody(values, options);

        return Encode(options, new MitsubishiRawCommandRequest(MitsubishiCommands.RandomWrite, 0x0000, body, "Random write"));
    }

    public static byte[] EncodeMonitorRegistration(MitsubishiClientOptions options, IReadOnlyList<MitsubishiDeviceAddress> wordDevices)
    {
        ArgumentNullException.ThrowIfNull(wordDevices);
        if (wordDevices.Count == 0)
        {
            throw new ArgumentException("At least one device must be supplied.", nameof(wordDevices));
        }

        var body = options.FrameType == MitsubishiFrameType.OneE
            ? throw new NotSupportedException("1E monitor registration is not implemented in this release.")
            : Encode3ERandomReadBody(wordDevices, options);

        return Encode(options, new MitsubishiRawCommandRequest(MitsubishiCommands.EntryMonitorDevice, 0x0000, body, "Entry monitor device"));
    }

    public static byte[] EncodeExecuteMonitor(MitsubishiClientOptions options)
        => Encode(options, new MitsubishiRawCommandRequest(MitsubishiCommands.ExecuteMonitor, 0x0000, Array.Empty<byte>(), "Execute monitor"));

    public static byte[] EncodeBlockRead(MitsubishiClientOptions options, MitsubishiBlockRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (options.FrameType == MitsubishiFrameType.OneE)
        {
            throw new NotSupportedException("1E block read is not implemented in this release.");
        }

        var body = Encode3EBlocks(request, options, includeValues: false);
        return Encode(options, new MitsubishiRawCommandRequest(MitsubishiCommands.BlockRead, 0x0000, body, "Block read"));
    }

    public static byte[] EncodeBlockWrite(MitsubishiClientOptions options, MitsubishiBlockRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (options.FrameType == MitsubishiFrameType.OneE)
        {
            throw new NotSupportedException("1E block write is not implemented in this release.");
        }

        var body = Encode3EBlocks(request, options, includeValues: true);
        return Encode(options, new MitsubishiRawCommandRequest(MitsubishiCommands.BlockWrite, 0x0000, body, "Block write"));
    }

    public static byte[] EncodeReadTypeName(MitsubishiClientOptions options)
        => Encode(options, new MitsubishiRawCommandRequest(MitsubishiCommands.ReadTypeName, 0x0000, Array.Empty<byte>(), "Read type name"));

    public static byte[] EncodeRemoteOperation(MitsubishiClientOptions options, ushort command, bool force = true, bool clearMode = false)
    {
        byte[] body = command switch
        {
            MitsubishiCommands.RemoteRun => options.DataCode == CommunicationDataCode.Binary
                ? [Convert.ToByte(force ? 0x01 : 0x00), Convert.ToByte(clearMode ? 0x01 : 0x00)]
                : Encoding.ASCII.GetBytes($"{(force ? "0001" : "0000")}{(clearMode ? "0001" : "0000")}"),
            MitsubishiCommands.RemoteStop or MitsubishiCommands.RemotePause or MitsubishiCommands.RemoteLatchClear or MitsubishiCommands.RemoteReset => Array.Empty<byte>(),
            _ => throw new ArgumentOutOfRangeException(nameof(command)),
        };

        return Encode(options, new MitsubishiRawCommandRequest(command, 0x0000, body, $"Remote op {command:X4}"));
    }

    public static byte[] EncodeLoopback(MitsubishiClientOptions options, ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            throw new ArgumentException("Loopback payload must not be empty.", nameof(data));
        }

        byte[] body;
        if (options.FrameType == MitsubishiFrameType.OneE)
        {
            body = options.DataCode == CommunicationDataCode.Binary
                ? BuildLoopback1EBinary(data)
                : BuildLoopback1EAscii(data);
        }
        else
        {
            body = options.DataCode == CommunicationDataCode.Binary
                ? BuildLoopback3EBinary(data)
                : BuildLoopback3EAscii(data);
        }

        return Encode(options, new MitsubishiRawCommandRequest(MitsubishiCommands.LoopbackTest, 0x0000, body, "Loopback"));
    }

    public static byte[] EncodeRemotePassword(MitsubishiClientOptions options, ushort command, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        var body = options.DataCode == CommunicationDataCode.Binary
            ? BuildBinaryPasswordPayload(password)
            : BuildAsciiPasswordPayload(password);
        return Encode(options, new MitsubishiRawCommandRequest(command, 0x0000, body, command == MitsubishiCommands.Unlock ? "Unlock" : "Lock"));
    }

    public static byte[] EncodeMemoryAccess(MitsubishiClientOptions options, ushort command, ushort address, int length, ReadOnlySpan<ushort> values)
    {
        if (options.FrameType == MitsubishiFrameType.OneE)
        {
            throw new NotSupportedException("1E memory / extend unit access is not implemented in this release.");
        }

        var list = new List<byte>();
        AppendUInt16(list, address, options.DataCode);
        AppendUInt16(list, checked((ushort)length), options.DataCode);
        if (command is MitsubishiCommands.MemoryWrite or MitsubishiCommands.ExtendUnitWrite)
        {
            foreach (var value in values)
            {
                AppendUInt16(list, value, options.DataCode);
            }
        }

        return Encode(options, new MitsubishiRawCommandRequest(command, 0x0000, list, $"Memory op {command:X4}"));
    }

    private static byte[] Encode1E(MitsubishiClientOptions options, MitsubishiRawCommandRequest request)
    {
        var body = request.ResolvedBody;
        var buffer = new List<byte>();
        AppendByte(buffer, Get1ECommand(request.Command, request.Subcommand), options.DataCode);
        AppendByte(buffer, options.LegacyPcNumber, options.DataCode);
        AppendUInt16(buffer, options.MonitoringTimer, options.DataCode);
        buffer.AddRange(body);
        return buffer.ToArray();
    }

    private static byte[] Encode3E(MitsubishiClientOptions options, MitsubishiRawCommandRequest request)
    {
        var body = request.ResolvedBody;
        var buffer = new List<byte>();
        Append3EOr4ESubheader(buffer, options.DataCode, fourE: false, options.GetNextSerialNumber());
        AppendRoute(buffer, options.ResolvedRoute, options.DataCode);
        AppendUInt16(buffer, checked((ushort)(2 + 2 + 2 + body.Count)), options.DataCode);
        AppendUInt16(buffer, options.MonitoringTimer, options.DataCode);
        AppendUInt16(buffer, request.Command, options.DataCode);
        AppendUInt16(buffer, request.Subcommand, options.DataCode);
        buffer.AddRange(body);
        return buffer.ToArray();
    }

    private static byte[] Encode4E(MitsubishiClientOptions options, MitsubishiRawCommandRequest request)
    {
        var body = request.ResolvedBody;
        var buffer = new List<byte>();
        Append3EOr4ESubheader(buffer, options.DataCode, fourE: true, options.GetNextSerialNumber());
        AppendRoute(buffer, options.ResolvedRoute, options.DataCode);
        AppendUInt16(buffer, checked((ushort)(2 + 2 + 2 + body.Count)), options.DataCode);
        AppendUInt16(buffer, options.MonitoringTimer, options.DataCode);
        AppendUInt16(buffer, request.Command, options.DataCode);
        AppendUInt16(buffer, request.Subcommand, options.DataCode);
        buffer.AddRange(body);
        return buffer.ToArray();
    }

    private static Responce<byte[]> Decode1E(byte[] response)
    {
        var result = new Responce<byte[]> { Response = Convert.ToHexString(response) };
        if (response.Length < 2)
        {
            return result.Fail("1E response too short.");
        }

        var endCode = response[1];
        if (endCode == 0x5B)
        {
            var errorCode = response.Length >= 4 ? BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(2, 2)) : (ushort)0;
            return result.Fail($"PLC returned 1E error 0x{errorCode:X4}.", errorCode);
        }

        result.Value = response.Length > 2 ? response[2..] : Array.Empty<byte>();
        return result.EndTime();
    }

    private static Responce<byte[]> Decode3EOr4E(byte[] response)
    {
        var result = new Responce<byte[]> { Response = Convert.ToHexString(response) };
        if (response.Length < 11)
        {
            return result.Fail("3E/4E response too short.");
        }

        var isFourE = response[0] == 0xD4;
        var offset = isFourE ? 6 : 2;
        if (response.Length < offset + 9)
        {
            return result.Fail("3E/4E response header incomplete.");
        }

        var responseDataLength = BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(offset + 5, 2));
        var endCode = BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(offset + 7, 2));
        var payloadLength = Math.Max(0, responseDataLength - 2);
        var payloadStart = offset + 9;
        if (response.Length < payloadStart + payloadLength)
        {
            return result.Fail("3E/4E response payload truncated.");
        }

        if (endCode != 0)
        {
            result.ErrCode = endCode;
            return result.Fail($"PLC returned error 0x{endCode:X4}.", endCode);
        }

        result.Value = payloadLength == 0 ? Array.Empty<byte>() : response[payloadStart..(payloadStart + payloadLength)];
        return result.EndTime();
    }

    private static Responce<byte[]> Decode1EAscii(byte[] response)
    {
        var text = Encoding.ASCII.GetString(response);
        var result = new Responce<byte[]> { Response = text };
        if (text.Length < 4)
        {
            return result.Fail("1E ASCII response too short.");
        }

        var endCode = text.Substring(2, 2);
        if (string.Equals(endCode, "5B", StringComparison.OrdinalIgnoreCase))
        {
            var errorCode = text.Length >= 8 ? Convert.ToInt32(text.Substring(4, 4), 16) : 0;
            return result.Fail($"PLC returned 1E ASCII error 0x{errorCode:X4}.", errorCode);
        }

        result.Value = text.Length > 4 ? Encoding.ASCII.GetBytes(text[4..]) : Array.Empty<byte>();
        return result.EndTime();
    }

    private static Responce<byte[]> Decode3EOr4EAscii(byte[] response)
    {
        var text = Encoding.ASCII.GetString(response);
        var result = new Responce<byte[]> { Response = text };
        var isFourE = text.StartsWith("D4", StringComparison.OrdinalIgnoreCase);
        var expectedPrefixLength = isFourE ? 30 : 22;
        if (text.Length < expectedPrefixLength)
        {
            return result.Fail("3E/4E ASCII response too short.");
        }

        var responseDataLength = Convert.ToInt32(text.Substring(isFourE ? 22 : 14, 4), 16);
        var endCode = Convert.ToInt32(text.Substring(isFourE ? 26 : 18, 4), 16);
        var payloadLength = Math.Max(0, responseDataLength - 2) * 2;
        var payloadStart = isFourE ? 30 : 22;
        var remaining = text.Length - payloadStart;
        if (remaining == payloadLength - 4)
        {
            payloadStart -= 4;
        }
        else if (remaining == payloadLength + 4)
        {
            payloadStart += 4;
        }

        if (text.Length < payloadStart + payloadLength)
        {
            return result.Fail("3E/4E ASCII response payload truncated.");
        }

        if (endCode != 0)
        {
            result.ErrCode = endCode;
            return result.Fail($"PLC returned ASCII error 0x{endCode:X4}.", endCode);
        }

        result.Value = payloadLength == 0 ? Array.Empty<byte>() : Encoding.ASCII.GetBytes(text.Substring(payloadStart, payloadLength));
        return result.EndTime();
    }

    private static int? GetFixedResponseLength1E(ushort command, int requestBodyLength, CommunicationDataCode dataCode)
        => command switch
        {
            MitsubishiCommands.DeviceRead when requestBodyLength >= 0 => dataCode == CommunicationDataCode.Ascii
                ? 4 + (Math.Max(1, requestBodyLength / 10) * 4)
                : 2 + Math.Max(1, requestBodyLength / 6),
            MitsubishiCommands.LoopbackTest => dataCode == CommunicationDataCode.Ascii
                ? 4 + requestBodyLength
                : 4 + requestBodyLength,
            _ => null,
        };

    private static byte[] Encode1EDeviceBody(MitsubishiDeviceAddress address, int points, MitsubishiClientOptions options)
    {
        var metadata = address.Descriptor;
        if (options.DataCode == CommunicationDataCode.Binary)
        {
            var buffer = new List<byte>(8);
            AppendLegacyHeadDevice(buffer, address, options.XyNotation);
            AppendLegacyDeviceCode(buffer, metadata);
            AppendByte(buffer, ConvertPointCountToLegacyByte(points), options.DataCode);
            AppendByte(buffer, 0x00, options.DataCode);
            return buffer.ToArray();
        }

        var text = FormatAsciiLegacyAddress(address, options.XyNotation) + FormatAsciiByte(ConvertPointCountToLegacyByte(points)) + "00";
        return Encoding.ASCII.GetBytes(text);
    }

    private static byte[] Encode1EDeviceWriteBody(MitsubishiDeviceAddress address, ReadOnlySpan<ushort> values, MitsubishiClientOptions options, bool bitUnits)
    {
        var baseBody = Encode1EDeviceBody(address, values.Length, options).ToList();
        if (options.DataCode == CommunicationDataCode.Binary)
        {
            if (bitUnits)
            {
                foreach (var value in values)
                {
                    baseBody.Add(Convert.ToByte(value != 0 ? 0x01 : 0x00));
                }
            }
            else
            {
                foreach (var value in values)
                {
                    AppendUInt16(baseBody, value, options.DataCode);
                }
            }

            return baseBody.ToArray();
        }

        var builder = new StringBuilder(Encoding.ASCII.GetString(baseBody.ToArray()));
        if (bitUnits)
        {
            foreach (var value in values)
            {
                builder.Append(value == 0 ? '0' : '1');
            }
        }
        else
        {
            foreach (var value in values)
            {
                builder.Append(FormatAsciiUInt16(value));
            }
        }

        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    private static byte[] Encode3EDeviceBody(MitsubishiDeviceAddress address, int points, MitsubishiClientOptions options)
    {
        var metadata = address.Descriptor;
        var buffer = new List<byte>();
        AppendDeviceAddress(buffer, address, options, legacy: false);
        AppendUInt16(buffer, checked((ushort)points), options.DataCode);
        return buffer.ToArray();
    }

    private static byte[] Encode3EDeviceWriteBody(MitsubishiDeviceAddress address, ReadOnlySpan<ushort> values, MitsubishiClientOptions options, bool bitUnits)
    {
        var buffer = new List<byte>();
        buffer.AddRange(Encode3EDeviceBody(address, values.Length, options));
        if (options.DataCode == CommunicationDataCode.Binary)
        {
            if (bitUnits)
            {
                foreach (var value in values)
                {
                    buffer.Add(Convert.ToByte(value == 0 ? 0x00 : 0x10));
                }
            }
            else
            {
                foreach (var value in values)
                {
                    AppendUInt16(buffer, value, options.DataCode);
                }
            }
        }
        else
        {
            if (bitUnits)
            {
                foreach (var value in values)
                {
                    buffer.Add((byte)(value == 0 ? '0' : '1'));
                }
            }
            else
            {
                foreach (var value in values)
                {
                    foreach (var b in Encoding.ASCII.GetBytes(FormatAsciiUInt16(value)))
                    {
                        buffer.Add(b);
                    }
                }
            }
        }

        return buffer.ToArray();
    }

    private static byte[] Encode3ERandomReadBody(IReadOnlyList<MitsubishiDeviceAddress> devices, MitsubishiClientOptions options)
    {
        var buffer = new List<byte>();
        AppendByte(buffer, checked((byte)devices.Count), options.DataCode);
        AppendByte(buffer, 0x00, options.DataCode);
        foreach (var device in devices)
        {
            AppendDeviceAddress(buffer, device, options, legacy: false);
        }

        return buffer.ToArray();
    }

    private static byte[] Encode3ERandomWriteBody(IReadOnlyList<MitsubishiDeviceValue> values, MitsubishiClientOptions options)
    {
        var buffer = new List<byte>();
        AppendUInt16(buffer, checked((ushort)values.Count), options.DataCode);
        AppendUInt16(buffer, 0x0000, options.DataCode);
        foreach (var value in values)
        {
            AppendDeviceAddress(buffer, value.Address, options, legacy: false);
            AppendUInt16(buffer, value.Value, options.DataCode);
        }

        return buffer.ToArray();
    }

    private static byte[] Encode3EBlocks(MitsubishiBlockRequest request, MitsubishiClientOptions options, bool includeValues)
    {
        var buffer = new List<byte>();
        AppendUInt16(buffer, checked((ushort)request.ResolvedWordBlocks.Count), options.DataCode);
        AppendUInt16(buffer, checked((ushort)request.ResolvedBitBlocks.Count), options.DataCode);
        foreach (var block in request.ResolvedWordBlocks)
        {
            AppendDeviceAddress(buffer, block.Address, options, legacy: false);
            AppendUInt16(buffer, checked((ushort)block.Values.Length), options.DataCode);
            if (includeValues)
            {
                foreach (var value in block.Values.Span)
                {
                    AppendUInt16(buffer, value, options.DataCode);
                }
            }
        }

        foreach (var block in request.ResolvedBitBlocks)
        {
            AppendDeviceAddress(buffer, block.Address, options, legacy: false);
            AppendUInt16(buffer, checked((ushort)block.Values.Length), options.DataCode);
            if (includeValues)
            {
                foreach (var value in block.Values.Span)
                {
                    AppendByte(buffer, value ? (byte)0x10 : (byte)0x00, options.DataCode);
                }
            }
        }

        return buffer.ToArray();
    }

    private static byte[] BuildLoopback1EBinary(ReadOnlySpan<byte> data)
    {
        var buffer = new List<byte> { (byte)data.Length, 0x00 };
        buffer.AddRange(data.ToArray());
        return buffer.ToArray();
    }

    private static byte[] BuildLoopback1EAscii(ReadOnlySpan<byte> data)
        => Encoding.ASCII.GetBytes(FormatAsciiByte((byte)data.Length) + Encoding.ASCII.GetString(data));

    private static byte[] BuildLoopback3EBinary(ReadOnlySpan<byte> data)
    {
        var buffer = new List<byte>();
        AppendUInt16(buffer, checked((ushort)data.Length), CommunicationDataCode.Binary);
        buffer.AddRange(data.ToArray());
        return buffer.ToArray();
    }

    private static byte[] BuildLoopback3EAscii(ReadOnlySpan<byte> data)
        => Encoding.ASCII.GetBytes(FormatAsciiUInt16((ushort)data.Length) + Encoding.ASCII.GetString(data));

    private static byte[] BuildBinaryPasswordPayload(string password)
    {
        var bytes = Encoding.ASCII.GetBytes(password);
        var buffer = new List<byte>();
        AppendUInt16(buffer, checked((ushort)bytes.Length), CommunicationDataCode.Binary);
        buffer.AddRange(bytes);
        return buffer.ToArray();
    }

    private static byte[] BuildAsciiPasswordPayload(string password)
        => Encoding.ASCII.GetBytes(FormatAsciiUInt16((ushort)password.Length) + password.ToUpperInvariant());

    private static void Append3EOr4ESubheader(List<byte> buffer, CommunicationDataCode dataCode, bool fourE, ushort serialNumber)
    {
        if (dataCode == CommunicationDataCode.Binary)
        {
            if (fourE)
            {
                buffer.Add(0x54);
                buffer.Add(0x00);
                AppendUInt16(buffer, serialNumber, dataCode);
                buffer.Add(0x00);
                buffer.Add(0x00);
            }
            else
            {
                buffer.Add(0x50);
                buffer.Add(0x00);
            }

            return;
        }

        if (fourE)
        {
            foreach (var b in Encoding.ASCII.GetBytes($"5400{serialNumber:X4}0000"))
            {
                buffer.Add(b);
            }
        }
        else
        {
            foreach (var b in Encoding.ASCII.GetBytes("5000"))
            {
                buffer.Add(b);
            }
        }
    }

    private static void AppendRoute(List<byte> buffer, MitsubishiRoute route, CommunicationDataCode dataCode)
    {
        AppendByte(buffer, route.NetworkNumber, dataCode);
        AppendByte(buffer, route.StationNumber, dataCode);
        AppendUInt16(buffer, route.ModuleIoNumber, dataCode);
        AppendByte(buffer, route.MultidropStationNumber, dataCode);
    }

    private static void AppendDeviceAddress(List<byte> buffer, MitsubishiDeviceAddress address, MitsubishiClientOptions options, bool legacy)
    {
        var metadata = address.Descriptor;
        if (options.DataCode == CommunicationDataCode.Binary)
        {
            if (legacy)
            {
                AppendLegacyHeadDevice(buffer, address, options.XyNotation);
                AppendLegacyDeviceCode(buffer, metadata);
            }
            else
            {
                var raw = BitConverter.GetBytes(address.Number);
                buffer.Add(raw[0]);
                buffer.Add(raw[1]);
                buffer.Add(raw[2]);
                buffer.Add((byte)metadata.BinaryCode);
            }

            return;
        }

        if (legacy)
        {
            foreach (var b in Encoding.ASCII.GetBytes(FormatAsciiLegacyAddress(address, options.XyNotation)))
            {
                buffer.Add(b);
            }

            return;
        }

        foreach (var b in Encoding.ASCII.GetBytes(FormatAsciiModernAddress(address, metadata, options.XyNotation)))
        {
            buffer.Add(b);
        }
    }

    private static void AppendLegacyHeadDevice(List<byte> buffer, MitsubishiDeviceAddress address, XyAddressNotation notation)
    {
        var bytes = BitConverter.GetBytes(address.Number);
        buffer.Add(bytes[0]);
        buffer.Add(bytes[1]);
        buffer.Add(bytes[2]);
        buffer.Add(0x00);
    }

    private static void AppendLegacyDeviceCode(List<byte> buffer, MitsubishiDeviceMetadata metadata)
    {
        var code = BitConverter.GetBytes(metadata.AsciiCode);
        buffer.Add(code[0]);
        buffer.Add(code[1]);
    }

    private static string FormatAsciiLegacyAddress(MitsubishiDeviceAddress address, XyAddressNotation notation)
        => address.Number.ToString("X8", CultureInfo.InvariantCulture) + address.Descriptor.Symbol.PadRight(2, '*');

    private static string FormatAsciiModernAddress(MitsubishiDeviceAddress address, MitsubishiDeviceMetadata metadata, XyAddressNotation notation)
        => address.Number.ToString("X6", CultureInfo.InvariantCulture) + metadata.Symbol.PadRight(2, '*');

    private static byte Get1ECommand(ushort command, ushort subcommand)
        => command switch
        {
            MitsubishiCommands.DeviceRead when subcommand == 0x0000 => 0x00,
            MitsubishiCommands.DeviceRead when subcommand == 0x0001 => 0x01,
            MitsubishiCommands.DeviceWrite when subcommand == 0x0002 => 0x02,
            MitsubishiCommands.DeviceWrite when subcommand == 0x0003 => 0x03,
            MitsubishiCommands.RandomWrite when subcommand == 0x0001 => 0x04,
            MitsubishiCommands.RandomWrite when subcommand == 0x0000 => 0x05,
            MitsubishiCommands.EntryMonitorDevice => 0x06,
            MitsubishiCommands.ExecuteMonitor => 0x08,
            MitsubishiCommands.RemoteRun => 0x13,
            MitsubishiCommands.RemoteStop => 0x14,
            MitsubishiCommands.ReadTypeName => 0x15,
            MitsubishiCommands.LoopbackTest => 0x16,
            _ => throw new NotSupportedException($"1E command {command:X4}/{subcommand:X4} is not supported in this release."),
        };

    private static byte ConvertPointCountToLegacyByte(int points)
    {
        if (points is < 1 or > 256)
        {
            throw new ArgumentOutOfRangeException(nameof(points), "1E point count must be between 1 and 256.");
        }

        return points == 256 ? (byte)0x00 : checked((byte)points);
    }

    private static void AppendByte(List<byte> buffer, byte value, CommunicationDataCode dataCode)
    {
        if (dataCode == CommunicationDataCode.Binary)
        {
            buffer.Add(value);
            return;
        }

        foreach (var b in Encoding.ASCII.GetBytes(FormatAsciiByte(value)))
        {
            buffer.Add(b);
        }
    }

    private static void AppendUInt16(List<byte> buffer, ushort value, CommunicationDataCode dataCode)
    {
        if (dataCode == CommunicationDataCode.Binary)
        {
            buffer.Add((byte)(value & 0xFF));
            buffer.Add((byte)(value >> 8));
            return;
        }

        foreach (var b in Encoding.ASCII.GetBytes(FormatAsciiUInt16(value)))
        {
            buffer.Add(b);
        }
    }

    private static string FormatAsciiByte(byte value) => value.ToString("X2", CultureInfo.InvariantCulture);

    private static string FormatAsciiUInt16(ushort value) => value.ToString("X4", CultureInfo.InvariantCulture);
}
