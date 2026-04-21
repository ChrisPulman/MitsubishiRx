# MitsubishiRx

Reactive Mitsubishi PLC client for **MC Protocol / SLMP** in C# with **ReactiveUI.Extensions** and **SerialPortRx** integration.

This README is the **primary usage guide** for the library. It explains:
- which PLC families, Ethernet frame types, and serial frame types are supported
- how to configure TCP/UDP/serial transports, binary/ASCII encodings, serial message formats, and X/Y notation
- how to use every public feature exposed by the client
- how to build and import a **tag database** so application code can use **tag names instead of PLC addresses**
- what CSV format is required to initialize the tag database

---

## Install

```bash
dotnet add package MitsubishiRx
```

---

## What this library provides

MitsubishiRx was refactored from a low-level socket wrapper into a protocol-aware Mitsubishi PLC client that:

- supports **1E**, **3E**, and **4E** Ethernet frame families
- supports **1C**, **3C**, and **4C** serial frame families
- supports **TCP**, **UDP**, and reactive **serial** transports
- uses **SerialPortRx** for reactive serial communications
- supports **binary** and **ASCII** MC Protocol / SLMP packet encodings
- supports direct device addressing and symbolic **tag-name-based** access
- exposes high-level async APIs for reads, writes, remote control, monitor, block, random, loopback, memory, and diagnostics operations
- exposes **ReactiveUI.Extensions**-based polling and health streams for reactive applications
- includes **TUnit** tests running on **Microsoft Testing Platform**

---

## Supported PLC families and how to choose settings

The library now covers Mitsubishi **Ethernet and serial MC protocol paths**. Ethernet support remains the broadest and deepest implementation. Serial support is now integrated through **SerialPortRx** and currently provides the first verified reactive serial path.

### Supported family guidance

| PLC family / endpoint type | Typical frame | Transport | Notes |
|---|---|---|---|
| **A / AnS** with legacy Ethernet interfaces | **1E** | TCP/UDP | Use when the target only exposes legacy A-compatible MC protocol behavior. |
| **QnA-compatible Ethernet endpoints** | **3E** | TCP/UDP | Default modern choice for most Q/QnA-compatible MC protocol use. |
| **Q / L / iQ-R / iQ-F / FX5** with modern SLMP/MC protocol endpoints | **3E** or **4E** | TCP/UDP | 3E is the normal first choice. 4E is used when serial correlation is required. |
| **FX3 compatibility paths** | **1E** or **3E** depending on module/path | TCP/UDP | Use the path documented for the installed Ethernet interface or gateway. |
| **FX3 / A-compatible serial computer-link style paths** | **1C** | Serial | Use for installed bases exposing serial MC / computer link compatible message structures. |
| **QnA-compatible serial modules** | **3C** | Serial | ASCII serial MC protocol path. |
| **QnA-compatible serial modules with extended access** | **4C** | Serial | ASCII or binary serial MC protocol path, depending on configured format. |

### Transport selection

| Transport | When to use |
|---|---|
| `MitsubishiTransportKind.Tcp` | Default choice for most PLC integrations. Use when you want connection-oriented request/response behavior. |
| `MitsubishiTransportKind.Udp` | Use when the target is configured for UDP SLMP/MC protocol and you want datagram-style communication. |
| `MitsubishiTransportKind.Serial` | Use for RS-232/RS-422/RS-485 MC protocol communication. The library uses **SerialPortRx** to provide the reactive serial transport implementation. |

### Data encoding selection

| Encoding | When to use |
|---|---|
| `CommunicationDataCode.Binary` | Default for most applications. Smaller frames and simpler payload handling. Required for `4C` serial format 5. |
| `CommunicationDataCode.Ascii` | Use when the target requires ASCII MC protocol / SLMP or when matching existing ASCII integrations. Required for `1C` and `3C`. |

### Serial message format selection

Serial MC protocol communication also depends on the serial **message format** configured on the PLC/module side.

| Serial message format | Meaning | Typical use |
|---|---|---|
| `MitsubishiSerialMessageFormat.Format1` | ASCII serial framing with ENQ/STX/ACK/NAK control characters | Legacy 1C/3C/4C serial ASCII integrations |
| `MitsubishiSerialMessageFormat.Format4` | ASCII serial framing with CR/LF delimiters | Serial endpoints configured for CR/LF terminated MC protocol |
| `MitsubishiSerialMessageFormat.Format5` | Binary serial framing using DLE/STX/ETX | `4C` binary serial communication |

### X/Y addressing notation

Mitsubishi `X` and `Y` device addressing is module/family dependent. The client makes that explicit.

| Setting | Meaning |
|---|---|
| `XyAddressNotation.Octal` | Interpret `X10` as octal `8`. This is common for classic Mitsubishi behavior. |
| `XyAddressNotation.Hexadecimal` | Interpret `X10` as hexadecimal `16`. Use when the Ethernet path/module is documented that way. |

---

## Core configuration

All communication starts from `MitsubishiClientOptions`.

```csharp
using MitsubishiRx;

var options = new MitsubishiClientOptions(
    Host: "192.168.0.10",
    Port: 5000,
    FrameType: MitsubishiFrameType.ThreeE,
    DataCode: CommunicationDataCode.Binary,
    TransportKind: MitsubishiTransportKind.Tcp,
    Route: MitsubishiRoute.Default,
    MonitoringTimer: 0x0010,
    Timeout: TimeSpan.FromSeconds(4),
    CpuType: CpuType.None,
    XyNotation: XyAddressNotation.Octal);
```

### Option reference

| Option | Meaning |
|---|---|
| `Host` | PLC IP address / DNS name for Ethernet, or serial port name such as `COM3` when using serial transport |
| `Port` | Ethernet port exposed by the PLC/module. Use `0` for serial transport. |
| `FrameType` | `OneE`, `ThreeE`, `FourE`, `OneC`, `ThreeC`, or `FourC` |
| `DataCode` | `Binary` or `Ascii` |
| `TransportKind` | `Tcp`, `Udp`, or `Serial` |
| `Route` | SLMP route metadata for 3E/4E |
| `MonitoringTimer` | PLC-side monitoring timer in 250 ms units |
| `Timeout` | Client-side transport timeout |
| `CpuType` | Optional family hint |
| `XyNotation` | Octal or hexadecimal parsing for `X`/`Y` |
| `LegacyPcNumber` | 1E PC number |
| `SerialNumberProvider` | 4E serial number generator |
| `Serial` | `MitsubishiSerialOptions` describing the serial port and serial MC protocol framing |

### Serial transport configuration

When using serial MC protocol communication, populate the `Serial` option and set `TransportKind.Serial`.

```csharp
using MitsubishiRx;
using System.IO.Ports;

var serialOptions = new MitsubishiClientOptions(
    Host: "COM3",
    Port: 0,
    FrameType: MitsubishiFrameType.FourC,
    DataCode: CommunicationDataCode.Binary,
    TransportKind: MitsubishiTransportKind.Serial,
    Timeout: TimeSpan.FromSeconds(2),
    CpuType: CpuType.Fx5,
    Serial: new MitsubishiSerialOptions(
        PortName: "COM3",
        BaudRate: 9600,
        DataBits: 7,
        Parity: Parity.Even,
        StopBits: StopBits.One,
        Handshake: Handshake.None,
        MessageFormat: MitsubishiSerialMessageFormat.Format5,
        StationNumber: 0x00,
        NetworkNumber: 0x00,
        PcNumber: 0xFF,
        RequestDestinationModuleIoNumber: 0x03FF,
        RequestDestinationModuleStationNumber: 0x00,
        SelfStationNumber: 0x00,
        MessageWait: 0x00));
```

### Serial option reference

