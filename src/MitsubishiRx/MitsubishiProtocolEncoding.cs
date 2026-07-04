// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Globalization;
using System.Text;

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Provides the MitsubishiProtocolEncoding type.</summary>
internal static class MitsubishiProtocolEncoding
{
    /// <summary>Executes the Encode operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="request">The request parameter.</param>
    /// <returns>The Encode operation result.</returns>
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

    /// <summary>Executes the Decode operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="request">The request parameter.</param>
    /// <param name="response">The response parameter.</param>
    /// <returns>The Decode operation result.</returns>
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

    /// <summary>Executes the GetFixedResponseLength operation.</summary>
    /// <param name="frameType">The frameType parameter.</param>
    /// <param name="dataCode">The dataCode parameter.</param>
    /// <param name="command">The command parameter.</param>
    /// <param name="subcommand">The subcommand parameter.</param>
    /// <param name="requestBodyLength">The requestBodyLength parameter.</param>
    /// <param name="explicitLength">The explicitLength parameter.</param>
    /// <returns>The GetFixedResponseLength operation result.</returns>
    public static int? GetFixedResponseLength(MitsubishiFrameType frameType, CommunicationDataCode dataCode, ushort command, ushort subcommand, int requestBodyLength, int? explicitLength = null)
    {
        return explicitLength ?? frameType switch
        {
            MitsubishiFrameType.OneE => GetFixedResponseLength1E(command, requestBodyLength, dataCode),
            MitsubishiFrameType.ThreeE or MitsubishiFrameType.FourE => null,
            _ => null,
        };
    }

