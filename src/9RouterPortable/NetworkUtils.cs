using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace NineRouterPortable
{
    public static class NetworkUtils
    {
        public static List<string> GetLocalIpAddresses()
        {
            var list = new List<string> { "127.0.0.1" };
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                foreach (var ip in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == AddressFamily.InterNetwork && !ip.Address.ToString().StartsWith("127."))
                        list.Add(ip.Address.ToString());
                }
            }
            return list;
        }
    }
}