| Serial option | Meaning |
|---|---|
| `PortName` | Serial port name, such as `COM3` |
| `BaudRate` | Configured baud rate |
| `DataBits` | Configured data bits |
| `Parity` | Configured serial parity |
| `StopBits` | Configured stop bits |
| `Handshake` | Configured hardware/software flow control |
| `MessageFormat` | Serial MC message format: `Format1`, `Format4`, or `Format5` |
| `StationNumber` | Target station number |
| `NetworkNumber` | Target network number for `3C/4C` |
| `PcNumber` | Target PC number |
| `RequestDestinationModuleIoNumber` | Request destination module I/O number for `4C` routing |
| `RequestDestinationModuleStationNumber` | Request destination module station number for `4C` routing |
| `SelfStationNumber` | Self-station number for multidrop layouts |
| `MessageWait` | Serial message wait in 10 ms units |
| `ReadBufferSize` / `WriteBufferSize` | Serial driver buffer sizing |
| `NewLine` | Newline sequence used by line-oriented serial modes |

### Default route

For direct own-station CPU access:

```csharp
var route = MitsubishiRoute.Default;
```

For routed access, supply explicit route values:

```csharp
var route = new MitsubishiRoute(
    NetworkNumber: 0x00,
    StationNumber: 0xFF,
    ModuleIoNumber: 0x03FF,
    MultidropStationNumber: 0x00);
```

---

## Creating the client

```csharp
using MitsubishiRx;

await using var client = new MitsubishiRx.MitsubishiRx(options);
```

### Legacy constructor

A compatibility constructor is also available:

```csharp
var client = new MitsubishiRx.MitsubishiRx(CpuType.QnA, "192.168.0.10", 5000, timeout: 1500);
```

---

## Connection lifecycle

### Open / close

```csharp
var open = await client.OpenAsync();
if (!open.IsSucceed)
{
    Console.WriteLine(open.Err);
}

var close = await client.CloseAsync();
```

Synchronous wrappers are also available:

```csharp
var openSync = client.Open();
var closeSync = client.Close();
```

### Connection state stream

```csharp
using var states = client.ConnectionStates.Subscribe(state =>
{
    Console.WriteLine($"Connection state: {state}");
});
```

Possible values:
- `Disconnected`
- `Connecting`
- `Connected`
- `Reconnecting`
- `Faulted`

---

## Feature guide: every public operation

The sections below map directly to the client’s public API.

---

## 1. Batch word reads and writes

### Read words by PLC address

```csharp
var result = await client.ReadWordsAsync("D100", 2);
if (result.IsSucceed)
{
    ushort d100 = result.Value![0];
    ushort d101 = result.Value[1];
}
```

### Read words over serial MC protocol

```csharp
using MitsubishiRx;
using System.IO.Ports;

var serialOptions = new MitsubishiClientOptions(
    Host: "COM3",
    Port: 0,
    FrameType: MitsubishiFrameType.FourC,
    DataCode: CommunicationDataCode.Binary,
    TransportKind: MitsubishiTransportKind.Serial,
    Timeout: TimeSpan.FromSeconds(2),
    CpuType: CpuType.Fx5,
    Serial: new MitsubishiSerialOptions(
        PortName: "COM3",
        BaudRate: 9600,
        DataBits: 7,
        Parity: Parity.Even,
        StopBits: StopBits.One,
        Handshake: Handshake.None,
        MessageFormat: MitsubishiSerialMessageFormat.Format5));

await using var serialClient = new MitsubishiRx.MitsubishiRx(serialOptions);
var serialRead = await serialClient.ReadWordsAsync("D100", 2);
```

### Write words by PLC address

```csharp
var write = await client.WriteWordsAsync("D100", new ushort[] { 123, 456, 789 });
```

### When to use
- data registers like `D`, `W`, `R`, `ZR`
- word-based timer/counter values like `TN`, `CN`, `SD`
- bulk register transfers

### PLC family guidance
- **1E**: use for legacy-compatible batch device operations over Ethernet
- **3E/4E**: preferred path for modern Ethernet PLCs
- **1C/3C/4C**: use when the installed connection is serial MC protocol rather than Ethernet

### Serial support status

Current serial implementation status:

| Serial area | Status |
|---|---|
| Reactive serial transport via `SerialPortRx` | **Implemented** |
| Serial frame modeling (`1C`, `3C`, `4C`) | **Implemented** |
| Serial option/configuration surface | **Implemented** |
| Batch word read over serial | **Implemented** |
| `1C` ASCII format 1/4 decode path | **Implemented** |
| `3C` ASCII format 1/4 decode path | **Implemented** |
| `4C` ASCII and binary format 5 decode path | **Implemented** |
| Serial writes / random / block / monitor / remote control / memory operations | **Not yet implemented** |
| Raw serial command execution | **Not yet implemented** |

---

## 2. Batch bit reads and writes

### Read bits by PLC address

```csharp
var bits = await client.ReadBitsAsync("M10", 8);
if (bits.IsSucceed)
{
    bool m10 = bits.Value![0];
    bool m11 = bits.Value[1];
}
```

### Write bits by PLC address

```csharp
var writeBits = await client.WriteBitsAsync("M10", new[] { true, false, true, true });
```

### Common device examples
- `M` internal relays
- `X` inputs
- `Y` outputs
- `L`, `SM`, `TS`, `TC`, `CS`, `CC`

### X/Y notation example

```csharp
var octalOptions = options with { XyNotation = XyAddressNotation.Octal };
var hexOptions = options with { XyNotation = XyAddressNotation.Hexadecimal };
```

---

## 3. Random reads and writes

Use random operations when you need non-contiguous word devices.

### Random read words

```csharp
var randomRead = await client.RandomReadWordsAsync(new[]
{
    "D100",
    "D250",
    "W10",
    "ZR200",
});
```

### Random write words

```csharp
var randomWrite = await client.RandomWriteWordsAsync(new Dictionary<string, ushort>
{
    ["D100"] = 100,
    ["D250"] = 250,
    ["W10"] = 0x1234,
});
```

### Best fit
- sparse register collection
- HMI/status pages pulling scattered registers
- writing a small set of independent values without multiple round-trips

---

## 4. Monitor registration and monitor execution

Monitoring is a two-stage operation.

### Register monitor devices

```csharp
var register = await client.RegisterMonitorAsync(new[]
{
    "D100",
    "D101",
    "D102",
});
```

### Execute monitor

```csharp
var monitor = await client.ExecuteMonitorAsync();
if (monitor.IsSucceed)
{
    byte[] rawMonitorPayload = monitor.Value!;
}
```

### Best fit
- repeated observation of a fixed register list
- lightweight monitoring loops coordinated by your application

---

## 5. Multiple block read and write

Use block operations when you want grouped contiguous word and/or bit blocks.

### Read blocks

```csharp
var blockRequest = new MitsubishiBlockRequest(
    WordBlocks:
    [
        new MitsubishiWordBlock(MitsubishiDeviceAddress.Parse("D100"), new ushort[10]),
        new MitsubishiWordBlock(MitsubishiDeviceAddress.Parse("W20", XyAddressNotation.Octal), new ushort[4]),
    ],
    BitBlocks:
    [
        new MitsubishiBitBlock(MitsubishiDeviceAddress.Parse("M10"), new bool[16]),
    ]);

var blockRead = await client.ReadBlocksAsync(blockRequest);
```

### Write blocks

```csharp
var writeRequest = new MitsubishiBlockRequest(
    WordBlocks:
    [
        new MitsubishiWordBlock(MitsubishiDeviceAddress.Parse("D100"), new ushort[] { 1, 2, 3, 4 }),
    ],
    BitBlocks:
    [
        new MitsubishiBitBlock(MitsubishiDeviceAddress.Parse("M10"), new[] { true, false, true, false }),
    ]);

var blockWrite = await client.WriteBlocksAsync(writeRequest);
```

