// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MitsubishiRx.Tests;

public sealed class MitsubishiDeviceAddressTests
{
    [Test]
    [Arguments("X10", XyAddressNotation.Octal, 8)]
    [Arguments("X10", XyAddressNotation.Hexadecimal, 16)]
    [Arguments("Y17", XyAddressNotation.Octal, 15)]
    [Arguments("B10", XyAddressNotation.Hexadecimal, 16)]
    [Arguments("D100", XyAddressNotation.Octal, 100)]
    public async Task ParseUsesExpectedRadix(string value, XyAddressNotation notation, int expected)
    {
        var address = MitsubishiDeviceAddress.Parse(value, notation);

        await Assert.That(address.Number).IsEqualTo(expected);
    }
}
