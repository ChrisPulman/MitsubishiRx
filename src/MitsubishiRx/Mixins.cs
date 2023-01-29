// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net.Sockets;

namespace MitsubishiRx
{
    internal static class Mixins
    {
        public static void SafeClose(this Socket socket)
        {
            try
            {
                if (socket?.Connected ?? false)
                {
                    socket?.Shutdown(SocketShutdown.Both);
                }
            }
            catch
            {
            }

            try
            {
                socket?.Close();
            }
            catch
            {
            }
        }
    }
}
