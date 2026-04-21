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

        var numberOfDataBytes = checked((ushort)(buffer.Count - 3));
        var prefix = new List<byte> { Dle, Stx };
        AppendUInt16LittleEndian(prefix, numberOfDataBytes);
        prefix.AddRange(buffer);
        var checksum = ComputeChecksum(prefix.Skip(2));
        prefix.AddRange(Encoding.ASCII.GetBytes(checksum));
        return prefix.ToArray();
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
}