### Best fit
- grouped transfer plans
- deterministic read/write structures
- coalesced data exchange where address continuity matters

---

## 6. PLC type-name read

```csharp
var typeName = await client.ReadTypeNameAsync();
if (typeName.IsSucceed)
{
    Console.WriteLine(typeName.Value!.ModelName);
    Console.WriteLine(typeName.Value.ModelCode);
}
```

### Best fit
- startup diagnostics
- logging exact connected PLC/module type
- validation that the integration is pointing at the expected target

---

## 7. Remote control operations

### Remote RUN

```csharp
await client.RemoteRunAsync(force: true, clearMode: false);
```

### Remote STOP / PAUSE / RESET / LATCH CLEAR

```csharp
await client.RemoteStopAsync();
await client.RemotePauseAsync();
await client.RemoteResetAsync();
await client.RemoteLatchClearAsync();
```

### Notes
- available behavior depends on PLC family, CPU mode, permissions, and Ethernet module configuration
- use carefully in production systems

---

## 8. Remote password unlock / lock

```csharp
await client.UnlockAsync("1234");
await client.LockAsync("1234");
```

### Best fit
- workflows where protected remote operations must be explicitly unlocked

---

## 9. Clear error

```csharp
var clear = await client.ClearErrorAsync();
```

### Best fit
- acknowledging module/PLC error conditions after diagnostic handling

---

## 10. Loopback

```csharp
var loop = await client.LoopbackAsync(new byte[] { 0x12, 0x34, 0x56, 0x78 });
if (loop.IsSucceed)
{
    var echoed = loop.Value!;
}
```

### Best fit
- link validation
- protocol path smoke tests
- troubleshooting Ethernet routes or gateway behavior

---

## 11. Memory read / write and intelligent-module access

These methods expose raw memory/buffer style commands.

### Memory read

```csharp
var memory = await client.ReadMemoryAsync(MitsubishiCommands.MemoryRead, address: 0x2000, length: 4);
```

### Memory write

```csharp
var memoryWrite = await client.WriteMemoryAsync(MitsubishiCommands.MemoryWrite, address: 0x2000, values: new ushort[] { 1, 2, 3, 4 });
```

### Extend unit read/write

```csharp
var unitRead = await client.ReadMemoryAsync(MitsubishiCommands.ExtendUnitRead, address: 0x0100, length: 8);
var unitWrite = await client.WriteMemoryAsync(MitsubishiCommands.ExtendUnitWrite, address: 0x0100, values: new ushort[] { 10, 20, 30 });
```

### Best fit
- intelligent function module buffer memory access
- lower-level system data exchange where documented by Mitsubishi manuals

---

## 12. Raw command execution

For advanced or unsupported workflows, execute a raw request.

```csharp
var raw = await client.ExecuteRawAsync(
    new MitsubishiRawCommandRequest(
        Command: MitsubishiCommands.DeviceRead,
        Subcommand: 0x0000,
        Body: Array.Empty<byte>(),
        Description: "Custom raw op"));
```

### Best fit
- experimental protocol work
- custom command shapes
- validating edge-case protocol scenarios

---

## 13. Reactive polling and diagnostics

Reactive features are built with **ReactiveUI.Extensions**.

### Observe words

```csharp
using var subscription = client
    .ObserveWords("D100", 2, TimeSpan.FromSeconds(1))
    .Subscribe(result =>
    {
        if (result.IsSucceed)
        {
            Console.WriteLine(string.Join(", ", result.Value!));
        }
    });
```

### Observe bits

```csharp
using var bitSubscription = client
    .ObserveBits("M10", 8, TimeSpan.FromMilliseconds(500))
    .Subscribe(result =>
    {
        if (result.IsSucceed)
        {
            Console.WriteLine(string.Join(", ", result.Value!));
        }
    });
```

### Observe words with heartbeat

```csharp
using var heartbeatSub = client
    .ObserveWordsHeartbeat(
        "D100",
        2,
        pollInterval: TimeSpan.FromSeconds(1),
        heartbeatAfter: TimeSpan.FromSeconds(2))
    .Subscribe(sample =>
    {
        if (sample.IsHeartbeat)
        {
            Console.WriteLine("Heartbeat");
            return;
        }

        Console.WriteLine(string.Join(", ", sample.Update.Value!));
    });
```

### Observe words with stale detection

```csharp
using var staleSub = client
    .ObserveWordsStale(
        "D100",
        2,
        pollInterval: TimeSpan.FromSeconds(1),
        staleAfter: TimeSpan.FromSeconds(5))
    .Subscribe(state =>
    {
        Console.WriteLine($"Is stale: {state.IsStale}");
    });
```

### Triggered latest-only reads

```csharp
using System.Reactive;
using System.Reactive.Subjects;

var trigger = new Subject<Unit>();
using var latestSub = client
    .ObserveWordsLatest("D100", 2, trigger)
    .Subscribe(result => Console.WriteLine(result.IsSucceed));

trigger.OnNext(Unit.Default);
```

### Reactive tag-group polling

Once tag groups are defined, you can observe grouped snapshots with the same reactive patterns used by the lower-level word/bit APIs.

### Observe a tag group

```csharp
using var groupSub = client
    .ObserveTagGroup("Line1Overview", TimeSpan.FromSeconds(1))
    .Subscribe(result =>
    {
        if (!result.IsSucceed || result.Value is null)
        {
            return;
        }

        var snapshot = result.Value;
        Console.WriteLine($"Temp={snapshot.GetRequired<short>("SignedTemp")}");
        Console.WriteLine($"Count={snapshot.GetRequired<uint>("TotalCount")}");
        Console.WriteLine($"Message={snapshot.GetRequired<string>("OperatorMessage")}");
        Console.WriteLine($"Pump={snapshot.GetRequired<bool>("PumpRunning")}");
    });
```

### Observe a tag group with heartbeat

```csharp
using var groupHeartbeat = client
    .ObserveTagGroupHeartbeat(
        "Line1Overview",
        pollInterval: TimeSpan.FromSeconds(5),
        heartbeatAfter: TimeSpan.FromSeconds(2))
    .Subscribe(sample =>
    {
        if (sample.IsHeartbeat)
        {
            Console.WriteLine("Group heartbeat");
            return;
        }

        var snapshot = sample.Update!.Value!;
        Console.WriteLine(snapshot.GetRequired<uint>("TotalCount"));
    });
```

### Observe a tag group with stale detection

```csharp
using var groupStale = client
    .ObserveTagGroupStale(
        "Line1Overview",
        pollInterval: TimeSpan.FromSeconds(5),
        staleAfter: TimeSpan.FromSeconds(2))
    .Subscribe(state =>
    {
        Console.WriteLine($"Group stale={state.IsStale}");
    });
```

### Triggered latest-only grouped reads

```csharp
using System.Reactive;
using System.Reactive.Subjects;

var groupTrigger = new Subject<Unit>();
using var latestGroup = client
    .ObserveTagGroupLatest("Line1Overview", groupTrigger)
    .Subscribe(result =>
    {
        if (result.IsSucceed && result.Value is not null)
        {
            Console.WriteLine(result.Value.GetRequired<uint>("TotalCount"));
        }
    });

groupTrigger.OnNext(Unit.Default);
```

These grouped reactive APIs are useful for HMI/dashboard polling loops because they keep the application written against stable symbolic names instead of raw PLC addresses.

### Operation logs and sampled diagnostics

```csharp
using var logs = client.OperationLogs.Subscribe(log =>
{
    Console.WriteLine($"{log.TimestampUtc:u} {log.Description} success={log.Success}");
});
```

```csharp
using System.Reactive.Linq;

var diagnosticTrigger = Observable.Interval(TimeSpan.FromSeconds(2)).Select(_ => new object());
using var diagnostics = client.SampleDiagnostics(diagnosticTrigger).Subscribe(log =>
{
    Console.WriteLine(log.Description);
});
```