    /// <summary>Executes the EncodeDeviceBatchRead operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="address">The address parameter.</param>
    /// <param name="points">The points parameter.</param>
    /// <param name="bitUnits">The bitUnits parameter.</param>
    /// <returns>The EncodeDeviceBatchRead operation result.</returns>
    public static byte[] EncodeDeviceBatchRead(MitsubishiClientOptions options, MitsubishiDeviceAddress address, int points, bool bitUnits)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(points);
        var subcommand = options.FrameType == MitsubishiFrameType.OneE ? (ushort)(bitUnits ? 0x0000 : 0x0001) : (ushort)(bitUnits ? 0x0001 : 0x0000);
        var body = options.FrameType == MitsubishiFrameType.OneE ? Encode1EDeviceBody(address, points, options) : Encode3EDeviceBody(address, points, options);
        return Encode(options, new MitsubishiRawCommandRequest(MitsubishiCommands.DeviceRead, subcommand, body, $"Read {address.Original}"));
    }

    /// <summary>Executes the EncodeDeviceBatchWrite operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="address">The address parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <param name="bitUnits">The bitUnits parameter.</param>
    /// <returns>The EncodeDeviceBatchWrite operation result.</returns>
    public static byte[] EncodeDeviceBatchWrite(MitsubishiClientOptions options, MitsubishiDeviceAddress address, ReadOnlySpan<ushort> values, bool bitUnits)
    {
        if (values.IsEmpty)
        {
            throw new ArgumentException("At least one value must be supplied.", nameof(values));
        }

        var subcommand = options.FrameType == MitsubishiFrameType.OneE ? (ushort)(bitUnits ? 0x0002 : 0x0003) : (ushort)(bitUnits ? 0x0001 : 0x0000);
        var body = options.FrameType == MitsubishiFrameType.OneE ? Encode1EDeviceWriteBody(address, values, options, bitUnits) : Encode3EDeviceWriteBody(address, values, options, bitUnits);
        return Encode(options, new MitsubishiRawCommandRequest(MitsubishiCommands.DeviceWrite, subcommand, body, $"Write {address.Original}"));
    }

    /// <summary>Executes the EncodeRandomRead operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="wordDevices">The wordDevices parameter.</param>
    /// <returns>The EncodeRandomRead operation result.</returns>
    public static byte[] EncodeRandomRead(MitsubishiClientOptions options, IReadOnlyList<MitsubishiDeviceAddress> wordDevices)
    {
        ArgumentNullException.ThrowIfNull(wordDevices);
        if (wordDevices.Count == 0)
        {
            throw new ArgumentException("At least one device must be supplied.", nameof(wordDevices));
        }

        var body = options.FrameType == MitsubishiFrameType.OneE ? throw new NotSupportedException("1E random read is not implemented in this release.") : Encode3ERandomReadBody(wordDevices, options);
        return Encode(options, new MitsubishiRawCommandRequest(MitsubishiCommands.RandomRead, 0x0000, body, "Random read"));
    }

    /// <summary>Executes the EncodeRandomWrite operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <returns>The EncodeRandomWrite operation result.</returns>
    public static byte[] EncodeRandomWrite(MitsubishiClientOptions options, IReadOnlyList<MitsubishiDeviceValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            throw new ArgumentException("At least one device value must be supplied.", nameof(values));
        }

        var body = options.FrameType == MitsubishiFrameType.OneE ? throw new NotSupportedException("1E random write is not implemented in this release.") : Encode3ERandomWriteBody(values, options);
        return Encode(options, new MitsubishiRawCommandRequest(MitsubishiCommands.RandomWrite, 0x0000, body, "Random write"));
    }

    /// <summary>Executes the EncodeMonitorRegistration operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="wordDevices">The wordDevices parameter.</param>
    /// <returns>The EncodeMonitorRegistration operation result.</returns>
    public static byte[] EncodeMonitorRegistration(MitsubishiClientOptions options, IReadOnlyList<MitsubishiDeviceAddress> wordDevices)
    {
        ArgumentNullException.ThrowIfNull(wordDevices);
        if (wordDevices.Count == 0)
        {
            throw new ArgumentException("At least one device must be supplied.", nameof(wordDevices));
        }

        var body = options.FrameType == MitsubishiFrameType.OneE ? throw new NotSupportedException("1E monitor registration is not implemented in this release.") : Encode3ERandomReadBody(wordDevices, options);
        return Encode(options, new MitsubishiRawCommandRequest(MitsubishiCommands.EntryMonitorDevice, 0x0000, body, "Entry monitor device"));
    }

    /// <summary>Executes the EncodeExecuteMonitor operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <returns>The EncodeExecuteMonitor operation result.</returns>
    public static byte[] EncodeExecuteMonitor(MitsubishiClientOptions options) => Encode(options, new MitsubishiRawCommandRequest(MitsubishiCommands.ExecuteMonitor, 0x0000, [], "Execute monitor"));

    /// <summary>Executes the EncodeBlockRead operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="request">The request parameter.</param>
    /// <returns>The EncodeBlockRead operation result.</returns>
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

    /// <summary>Executes the EncodeBlockWrite operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="request">The request parameter.</param>
    /// <returns>The EncodeBlockWrite operation result.</returns>
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

    /// <summary>Executes the EncodeReadTypeName operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <returns>The EncodeReadTypeName operation result.</returns>
    public static byte[] EncodeReadTypeName(MitsubishiClientOptions options) => Encode(options, new MitsubishiRawCommandRequest(MitsubishiCommands.ReadTypeName, 0x0000, [], "Read type name"));

    /// <summary>Executes the EncodeRemoteOperation operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="command">The command parameter.</param>
    /// <param name="force">The force parameter.</param>
    /// <param name="clearMode">The clearMode parameter.</param>
    /// <returns>The EncodeRemoteOperation operation result.</returns>
    public static byte[] EncodeRemoteOperation(MitsubishiClientOptions options, ushort command, bool force = true, bool clearMode = false)
    {
        byte[] body = command switch
        {
            MitsubishiCommands.RemoteRun => options.DataCode == CommunicationDataCode.Binary ? [Convert.ToByte(force ? 0x01 : 0x00), Convert.ToByte(clearMode ? 0x01 : 0x00)] : Encoding.ASCII.GetBytes((force ? "0001" : "0000") + (clearMode ? "0001" : "0000")),
            MitsubishiCommands.RemoteStop or MitsubishiCommands.RemotePause or MitsubishiCommands.RemoteLatchClear or MitsubishiCommands.RemoteReset => Array.Empty<byte>(),
            _ => throw new ArgumentOutOfRangeException(nameof(command)),
        };
        return Encode(options, new MitsubishiRawCommandRequest(command, 0x0000, body, $"Remote op {command:X4}"));
    }

    /// <summary>Executes the EncodeLoopback operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="data">The data parameter.</param>
    /// <returns>The EncodeLoopback operation result.</returns>
    public static byte[] EncodeLoopback(MitsubishiClientOptions options, ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            throw new ArgumentException("Loopback payload must not be empty.", nameof(data));
        }

        var body = (options.FrameType, options.DataCode) switch
        {
            (MitsubishiFrameType.OneE, CommunicationDataCode.Binary) => BuildLoopback1EBinary(data),
            (MitsubishiFrameType.OneE, _) => BuildLoopback1EAscii(data),
            (_, CommunicationDataCode.Binary) => BuildLoopback3EBinary(data),
            _ => BuildLoopback3EAscii(data),
        };

        return Encode(options, new MitsubishiRawCommandRequest(MitsubishiCommands.LoopbackTest, 0x0000, body, "Loopback"));
    }

    /// <summary>Executes the EncodeRemotePassword operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="command">The command parameter.</param>
    /// <param name="password">The password parameter.</param>
    /// <returns>The EncodeRemotePassword operation result.</returns>
    public static byte[] EncodeRemotePassword(MitsubishiClientOptions options, ushort command, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        var body = options.DataCode == CommunicationDataCode.Binary ? BuildBinaryPasswordPayload(password) : BuildAsciiPasswordPayload(password);
        return Encode(options, new MitsubishiRawCommandRequest(command, 0x0000, body, command == MitsubishiCommands.Unlock ? "Unlock" : "Lock"));
    }

    /// <summary>Executes the EncodeMemoryAccess operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="command">The command parameter.</param>
    /// <param name="address">The address parameter.</param>
    /// <param name="length">The length parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <returns>The EncodeMemoryAccess operation result.</returns>
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

    /// <summary>Executes the Encode1E operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="request">The request parameter.</param>
    /// <returns>The Encode1E operation result.</returns>
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

    /// <summary>Executes the Encode3E operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="request">The request parameter.</param>
    /// <returns>The Encode3E operation result.</returns>
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

    /// <summary>Executes the Encode4E operation.</summary>
    /// <param name="options">The options parameter.</param>
    /// <param name="request">The request parameter.</param>
    /// <returns>The Encode4E operation result.</returns>
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

    /// <summary>Executes the Decode1E operation.</summary>
    /// <param name="response">The response parameter.</param>
    /// <returns>The Decode1E operation result.</returns>
    private static Responce<byte[]> Decode1E(byte[] response)
    {
        var result = new Responce<byte[]>
        {
            Response = Convert.ToHexString(response)
        };
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

    /// <summary>Executes the Decode3EOr4E operation.</summary>
    /// <param name="response">The response parameter.</param>
    /// <returns>The Decode3EOr4E operation result.</returns>
    private static Responce<byte[]> Decode3EOr4E(byte[] response)
    {
        var result = new Responce<byte[]>
        {
            Response = Convert.ToHexString(response)
        };
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

    /// <summary>Executes the Decode1EAscii operation.</summary>
    /// <param name="response">The response parameter.</param>
    /// <returns>The Decode1EAscii operation result.</returns>
    private static Responce<byte[]> Decode1EAscii(byte[] response)
    {
        var text = Encoding.ASCII.GetString(response);
        var result = new Responce<byte[]>
        {
            Response = text
        };
        if (text.Length < 4)
        {
            return result.Fail("1E ASCII response too short.");
        }

        var endCode = text[2..(2 + 2)];
        if (string.Equals(endCode, "5B", StringComparison.OrdinalIgnoreCase))
        {
            var errorCode = text.Length >= 8 ? Convert.ToInt32(text[4..(4 + 4)], 16) : 0;
            return result.Fail($"PLC returned 1E ASCII error 0x{errorCode:X4}.", errorCode);
        }

        result.Value = text.Length > 4 ? Encoding.ASCII.GetBytes(text[4..]) : Array.Empty<byte>();
        return result.EndTime();
    }

    /// <summary>Executes the Decode3EOr4EAscii operation.</summary>
    /// <param name="response">The response parameter.</param>
    /// <returns>The Decode3EOr4EAscii operation result.</returns>
    private static Responce<byte[]> Decode3EOr4EAscii(byte[] response)
    {
        var text = Encoding.ASCII.GetString(response);
        var result = new Responce<byte[]>
        {
            Response = text
        };
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
        payloadStart = AdjustAsciiPayloadStart(payloadStart, remaining, payloadLength);

        if (text.Length < payloadStart + payloadLength)
        {
            return result.Fail("3E/4E ASCII response payload truncated.");
        }

        if (endCode != 0)
        {
            result.ErrCode = endCode;
            return result.Fail($"PLC returned ASCII error 0x{endCode:X4}.", endCode);
        }

        result.Value = payloadLength == 0 ? Array.Empty<byte>() : Encoding.ASCII.GetBytes(text[payloadStart..(payloadStart + payloadLength)]);
        return result.EndTime();
    }

    /// <summary>Executes the AdjustAsciiPayloadStart operation.</summary>
    /// <param name="payloadStart">The payloadStart parameter.</param>
    /// <param name="remaining">The remaining parameter.</param>
    /// <param name="payloadLength">The payloadLength parameter.</param>
    /// <returns>The AdjustAsciiPayloadStart operation result.</returns>
    private static int AdjustAsciiPayloadStart(int payloadStart, int remaining, int payloadLength)
    {
        if (remaining == payloadLength - 4)
        {
            return payloadStart - 4;
        }

        return remaining == payloadLength + 4 ? payloadStart + 4 : payloadStart;
    }

    /// <summary>Executes the GetFixedResponseLength1E operation.</summary>
    /// <param name="command">The command parameter.</param>
    /// <param name="requestBodyLength">The requestBodyLength parameter.</param>
    /// <param name="dataCode">The dataCode parameter.</param>
    /// <returns>The GetFixedResponseLength1E operation result.</returns>
    private static int? GetFixedResponseLength1E(ushort command, int requestBodyLength, CommunicationDataCode dataCode) => command switch
    {
        MitsubishiCommands.DeviceRead when requestBodyLength >= 0 => dataCode == CommunicationDataCode.Ascii ? 4 + (Math.Max(1, requestBodyLength / 10) * 4) : 2 + Math.Max(1, requestBodyLength / 6),
        MitsubishiCommands.LoopbackTest => dataCode == CommunicationDataCode.Ascii ? 4 + requestBodyLength : 4 + requestBodyLength,
        _ => null,
    };

    /// <summary>Executes the Encode1EDeviceBody operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="points">The points parameter.</param>
    /// <param name="options">The options parameter.</param>
    /// <returns>The Encode1EDeviceBody operation result.</returns>
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

    /// <summary>Executes the Encode1EDeviceWriteBody operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <param name="options">The options parameter.</param>
    /// <param name="bitUnits">The bitUnits parameter.</param>
    /// <returns>The Encode1EDeviceWriteBody operation result.</returns>
    private static byte[] Encode1EDeviceWriteBody(MitsubishiDeviceAddress address, ReadOnlySpan<ushort> values, MitsubishiClientOptions options, bool bitUnits)
    {
        var baseBody = Encode1EDeviceBody(address, values.Length, options).ToList();
        if (options.DataCode == CommunicationDataCode.Binary)
        {
            Append1EBinaryWriteValues(baseBody, values, options, bitUnits);
            return baseBody.ToArray();
        }

        var builder = new StringBuilder(Encoding.ASCII.GetString(baseBody.ToArray()));
        Append1EAsciiWriteValues(builder, values, bitUnits);
        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    /// <summary>Executes the Append1EBinaryWriteValues operation.</summary>
    /// <param name="buffer">The buffer parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <param name="options">The options parameter.</param>
    /// <param name="bitUnits">The bitUnits parameter.</param>
    private static void Append1EBinaryWriteValues(List<byte> buffer, ReadOnlySpan<ushort> values, MitsubishiClientOptions options, bool bitUnits)
    {
        if (bitUnits)
        {
            foreach (var value in values)
            {
                buffer.Add(Convert.ToByte(value != 0 ? 0x01 : 0x00));
            }

            return;
        }

        foreach (var value in values)
        {
            AppendUInt16(buffer, value, options.DataCode);
        }
    }

    /// <summary>Executes the Append1EAsciiWriteValues operation.</summary>
    /// <param name="builder">The builder parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <param name="bitUnits">The bitUnits parameter.</param>
    private static void Append1EAsciiWriteValues(StringBuilder builder, ReadOnlySpan<ushort> values, bool bitUnits)
    {
        if (bitUnits)
        {
            foreach (var value in values)
            {
                _ = builder.Append(value == 0 ? '0' : '1');
            }

            return;
        }

        foreach (var value in values)
        {
            _ = builder.Append(FormatAsciiUInt16(value));
        }
    }

    /// <summary>Executes the Encode3EDeviceBody operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="points">The points parameter.</param>
    /// <param name="options">The options parameter.</param>
    /// <returns>The Encode3EDeviceBody operation result.</returns>
    private static byte[] Encode3EDeviceBody(MitsubishiDeviceAddress address, int points, MitsubishiClientOptions options)
    {
        var metadata = address.Descriptor;
        var buffer = new List<byte>();
        AppendDeviceAddress(buffer, address, options, legacy: false);
        AppendUInt16(buffer, checked((ushort)points), options.DataCode);
        return buffer.ToArray();
    }

    /// <summary>Executes the Encode3EDeviceWriteBody operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <param name="options">The options parameter.</param>
    /// <param name="bitUnits">The bitUnits parameter.</param>
    /// <returns>The Encode3EDeviceWriteBody operation result.</returns>
    private static byte[] Encode3EDeviceWriteBody(MitsubishiDeviceAddress address, ReadOnlySpan<ushort> values, MitsubishiClientOptions options, bool bitUnits)
    {
        var buffer = new List<byte>();
        buffer.AddRange(Encode3EDeviceBody(address, values.Length, options));
        Append3EWriteValues(buffer, values, options, bitUnits);
        return buffer.ToArray();
    }

    /// <summary>Executes the Append3EWriteValues operation.</summary>
    /// <param name="buffer">The buffer parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <param name="options">The options parameter.</param>
    /// <param name="bitUnits">The bitUnits parameter.</param>
    private static void Append3EWriteValues(List<byte> buffer, ReadOnlySpan<ushort> values, MitsubishiClientOptions options, bool bitUnits)
    {
        if (options.DataCode == CommunicationDataCode.Binary)
        {
            Append3EBinaryWriteValues(buffer, values, options, bitUnits);
            return;
        }

        Append3EAsciiWriteValues(buffer, values, bitUnits);
    }

    /// <summary>Executes the Append3EBinaryWriteValues operation.</summary>
    /// <param name="buffer">The buffer parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <param name="options">The options parameter.</param>
    /// <param name="bitUnits">The bitUnits parameter.</param>
    private static void Append3EBinaryWriteValues(List<byte> buffer, ReadOnlySpan<ushort> values, MitsubishiClientOptions options, bool bitUnits)
    {
        if (bitUnits)
        {
            foreach (var value in values)
            {
                buffer.Add(Convert.ToByte(value == 0 ? 0x00 : 0x10));
            }

            return;
        }

        foreach (var value in values)
        {
            AppendUInt16(buffer, value, options.DataCode);
        }
    }

    /// <summary>Executes the Append3EAsciiWriteValues operation.</summary>
    /// <param name="buffer">The buffer parameter.</param>
    /// <param name="values">The values parameter.</param>
    /// <param name="bitUnits">The bitUnits parameter.</param>
    private static void Append3EAsciiWriteValues(List<byte> buffer, ReadOnlySpan<ushort> values, bool bitUnits)
    {
        if (bitUnits)
        {
            foreach (var value in values)
            {
                buffer.Add((byte)(value == 0 ? '0' : '1'));
            }

            return;
        }

        foreach (var value in values)
        {
            buffer.AddRange(Encoding.ASCII.GetBytes(FormatAsciiUInt16(value)));
        }
    }

    /// <summary>Executes the Encode3ERandomReadBody operation.</summary>
    /// <param name="devices">The devices parameter.</param>
    /// <param name="options">The options parameter.</param>
    /// <returns>The Encode3ERandomReadBody operation result.</returns>
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

    /// <summary>Executes the Encode3ERandomWriteBody operation.</summary>
    /// <param name="values">The values parameter.</param>
    /// <param name="options">The options parameter.</param>
    /// <returns>The Encode3ERandomWriteBody operation result.</returns>
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

    /// <summary>Executes the Encode3EBlocks operation.</summary>
    /// <param name="request">The request parameter.</param>
    /// <param name="options">The options parameter.</param>
    /// <param name="includeValues">The includeValues parameter.</param>
    /// <returns>The Encode3EBlocks operation result.</returns>
    private static byte[] Encode3EBlocks(MitsubishiBlockRequest request, MitsubishiClientOptions options, bool includeValues)
    {
        var buffer = new List<byte>();
        AppendUInt16(buffer, checked((ushort)request.ResolvedWordBlocks.Count), options.DataCode);
        AppendUInt16(buffer, checked((ushort)request.ResolvedBitBlocks.Count), options.DataCode);
        foreach (var block in request.ResolvedWordBlocks)
        {
            Append3EWordBlock(buffer, block, options, includeValues);
        }

        foreach (var block in request.ResolvedBitBlocks)
        {
            Append3EBitBlock(buffer, block, options, includeValues);
        }

        return buffer.ToArray();
    }

    /// <summary>Executes the Append3EWordBlock operation.</summary>
    /// <param name="buffer">The buffer parameter.</param>
    /// <param name="block">The block parameter.</param>
    /// <param name="options">The options parameter.</param>
    /// <param name="includeValues">The includeValues parameter.</param>
    private static void Append3EWordBlock(List<byte> buffer, MitsubishiWordBlock block, MitsubishiClientOptions options, bool includeValues)
    {
        AppendDeviceAddress(buffer, block.Address, options, legacy: false);
        AppendUInt16(buffer, checked((ushort)block.Values.Length), options.DataCode);
        if (!includeValues)
        {
            return;
        }

        foreach (var value in block.Values.Span)
        {
            AppendUInt16(buffer, value, options.DataCode);
        }
    }

    /// <summary>Executes the Append3EBitBlock operation.</summary>
    /// <param name="buffer">The buffer parameter.</param>
    /// <param name="block">The block parameter.</param>
    /// <param name="options">The options parameter.</param>
    /// <param name="includeValues">The includeValues parameter.</param>
    private static void Append3EBitBlock(List<byte> buffer, MitsubishiBitBlock block, MitsubishiClientOptions options, bool includeValues)
    {
        AppendDeviceAddress(buffer, block.Address, options, legacy: false);
        AppendUInt16(buffer, checked((ushort)block.Values.Length), options.DataCode);
        if (!includeValues)
        {
            return;
        }

        foreach (var value in block.Values.Span)
        {
            AppendByte(buffer, value ? (byte)0x10 : (byte)0x00, options.DataCode);
        }
    }

    /// <summary>Executes the BuildLoopback1EBinary operation.</summary>
    /// <param name="data">The data parameter.</param>
    /// <returns>The BuildLoopback1EBinary operation result.</returns>
    private static byte[] BuildLoopback1EBinary(ReadOnlySpan<byte> data)
    {
        var buffer = new List<byte>
        {
            (byte)data.Length,
            0x00
        };
        buffer.AddRange(data.ToArray());
        return buffer.ToArray();
    }

    /// <summary>Executes the BuildLoopback1EAscii operation.</summary>
    /// <param name="data">The data parameter.</param>
    /// <returns>The BuildLoopback1EAscii operation result.</returns>
    private static byte[] BuildLoopback1EAscii(ReadOnlySpan<byte> data) => Encoding.ASCII.GetBytes(FormatAsciiByte((byte)data.Length) + Encoding.ASCII.GetString(data));

    /// <summary>Executes the BuildLoopback3EBinary operation.</summary>
    /// <param name="data">The data parameter.</param>
    /// <returns>The BuildLoopback3EBinary operation result.</returns>
    private static byte[] BuildLoopback3EBinary(ReadOnlySpan<byte> data)
    {
        var buffer = new List<byte>();
        AppendUInt16(buffer, checked((ushort)data.Length), CommunicationDataCode.Binary);
        buffer.AddRange(data.ToArray());
        return buffer.ToArray();
    }

    /// <summary>Executes the BuildLoopback3EAscii operation.</summary>
    /// <param name="data">The data parameter.</param>
    /// <returns>The BuildLoopback3EAscii operation result.</returns>
    private static byte[] BuildLoopback3EAscii(ReadOnlySpan<byte> data) => Encoding.ASCII.GetBytes(FormatAsciiUInt16((ushort)data.Length) + Encoding.ASCII.GetString(data));

    /// <summary>Executes the BuildBinaryPasswordPayload operation.</summary>
    /// <param name="password">The password parameter.</param>
    /// <returns>The BuildBinaryPasswordPayload operation result.</returns>
    private static byte[] BuildBinaryPasswordPayload(string password)
    {
        var bytes = Encoding.ASCII.GetBytes(password);
        var buffer = new List<byte>();
        AppendUInt16(buffer, checked((ushort)bytes.Length), CommunicationDataCode.Binary);
        buffer.AddRange(bytes);
        return buffer.ToArray();
    }

    /// <summary>Executes the BuildAsciiPasswordPayload operation.</summary>
    /// <param name="password">The password parameter.</param>
    /// <returns>The BuildAsciiPasswordPayload operation result.</returns>
    private static byte[] BuildAsciiPasswordPayload(string password) => Encoding.ASCII.GetBytes(FormatAsciiUInt16((ushort)password.Length) + password.ToUpperInvariant());

    /// <summary>Executes the Append3EOr4ESubheader operation.</summary>
    /// <param name="buffer">The buffer parameter.</param>
    /// <param name="dataCode">The dataCode parameter.</param>
    /// <param name="fourE">The fourE parameter.</param>
    /// <param name="serialNumber">The serialNumber parameter.</param>
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
            buffer.AddRange(Encoding.ASCII.GetBytes($"5400{serialNumber:X4}0000"));
        }
        else
        {
            buffer.AddRange(Encoding.ASCII.GetBytes("5000"));
        }
    }

    /// <summary>Executes the AppendRoute operation.</summary>
    /// <param name="buffer">The buffer parameter.</param>
    /// <param name="route">The route parameter.</param>
    /// <param name="dataCode">The dataCode parameter.</param>
    private static void AppendRoute(List<byte> buffer, MitsubishiRoute route, CommunicationDataCode dataCode)
    {
        AppendByte(buffer, route.NetworkNumber, dataCode);
        AppendByte(buffer, route.StationNumber, dataCode);
        AppendUInt16(buffer, route.ModuleIoNumber, dataCode);
        AppendByte(buffer, route.MultidropStationNumber, dataCode);
    }

    /// <summary>Executes the AppendDeviceAddress operation.</summary>
    /// <param name="buffer">The buffer parameter.</param>
    /// <param name="address">The address parameter.</param>
    /// <param name="options">The options parameter.</param>
    /// <param name="legacy">The legacy parameter.</param>
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
            buffer.AddRange(Encoding.ASCII.GetBytes(FormatAsciiLegacyAddress(address, options.XyNotation)));
            return;
        }

        buffer.AddRange(Encoding.ASCII.GetBytes(FormatAsciiModernAddress(address, metadata, options.XyNotation)));
    }

    /// <summary>Executes the AppendLegacyHeadDevice operation.</summary>
    /// <param name="buffer">The buffer parameter.</param>
    /// <param name="address">The address parameter.</param>
    /// <param name="notation">The notation parameter.</param>
    private static void AppendLegacyHeadDevice(List<byte> buffer, MitsubishiDeviceAddress address, XyAddressNotation notation)
    {
        var bytes = BitConverter.GetBytes(address.Number);
        buffer.Add(bytes[0]);
        buffer.Add(bytes[1]);
        buffer.Add(bytes[2]);
        buffer.Add(0x00);
    }

    /// <summary>Executes the AppendLegacyDeviceCode operation.</summary>
    /// <param name="buffer">The buffer parameter.</param>
    /// <param name="metadata">The metadata parameter.</param>
    private static void AppendLegacyDeviceCode(List<byte> buffer, MitsubishiDeviceMetadata metadata)
    {
        var code = BitConverter.GetBytes(metadata.AsciiCode);
        buffer.Add(code[0]);
        buffer.Add(code[1]);
    }

    /// <summary>Executes the FormatAsciiLegacyAddress operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="notation">The notation parameter.</param>
    /// <returns>The FormatAsciiLegacyAddress operation result.</returns>
    private static string FormatAsciiLegacyAddress(MitsubishiDeviceAddress address, XyAddressNotation notation) => address.Number.ToString("X8", CultureInfo.InvariantCulture) + address.Descriptor.Symbol.PadRight(2, '*');

    /// <summary>Executes the FormatAsciiModernAddress operation.</summary>
    /// <param name="address">The address parameter.</param>
    /// <param name="metadata">The metadata parameter.</param>
    /// <param name="notation">The notation parameter.</param>
    /// <returns>The FormatAsciiModernAddress operation result.</returns>
    private static string FormatAsciiModernAddress(MitsubishiDeviceAddress address, MitsubishiDeviceMetadata metadata, XyAddressNotation notation) => address.Number.ToString("X6", CultureInfo.InvariantCulture) + metadata.Symbol.PadRight(2, '*');

    /// <summary>Executes the Get1ECommand operation.</summary>
    /// <param name="command">The command parameter.</param>
    /// <param name="subcommand">The subcommand parameter.</param>
    /// <returns>The Get1ECommand operation result.</returns>
    private static byte Get1ECommand(ushort command, ushort subcommand) => command switch
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

    /// <summary>Executes the ConvertPointCountToLegacyByte operation.</summary>
    /// <param name="points">The points parameter.</param>
    /// <returns>The ConvertPointCountToLegacyByte operation result.</returns>
    private static byte ConvertPointCountToLegacyByte(int points)
    {
        if (points is < 1 or > 256)
        {
            throw new ArgumentOutOfRangeException(nameof(points), "1E point count must be between 1 and 256.");
        }

        return points == 256 ? (byte)0x00 : checked((byte)points);
    }

    /// <summary>Executes the AppendByte operation.</summary>
    /// <param name="buffer">The buffer parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="dataCode">The dataCode parameter.</param>
    private static void AppendByte(List<byte> buffer, byte value, CommunicationDataCode dataCode)
    {
        if (dataCode == CommunicationDataCode.Binary)
        {
            buffer.Add(value);
            return;
        }

        buffer.AddRange(Encoding.ASCII.GetBytes(FormatAsciiByte(value)));
    }

    /// <summary>Executes the AppendUInt16 operation.</summary>
    /// <param name="buffer">The buffer parameter.</param>
    /// <param name="value">The value parameter.</param>
    /// <param name="dataCode">The dataCode parameter.</param>
    private static void AppendUInt16(List<byte> buffer, ushort value, CommunicationDataCode dataCode)
    {
        if (dataCode == CommunicationDataCode.Binary)
        {
            buffer.Add((byte)(value & 0xFF));
            buffer.Add((byte)(value >> 8));
            return;
        }

        buffer.AddRange(Encoding.ASCII.GetBytes(FormatAsciiUInt16(value)));
    }

    /// <summary>Executes the FormatAsciiByte operation.</summary>
    /// <param name="value">The value parameter.</param>
    /// <returns>The FormatAsciiByte operation result.</returns>
    private static string FormatAsciiByte(byte value) => value.ToString("X2", CultureInfo.InvariantCulture);

    /// <summary>Executes the FormatAsciiUInt16 operation.</summary>
    /// <param name="value">The value parameter.</param>
    /// <returns>The FormatAsciiUInt16 operation result.</returns>
    private static string FormatAsciiUInt16(ushort value) => value.ToString("X4", CultureInfo.InvariantCulture);
}
