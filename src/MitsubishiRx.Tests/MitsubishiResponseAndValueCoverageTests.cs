// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive.Tests;
#else

namespace MitsubishiRx.Tests;
#endif

/// <summary>Provides response and value coverage tests.</summary>
public sealed class MitsubishiResponseAndValueCoverageTests
{
    /// <summary>Executes the TypedResponseConstructorsHandleNullSources operation.</summary>
    /// <returns>The TypedResponseConstructorsHandleNullSources operation result.</returns>
    [Test]
    public async Task TypedResponseConstructorsHandleNullSources()
    {
        var copyOnly = new Responce<int>((Responce)null!);
        var copyWithValue = new Responce<int>((Responce)null!, 42);

        await Assert.That(copyOnly.Value).IsEqualTo(default(int));
        await Assert.That(copyWithValue.Value).IsEqualTo(default(int));
        await Assert.That(copyOnly.IsSucceed).IsTrue();
        await Assert.That(copyWithValue.IsSucceed).IsTrue();
    }

    /// <summary>Executes the TypedResponseCopiesBaseResponseErrors operation.</summary>
    /// <returns>The TypedResponseCopiesBaseResponseErrors operation result.</returns>
    [Test]
    public async Task TypedResponseCopiesBaseResponseErrors()
    {
        var source = new Responce
        {
            IsSucceed = false,
            Err = "failed",
            ErrCode = 17,
            Request = "request",
            Response = "response",
            Exception = new InvalidOperationException("boom"),
        };
        source.ErrList.Add("first");
        source.ErrList.Add("first");

        var typed = new Responce<int>(source, 123);

        await Assert.That(typed.Value).IsEqualTo(123);
        await Assert.That(typed.IsSucceed).IsFalse();
        await Assert.That(typed.Err).IsEqualTo("failed");
        await Assert.That(typed.ErrCode).IsEqualTo(17);
        await Assert.That(typed.Request).IsEqualTo("request");
        await Assert.That(typed.Response).IsEqualTo("response");
        await Assert.That(typed.Exception).IsNotNull();
        await Assert.That(typed.ErrList.Count).IsEqualTo(2);
        await Assert.That(typed.ErrList[0]).IsEqualTo("failed");
        await Assert.That(typed.ErrList[1]).IsEqualTo("first");
    }

    /// <summary>Executes the TypedResponseSetErrInfoIgnoresNullSource operation.</summary>
    /// <returns>The TypedResponseSetErrInfoIgnoresNullSource operation result.</returns>
    [Test]
    public async Task TypedResponseSetErrInfoIgnoresNullSource()
    {
        var typed = new Responce<string>("ok");

        var returned = typed.SetErrInfo(null!);

        await Assert.That(returned).IsSameReferenceAs(typed);
        await Assert.That(returned.Value).IsEqualTo("ok");
        await Assert.That(returned.IsSucceed).IsTrue();
    }

    /// <summary>Executes the TagGroupSnapshotReportsMissingAndMismatchedValues operation.</summary>
    /// <returns>The TagGroupSnapshotReportsMissingAndMismatchedValues operation result.</returns>
    [Test]
    public async Task TagGroupSnapshotReportsMissingAndMismatchedValues()
    {
        var snapshot = new MitsubishiTagGroupSnapshot(
            "Line",
            new Dictionary<string, object?>
            {
                ["Count"] = 12,
                ["Empty"] = null,
            });

        await Assert.That(snapshot.GetOptional<int>("Missing")).IsEqualTo(default(int));
        await Assert.That(snapshot.GetOptional<string>("Count")).IsNull();
        _ = Assert.Throws<KeyNotFoundException>(() => snapshot.GetRequired<int>("Missing"));
        _ = Assert.Throws<InvalidCastException>(() => snapshot.GetRequired<string>("Empty"));
    }

    /// <summary>Executes the SerialRouteStoresConstructorValues operation.</summary>
    /// <returns>The SerialRouteStoresConstructorValues operation result.</returns>
    [Test]
    public async Task SerialRouteStoresConstructorValues()
    {
        var route = new MitsubishiSerialRoute(1, 2, 3, 0x1234, 4, 5);

        await Assert.That(route.StationNumber).IsEqualTo((byte)1);
        await Assert.That(route.NetworkNumber).IsEqualTo((byte)2);
        await Assert.That(route.PcNumber).IsEqualTo((byte)3);
        await Assert.That(route.RequestDestinationModuleIoNumber).IsEqualTo((ushort)0x1234);
        await Assert.That(route.RequestDestinationModuleStationNumber).IsEqualTo((byte)4);
        await Assert.That(route.SelfStationNumber).IsEqualTo((byte)5);
    }
}
