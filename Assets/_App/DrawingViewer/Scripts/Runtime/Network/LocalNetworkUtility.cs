using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Unity.XR.XREAL.DrawingViewer
{
    /// <summary>
    /// Helpers for resolving the device's LAN address used by PC upload discovery.
    /// </summary>
    public static class LocalNetworkUtility
    {
        /// <summary>
        /// Returns the first usable IPv4 address on an active non-loopback interface.
        /// Prefers common private LAN ranges (192.168.x.x, 10.x.x.x, 172.16-31.x.x).
        /// </summary>
        public static string GetLocalIPv4()
        {
            string fallback = null;

            try
            {
                foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (networkInterface.OperationalStatus != OperationalStatus.Up)
                        continue;

                    if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;

                    foreach (var address in networkInterface.GetIPProperties().UnicastAddresses)
                    {
                        if (address.Address.AddressFamily != AddressFamily.InterNetwork)
                            continue;

                        if (IPAddress.IsLoopback(address.Address))
                            continue;

                        var ip = address.Address.ToString();
                        if (IsPrivateLanAddress(ip))
                            return ip;

                        fallback ??= ip;
                    }
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[LocalNetworkUtility] Failed to enumerate interfaces: {ex.Message}");
            }

            if (!string.IsNullOrEmpty(fallback))
                return fallback;

            try
            {
                var hostEntry = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var address in hostEntry.AddressList)
                {
                    if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address))
                        continue;

                    var ip = address.ToString();
                    if (IsPrivateLanAddress(ip))
                        return ip;

                    fallback ??= ip;
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[LocalNetworkUtility] DNS lookup failed: {ex.Message}");
            }

            return fallback;
        }

        private static bool IsPrivateLanAddress(string ip)
        {
            if (string.IsNullOrEmpty(ip))
                return false;

            if (ip.StartsWith("192.168."))
                return true;

            if (ip.StartsWith("10."))
                return true;

            if (!ip.StartsWith("172."))
                return false;

            var parts = ip.Split('.');
            if (parts.Length < 2 || !int.TryParse(parts[1], out var secondOctet))
                return false;

            return secondOctet >= 16 && secondOctet <= 31;
        }
    }
}