### Connection health

```csharp
using var health = client.ObserveConnectionHealth(TimeSpan.FromSeconds(10)).Subscribe(state =>
{
    Console.WriteLine($"Connection stale={state.IsStale}, state={state.Update}");
});
```

### Reactive operators used internally

The library meaningfully uses these `ReactiveUI.Extensions` operators:
- `RetryWithBackoff(...)`
- `SelectAsyncSequential(...)`
- `SelectLatestAsync(...)`
- `Heartbeat(...)`
- `DetectStale(...)`
- `Conflate(...)`
- `SampleLatest(...)`
- `DoOnSubscribe(...)`
- `DoOnDispose(...)`

---

## Tag database: use tag names instead of PLC addresses

For production applications, raw addresses like `D100` and `M10` usually belong in configuration, not code.

The library now includes an in-memory **tag database** that maps symbolic names to PLC addresses.

### What it gives you

Instead of this:

```csharp
var speed = await client.ReadWordsAsync("D100", 2);
var pump = await client.ReadBitsAsync("M10", 1);
await client.WriteWordsAsync("D300", new ushort[] { 12 });
await client.RandomWriteWordsAsync(new[]
{
    new KeyValuePair<string, ushort>("D500", 100),
    new KeyValuePair<string, ushort>("D501", 200),
});
```

you can do this:

```csharp
var speed = await client.ReadWordsByTagAsync("MotorSpeed", 2);
var pump = await client.ReadBitsByTagAsync("PumpRunning", 1);
await client.WriteWordsByTagAsync("RecipeNumber", new ushort[] { 12 });
await client.RandomWriteWordsByTagAsync(new[]
{
    new KeyValuePair<string, ushort>("RecipeSetpointA", 100),
    new KeyValuePair<string, ushort>("RecipeSetpointB", 200),
});
```

### Tag database types

- `MitsubishiTagDefinition`
- `MitsubishiTagDatabase`
- `MitsubishiTagGroupDefinition`
- `MitsubishiTagGroupSnapshot`
- `MitsubishiRx.TagDatabase`
- `ReadWordsByTagAsync(...)`
- `ReadBitsByTagAsync(...)`
- `WriteWordsByTagAsync(...)`
- `WriteBitsByTagAsync(...)`
- `RandomReadWordsByTagAsync(...)`
- `RandomWriteWordsByTagAsync(...)`
- `ReadInt16ByTagAsync(...)`
- `WriteInt16ByTagAsync(...)`
- `ReadUInt16ByTagAsync(...)`
- `WriteUInt16ByTagAsync(...)`
- `ReadInt32ByTagAsync(...)`
- `WriteInt32ByTagAsync(...)`
- `ReadDWordByTagAsync(...)`
- `WriteDWordByTagAsync(...)`
- `ReadFloatByTagAsync(...)`
- `WriteFloatByTagAsync(...)`
- `ReadScaledDoubleByTagAsync(...)`
- `WriteScaledDoubleByTagAsync(...)`
- `ReadStringByTagAsync(...)`
- `WriteStringByTagAsync(...)`
- `ValidateTagDatabase()`
- `ReadTagGroupSnapshotAsync(...)`

### Build a tag database in code

```csharp
using MitsubishiRx;

var tags = new MitsubishiTagDatabase(new[]
{
    new MitsubishiTagDefinition(
        Name: "MotorSpeed",
        Address: "D100",
        DataType: "Word",
        Description: "Main spindle RPM",
        Scale: 0.1,
        Offset: 0.0,
        Notes: "Engineering scaling 0.1 RPM per count"),

    new MitsubishiTagDefinition(
        Name: "PumpRunning",
        Address: "M10",
        DataType: "Bit",
        Description: "Coolant pump running"),

    new MitsubishiTagDefinition(
        Name: "RecipeNumber",
        Address: "D300",
        DataType: "Word",
        Description: "Selected recipe number"),

    new MitsubishiTagDefinition(
        Name: "RecipeSetpointA",
        Address: "D500",
        DataType: "Word"),

    new MitsubishiTagDefinition(
        Name: "RecipeSetpointB",
        Address: "D501",
        DataType: "Word"),
});

client.TagDatabase = tags;
```

### Read and write using tag names

```csharp
var speed = await client.ReadWordsByTagAsync("MotorSpeed", 2);
var running = await client.ReadBitsByTagAsync("PumpRunning", 1);

await client.WriteWordsByTagAsync("RecipeNumber", new ushort[] { 12 });
await client.WriteBitsByTagAsync("PumpRunning", new[] { true });
```

### Random operations using tag names

```csharp
var recipeValues = await client.RandomReadWordsByTagAsync(new[]
{
    "RecipeSetpointA",
    "RecipeSetpointB",
    "RecipeNumber",
});

await client.RandomWriteWordsByTagAsync(new[]
{
    new KeyValuePair<string, ushort>("RecipeSetpointA", 100),
    new KeyValuePair<string, ushort>("RecipeSetpointB", 200),
    new KeyValuePair<string, ushort>("RecipeNumber", 12),
});
```

### Typed tag helpers

Use `DataType` to make tag intent explicit and then call the typed helpers directly.

```csharp
var signedTemp = await client.ReadInt16ByTagAsync("SignedTemp");
await client.WriteInt16ByTagAsync("SignedTemp", -100);

var wordValue = await client.ReadUInt16ByTagAsync("RecipeNumber");
await client.WriteUInt16ByTagAsync("RecipeNumber", 12);

var signedTotal = await client.ReadInt32ByTagAsync("SignedTotal");
await client.WriteInt32ByTagAsync("SignedTotal", 123456);

var totalCount = await client.ReadDWordByTagAsync("TotalCount");
await client.WriteDWordByTagAsync("TotalCount", 123456u);

var processValue = await client.ReadFloatByTagAsync("ProcessValue");
await client.WriteFloatByTagAsync("ProcessValue", 12.5f);
```

Integer helpers supported in the current API surface:
- `ReadInt16ByTagAsync(...)`
- `WriteInt16ByTagAsync(...)`
- `ReadUInt16ByTagAsync(...)`
- `WriteUInt16ByTagAsync(...)`
- `ReadInt32ByTagAsync(...)`
- `WriteInt32ByTagAsync(...)`
- `ReadDWordByTagAsync(...)`
- `WriteDWordByTagAsync(...)`
- `ReadFloatByTagAsync(...)`
- `WriteFloatByTagAsync(...)`

`Int32`, `UInt32`/`DWord`, and `Float` values are encoded across two Mitsubishi words. `ByteOrder` controls whether those two words are interpreted as `LittleEndian` or `BigEndian`.

### Scaled engineering values

If a tag carries engineering metadata in `Scale` and `Offset`, use the scaled helpers so application code can work with engineering units instead of raw PLC counts.

```csharp
var headTemp = await client.ReadScaledDoubleByTagAsync("HeadTemp");
await client.WriteScaledDoubleByTagAsync("HeadTemp", 15.0d);
```

With this CSV row:

```csv
Name,Address,DataType,Scale,Offset
HeadTemp,D200,Word,0.1,-10
```

the PLC raw value `250` becomes `(250 * 0.1) + (-10) = 15.0`.

Scaled read/write currently supports:
- `Word`
- `DWord`
- `Float`

### PLC strings using tag names

For PLC text stored in word registers, use the string helpers with an explicit word length or let the tag schema provide it.

```csharp
var message = await client.ReadStringByTagAsync("OperatorMessage", wordLength: 8);
await client.WriteStringByTagAsync("OperatorMessage", "READY", wordLength: 8);

var schemaDrivenMessage = await client.ReadStringByTagAsync("Utf8Message");
await client.WriteStringByTagAsync("Utf8Message", "Aé");
```

