// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

namespace MitsubishiRx;

internal static class MitsubishiSerialProtocolEncoding
{
    private const byte Enq = 0x05;
    private const byte Ack = 0x06;
    private const byte Nak = 0x15;
    private const byte Stx = 0x02;
    private const byte Etx = 0x03;
    private const byte Cr = 0x0D;
    private const byte Lf = 0x0A;
    private const byte Dle = 0x10;
    private const byte FourCFrameId = 0xF8;
    private const byte ThreeCFrameId = 0xF9;
    private const string ResponseIdAscii = "FFFF";
    private const ushort ResponseIdBinary = 0xFFFF;

    public static byte[] EncodeWordReadRequest(MitsubishiClientOptions options, MitsubishiDeviceAddress address, int points)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(address);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(points);

        return options.FrameType switch
        {
            MitsubishiFrameType.OneC => Encode1CRequest(options, address, points, wordUnits: true),
            MitsubishiFrameType.ThreeC => Encode3CRequest(options, address, points, wordUnits: true),
            MitsubishiFrameType.FourC => Encode4CRequest(options, address, points, wordUnits: true),
            _ => throw new NotSupportedException($"Serial encoding is not supported for frame type '{options.FrameType}'."),
        };
    }

    public static byte[] EncodeWordWriteRequest(MitsubishiClientOptions options, MitsubishiDeviceAddress address, IReadOnlyList<ushort> values)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            throw new ArgumentException("At least one value must be supplied.", nameof(values));
        }

        return options.FrameType switch
        {
            MitsubishiFrameType.OneC => Encode1CWriteRequest(options, address, values),
            MitsubishiFrameType.ThreeC => Encode3CWriteRequest(options, address, values),
            MitsubishiFrameType.FourC => Encode4CWriteRequest(options, address, values),
            _ => throw new NotSupportedException($"Serial encoding is not supported for frame type '{options.FrameType}'."),
        };
    }

    public static byte[] EncodeBitReadRequest(MitsubishiClientOptions options, MitsubishiDeviceAddress address, int points)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(address);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(points);

        return options.FrameType switch
        {
            MitsubishiFrameType.OneC => Encode1CRequest(options, address, points, wordUnits: false),
            MitsubishiFrameType.ThreeC => Encode3CRequest(options, address, points, wordUnits: false),
            MitsubishiFrameType.FourC => Encode4CRequest(options, address, points, wordUnits: false),
            _ => throw new NotSupportedException($"Serial encoding is not supported for frame type '{options.FrameType}'."),
        };
    }

    public static byte[] EncodeBitWriteRequest(MitsubishiClientOptions options, MitsubishiDeviceAddress address, IReadOnlyList<bool> values)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            throw new ArgumentException("At least one value must be supplied.", nameof(values));
        }

        return options.FrameType switch
        {
            MitsubishiFrameType.OneC => Encode1CBitWriteRequest(options, address, values),
            MitsubishiFrameType.ThreeC => Encode3CBitWriteRequest(options, address, values),
            MitsubishiFrameType.FourC => Encode4CBitWriteRequest(options, address, values),
            _ => throw new NotSupportedException($"Serial encoding is not supported for frame type '{options.FrameType}'."),
        };
    }

    public static byte[] EncodeRandomReadRequest(MitsubishiClientOptions options, IReadOnlyList<MitsubishiDeviceAddress> addresses)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(addresses);
        if (addresses.Count == 0)
        {
            throw new ArgumentException("At least one device must be supplied.", nameof(addresses));
        }

        return options.FrameType switch
        {
            MitsubishiFrameType.OneC => throw new NotSupportedException("1C serial random read is not implemented in this release."),
            MitsubishiFrameType.ThreeC => Encode3CRandomReadRequest(options, addresses),
            MitsubishiFrameType.FourC => Encode4CRandomReadRequest(options, addresses),
            _ => throw new NotSupportedException($"Serial encoding is not supported for frame type '{options.FrameType}'."),
        };
    }

    public static byte[] EncodeRandomWriteRequest(MitsubishiClientOptions options, IReadOnlyList<MitsubishiDeviceValue> values)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            throw new ArgumentException("At least one device value must be supplied.", nameof(values));
        }

        return options.FrameType switch
        {
            MitsubishiFrameType.OneC => throw new NotSupportedException("1C serial random write is not implemented in this release."),
            MitsubishiFrameType.ThreeC => Encode3CRandomWriteRequest(options, values),
            MitsubishiFrameType.FourC => Encode4CRandomWriteRequest(options, values),
            _ => throw new NotSupportedException($"Serial encoding is not supported for frame type '{options.FrameType}'."),
        };
    }

    public static byte[] EncodeBlockReadRequest(MitsubishiClientOptions options, MitsubishiBlockRequest request)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(request);

        return options.FrameType switch
        {
            MitsubishiFrameType.OneC => throw new NotSupportedException("1C serial block read is not implemented in this release."),
            MitsubishiFrameType.ThreeC => Encode3CBlockReadRequest(options, request),
            MitsubishiFrameType.FourC => Encode4CBlockReadRequest(options, request),
            _ => throw new NotSupportedException($"Serial encoding is not supported for frame type '{options.FrameType}'."),
        };
    }

    public static byte[] EncodeBlockWriteRequest(MitsubishiClientOptions options, MitsubishiBlockRequest request)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(request);

        return options.FrameType switch
        {
            MitsubishiFrameType.OneC => throw new NotSupportedException("1C serial block write is not implemented in this release."),
            MitsubishiFrameType.ThreeC => Encode3CBlockWriteRequest(options, request),
            MitsubishiFrameType.FourC => Encode4CBlockWriteRequest(options, request),
            _ => throw new NotSupportedException($"Serial encoding is not supported for frame type '{options.FrameType}'."),
        };
    }

    public static byte[] EncodeMonitorRegistrationRequest(MitsubishiClientOptions options, IReadOnlyList<MitsubishiDeviceAddress> addresses)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(addresses);
        if (addresses.Count == 0)
        {
            throw new ArgumentException("At least one device must be supplied.", nameof(addresses));
        }

        return options.FrameType switch
        {
            MitsubishiFrameType.OneC => throw new NotSupportedException("1C serial monitor registration is not implemented in this release."),
            MitsubishiFrameType.ThreeC => Encode3CMonitorRegistrationRequest(options, addresses),
            MitsubishiFrameType.FourC => Encode4CMonitorRegistrationRequest(options, addresses),
            _ => throw new NotSupportedException($"Serial encoding is not supported for frame type '{options.FrameType}'."),
        };
    }

    public static byte[] EncodeExecuteMonitorRequest(MitsubishiClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.FrameType switch
        {
            MitsubishiFrameType.OneC => throw new NotSupportedException("1C serial execute monitor is not implemented in this release."),
            MitsubishiFrameType.ThreeC => Encode3CExecuteMonitorRequest(options),
            MitsubishiFrameType.FourC => Encode4CExecuteMonitorRequest(options),
            _ => throw new NotSupportedException($"Serial encoding is not supported for frame type '{options.FrameType}'."),
        };
    }

    public static byte[] EncodeRemoteOperationRequest(MitsubishiClientOptions options, ushort command, bool force = true, bool clearMode = false)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.FrameType switch
        {
            MitsubishiFrameType.OneC => throw new NotSupportedException("1C serial remote operation is not implemented in this release."),
            MitsubishiFrameType.ThreeC => Encode3CRemoteOperationRequest(options, command, force, clearMode),
            MitsubishiFrameType.FourC => Encode4CRemoteOperationRequest(options, command, force, clearMode),
            _ => throw new NotSupportedException($"Serial encoding is not supported for frame type '{options.FrameType}'."),
        };
    }

    public static byte[] EncodeReadTypeNameRequest(MitsubishiClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.FrameType switch
        {
            MitsubishiFrameType.OneC => throw new NotSupportedException("1C serial type-name read is not implemented in this release."),
            MitsubishiFrameType.ThreeC => Encode3CRawRequest(options, MitsubishiCommands.ReadTypeName, 0x0000, Array.Empty<byte>()),
            MitsubishiFrameType.FourC => Encode4CRawRequest(options, MitsubishiCommands.ReadTypeName, 0x0000, Array.Empty<byte>()),
            _ => throw new NotSupportedException($"Serial encoding is not supported for frame type '{options.FrameType}'."),
        };
    }

    public static byte[] EncodeLoopbackRequest(MitsubishiClientOptions options, ReadOnlySpan<byte> data)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (data.IsEmpty)
        {
            throw new ArgumentException("Loopback payload must not be empty.", nameof(data));
        }

        return options.FrameType switch
        {
            MitsubishiFrameType.OneC => throw new NotSupportedException("1C serial loopback is not implemented in this release."),
            MitsubishiFrameType.ThreeC => Encode3CRawRequest(options, MitsubishiCommands.LoopbackTest, 0x0000, BuildLoopbackBody(options, data)),
            MitsubishiFrameType.FourC => Encode4CRawRequest(options, MitsubishiCommands.LoopbackTest, 0x0000, BuildLoopbackBody(options, data)),
            _ => throw new NotSupportedException($"Serial encoding is not supported for frame type '{options.FrameType}'."),
        };
    }

    public static byte[] EncodeMemoryAccessRequest(MitsubishiClientOptions options, ushort command, ushort address, int length, ReadOnlySpan<ushort> values)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.FrameType switch
        {
            MitsubishiFrameType.OneC => throw new NotSupportedException("1C serial memory / extend-unit access is not implemented in this release."),
            MitsubishiFrameType.ThreeC => Encode3CRawRequest(options, command, 0x0000, BuildMemoryAccessBody(options, command, address, length, values)),
            MitsubishiFrameType.FourC => Encode4CRawRequest(options, command, 0x0000, BuildMemoryAccessBody(options, command, address, length, values)),
            _ => throw new NotSupportedException($"Serial encoding is not supported for frame type '{options.FrameType}'."),
        };
    }

    public static byte[] EncodeRawRequest(MitsubishiClientOptions options, MitsubishiRawCommandRequest request)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(request);

        return options.FrameType switch
        {
            MitsubishiFrameType.OneC => throw new NotSupportedException("Raw serial execution is not yet implemented for 1C commands."),
            MitsubishiFrameType.ThreeC => Encode3CRawRequest(options, request.Command, request.Subcommand, request.ResolvedBody),
            MitsubishiFrameType.FourC => Encode4CRawRequest(options, request.Command, request.Subcommand, request.ResolvedBody),
            _ => throw new NotSupportedException($"Serial encoding is not supported for frame type '{options.FrameType}'."),
        };
    }

    public static Responce<byte[]> Decode(MitsubishiClientOptions options, byte[] response)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(response);

        return options.FrameType switch
        {
            MitsubishiFrameType.OneC => Decode1C(options, response),
            MitsubishiFrameType.ThreeC => Decode3C(options, response),
            MitsubishiFrameType.FourC => Decode4C(options, response),
            _ => throw new NotSupportedException($"Serial decoding is not supported for frame type '{options.FrameType}'."),
        };
    }

    public static bool IsExpectedFrameComplete(MitsubishiClientOptions options, ReadOnlySpan<byte> buffer)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (buffer.IsEmpty)
        {
            return false;
        }

        return options.FrameType switch
        {
            MitsubishiFrameType.OneC => options.ResolvedSerial.MessageFormat switch
            {
                MitsubishiSerialMessageFormat.Format1 => HasAsciiChecksumFramedMessage(buffer),
                MitsubishiSerialMessageFormat.Format4 => HasAsciiChecksumFramedMessage(buffer),
                _ => false,
            },
            MitsubishiFrameType.ThreeC => options.ResolvedSerial.MessageFormat switch
            {
                MitsubishiSerialMessageFormat.Format1 => HasAsciiChecksumFramedMessage(buffer),
                MitsubishiSerialMessageFormat.Format4 => HasAsciiChecksumFramedMessage(buffer),
                _ => false,
            },
            MitsubishiFrameType.FourC => options.ResolvedSerial.MessageFormat switch
            {
                MitsubishiSerialMessageFormat.Format1 => HasAsciiChecksumFramedMessage(buffer),
                MitsubishiSerialMessageFormat.Format4 => HasAsciiChecksumFramedMessage(buffer),
                MitsubishiSerialMessageFormat.Format5 => HasBinaryFormat5Message(buffer),
                _ => false,
            },
            _ => false,
        };
    }

    private static byte[] Encode1CRequest(MitsubishiClientOptions options, MitsubishiDeviceAddress address, int points, bool wordUnits)
    {
        EnsureAscii(options);
        var serial = options.ResolvedSerial;
        var metadata = address.Descriptor;
        var command = wordUnits && metadata.Kind == DeviceValueKind.Word ? "WR" : "BR";
        var body = string.Concat(
            FormatAsciiByte(serial.StationNumber),
            FormatAsciiByte(serial.PcNumber),
            command,
            FormatAsciiNibble(serial.MessageWait),
            Format1CAddress(address, metadata),
            FormatPointCount(points));

        return WrapAscii(body, serial.MessageFormat);
    }

    private static byte[] Encode3CRequest(MitsubishiClientOptions options, MitsubishiDeviceAddress address, int points, bool wordUnits)
    {
        EnsureAscii(options);
        var serial = options.ResolvedSerial;
        var metadata = address.Descriptor;
        var command = wordUnits && metadata.Kind == DeviceValueKind.Word ? "0401" : "0401";
        var subcommand = wordUnits ? "0000" : "0001";
        var body = string.Concat(
            FormatAsciiByte(ThreeCFrameId),
            FormatAsciiByte(serial.StationNumber),
            FormatAsciiByte(serial.NetworkNumber),
            FormatAsciiByte(serial.PcNumber),
            FormatAsciiByte(serial.SelfStationNumber),
            command,
            subcommand,
            FormatDeviceAddressModern(address, metadata),
            FormatPointCount(points));

        return WrapAscii(body, serial.MessageFormat);
    }

    private static byte[] Encode4CRequest(MitsubishiClientOptions options, MitsubishiDeviceAddress address, int points, bool wordUnits)
    {
        var serial = options.ResolvedSerial;
        var metadata = address.Descriptor;
        return serial.MessageFormat switch
        {
            MitsubishiSerialMessageFormat.Format1 or MitsubishiSerialMessageFormat.Format4 => Encode4CAsciiRequest(serial, address, metadata, points, wordUnits),
            MitsubishiSerialMessageFormat.Format5 => Encode4CBinaryRequest(serial, address, metadata, points, wordUnits),
            _ => throw new NotSupportedException($"Unsupported 4C serial message format '{serial.MessageFormat}'."),
        };
    }

    private static byte[] Encode4CAsciiRequest(MitsubishiSerialOptions serial, MitsubishiDeviceAddress address, MitsubishiDeviceMetadata metadata, int points, bool wordUnits)
    {
        var command = wordUnits && metadata.Kind == DeviceValueKind.Word ? "0401" : "0401";
        var subcommand = wordUnits ? "0000" : "0001";
        var body = string.Concat(
            FormatAsciiByte(FourCFrameId),
            FormatAsciiByte(serial.StationNumber),
            FormatAsciiByte(serial.NetworkNumber),
            FormatAsciiByte(serial.PcNumber),
            FormatAsciiUInt16(serial.RequestDestinationModuleIoNumber),
            FormatAsciiByte(serial.RequestDestinationModuleStationNumber),
            FormatAsciiByte(serial.SelfStationNumber),
            command,
            subcommand,
            FormatDeviceAddressModern(address, metadata),
            FormatPointCount(points));

        return WrapAscii(body, serial.MessageFormat);
    }

    private static byte[] Encode4CBinaryRequest(MitsubishiSerialOptions serial, MitsubishiDeviceAddress address, MitsubishiDeviceMetadata metadata, int points, bool wordUnits)
    {
        var buffer = new List<byte>
        {
            FourCFrameId,
            serial.StationNumber,
            serial.NetworkNumber,
            serial.PcNumber,
            (byte)(serial.RequestDestinationModuleIoNumber & 0xFF),
            (byte)(serial.RequestDestinationModuleIoNumber >> 8),
            serial.RequestDestinationModuleStationNumber,
            serial.SelfStationNumber,
        };

        AppendUInt16LittleEndian(buffer, 0x0401);
        AppendUInt16LittleEndian(buffer, wordUnits ? (ushort)0x0000 : (ushort)0x0001);
        AppendThreeByteLittleEndian(buffer, address.Number);
        buffer.Add((byte)metadata.BinaryCode);
        AppendUInt16LittleEndian(buffer, checked((ushort)points));
        buffer.Add(Dle);
        buffer.Add(Etx);

        var numberOfDataBytes = checked((ushort)(buffer.Count - 2));
        var prefix = new List<byte> { Dle, Stx };
        AppendUInt16LittleEndian(prefix, numberOfDataBytes);
        prefix.AddRange(buffer);
        var checksum = ComputeChecksum(prefix.Skip(2).Take(2 + numberOfDataBytes));
        prefix.AddRange(Encoding.ASCII.GetBytes(checksum));
        return prefix.ToArray();
    }

    private static byte[] Encode3CRandomReadRequest(MitsubishiClientOptions options, IReadOnlyList<MitsubishiDeviceAddress> addresses)
    {
        EnsureAscii(options);
        var serial = options.ResolvedSerial;
        var body = string.Concat(
            FormatAsciiByte(ThreeCFrameId),
            FormatAsciiByte(serial.StationNumber),
            FormatAsciiByte(serial.NetworkNumber),
            FormatAsciiByte(serial.PcNumber),
            FormatAsciiByte(serial.SelfStationNumber),
            "0403",
            "0000",
            FormatAsciiUInt16(checked((ushort)addresses.Count)),
            "0000",
            string.Concat(addresses.Select(static address => FormatDeviceAddressModern(address, address.Descriptor))));

        return WrapAscii(body, serial.MessageFormat);
    }

    private static byte[] Encode4CRandomReadRequest(MitsubishiClientOptions options, IReadOnlyList<MitsubishiDeviceAddress> addresses)
    {
        var serial = options.ResolvedSerial;
        return serial.MessageFormat switch
        {
            MitsubishiSerialMessageFormat.Format1 or MitsubishiSerialMessageFormat.Format4 => Encode4CAsciiRandomReadRequest(serial, addresses),
            MitsubishiSerialMessageFormat.Format5 => Encode4CBinaryRandomReadRequest(serial, addresses),
            _ => throw new NotSupportedException($"Unsupported 4C serial message format '{serial.MessageFormat}'."),
        };
    }

    private static byte[] Encode3CBlockReadRequest(MitsubishiClientOptions options, MitsubishiBlockRequest request)
    {
        EnsureAscii(options);
        var serial = options.ResolvedSerial;
        var body = string.Concat(
            FormatAsciiByte(ThreeCFrameId),
            FormatAsciiByte(serial.StationNumber),
            FormatAsciiByte(serial.NetworkNumber),
            FormatAsciiByte(serial.PcNumber),
            FormatAsciiByte(serial.SelfStationNumber),
            "0406",
            "0000",
            FormatBlocksAscii(request, includeValues: false));

        return WrapAscii(body, serial.MessageFormat);
    }

    private static byte[] Encode3CMonitorRegistrationRequest(MitsubishiClientOptions options, IReadOnlyList<MitsubishiDeviceAddress> addresses)
    {
        EnsureAscii(options);
        var serial = options.ResolvedSerial;
        var body = string.Concat(
            FormatAsciiByte(ThreeCFrameId),
            FormatAsciiByte(serial.StationNumber),
            FormatAsciiByte(serial.NetworkNumber),
            FormatAsciiByte(serial.PcNumber),
            FormatAsciiByte(serial.SelfStationNumber),
            "0801",
            "0000",
            FormatAsciiUInt16(checked((ushort)addresses.Count)),
            "0000",
            string.Concat(addresses.Select(static address => FormatDeviceAddressModern(address, address.Descriptor))));

        return WrapAscii(body, serial.MessageFormat);
    }

    private static byte[] Encode4CBlockReadRequest(MitsubishiClientOptions options, MitsubishiBlockRequest request)
    {
        var serial = options.ResolvedSerial;
        return serial.MessageFormat switch
        {
            MitsubishiSerialMessageFormat.Format1 or MitsubishiSerialMessageFormat.Format4 => Encode4CAsciiBlockReadRequest(serial, request),
            MitsubishiSerialMessageFormat.Format5 => Encode4CBinaryBlockReadRequest(serial, request),
            _ => throw new NotSupportedException($"Unsupported 4C serial message format '{serial.MessageFormat}'."),
        };
    }

    private static byte[] Encode4CMonitorRegistrationRequest(MitsubishiClientOptions options, IReadOnlyList<MitsubishiDeviceAddress> addresses)
    {
        var serial = options.ResolvedSerial;
        return serial.MessageFormat switch
        {
            MitsubishiSerialMessageFormat.Format1 or MitsubishiSerialMessageFormat.Format4 => Encode4CAsciiMonitorRegistrationRequest(serial, addresses),
            MitsubishiSerialMessageFormat.Format5 => Encode4CBinaryMonitorRegistrationRequest(serial, addresses),
            _ => throw new NotSupportedException($"Unsupported 4C serial message format '{serial.MessageFormat}'."),
        };
    }

    private static byte[] Encode1CWriteRequest(MitsubishiClientOptions options, MitsubishiDeviceAddress address, IReadOnlyList<ushort> values)
    {
        EnsureAscii(options);
        var serial = options.ResolvedSerial;
        var metadata = address.Descriptor;
        var body = string.Concat(
            FormatAsciiByte(serial.StationNumber),
            FormatAsciiByte(serial.PcNumber),
            "WW",
            FormatAsciiNibble(serial.MessageWait),
            Format1CAddress(address, metadata),
            FormatPointCount(values.Count),
            FormatWordValuesAscii(values));

        return WrapAscii(body, serial.MessageFormat);
    }

    private static byte[] Encode1CBitWriteRequest(MitsubishiClientOptions options, MitsubishiDeviceAddress address, IReadOnlyList<bool> values)
    {
        EnsureAscii(options);
        var serial = options.ResolvedSerial;
        var metadata = address.Descriptor;
        var body = string.Concat(
            FormatAsciiByte(serial.StationNumber),
            FormatAsciiByte(serial.PcNumber),
            "BW",
            FormatAsciiNibble(serial.MessageWait),
            Format1CAddress(address, metadata),
            FormatPointCount(values.Count),
            FormatBitValuesAscii(values));

        return WrapAscii(body, serial.MessageFormat);
    }

    private static byte[] Encode3CWriteRequest(MitsubishiClientOptions options, MitsubishiDeviceAddress address, IReadOnlyList<ushort> values)
    {
        EnsureAscii(options);
        var serial = options.ResolvedSerial;
        var metadata = address.Descriptor;
        var body = string.Concat(
            FormatAsciiByte(ThreeCFrameId),
            FormatAsciiByte(serial.StationNumber),
            FormatAsciiByte(serial.NetworkNumber),
            FormatAsciiByte(serial.PcNumber),
            FormatAsciiByte(serial.SelfStationNumber),
            "1401",
            "0000",
            FormatDeviceAddressModern(address, metadata),
            FormatAsciiUInt16(checked((ushort)values.Count)),
            FormatWordValuesAscii(values));

        return WrapAscii(body, serial.MessageFormat);
    }

    private static byte[] Encode3CBitWriteRequest(MitsubishiClientOptions options, MitsubishiDeviceAddress address, IReadOnlyList<bool> values)
    {
        EnsureAscii(options);
        var serial = options.ResolvedSerial;
        var metadata = address.Descriptor;
        var body = string.Concat(
            FormatAsciiByte(ThreeCFrameId),
            FormatAsciiByte(serial.StationNumber),
            FormatAsciiByte(serial.NetworkNumber),
            FormatAsciiByte(serial.PcNumber),
            FormatAsciiByte(serial.SelfStationNumber),
            "1401",
            "0001",
            FormatDeviceAddressModern(address, metadata),
            FormatAsciiUInt16(checked((ushort)values.Count)),
            FormatBitValuesAscii(values));

        return WrapAscii(body, serial.MessageFormat);
    }

    private static byte[] Encode3CRandomWriteRequest(MitsubishiClientOptions options, IReadOnlyList<MitsubishiDeviceValue> values)
    {
        EnsureAscii(options);
        var serial = options.ResolvedSerial;
        var body = string.Concat(
            FormatAsciiByte(ThreeCFrameId),
            FormatAsciiByte(serial.StationNumber),
            FormatAsciiByte(serial.NetworkNumber),
            FormatAsciiByte(serial.PcNumber),
            FormatAsciiByte(serial.SelfStationNumber),
            "1402",
            "0000",
            FormatAsciiUInt16(checked((ushort)values.Count)),
            "0000",
            string.Concat(values.Select(static value => FormatDeviceAddressModern(value.Address, value.Address.Descriptor) + value.Value.ToString("X4", CultureInfo.InvariantCulture))));

        return WrapAscii(body, serial.MessageFormat);
    }

    private static byte[] Encode3CBlockWriteRequest(MitsubishiClientOptions options, MitsubishiBlockRequest request)
    {
        EnsureAscii(options);
        var serial = options.ResolvedSerial;
        var body = string.Concat(
            FormatAsciiByte(ThreeCFrameId),
            FormatAsciiByte(serial.StationNumber),
            FormatAsciiByte(serial.NetworkNumber),
            FormatAsciiByte(serial.PcNumber),
            FormatAsciiByte(serial.SelfStationNumber),
            "1406",
            "0000",
            FormatBlocksAscii(request, includeValues: true));

        return WrapAscii(body, serial.MessageFormat);
    }

    private static byte[] Encode3CExecuteMonitorRequest(MitsubishiClientOptions options)
    {
        EnsureAscii(options);
        var serial = options.ResolvedSerial;
        var body = string.Concat(
            FormatAsciiByte(ThreeCFrameId),
            FormatAsciiByte(serial.StationNumber),
            FormatAsciiByte(serial.NetworkNumber),
            FormatAsciiByte(serial.PcNumber),
            FormatAsciiByte(serial.SelfStationNumber),
            "0802",
            "0000");

        return WrapAscii(body, serial.MessageFormat);
    }

    private static byte[] Encode3CRemoteOperationRequest(MitsubishiClientOptions options, ushort command, bool force, bool clearMode)
    {
        EnsureAscii(options);
        var serial = options.ResolvedSerial;
        var body = string.Concat(
            FormatAsciiByte(ThreeCFrameId),
            FormatAsciiByte(serial.StationNumber),
            FormatAsciiByte(serial.NetworkNumber),
            FormatAsciiByte(serial.PcNumber),
            FormatAsciiByte(serial.SelfStationNumber),
            command.ToString("X4", CultureInfo.InvariantCulture),
            "0000",
            FormatRemoteOperationPayloadAscii(command, force, clearMode));

        return WrapAscii(body, serial.MessageFormat);
    }

    private static byte[] Encode3CRawRequest(MitsubishiClientOptions options, ushort command, ushort subcommand, IReadOnlyList<byte> body)
    {
        EnsureAscii(options);
        var serial = options.ResolvedSerial;
        var payload = body.Count == 0 ? string.Empty : Encoding.ASCII.GetString(body.ToArray());
        var requestBody = string.Concat(
            FormatAsciiByte(ThreeCFrameId),
            FormatAsciiByte(serial.StationNumber),
            FormatAsciiByte(serial.NetworkNumber),
            FormatAsciiByte(serial.PcNumber),
            FormatAsciiByte(serial.SelfStationNumber),
            command.ToString("X4", CultureInfo.InvariantCulture),
            subcommand.ToString("X4", CultureInfo.InvariantCulture),
            payload);

        return WrapAscii(requestBody, serial.MessageFormat);
    }

    private static byte[] Encode4CWriteRequest(MitsubishiClientOptions options, MitsubishiDeviceAddress address, IReadOnlyList<ushort> values)
    {
        var serial = options.ResolvedSerial;
        var metadata = address.Descriptor;
        return serial.MessageFormat switch
        {
            MitsubishiSerialMessageFormat.Format1 or MitsubishiSerialMessageFormat.Format4 => Encode4CAsciiWriteRequest(serial, address, metadata, values),
            MitsubishiSerialMessageFormat.Format5 => Encode4CBinaryWriteRequest(serial, address, metadata, values),
            _ => throw new NotSupportedException($"Unsupported 4C serial message format '{serial.MessageFormat}'."),
        };
    }

    private static byte[] Encode4CBitWriteRequest(MitsubishiClientOptions options, MitsubishiDeviceAddress address, IReadOnlyList<bool> values)
    {
        var serial = options.ResolvedSerial;
        var metadata = address.Descriptor;
        return serial.MessageFormat switch
        {
            MitsubishiSerialMessageFormat.Format1 or MitsubishiSerialMessageFormat.Format4 => Encode4CAsciiBitWriteRequest(serial, address, metadata, values),
            MitsubishiSerialMessageFormat.Format5 => Encode4CBinaryBitWriteRequest(serial, address, metadata, values),
            _ => throw new NotSupportedException($"Unsupported 4C serial message format '{serial.MessageFormat}'."),
        };
    }

    private static byte[] Encode4CRandomWriteRequest(MitsubishiClientOptions options, IReadOnlyList<MitsubishiDeviceValue> values)
    {
        var serial = options.ResolvedSerial;
        return serial.MessageFormat switch
        {
            MitsubishiSerialMessageFormat.Format1 or MitsubishiSerialMessageFormat.Format4 => Encode4CAsciiRandomWriteRequest(serial, values),
            MitsubishiSerialMessageFormat.Format5 => Encode4CBinaryRandomWriteRequest(serial, values),
            _ => throw new NotSupportedException($"Unsupported 4C serial message format '{serial.MessageFormat}'."),
        };
    }

    private static byte[] Encode4CBlockWriteRequest(MitsubishiClientOptions options, MitsubishiBlockRequest request)
    {
        var serial = options.ResolvedSerial;
        return serial.MessageFormat switch
        {
            MitsubishiSerialMessageFormat.Format1 or MitsubishiSerialMessageFormat.Format4 => Encode4CAsciiBlockWriteRequest(serial, request),
            MitsubishiSerialMessageFormat.Format5 => Encode4CBinaryBlockWriteRequest(serial, request),
            _ => throw new NotSupportedException($"Unsupported 4C serial message format '{serial.MessageFormat}'."),
        };
    }

    private static byte[] Encode4CExecuteMonitorRequest(MitsubishiClientOptions options)
    {
        var serial = options.ResolvedSerial;
        return serial.MessageFormat switch
        {
            MitsubishiSerialMessageFormat.Format1 or MitsubishiSerialMessageFormat.Format4 => Encode4CAsciiExecuteMonitorRequest(serial),
            MitsubishiSerialMessageFormat.Format5 => Encode4CBinaryExecuteMonitorRequest(serial),
            _ => throw new NotSupportedException($"Unsupported 4C serial message format '{serial.MessageFormat}'."),
        };
    }

    private static byte[] Encode4CRemoteOperationRequest(MitsubishiClientOptions options, ushort command, bool force, bool clearMode)
    {
        var serial = options.ResolvedSerial;
        return serial.MessageFormat switch
        {
            MitsubishiSerialMessageFormat.Format1 or MitsubishiSerialMessageFormat.Format4 => Encode4CAsciiRemoteOperationRequest(serial, command, force, clearMode),
            MitsubishiSerialMessageFormat.Format5 => Encode4CBinaryRemoteOperationRequest(serial, command, force, clearMode),
            _ => throw new NotSupportedException($"Unsupported 4C serial message format '{serial.MessageFormat}'."),
        };
    }

    private static byte[] Encode4CRawRequest(MitsubishiClientOptions options, ushort command, ushort subcommand, IReadOnlyList<byte> body)
    {
        var serial = options.ResolvedSerial;
        return serial.MessageFormat switch
        {
            MitsubishiSerialMessageFormat.Format1 or MitsubishiSerialMessageFormat.Format4 => Encode4CAsciiRawRequest(serial, command, subcommand, body),
            MitsubishiSerialMessageFormat.Format5 => Encode4CBinaryRawRequest(serial, command, subcommand, body),
            _ => throw new NotSupportedException($"Unsupported 4C serial message format '{serial.MessageFormat}'."),
        };
    }

    private static byte[] Encode4CAsciiWriteRequest(MitsubishiSerialOptions serial, MitsubishiDeviceAddress address, MitsubishiDeviceMetadata metadata, IReadOnlyList<ushort> values)
    {
        var body = string.Concat(
            FormatAsciiByte(FourCFrameId),
            FormatAsciiByte(serial.StationNumber),
            FormatAsciiByte(serial.NetworkNumber),
            FormatAsciiByte(serial.PcNumber),
            FormatAsciiUInt16(serial.RequestDestinationModuleIoNumber),
            FormatAsciiByte(serial.RequestDestinationModuleStationNumber),
            FormatAsciiByte(serial.SelfStationNumber),
            "1401",
            "0000",
            FormatDeviceAddressModern(address, metadata),
            FormatAsciiUInt16(checked((ushort)values.Count)),
            FormatWordValuesAscii(values));

        return WrapAscii(body, serial.MessageFormat);
    }

    private static byte[] Encode4CAsciiBitWriteRequest(MitsubishiSerialOptions serial, MitsubishiDeviceAddress address, MitsubishiDeviceMetadata metadata, IReadOnlyList<bool> values)
    {
        var body = string.Concat(
            FormatAsciiByte(FourCFrameId),
            FormatAsciiByte(serial.StationNumber),
            FormatAsciiByte(serial.NetworkNumber),
            FormatAsciiByte(serial.PcNumber),
            FormatAsciiUInt16(serial.RequestDestinationModuleIoNumber),
            FormatAsciiByte(serial.RequestDestinationModuleStationNumber),
            FormatAsciiByte(serial.SelfStationNumber),
            "1401",
            "0001",
            FormatDeviceAddressModern(address, metadata),
            FormatAsciiUInt16(checked((ushort)values.Count)),
            FormatBitValuesAscii(values));

        return WrapAscii(body, serial.MessageFormat);
    }

    private static byte[] Encode4CAsciiRandomReadRequest(MitsubishiSerialOptions serial, IReadOnlyList<MitsubishiDeviceAddress> addresses)
    {
        var body = string.Concat(
            FormatAsciiByte(FourCFrameId),
            FormatAsciiByte(serial.StationNumber),
            FormatAsciiByte(serial.NetworkNumber),
            FormatAsciiByte(serial.PcNumber),
            FormatAsciiUInt16(serial.RequestDestinationModuleIoNumber),
            FormatAsciiByte(serial.RequestDestinationModuleStationNumber),
            FormatAsciiByte(serial.SelfStationNumber),
            "0403",
            "0000",
            FormatAsciiUInt16(checked((ushort)addresses.Count)),
            "0000",
            string.Concat(addresses.Select(static address => FormatDeviceAddressModern(address, address.Descriptor))));

        return WrapAscii(body, serial.MessageFormat);
    }

    private static byte[] Encode4CAsciiRandomWriteRequest(MitsubishiSerialOptions serial, IReadOnlyList<MitsubishiDeviceValue> values)
    {
        var body = string.Concat(
            FormatAsciiByte(FourCFrameId),
            FormatAsciiByte(serial.StationNumber),
            FormatAsciiByte(serial.NetworkNumber),
            FormatAsciiByte(serial.PcNumber),
            FormatAsciiUInt16(serial.RequestDestinationModuleIoNumber),
            FormatAsciiByte(serial.RequestDestinationModuleStationNumber),
            FormatAsciiByte(serial.SelfStationNumber),
            "1402",
            "0000",
            FormatAsciiUInt16(checked((ushort)values.Count)),
            "0000",
            string.Concat(values.Select(static value => FormatDeviceAddressModern(value.Address, value.Address.Descriptor) + value.Value.ToString("X4", CultureInfo.InvariantCulture))));

        return WrapAscii(body, serial.MessageFormat);
    }

    private static byte[] Encode4CAsciiBlockReadRequest(MitsubishiSerialOptions serial, MitsubishiBlockRequest request)
    {
        var body = string.Concat(
            FormatAsciiByte(FourCFrameId),
            FormatAsciiByte(serial.StationNumber),
            FormatAsciiByte(serial.NetworkNumber),
            FormatAsciiByte(serial.PcNumber),
            FormatAsciiUInt16(serial.RequestDestinationModuleIoNumber),
            FormatAsciiByte(serial.RequestDestinationModuleStationNumber),
            FormatAsciiByte(serial.SelfStationNumber),
            "0406",
            "0000",
            FormatBlocksAscii(request, includeValues: false));

        return WrapAscii(body, serial.MessageFormat);
    }

    private static byte[] Encode4CAsciiBlockWriteRequest(MitsubishiSerialOptions serial, MitsubishiBlockRequest request)
    {
        var body = string.Concat(
            FormatAsciiByte(FourCFrameId),
            FormatAsciiByte(serial.StationNumber),
            FormatAsciiByte(serial.NetworkNumber),
            FormatAsciiByte(serial.PcNumber),
            FormatAsciiUInt16(serial.RequestDestinationModuleIoNumber),
            FormatAsciiByte(serial.RequestDestinationModuleStationNumber),
            FormatAsciiByte(serial.SelfStationNumber),
            "1406",
            "0000",
            FormatBlocksAscii(request, includeValues: true));

        return WrapAscii(body, serial.MessageFormat);
    }

    private static byte[] Encode4CAsciiMonitorRegistrationRequest(MitsubishiSerialOptions serial, IReadOnlyList<MitsubishiDeviceAddress> addresses)
    {
        var body = string.Concat(
            FormatAsciiByte(FourCFrameId),
            FormatAsciiByte(serial.StationNumber),
            FormatAsciiByte(serial.NetworkNumber),
            FormatAsciiByte(serial.PcNumber),
            FormatAsciiUInt16(serial.RequestDestinationModuleIoNumber),
            FormatAsciiByte(serial.RequestDestinationModuleStationNumber),
            FormatAsciiByte(serial.SelfStationNumber),
            "0801",
            "0000",
            FormatAsciiUInt16(checked((ushort)addresses.Count)),
            "0000",
            string.Concat(addresses.Select(static address => FormatDeviceAddressModern(address, address.Descriptor))));

        return WrapAscii(body, serial.MessageFormat);
    }

    private static byte[] Encode4CAsciiExecuteMonitorRequest(MitsubishiSerialOptions serial)
    {
        var body = string.Concat(
            FormatAsciiByte(FourCFrameId),
            FormatAsciiByte(serial.StationNumber),
            FormatAsciiByte(serial.NetworkNumber),
            FormatAsciiByte(serial.PcNumber),
            FormatAsciiUInt16(serial.RequestDestinationModuleIoNumber),
            FormatAsciiByte(serial.RequestDestinationModuleStationNumber),
            FormatAsciiByte(serial.SelfStationNumber),
            "0802",
            "0000");

        return WrapAscii(body, serial.MessageFormat);
    }

    private static byte[] Encode4CAsciiRemoteOperationRequest(MitsubishiSerialOptions serial, ushort command, bool force, bool clearMode)
    {
        var body = string.Concat(
            FormatAsciiByte(FourCFrameId),
            FormatAsciiByte(serial.StationNumber),
            FormatAsciiByte(serial.NetworkNumber),
            FormatAsciiByte(serial.PcNumber),
            FormatAsciiUInt16(serial.RequestDestinationModuleIoNumber),
            FormatAsciiByte(serial.RequestDestinationModuleStationNumber),
            FormatAsciiByte(serial.SelfStationNumber),
            command.ToString("X4", CultureInfo.InvariantCulture),
            "0000",
            FormatRemoteOperationPayloadAscii(command, force, clearMode));

        return WrapAscii(body, serial.MessageFormat);
    }

    private static byte[] Encode4CAsciiRawRequest(MitsubishiSerialOptions serial, ushort command, ushort subcommand, IReadOnlyList<byte> body)
    {
        var payload = body.Count == 0 ? string.Empty : Encoding.ASCII.GetString(body.ToArray());
        var requestBody = string.Concat(
            FormatAsciiByte(FourCFrameId),
            FormatAsciiByte(serial.StationNumber),
            FormatAsciiByte(serial.NetworkNumber),
            FormatAsciiByte(serial.PcNumber),
            FormatAsciiUInt16(serial.RequestDestinationModuleIoNumber),
            FormatAsciiByte(serial.RequestDestinationModuleStationNumber),
            FormatAsciiByte(serial.SelfStationNumber),
            command.ToString("X4", CultureInfo.InvariantCulture),
            subcommand.ToString("X4", CultureInfo.InvariantCulture),
            payload);

        return WrapAscii(requestBody, serial.MessageFormat);
    }

    private static byte[] Encode4CBinaryWriteRequest(MitsubishiSerialOptions serial, MitsubishiDeviceAddress address, MitsubishiDeviceMetadata metadata, IReadOnlyList<ushort> values)
    {
        var buffer = Create4CBinaryHeader(serial);

        AppendUInt16LittleEndian(buffer, 0x1401);
        AppendUInt16LittleEndian(buffer, 0x0000);
        AppendThreeByteLittleEndian(buffer, address.Number);
        buffer.Add((byte)metadata.BinaryCode);
        AppendUInt16LittleEndian(buffer, checked((ushort)values.Count));
        AppendWordsLittleEndian(buffer, values);
        return Finalize4CBinaryFrame(buffer);
    }

    private static byte[] Encode4CBinaryBitWriteRequest(MitsubishiSerialOptions serial, MitsubishiDeviceAddress address, MitsubishiDeviceMetadata metadata, IReadOnlyList<bool> values)
    {
        var buffer = Create4CBinaryHeader(serial);

        AppendUInt16LittleEndian(buffer, 0x1401);
        AppendUInt16LittleEndian(buffer, 0x0001);
        AppendThreeByteLittleEndian(buffer, address.Number);
        buffer.Add((byte)metadata.BinaryCode);
        AppendUInt16LittleEndian(buffer, checked((ushort)values.Count));
        AppendBitsBinary(buffer, values);
        return Finalize4CBinaryFrame(buffer);
    }

    private static byte[] Encode4CBinaryRandomReadRequest(MitsubishiSerialOptions serial, IReadOnlyList<MitsubishiDeviceAddress> addresses)
    {
        var buffer = Create4CBinaryHeader(serial);
        AppendUInt16LittleEndian(buffer, 0x0403);
        AppendUInt16LittleEndian(buffer, 0x0000);
        AppendUInt16LittleEndian(buffer, checked((ushort)addresses.Count));
        AppendUInt16LittleEndian(buffer, 0x0000);
        foreach (var address in addresses)
        {
            AppendThreeByteLittleEndian(buffer, address.Number);
            buffer.Add((byte)address.Descriptor.BinaryCode);
        }

        return Finalize4CBinaryFrame(buffer);
    }

    private static byte[] Encode4CBinaryRandomWriteRequest(MitsubishiSerialOptions serial, IReadOnlyList<MitsubishiDeviceValue> values)
    {
        var buffer = Create4CBinaryHeader(serial);
        AppendUInt16LittleEndian(buffer, 0x1402);
        AppendUInt16LittleEndian(buffer, 0x0000);
        AppendUInt16LittleEndian(buffer, checked((ushort)values.Count));
        AppendUInt16LittleEndian(buffer, 0x0000);
        foreach (var value in values)
        {
            AppendThreeByteLittleEndian(buffer, value.Address.Number);
            buffer.Add((byte)value.Address.Descriptor.BinaryCode);
            AppendUInt16LittleEndian(buffer, value.Value);
        }

        return Finalize4CBinaryFrame(buffer);
    }

    private static byte[] Encode4CBinaryBlockReadRequest(MitsubishiSerialOptions serial, MitsubishiBlockRequest request)
    {
        var buffer = Create4CBinaryHeader(serial);
        AppendUInt16LittleEndian(buffer, 0x0406);
        AppendUInt16LittleEndian(buffer, 0x0000);
        AppendBlocksBinary(buffer, request, includeValues: false);
        return Finalize4CBinaryFrame(buffer);
    }

    private static byte[] Encode4CBinaryBlockWriteRequest(MitsubishiSerialOptions serial, MitsubishiBlockRequest request)
    {
        var buffer = Create4CBinaryHeader(serial);
        AppendUInt16LittleEndian(buffer, 0x1406);
        AppendUInt16LittleEndian(buffer, 0x0000);
        AppendBlocksBinary(buffer, request, includeValues: true);
        return Finalize4CBinaryFrame(buffer);
    }

    private static byte[] Encode4CBinaryMonitorRegistrationRequest(MitsubishiSerialOptions serial, IReadOnlyList<MitsubishiDeviceAddress> addresses)
    {
        var buffer = Create4CBinaryHeader(serial);
        AppendUInt16LittleEndian(buffer, 0x0801);
        AppendUInt16LittleEndian(buffer, 0x0000);
        AppendUInt16LittleEndian(buffer, checked((ushort)addresses.Count));
        AppendUInt16LittleEndian(buffer, 0x0000);
        foreach (var address in addresses)
        {
            AppendThreeByteLittleEndian(buffer, address.Number);
            buffer.Add((byte)address.Descriptor.BinaryCode);
        }

        return Finalize4CBinaryFrame(buffer);
    }

    private static byte[] Encode4CBinaryExecuteMonitorRequest(MitsubishiSerialOptions serial)
    {
        var buffer = Create4CBinaryHeader(serial);
        AppendUInt16LittleEndian(buffer, 0x0802);
        AppendUInt16LittleEndian(buffer, 0x0000);
        return Finalize4CBinaryFrame(buffer);
    }

    private static byte[] Encode4CBinaryRemoteOperationRequest(MitsubishiSerialOptions serial, ushort command, bool force, bool clearMode)
    {
        var buffer = Create4CBinaryHeader(serial);
        AppendUInt16LittleEndian(buffer, command);
        AppendUInt16LittleEndian(buffer, 0x0000);
        if (command == MitsubishiCommands.RemoteRun)
        {
            AppendUInt16LittleEndian(buffer, force ? (ushort)0x0001 : (ushort)0x0000);
            AppendUInt16LittleEndian(buffer, clearMode ? (ushort)0x0001 : (ushort)0x0000);
        }

        return Finalize4CBinaryFrame(buffer);
    }

    private static byte[] Encode4CBinaryRawRequest(MitsubishiSerialOptions serial, ushort command, ushort subcommand, IReadOnlyList<byte> body)
    {
        var buffer = Create4CBinaryHeader(serial);
        AppendUInt16LittleEndian(buffer, command);
        AppendUInt16LittleEndian(buffer, subcommand);
        buffer.AddRange(body);
        return Finalize4CBinaryFrame(buffer);
    }

    private static Responce<byte[]> Decode1C(MitsubishiClientOptions options, byte[] response)
    {
        var serial = options.ResolvedSerial;
        var text = ExtractAsciiPayload(serial.MessageFormat, response);
        var result = new Responce<byte[]> { Response = text };

        if (text.Length < 4)
        {
            return result.Fail("1C serial response too short.");
        }

        if (text[0] == (char)Nak)
        {
            var errorCode = text.Length >= 6 ? Convert.ToInt32(text.Substring(4, 2), 16) : 0;
            return result.Fail($"PLC returned 1C serial error 0x{errorCode:X2}.", errorCode);
        }

        var payloadStart = text[0] == (char)Ack ? 3 : 4;
        result.Value = Encoding.ASCII.GetBytes(text[payloadStart..]);
        return result.EndTime();
    }

    private static Responce<byte[]> Decode3C(MitsubishiClientOptions options, byte[] response)
    {
        var serial = options.ResolvedSerial;
        var text = ExtractAsciiPayload(serial.MessageFormat, response);
        var result = new Responce<byte[]> { Response = text };

        if (text.Length < 6)
        {
            return result.Fail("3C serial response too short.");
        }

        if (text[0] == (char)Nak)
        {
            var errorCode = text.Length >= 8 ? Convert.ToInt32(text.Substring(6, 2), 16) : 0;
            return result.Fail($"PLC returned 3C serial error 0x{errorCode:X2}.", errorCode);
        }

        var payloadStart = text[0] == (char)Ack ? 5 : 6;
        result.Value = Encoding.ASCII.GetBytes(text[payloadStart..]);
        return result.EndTime();
    }

    private static Responce<byte[]> Decode4C(MitsubishiClientOptions options, byte[] response)
    {
        var serial = options.ResolvedSerial;
        return serial.MessageFormat == MitsubishiSerialMessageFormat.Format5
            ? Decode4CBinary(response)
            : Decode4CAscii(serial.MessageFormat, response);
    }

    private static Responce<byte[]> Decode4CAscii(MitsubishiSerialMessageFormat messageFormat, byte[] response)
    {
        var text = ExtractAsciiPayload(messageFormat, response);
        var result = new Responce<byte[]> { Response = text };

        if (text.Length < 12)
        {
            return result.Fail("4C serial ASCII response too short.");
        }

        if (text[0] == (char)Nak)
        {
            var errorCode = text.Length >= 14 ? Convert.ToInt32(text.Substring(12, 2), 16) : 0;
            return result.Fail($"PLC returned 4C serial ASCII error 0x{errorCode:X2}.", errorCode);
        }

        var payloadStart = text[0] == (char)Ack ? 11 : 12;
        result.Value = Encoding.ASCII.GetBytes(text[payloadStart..]);
        return result.EndTime();
    }

    private static Responce<byte[]> Decode4CBinary(byte[] response)
    {
        var result = new Responce<byte[]> { Response = Convert.ToHexString(response) };
        if (!HasBinaryFormat5Message(response))
        {
            return result.Fail("4C serial binary response is incomplete.");
        }

        if (response.Length < 17)
        {
            return result.Fail("4C serial binary response too short.");
        }

        var payloadStart = 14;
        var responseId = BitConverter.ToUInt16(response, 12);
        if (responseId != ResponseIdBinary)
        {
            return result.Fail("4C serial binary response has an invalid response identifier.");
        }

        var endCode = BitConverter.ToUInt16(response, payloadStart);
        if (endCode != 0)
        {
            return result.Fail($"PLC returned 4C serial binary error 0x{endCode:X4}.", endCode);
        }

        var dataStart = payloadStart + 2;
        var dataLength = response.Length - dataStart - 4;
        result.Value = dataLength <= 0 ? Array.Empty<byte>() : response[dataStart..(dataStart + dataLength)];
        return result.EndTime();
    }

    private static void EnsureAscii(MitsubishiClientOptions options)
    {
        if (options.DataCode != CommunicationDataCode.Ascii)
        {
            throw new NotSupportedException("1C and 3C serial communication require ASCII data encoding.");
        }
    }

    private static string Format1CAddress(MitsubishiDeviceAddress address, MitsubishiDeviceMetadata metadata)
    {
        var digits = metadata.Symbol.Length > 1 ? 3 : 4;
        return metadata.Symbol + address.Number.ToString($"D{digits}", CultureInfo.InvariantCulture);
    }

    private static string FormatDeviceAddressModern(MitsubishiDeviceAddress address, MitsubishiDeviceMetadata metadata)
        => address.Number.ToString("X6", CultureInfo.InvariantCulture) + metadata.Symbol.PadRight(2, '*');

    private static string FormatPointCount(int points)
        => (points == 256 ? 0 : points).ToString("X2", CultureInfo.InvariantCulture);

    private static string FormatWordValuesAscii(IEnumerable<ushort> values)
        => string.Concat(values.Select(static value => value.ToString("X4", CultureInfo.InvariantCulture)));

    private static string FormatBitValuesAscii(IEnumerable<bool> values)
        => string.Concat(values.Select(static value => value ? '1' : '0'));

    private static string FormatBlocksAscii(MitsubishiBlockRequest request, bool includeValues)
    {
        var builder = new StringBuilder();
        builder.Append(FormatAsciiUInt16(checked((ushort)request.ResolvedWordBlocks.Count)));
        builder.Append(FormatAsciiUInt16(checked((ushort)request.ResolvedBitBlocks.Count)));

        foreach (var block in request.ResolvedWordBlocks)
        {
            builder.Append(FormatDeviceAddressModern(block.Address, block.Address.Descriptor));
            builder.Append(FormatAsciiUInt16(checked((ushort)block.Values.Length)));
            if (includeValues)
            {
                builder.Append(FormatWordValuesAscii(block.Values.ToArray()));
            }
        }

        foreach (var block in request.ResolvedBitBlocks)
        {
            builder.Append(FormatDeviceAddressModern(block.Address, block.Address.Descriptor));
            builder.Append(FormatAsciiUInt16(checked((ushort)block.Values.Length)));
            if (includeValues)
            {
                builder.Append(string.Concat(block.Values.ToArray().Select(static value => value ? "10" : "00")));
            }
        }

        return builder.ToString();
    }

    private static string FormatRemoteOperationPayloadAscii(ushort command, bool force, bool clearMode)
        => command == MitsubishiCommands.RemoteRun
            ? $"{(force ? "0001" : "0000")}{(clearMode ? "0001" : "0000")}"
            : string.Empty;

    private static byte[] BuildLoopbackBody(MitsubishiClientOptions options, ReadOnlySpan<byte> data)
    {
        if (options.DataCode == CommunicationDataCode.Ascii)
        {
            return Encoding.ASCII.GetBytes(FormatAsciiUInt16(checked((ushort)data.Length)) + Encoding.ASCII.GetString(data));
        }

        var buffer = new List<byte>();
        AppendUInt16LittleEndian(buffer, checked((ushort)data.Length));
        buffer.AddRange(data.ToArray());
        return buffer.ToArray();
    }

    private static byte[] BuildMemoryAccessBody(MitsubishiClientOptions options, ushort command, ushort address, int length, ReadOnlySpan<ushort> values)
    {
        var buffer = new List<byte>();
        if (options.DataCode == CommunicationDataCode.Ascii)
        {
            buffer.AddRange(Encoding.ASCII.GetBytes(FormatAsciiUInt16(address)));
            buffer.AddRange(Encoding.ASCII.GetBytes(FormatAsciiUInt16(checked((ushort)length))));
            if (command is MitsubishiCommands.MemoryWrite or MitsubishiCommands.ExtendUnitWrite)
            {
                buffer.AddRange(Encoding.ASCII.GetBytes(FormatWordValuesAscii(values.ToArray())));
            }

            return buffer.ToArray();
        }

        AppendUInt16LittleEndian(buffer, address);
        AppendUInt16LittleEndian(buffer, checked((ushort)length));
        if (command is MitsubishiCommands.MemoryWrite or MitsubishiCommands.ExtendUnitWrite)
        {
            AppendWordsLittleEndian(buffer, values.ToArray());
        }

        return buffer.ToArray();
    }

    private static string FormatAsciiByte(byte value) => value.ToString("X2", CultureInfo.InvariantCulture);

    private static string FormatAsciiNibble(byte value) => (value & 0x0F).ToString("X1", CultureInfo.InvariantCulture);

    private static string FormatAsciiUInt16(ushort value) => value.ToString("X4", CultureInfo.InvariantCulture);

    private static byte[] WrapAscii(string body, MitsubishiSerialMessageFormat format)
    {
        var checksum = Encoding.ASCII.GetBytes(ComputeChecksum(Encoding.ASCII.GetBytes(body)));
        return format switch
        {
            MitsubishiSerialMessageFormat.Format1 => [Enq, .. Encoding.ASCII.GetBytes(body), .. checksum],
            MitsubishiSerialMessageFormat.Format4 => [Cr, Lf, Enq, .. Encoding.ASCII.GetBytes(body), .. checksum, Cr, Lf],
            _ => throw new NotSupportedException($"ASCII wrapping is not supported for serial format '{format}'."),
        };
    }

    private static string ExtractAsciiPayload(MitsubishiSerialMessageFormat format, byte[] response)
    {
        var trimmed = format == MitsubishiSerialMessageFormat.Format4
            ? response.Where(static value => value is not Cr and not Lf).ToArray()
            : response;

        if (trimmed.Length < 3)
        {
            return string.Empty;
        }

        var withoutChecksum = trimmed[..^2];
        return Encoding.ASCII.GetString(withoutChecksum);
    }

    private static bool HasAsciiChecksumFramedMessage(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < 5)
        {
            return false;
        }

        if (buffer[0] == Cr)
        {
            var trimmed = buffer.ToArray().Where(static value => value is not Cr and not Lf).ToArray();
            return trimmed.Length >= 5 && (trimmed[0] == Enq || trimmed[0] == Stx || trimmed[0] == Ack || trimmed[0] == Nak);
        }

        return buffer[0] == Enq || buffer[0] == Stx || buffer[0] == Ack || buffer[0] == Nak;
    }

    private static bool HasBinaryFormat5Message(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < 8 || buffer[0] != Dle || buffer[1] != Stx)
        {
            return false;
        }

        var byteCount = buffer[2] | (buffer[3] << 8);
        var expectedLength = 4 + byteCount + 2 + 2;
        return buffer.Length >= expectedLength;
    }

    private static string ComputeChecksum(IEnumerable<byte> bytes)
    {
        var sum = bytes.Aggregate(0, static (current, value) => current + value);
        return (sum & 0xFF).ToString("X2", CultureInfo.InvariantCulture);
    }

    private static void AppendUInt16LittleEndian(List<byte> buffer, ushort value)
    {
        buffer.Add((byte)(value & 0xFF));
        buffer.Add((byte)(value >> 8));
    }

    private static void AppendThreeByteLittleEndian(List<byte> buffer, int value)
    {
        buffer.Add((byte)(value & 0xFF));
        buffer.Add((byte)((value >> 8) & 0xFF));
        buffer.Add((byte)((value >> 16) & 0xFF));
    }

    private static void AppendWordsLittleEndian(List<byte> buffer, IEnumerable<ushort> values)
    {
        foreach (var value in values)
        {
            AppendUInt16LittleEndian(buffer, value);
        }
    }

    private static void AppendBlocksBinary(List<byte> buffer, MitsubishiBlockRequest request, bool includeValues)
    {
        AppendUInt16LittleEndian(buffer, checked((ushort)request.ResolvedWordBlocks.Count));
        AppendUInt16LittleEndian(buffer, checked((ushort)request.ResolvedBitBlocks.Count));

        foreach (var block in request.ResolvedWordBlocks)
        {
            AppendThreeByteLittleEndian(buffer, block.Address.Number);
            buffer.Add((byte)block.Address.Descriptor.BinaryCode);
            AppendUInt16LittleEndian(buffer, checked((ushort)block.Values.Length));
            if (includeValues)
            {
                AppendWordsLittleEndian(buffer, block.Values.ToArray());
            }
        }

        foreach (var block in request.ResolvedBitBlocks)
        {
            AppendThreeByteLittleEndian(buffer, block.Address.Number);
            buffer.Add((byte)block.Address.Descriptor.BinaryCode);
            AppendUInt16LittleEndian(buffer, checked((ushort)block.Values.Length));
            if (includeValues)
            {
                AppendBitsBinary(buffer, block.Values.ToArray());
            }
        }
    }

    private static List<byte> Create4CBinaryHeader(MitsubishiSerialOptions serial)
        =>
        [
            FourCFrameId,
            serial.StationNumber,
            serial.NetworkNumber,
            serial.PcNumber,
            (byte)(serial.RequestDestinationModuleIoNumber & 0xFF),
            (byte)(serial.RequestDestinationModuleIoNumber >> 8),
            serial.RequestDestinationModuleStationNumber,
            serial.SelfStationNumber,
        ];

    private static byte[] Finalize4CBinaryFrame(List<byte> buffer)
    {
        buffer.Add(Dle);
        buffer.Add(Etx);

        var numberOfDataBytes = checked((ushort)(buffer.Count - 2));
        var prefix = new List<byte> { Dle, Stx };
        AppendUInt16LittleEndian(prefix, numberOfDataBytes);
        prefix.AddRange(buffer);
        var checksum = ComputeChecksum(prefix.Skip(2).Take(2 + numberOfDataBytes));
        prefix.AddRange(Encoding.ASCII.GetBytes(checksum));
        return prefix.ToArray();
    }

    private static void AppendBitsBinary(List<byte> buffer, IEnumerable<bool> values)
    {
        foreach (var value in values)
        {
            buffer.Add(value ? (byte)0x10 : (byte)0x00);
        }
    }
}
