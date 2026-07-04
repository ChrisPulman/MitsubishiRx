// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Provides the MitsubishiTypeName record.</summary>
/// <param name="ModelName">The ModelName parameter.</param>
/// <param name="ModelCode">The ModelCode parameter.</param>
public sealed record MitsubishiTypeName(string ModelName, ushort ModelCode);