String values are packed into successive words using the configured `Encoding`. Each Mitsubishi word stores two bytes, and `ByteOrder` controls how those two bytes are packed inside each word.

### How tag resolution works

- tag names are resolved case-insensitively
- the resolved tag supplies the PLC `Address`
- `DataType` is validated when tags are added or imported from CSV
- supported `DataType` values are:
  - `Bit`
  - `Word`
  - `DWord`
  - `Float`
  - `String`
- `DataType` matching is case-insensitive and normalized to the canonical values above
- the current tag-based convenience API supports:
  - `ReadWordsByTagAsync(...)`
  - `ReadBitsByTagAsync(...)`
  - `WriteWordsByTagAsync(...)`
  - `WriteBitsByTagAsync(...)`
  - `RandomReadWordsByTagAsync(...)`
  - `RandomWriteWordsByTagAsync(...)`
  - `ReadDWordByTagAsync(...)`
  - `WriteDWordByTagAsync(...)`
  - `ReadFloatByTagAsync(...)`
  - `WriteFloatByTagAsync(...)`
  - `ReadScaledDoubleByTagAsync(...)`
  - `WriteScaledDoubleByTagAsync(...)`
  - `ReadStringByTagAsync(...)`
  - `WriteStringByTagAsync(...)`
- tag APIs are transport/frame agnostic: they work with `1E`, `3E`, `4E`, `TCP`, `UDP`, `Binary`, and `Ascii` wherever the underlying operation is supported by the target PLC family/module
- all tag APIs eventually resolve to the same raw address-based methods, so protocol behavior stays identical after resolution
- other operations can still use the same database manually:

```csharp
var tag = client.TagDatabase!.GetRequired("RecipeSetpointA");
await client.WriteWordsAsync(tag.Address, new ushort[] { 2500 });
```

### Recommended usage model

- store PLC addressing in CSV/configuration
- load it at application startup
- assign it to `client.TagDatabase`
- keep application logic written against stable tag names
- let maintenance teams change addresses in CSV without changing application code
- define `MitsubishiTagGroupDefinition` scan classes for common screens, loops, and reporting views
- call `ValidateTagDatabase()` during startup so bad addresses, missing string lengths, and broken group references fail fast

### Tag groups and grouped snapshots

For higher-level workflows, define named groups of tags and read them as a single heterogeneous snapshot.

```csharp
var tags = MitsubishiTagDatabase.FromCsv(File.ReadAllText("plc-tags.csv"));

tags.AddGroup(new MitsubishiTagGroupDefinition(
    Name: "Line1Overview",
    TagNames: new[]
    {
        "SignedTemp",
        "TotalCount",
        "OperatorMessage",
        "PumpRunning",
    }));

tags.AddGroup(new MitsubishiTagGroupDefinition(
    Name: "RecipeWrite",
    TagNames: new[]
    {
        "RecipeNumber",
        "OperatorMessage",
    }));

client.TagDatabase = tags;

var validation = client.ValidateTagDatabase();
if (!validation.IsSucceed)
{
    throw new InvalidOperationException(validation.Err);
}

var snapshot = await client.ReadTagGroupSnapshotAsync("Line1Overview");

var signedTemp = snapshot.Value!.GetRequired<short>("SignedTemp");
var totalCount = snapshot.Value.GetRequired<uint>("TotalCount");
var operatorMessage = snapshot.Value.GetRequired<string>("OperatorMessage");
var pumpRunning = snapshot.Value.GetRequired<bool>("PumpRunning");
```

Use tag groups when you want:
- startup validation of known screen/report/scan-class dependencies
- a single named collection for related tags
- typed access to heterogeneous values without scattering tag names across the application

### Grouped writes and setpoint commits

For HMI/setpoint workflows, validate and write only the values you want to commit.

```csharp
var pendingValues = new Dictionary<string, object?>
{
    ["RecipeNumber"] = (ushort)7,
    ["OperatorMessage"] = "OK!",
};

var writeValidation = client.ValidateTagGroupWrite("RecipeWrite", pendingValues);
if (!writeValidation.IsSucceed)
{
    throw new InvalidOperationException(writeValidation.Err);
}

await client.WriteTagGroupValuesAsync("RecipeWrite", pendingValues);
```

You can also write a full grouped snapshot directly:

```csharp
var writeSnapshot = new MitsubishiTagGroupSnapshot(
    "Line1Overview",
    new Dictionary<string, object?>
    {
        ["SignedTemp"] = (short)-100,
        ["TotalCount"] = 0x12345678u,
        ["OperatorMessage"] = "OK!",
    });

await client.WriteTagGroupSnapshotAsync(writeSnapshot);
```

`ValidateTagGroupWrite(...)` reports:
- values whose CLR types do not match the target tag schema
- values for tags that are not part of the named group
- the same underlying tag/schema issues already enforced by the individual tag helpers

---

## CSV import: initialize the tag database from a file

You can initialize the tag database directly from CSV.

### Supported required/optional columns

| Column | Required | Meaning |
|---|---|---|
| `Name` | **Yes** | Unique symbolic tag name used by application code |
| `Address` | **Yes** | Mitsubishi PLC address such as `D100`, `M10`, `X20`, `ZR200` |
| `DataType` | No | Type hint such as `Bit`, `Word`, `DWord`, `Float`, `String`, `Int16`, `UInt16`, `Int32`, `UInt32` |
| `Description` | No | Human-readable description |
| `Scale` | No | Engineering scale factor, default `1.0` |
| `Offset` | No | Engineering offset, default `0.0` |
| `Length` | No | Logical tag length in PLC words, mainly used by string tags and fixed-width layouts |
| `Encoding` | No | Text encoding hint such as `Ascii`, `Utf8`, or `Utf16` |
| `Units` | No | Engineering units label such as `rpm`, `°C`, or `items` |
| `Signed` | No | Boolean signedness hint for integer word/double-word tags, default `false` |
| `ByteOrder` | No | Multi-word and string packing order: `LittleEndian` or `BigEndian` |
| `Notes` | No | Free-form notes |

### Required CSV formatting

- first row **must** be a header row
- at minimum the header must include:
  - `Name`
  - `Address`
- column names are matched case-insensitively
- blank lines are ignored
- quoted CSV fields are supported
- embedded double quotes inside quoted fields should be escaped as `""`
- numeric `Scale`, `Offset`, and `Length` values should use invariant-culture formatting
- `Signed` should be `true` or `false`
- do **not** include engineering units inside `Scale` or `Offset`
- `Address` must contain a valid Mitsubishi device address string usable by the client
- if `DataType` is supplied it must be one of:
  - `Bit`
  - `Word`
  - `DWord`
  - `Float`
  - `String`
  - `Int16`
  - `UInt16`
  - `Int32`
  - `UInt32`
- if `Encoding` is supplied it must be one of:
  - `Ascii`
  - `Utf8`
  - `Utf16`
- if `ByteOrder` is supplied it must be one of:
  - `LittleEndian`
  - `BigEndian`
- `DataType`, `Encoding`, and `ByteOrder` matching is case-insensitive when imported, but stored in canonical form
- use one logical PLC item per CSV row
- keep `Name` unique across the file so it can be used safely as the application lookup key

### Example CSV file

