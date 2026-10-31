using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Koekatamarin
{
    public static class LanAddress
    {
        // Literal dotted IPv4 only. No DNS, URL, abbreviated, octal or hexadecimal forms.
        public static bool TryParse(string text, out IPAddress address)
        {
            address = null;
            if (string.IsNullOrEmpty(text) || text.Length > 15) return false;
            var parts = text.Split('.');
            if (parts.Length != 4) return false;
            var bytes = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                if (parts[i].Length == 0 || parts[i].Length > 3 || (parts[i].Length > 1 && parts[i][0] == '0')) return false;
                int value = 0;
                foreach (char c in parts[i]) { if (c < '0' || c > '9') return false; value = value * 10 + c - '0'; }
                if (value > 255) return false;
                bytes[i] = (byte)value;
            }
            if (bytes[0] == 0 || bytes[0] >= 224) return false;
            address = new IPAddress(bytes);
            return true;
        }

        public static string LocalAddresses()
        {
            var addresses = new List<string>();
            try
            {
                foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (adapter.OperationalStatus != OperationalStatus.Up) continue;
                    foreach (var entry in adapter.GetIPProperties().UnicastAddresses)
                    {
                        var address = entry.Address;
                        if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address)) continue;
                        string value = address.ToString();
                        if (!addresses.Contains(value)) addresses.Add(value);
                        if (addresses.Count == 3) return string.Join(" / ", addresses);
                    }
                }
            }
            catch (Exception) { /* Some Android runtimes cannot enumerate interfaces; use Quest Wi-Fi details. */ }
            return addresses.Count > 0 ? string.Join(" / ", addresses) : "Quest設定のWi-Fi詳細で確認";
        }
    }
}
