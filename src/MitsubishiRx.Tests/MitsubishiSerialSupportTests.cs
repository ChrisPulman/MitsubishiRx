// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MitsubishiRx.Tests;

public sealed class MitsubishiSerialSupportTests
{
    [Test]
    public async Task SerialTransportKindEnumShouldExposeSerialMember()
    {
        await Assert.That(Enum.GetNames<MitsubishiTransportKind>()).Contains("Serial");
    }

    [Test]
    public async Task SerialFrameTypesShouldExistForSerialMcProtocols()
    {
        await Assert.That(Enum.GetNames<MitsubishiFrameType>()).Contains("OneC");
        await Assert.That(Enum.GetNames<MitsubishiFrameType>()).Contains("ThreeC");
        await Assert.That(Enum.GetNames<MitsubishiFrameType>()).Contains("FourC");
    }

    [Test]
    public async Task LibraryShouldDefineReactiveSerialOptionsType()
    {
        var assembly = typeof(MitsubishiRx).Assembly;
        var serialOptionsType = assembly.GetType("MitsubishiRx.MitsubishiSerialOptions");
        await Assert.That(serialOptionsType is not null).IsTrue();
    }

    [Test]
    public async Task LibraryProjectShouldReferenceSerialportRxPackage()
    {
        var projectPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "MitsubishiRx", "MitsubishiRx.csproj");
        projectPath = Path.GetFullPath(projectPath);
        var projectText = await File.ReadAllTextAsync(projectPath);

        await Assert.That(projectText.Contains("SerialPortRx", StringComparison.OrdinalIgnoreCase)).IsTrue();
    }
}