```csv
Name,Address,DataType,Description,Scale,Offset,Length,Encoding,Units,Signed,ByteOrder,Notes
MotorSpeed,D100,Word,Main spindle RPM,0.1,0,,,rpm,false,,From commissioning sheet
PumpRunning,M10,Bit,Coolant pump running,1,0,,,,false,,
HeadTemp,D200,Word,Head temperature,0.1,-10,,,°C,true,,Signed engineering temperature tag
RecipeNumber,D300,UInt16,Selected recipe,1,0,,,recipe,false,,
TotalCount,D400,UInt32,Accumulated production count,1,0,,,items,false,LittleEndian,32-bit unsigned counter
ProcessValue,D500,Float,Engineering process value,1,0,,,bar,false,LittleEndian,IEEE754 single precision across two words
OperatorMessage,D600,String,Operator status message,1,0,8,Ascii,,false,LittleEndian,Packed text in word registers
SignedTemp,D700,Int16,Signed temperature raw count,1,0,,,counts,true,,Two's complement 16-bit value
SignedTotal,D710,Int32,Signed accumulated count,1,0,,,items,true,BigEndian,Big-endian multiword example
Utf8Message,D720,String,UTF-8 operator text,1,0,2,Utf8,,false,LittleEndian,Schema-driven string length
ServoReady,M100,Bit,Servo ready,1,0,,,,false,,
XAxisLimit,X20,Bit,X-axis forward limit,1,0,,,,false,,X uses configured XyNotation
ZoneRegister,ZR200,Word,Zone parameter register,1,0,,,,false,,
```

### Load from a CSV string

```csharp
var csv = File.ReadAllText("plc-tags.csv");
var tagDatabase = MitsubishiTagDatabase.FromCsv(csv);
client.TagDatabase = tagDatabase;
```

Or load the same CSV directly by file extension:

```csharp
client.TagDatabase = MitsubishiTagDatabase.Load("plc-tags.csv");
```

### Full startup example

```csharp
using MitsubishiRx;

var options = new MitsubishiClientOptions(
    Host: "192.168.0.10",
    Port: 5000,
    FrameType: MitsubishiFrameType.ThreeE,
    DataCode: CommunicationDataCode.Binary,
    TransportKind: MitsubishiTransportKind.Tcp,
    Route: MitsubishiRoute.Default,
    MonitoringTimer: 0x0010,
    XyNotation: XyAddressNotation.Octal);

var csv = File.ReadAllText("plc-tags.csv");
var tags = MitsubishiTagDatabase.FromCsv(csv);

await using var client = new MitsubishiRx.MitsubishiRx(options)
{
    TagDatabase = tags,
};

var speed = await client.ReadWordsByTagAsync("MotorSpeed", 2);
var pump = await client.ReadBitsByTagAsync("PumpRunning", 1);
await client.WriteUInt16ByTagAsync("RecipeNumber", 7);
await client.RandomWriteWordsByTagAsync(new[]
{
    new KeyValuePair<string, ushort>("RecipeSetpointA", 100),
    new KeyValuePair<string, ushort>("RecipeSetpointB", 200),
});

var signedTemp = await client.ReadInt16ByTagAsync("SignedTemp");
var signedTotal = await client.ReadInt32ByTagAsync("SignedTotal");
var totalCount = await client.ReadDWordByTagAsync("TotalCount");
var processValue = await client.ReadFloatByTagAsync("ProcessValue");
var engineeringTemp = await client.ReadScaledDoubleByTagAsync("HeadTemp");
var operatorMessage = await client.ReadStringByTagAsync("OperatorMessage");
var utf8Message = await client.ReadStringByTagAsync("Utf8Message");
```

### JSON and YAML schema workflows

CSV is useful for spreadsheets and maintenance exports, but JSON/YAML are better when you want full schema persistence, groups, and richer version-controlled configuration.

### Export the full schema to JSON

```csharp
client.TagDatabase!.Save("plc-tags.json");
```

Equivalent explicit form:

```csharp
var json = client.TagDatabase!.ToJson();
File.WriteAllText("plc-tags.json", json);
```

### Load the full schema from JSON

```csharp
client.TagDatabase = MitsubishiTagDatabase.Load("plc-tags.json");
```

Equivalent explicit form:

```csharp
var json = File.ReadAllText("plc-tags.json");
var tags = MitsubishiTagDatabase.FromJson(json);
client.TagDatabase = tags;
```

### Export the full schema to YAML

```csharp
client.TagDatabase!.Save("plc-tags.yaml");
client.TagDatabase!.Save("plc-tags.yml");
```

Equivalent explicit form:

```csharp
var yaml = client.TagDatabase!.ToYaml();
File.WriteAllText("plc-tags.yaml", yaml);
```

### Load the full schema from YAML

```csharp
client.TagDatabase = MitsubishiTagDatabase.Load("plc-tags.yaml");
client.TagDatabase = MitsubishiTagDatabase.Load("plc-tags.yml");
```

Equivalent explicit form:

```csharp
var yaml = File.ReadAllText("plc-tags.yaml");
var tags = MitsubishiTagDatabase.FromYaml(yaml);
client.TagDatabase = tags;
```

### Example YAML schema with groups

```yaml
tags:
  - name: SignedTemp
    address: D700
    dataType: Int16
    signed: true
    units: °C
  - name: OperatorMessage
    address: D600
    dataType: String
    length: 2
    encoding: Utf8
    byteOrder: BigEndian
groups:
  - name: Overview
    tagNames:
      - SignedTemp
      - OperatorMessage
```

Use JSON/YAML when you want:
- full schema persistence including groups
- easier code review of tag model changes in version control
- richer metadata than a compact CSV worksheet usually carries

Use `MitsubishiTagDatabase.Load(path)` / `Save(path)` when you want one-line startup configuration or persistence with automatic detection for `.csv`, `.json`, `.yaml`, and `.yml`.

For commissioning workflows, `client.LoadAndValidateTagDatabase(path)` loads, validates, and applies the schema in one step, `client.PreviewTagDatabaseDiff(path)` shows what would change before you commit it, and `client.ObserveTagDatabaseReload(path, pollInterval)` / `client.ObserveTagDatabaseDiff(path, pollInterval)` provide reactive reload and audit streams. Use rollout policies when you want to allow metadata/group edits automatically while blocking address or datatype changes.

### Load, validate, and apply a schema in one step

```csharp
var loadResult = client.LoadAndValidateTagDatabase("plc-tags.yaml");
if (!loadResult.IsSucceed)
{
    throw new InvalidOperationException(loadResult.Err);
}
```

### Reactively hot-reload a schema during commissioning

```csharp
using var schemaReload = client
    .ObserveTagDatabaseReload("plc-tags.yaml", TimeSpan.FromSeconds(2))
    .Subscribe(result =>
    {
        if (!result.IsSucceed)
        {
            Console.WriteLine($"Schema reload failed: {result.Err}");
            return;
        }

        Console.WriteLine($"Reloaded {result.Value!.Count} tags and {result.Value.GroupCount} groups");
    });
```

`ObserveTagDatabaseReload(...)` only applies a newly loaded database when validation succeeds. Invalid reloads are emitted as failed results and the last valid `client.TagDatabase` remains active.

### Preview schema changes before applying them

```csharp
var preview = client.PreviewTagDatabaseDiff("plc-tags.yaml");
if (!preview.IsSucceed)
{
    throw new InvalidOperationException(preview.Err);
}

Console.WriteLine($"Added tags: {preview.Value!.AddedTags.Count}");
Console.WriteLine($"Removed tags: {preview.Value.RemovedTags.Count}");
Console.WriteLine($"Changed tags: {preview.Value.ChangedTags.Count}");
```

### Reactively audit schema changes during hot reload

```csharp
using var schemaAudit = client
    .ObserveTagDatabaseDiff("plc-tags.yaml", TimeSpan.FromSeconds(2), emitInitial: false)
    .Subscribe(result =>
    {
        if (!result.IsSucceed)
        {
            Console.WriteLine($"Schema diff failed: {result.Err}");
            return;
        }

        var diff = result.Value!;
        Console.WriteLine($"Schema changed: {diff.ChangeCount} semantic changes");
    });
```

`ObserveTagDatabaseDiff(...)` emits semantic tag/group changes for each successful reload and keeps the last valid `client.TagDatabase` when an update is invalid.

### Apply rollout policy gates during commissioning

