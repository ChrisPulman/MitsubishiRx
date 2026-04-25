# MitsubishiRx Reactive Protocol Overhaul Implementation Plan

> **For Hermes:** Execute in-place with strict TDD slices and verify with Windows `dotnet`.

**Goal:** Replace the current socket wrapper with a protocol-aware Mitsubishi Ethernet client that meaningfully adopts `ReactiveUI.Extensions` and supports broad MC/SLMP Ethernet communication across legacy and modern Mitsubishi PLC families.

**Architecture:** Split the library into protocol models/codecs, transport abstractions, synchronous/async request execution, and reactive polling/health streams. Support 1E/3E/4E MC/SLMP framing, TCP/UDP, ASCII/binary encodings, routing metadata, core device read/write operations, monitor operations, remote control commands, and diagnostics.

**Tech Stack:** .NET 8/9/10, System.Reactive, ReactiveUI.Extensions, xUnit, Microsoft.Reactive.Testing.

---

## Task slices

1. Create protocol/transport abstraction tests and project structure.
2. Implement core enums, request/response models, frame settings, and device addressing.
3. Implement 1E / 3E / 4E codecs for binary+ASCII requests and replies.
4. Implement TCP/UDP transport abstractions and request executor.
5. Implement command APIs for batch/random/block read/write, monitor, remote ops, metadata, diagnostics, password lock/unlock, and clear error.
6. Implement reactive polling/health/diagnostic APIs using `ReactiveUI.Extensions` operators.
7. Add tests for encoding/decoding, command semantics, and reactive flows.
8. Update package metadata and README to match actual supported behavior.
