# MitsubishiRx

A Reactive Mitsubishi PLC Driver written in C#. This library provides an easy-to-use, reactive interface for communicating with Mitsubishi PLCs, enabling efficient and clean code for automation and control applications.

## Features

- Reactive programming for PLC communication
- High-performance and easy-to-integrate
- Supports Mitsubishi PLC communication protocols
- Simplifies interaction with PLC devices

## Installation

To install MitsubishiRx, add the library to your project using NuGet Package Manager:

```bash
Install-Package MitsubishiRx
```
Alternatively, add it via the .csproj file:
```
<PackageReference Include="MitsubishiRx" Version="1.0.0" />
```
## Getting Started
Here's an example of how to use MitsubishiRx to communicate with a Mitsubishi PLC:

## Basic Example
```C#
using System;
using MitsubishiRx;

class Program
{
    static void Main()
    {
        // Initialize the PLC driver
        var plc = new MitsubishiPlc("192.168.0.100", MitsubishiProtocolType.MCProtocol);
        
        // Connect to the PLC
        plc.Connect();

        // Read data from a device
        var data = plc.ReadDevice("D100");
        Console.WriteLine($"Value of D100: {data}");

        // Write data to a device
        plc.WriteDevice("D100", 123);
        Console.WriteLine("Value written to D100.");

        // Disconnect from the PLC
        plc.Disconnect();
    }
}
```

## Reactive Example

```C#
using System;
using System.Reactive.Linq;
using MitsubishiRx;

class Program
{
    static void Main()
    {
        // Initialize the PLC driver
        var plc = new MitsubishiPlc("192.168.0.100", MitsubishiProtocolType.MCProtocol);

        // Connect to the PLC
        plc.Connect();

        // Observe changes on a specific device
        var deviceObservable = plc.Observe("D100");

        // Subscribe to the observable to react to changes
        var subscription = deviceObservable.Subscribe(value =>
        {
            Console.WriteLine($"D100 changed to: {value}");
        });

        // Simulate a delay for demonstration purposes
        Console.WriteLine("Observing changes. Press any key to stop...");
        Console.ReadKey();

        // Dispose of the subscription and disconnect
        subscription.Dispose();
        plc.Disconnect();
    }
}
```

## Contributing
Contributions are welcome! Please fork the repository and submit a pull request for review. Be sure to adhere to the contribution guidelines.

## License
This project is licensed under the MIT License. See the LICENSE file for details.

## Support
If you encounter any issues or have questions, feel free to open an issue in the Issues section.