```csharp
var gatedLoad = client.LoadAndValidateTagDatabase(
    "plc-tags.yaml",
    MitsubishiTagRolloutPolicy.SafeMetadataAndGroups);

if (!gatedLoad.IsSucceed)
{
    throw new InvalidOperationException(gatedLoad.Err);
}
```

`MitsubishiTagRolloutPolicy.SafeMetadataAndGroups` allows:
- metadata-only tag changes
- tag-group membership/order changes

It rejects:
- address changes
- datatype/encoding/length/signedness/byte-order changes
- added/removed tags or groups

### Preview classified changes before applying them

```csharp
var preview = client.PreviewTagDatabaseDiff(
    "plc-tags.yaml",
    MitsubishiTagRolloutPolicy.SafeMetadataAndGroups);

if (preview.Value is not null)
{
    Console.WriteLine($"Kinds: {preview.Value.ChangeKinds}");
    Console.WriteLine($"Total changes: {preview.Value.ChangeCount}");
}
```

### Reactively enforce rollout policy during hot reload

```csharp
using var gatedReload = client
    .ObserveTagDatabaseReload(
        "plc-tags.yaml",
        TimeSpan.FromSeconds(2),
        emitInitial: false,
        policy: MitsubishiTagRolloutPolicy.SafeMetadataAndGroups)
    .Subscribe(result =>
    {
        if (!result.IsSucceed)
        {
            Console.WriteLine($"Reload blocked: {result.Err}");
        }
    });
```

### Practical CSV rules for maintenance teams

Recommended conventions:
- `Name`: PascalCase or a consistent SCADA/HMI-friendly convention
- `Address`: exact Mitsubishi address string with no extra spaces
- `DataType`: one of `Bit`, `Word`, `DWord`, `Float`, `String`, `Int16`, `UInt16`, `Int32`, `UInt32`
- `Length`: set this for string tags so code can call `ReadStringByTagAsync(tagName)` without supplying a length each time
- `Encoding`: use `Ascii` unless the PLC text really requires `Utf8` or `Utf16`
- `Signed`: set to `true` for signed integer word/double-word values or signed scaled engineering values
- `ByteOrder`: use `LittleEndian` for the normal two-word Mitsubishi layout and `BigEndian` only when the external data contract requires it
- `Units`: use for UI/reporting metadata such as `rpm`, `°C`, or `items`
- `Description`: operator-facing sentence
- `Notes`: use for commissioning notes, source document, or unit conversion notes

### Example with quoted fields

```csv
Name,Address,DataType,Description,Scale,Offset,Notes
LineSpeed,D110,Word,"Main conveyor speed, calculated",0.01,0,"Imported from ""Line-1 IO List"""
```

### Validation behavior

`MitsubishiTagDatabase.FromCsv(...)` will fail when:
- there is no header row
- `Name` is missing from the header
- `Address` is missing from the header
- a row has an empty required value for `Name` or `Address`
- `Scale`, `Offset`, or `Length` contain invalid numeric values
- `Signed` contains an invalid boolean value
- `DataType` contains an unsupported value
- `Encoding` contains an unsupported value
- `ByteOrder` contains an unsupported value

`client.ValidateTagDatabase()` additionally reports:
- tag addresses that cannot be parsed for the configured `XyNotation`
- string tags that do not define a positive `Length`
- tag groups that reference missing tags

The accepted `DataType` values are exactly:
- `Bit`
- `Word`
- `DWord`
- `Float`
- `String`
- `Int16`
- `UInt16`
- `Int32`
- `UInt32`

The accepted `Encoding` values are exactly:
- `Ascii`
- `Utf8`
- `Utf16`

The accepted `ByteOrder` values are exactly:
- `LittleEndian`
- `BigEndian`

---

## PLC-family-specific usage guidance

This section shows how to think about feature usage by PLC family.

### A / AnS legacy paths

Use **1E** when the installed Ethernet interface/module exposes A-compatible MC protocol only.

Typical operations:
- `ReadWordsAsync`
- `WriteWordsAsync`
- `ReadBitsAsync`
- `WriteBitsAsync`
- `ReadTypeNameAsync`
- `LoopbackAsync`
- core remote operations where supported by the target path

Example:

```csharp
var options = new MitsubishiClientOptions(
    Host: "192.168.0.20",
    Port: 5000,
    FrameType: MitsubishiFrameType.OneE,
    DataCode: CommunicationDataCode.Binary,
    TransportKind: MitsubishiTransportKind.Tcp,
    MonitoringTimer: 0x0010,
    CpuType: CpuType.ASeries,
    LegacyPcNumber: 0xFF);
```

### Q / QnA / L / iQ-R / iQ-F / FX5 modern Ethernet

Use **3E** as the default unless the integration explicitly needs **4E** serial correlation.

Typical operations:
- all batch operations
- random read/write
- block read/write
- monitor registration/execute
- type-name read
- remote control
- password unlock/lock
- memory and extend-unit access
- TCP or UDP depending on endpoint configuration

3E example:

```csharp
var options = new MitsubishiClientOptions(
    Host: "192.168.0.30",
    Port: 5000,
    FrameType: MitsubishiFrameType.ThreeE,
    DataCode: CommunicationDataCode.Binary,
    TransportKind: MitsubishiTransportKind.Tcp,
    Route: MitsubishiRoute.Default,
    MonitoringTimer: 0x0010);
```

4E example:

```csharp
var options = new MitsubishiClientOptions(
    Host: "192.168.0.31",
    Port: 5000,
    FrameType: MitsubishiFrameType.FourE,
    DataCode: CommunicationDataCode.Binary,
    TransportKind: MitsubishiTransportKind.Tcp,
    Route: MitsubishiRoute.Default,
    MonitoringTimer: 0x0010,
    SerialNumberProvider: () => (ushort)Environment.TickCount);
```

### ASCII endpoint example

```csharp
var asciiOptions = new MitsubishiClientOptions(
    Host: "192.168.0.40",
    Port: 5000,
    FrameType: MitsubishiFrameType.ThreeE,
    DataCode: CommunicationDataCode.Ascii,
    TransportKind: MitsubishiTransportKind.Tcp,
    Route: MitsubishiRoute.Default,
    MonitoringTimer: 0x0010);
```

### UDP endpoint example

```csharp
var udpOptions = new MitsubishiClientOptions(
    Host: "192.168.0.50",
    Port: 5000,
    FrameType: MitsubishiFrameType.ThreeE,
    DataCode: CommunicationDataCode.Binary,
    TransportKind: MitsubishiTransportKind.Udp,
    Route: MitsubishiRoute.Default,
    MonitoringTimer: 0x0010);
```

### Serial endpoint example

```csharp
using System.IO.Ports;

var serialEndpoint = new MitsubishiClientOptions(
    Host: "COM3",
    Port: 0,
    FrameType: MitsubishiFrameType.OneC,
    DataCode: CommunicationDataCode.Ascii,
    TransportKind: MitsubishiTransportKind.Serial,
    Timeout: TimeSpan.FromSeconds(2),
    CpuType: CpuType.Fx3,
    Serial: new MitsubishiSerialOptions(
        PortName: "COM3",
        BaudRate: 9600,
        DataBits: 7,
        Parity: Parity.Even,
        StopBits: StopBits.One,
        Handshake: Handshake.None,
        MessageFormat: MitsubishiSerialMessageFormat.Format1,
        StationNumber: 0x00,
        PcNumber: 0xFF,
        MessageWait: 0x0));
```

---

## Feature-to-API quick map

