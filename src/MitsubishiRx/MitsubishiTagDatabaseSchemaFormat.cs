// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Defines the MitsubishiTagDatabaseSchemaFormat values.</summary>
internal enum MitsubishiTagDatabaseSchemaFormat
{
    /// <summary>Represents the Csv option.</summary>
    Csv,
    /// <summary>Represents the Json option.</summary>
    Json,
    /// <summary>Represents the Yaml option.</summary>
    Yaml,
}
