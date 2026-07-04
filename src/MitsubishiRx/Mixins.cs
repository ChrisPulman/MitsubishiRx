// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net.Sockets;

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Provides the Mixins type.</summary>
internal static class Mixins
{
    /// <summary>Extends sockets with cleanup helpers.</summary>
    /// <param name="socket">The socket being extended.</param>
    extension(Socket? socket)
    {
        /// <summary>Executes the SafeClose operation.</summary>
        public void SafeClose()
        {
            try
            {
                if (socket?.Connected ?? false)
                {
                    socket.Shutdown(SocketShutdown.Both);
                }
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }

            try
            {
                socket?.Close();
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }
}