| Feature | API |
|---|---|
| Open transport | `OpenAsync()` / `Open()` |
| Close transport | `CloseAsync()` / `Close()` |
| Batch word read | `ReadWordsAsync(address, points)` |
| Batch word write | `WriteWordsAsync(address, values)` |
| Batch bit read | `ReadBitsAsync(address, points)` |
| Batch bit write | `WriteBitsAsync(address, values)` |
| Random word read | `RandomReadWordsAsync(addresses)` |
| Random word write | `RandomWriteWordsAsync(values)` |
| Register monitor devices | `RegisterMonitorAsync(addresses)` |
| Execute monitor | `ExecuteMonitorAsync()` |
| Block read | `ReadBlocksAsync(request)` |
| Block write | `WriteBlocksAsync(request)` |
| Read PLC type | `ReadTypeNameAsync()` |
| Remote RUN | `RemoteRunAsync(force, clearMode)` |
| Remote STOP | `RemoteStopAsync()` |
| Remote PAUSE | `RemotePauseAsync()` |
| Remote LATCH CLEAR | `RemoteLatchClearAsync()` |
| Remote RESET | `RemoteResetAsync()` |
| Unlock | `UnlockAsync(password)` |
| Lock | `LockAsync(password)` |
| Clear error | `ClearErrorAsync()` |
| Loopback | `LoopbackAsync(data)` |
| Memory read | `ReadMemoryAsync(command, address, length)` |
| Memory write | `WriteMemoryAsync(command, address, values)` |
| Raw command execution | `ExecuteRawAsync(request)` |
| Observe words | `ObserveWords(...)` |
| Observe bits | `ObserveBits(...)` |
| Observe heartbeat | `ObserveWordsHeartbeat(...)` |
| Observe staleness | `ObserveWordsStale(...)` |
| Triggered latest read | `ObserveWordsLatest(...)` |
| Observe tag group | `ObserveTagGroup(...)` |
| Observe tag group heartbeat | `ObserveTagGroupHeartbeat(...)` |
| Observe tag group staleness | `ObserveTagGroupStale(...)` |
| Triggered latest tag-group read | `ObserveTagGroupLatest(...)` |
| Operation logs | `OperationLogs` |
| Connection states | `ConnectionStates` |
| Connection stale detection | `ObserveConnectionHealth(...)` |
| Symbolic word read | `ReadWordsByTagAsync(tagName, points)` |
| Symbolic bit read | `ReadBitsByTagAsync(tagName, points)` |
| Symbolic word write | `WriteWordsByTagAsync(tagName, values)` |
| Symbolic bit write | `WriteBitsByTagAsync(tagName, values)` |
| Symbolic random word read | `RandomReadWordsByTagAsync(tagNames)` |
| Symbolic random word write | `RandomWriteWordsByTagAsync(values)` |
| Symbolic Int16 read | `ReadInt16ByTagAsync(tagName)` |
| Symbolic Int16 write | `WriteInt16ByTagAsync(tagName, value)` |
| Symbolic UInt16 read | `ReadUInt16ByTagAsync(tagName)` |
| Symbolic UInt16 write | `WriteUInt16ByTagAsync(tagName, value)` |
| Symbolic Int32 read | `ReadInt32ByTagAsync(tagName)` |
| Symbolic Int32 write | `WriteInt32ByTagAsync(tagName, value)` |
| Symbolic DWord read | `ReadDWordByTagAsync(tagName)` |
| Symbolic DWord write | `WriteDWordByTagAsync(tagName, value)` |
| Symbolic float read | `ReadFloatByTagAsync(tagName)` |
| Symbolic float write | `WriteFloatByTagAsync(tagName, value)` |
| Symbolic scaled read | `ReadScaledDoubleByTagAsync(tagName)` |
| Symbolic scaled write | `WriteScaledDoubleByTagAsync(tagName, value)` |
| Symbolic string read | `ReadStringByTagAsync(tagName, wordLength)` / `ReadStringByTagAsync(tagName)` |
| Symbolic string write | `WriteStringByTagAsync(tagName, value, wordLength)` / `WriteStringByTagAsync(tagName, value)` |
| Tag group definition | `MitsubishiTagGroupDefinition(name, tagNames)` |
| Tag group registration | `TagDatabase.AddGroup(group)` |
| Tag database validation | `ValidateTagDatabase()` |
| Tag database load + validate | `LoadAndValidateTagDatabase(path)` / `LoadAndValidateTagDatabase(path, policy)` |
| Tag database diff preview | `PreviewTagDatabaseDiff(path)` / `PreviewTagDatabaseDiff(path, policy)` |
| Tag database reload stream | `ObserveTagDatabaseReload(path, pollInterval)` / `ObserveTagDatabaseReload(path, pollInterval, emitInitial, policy)` |
| Tag database diff stream | `ObserveTagDatabaseDiff(path, pollInterval)` / `ObserveTagDatabaseDiff(path, pollInterval, emitInitial, policy)` |
| Tag group snapshot read | `ReadTagGroupSnapshotAsync(groupName)` |
| Snapshot typed accessor | `snapshot.GetRequired<T>(tagName)` |
| Tag group write validation | `ValidateTagGroupWrite(groupName, values)` |
| Partial tag-group write | `WriteTagGroupValuesAsync(groupName, values)` |
| Full tag-group snapshot write | `WriteTagGroupSnapshotAsync(snapshot)` |
| Tag database assignment | `client.TagDatabase = ...` |
| CSV tag import | `MitsubishiTagDatabase.FromCsv(csvContent)` |
| Schema file load | `MitsubishiTagDatabase.Load(path)` |
| Schema file save | `TagDatabase.Save(path)` |
| JSON schema import | `MitsubishiTagDatabase.FromJson(json)` |
| JSON schema export | `TagDatabase.ToJson()` |
| YAML schema import | `MitsubishiTagDatabase.FromYaml(yaml)` |
| YAML schema export | `TagDatabase.ToYaml()` |

### Serial coverage note

The quick map lists the full public API surface. Current serial support is narrower than Ethernet support. At this stage, the verified serial operation is **batch word read** via `ReadWordsAsync(address, points)` using `1C`, `3C`, or `4C` plus `MitsubishiTransportKind.Serial`.

---

## Troubleshooting notes

### Tag name not found

If tag-based reads fail, verify:
- `client.TagDatabase` has been assigned
- `Name` matches the CSV/configured value
- the tag’s `Address` is a valid Mitsubishi address string

### Wrong X / Y values

If `X` or `Y` values do not match the PLC documentation:
- switch `XyNotation` between `Octal` and `Hexadecimal`
- confirm the expected addressing rule for the installed Ethernet module/path

### ASCII vs binary mismatch

If communication succeeds on one endpoint but not another:
- verify whether the PLC/module is configured for `Binary` or `Ascii`
- verify frame family (`OneE`, `ThreeE`, `FourE`, `OneC`, `ThreeC`, `FourC`)
- verify TCP vs UDP vs serial configuration on the PLC/module side
- for serial, also verify `MitsubishiSerialMessageFormat` (`Format1`, `Format4`, `Format5`) and serial-port settings such as baud rate, parity, stop bits, and handshake

### Serial support limitations

Current serial implementation is intentionally narrower than Ethernet support. If a serial operation fails, first confirm that the current serial implementation actually covers that scenario.

Currently implemented and verified for serial:
- reactive SerialPortRx-based transport
- serial batch word reads through `ReadWordsAsync(address, points)`
- `1C`, `3C`, and `4C` frame selection
- serial ASCII format 1/4 and 4C binary format 5 decode paths

Not yet implemented for serial:
- serial writes
- random operations
- block operations
- monitor registration/execution
- remote control commands
- memory / extend-unit commands
- raw serial command execution

### Remote operations do not execute

Remote operations are target-dependent. Check:
- CPU mode and permissions
- Ethernet module settings
- remote password/lock state
- target family support and documented operational constraints

---

## Verification

Validated with:

```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" build src/MitsubishiRx.sln -v minimal
"/mnt/c/Program Files/dotnet/dotnet.exe" test --project tests/MitsubishiRx.Tests/MitsubishiRx.Tests.csproj -v minimal
```

---

## License

MIT
