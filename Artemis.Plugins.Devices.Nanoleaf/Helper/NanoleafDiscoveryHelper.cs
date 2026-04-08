using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Zeroconf;

namespace Artemis.Plugins.Devices.Nanoleaf.Helper
{
    /// <summary>
    /// Helper class for discovering Nanoleaf devices on the network using SSDP and mDNS.
    /// </summary>
    public class NanoleafDiscoveryHelper
    {
        /// <summary>
        /// Discovers panel-based Nanoleaf devices on the network using SSDP.
        /// </summary>
        /// <param name="waitFor">The time to wait for responses in milliseconds.</param>
        /// <returns>A list of tuples containing the address and model of the discovered devices.</returns>
        public static List<(string address, string model)> DiscoverDevices(int waitFor = 5000)
        {
            var multicastEndpoint = new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900);
            var localEndpoint = new IPEndPoint(IPAddress.Any, 0);

            List<(string address, string model)> devices = [];

            var udpClient = new UdpClient();

            string buffer =
                "M-SEARCH * HTTP/1.1\r\nHost: 239.255.255.250:1900\r\nST: nanoleaf_aurora:light\r\nMan: \"ssdp:all\"\r\nMX: 3\r\n\r\n";
            byte[] data = System.Text.Encoding.UTF8.GetBytes(buffer);

            udpClient.Send(data, data.Length, multicastEndpoint);
            udpClient.Client.ReceiveTimeout = waitFor;

            while (true)
            {
                try
                {
                    byte[] result = udpClient.Receive(ref localEndpoint);
                    string response = System.Text.Encoding.UTF8.GetString(result);
                    var ssdpResponse = ParseSsdpResponse(response);

                    if (!ssdpResponse.Headers.TryGetValue("ST", out string? st) || !st.Contains("nanoleaf") ||
                        !ssdpResponse.Headers.TryGetValue("Location", out string? location)) continue;
                    string address = new Uri(location).Host;
                    devices.Add((address, st.Split(':')[1].ToUpper()));
                }
                catch (SocketException)
                {
                    break;
                }
            }

            return devices;
        }

        /// <summary>
        /// Discovers Nanoleaf Matter WiFi Essentials devices on the network using mDNS.
        /// </summary>
        /// <param name="scanTimeSeconds">How long to scan for devices in seconds.</param>
        /// <returns>A list of tuples containing the address and model of the discovered devices.</returns>
        public static List<(string address, string model)> DiscoverMatterDevices(int scanTimeSeconds = 5)
        {
            try
            {
                return Task.Run(async () =>
                {
                    var results = await ZeroconfResolver.ResolveAsync("_nanoleafapi._tcp.local.",
                        scanTime: TimeSpan.FromSeconds(scanTimeSeconds));

                    return results
                        .Select(host => (address: host.IPAddress, model: ExtractModelFromMdns(host)))
                        .Where(d => !string.IsNullOrEmpty(d.address))
                        .ToList();
                }).GetAwaiter().GetResult();
            }
            catch
            {
                return [];
            }
        }

        /// <summary>
        /// Discovers all Nanoleaf devices (both panel-based and Matter WiFi Essentials).
        /// </summary>
        /// <param name="waitFor">The time to wait for SSDP responses in milliseconds.</param>
        /// <param name="mdnsScanTimeSeconds">How long to scan for mDNS devices in seconds.</param>
        /// <returns>A combined list of tuples containing the address and model of the discovered devices.</returns>
        public static List<(string address, string model)> DiscoverAllDevices(int waitFor = 5000,
            int mdnsScanTimeSeconds = 5)
        {
            var devices = new List<(string address, string model)>();
            var seenAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // SSDP discovery for panel-based devices
            foreach (var device in DiscoverDevices(waitFor))
            {
                if (seenAddresses.Add(device.address))
                    devices.Add(device);
            }

            // mDNS discovery for Matter WiFi Essentials devices
            foreach (var device in DiscoverMatterDevices(mdnsScanTimeSeconds))
            {
                if (seenAddresses.Add(device.address))
                    devices.Add(device);
            }

            return devices;
        }

        /// <summary>
        /// Asynchronously discovers panel-based Nanoleaf devices using SSDP, invoking a callback for each device found.
        /// </summary>
        /// <param name="onDeviceFound">Callback invoked on the calling thread for each device as it is discovered.</param>
        /// <param name="cancellationToken">Token to cancel the discovery early.</param>
        /// <param name="waitFor">The time to wait for SSDP responses in milliseconds.</param>
        public static async Task DiscoverDevicesAsync(Action<(string address, string model)> onDeviceFound,
            CancellationToken cancellationToken = default, int waitFor = 5000)
        {
            var multicastEndpoint = new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900);

            using var timeoutCts = new CancellationTokenSource(waitFor);
            using var linkedCts =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            using var udpClient = new UdpClient();
            string buffer =
                "M-SEARCH * HTTP/1.1\r\nHost: 239.255.255.250:1900\r\nST: nanoleaf_aurora:light\r\nMan: \"ssdp:all\"\r\nMX: 3\r\n\r\n";
            byte[] data = System.Text.Encoding.UTF8.GetBytes(buffer);
            await udpClient.SendAsync(data.AsMemory(), multicastEndpoint, linkedCts.Token);

            while (!linkedCts.Token.IsCancellationRequested)
            {
                try
                {
                    var result = await udpClient.ReceiveAsync(linkedCts.Token);
                    string response = System.Text.Encoding.UTF8.GetString(result.Buffer);
                    var ssdpResponse = ParseSsdpResponse(response);

                    if (!ssdpResponse.Headers.TryGetValue("ST", out string? st) || !st.Contains("nanoleaf") ||
                        !ssdpResponse.Headers.TryGetValue("Location", out string? location)) continue;

                    string address = new Uri(location).Host;
                    onDeviceFound((address, st.Split(':')[1].ToUpper()));
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (SocketException)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Asynchronously discovers Nanoleaf Matter WiFi Essentials devices using mDNS, invoking a callback for each device found.
        /// </summary>
        /// <param name="onDeviceFound">Callback invoked for each device as it is discovered.</param>
        /// <param name="cancellationToken">Token to cancel the discovery early.</param>
        /// <param name="scanTimeSeconds">How long to scan for devices in seconds.</param>
        public static async Task DiscoverMatterDevicesAsync(Action<(string address, string model)> onDeviceFound,
            CancellationToken cancellationToken = default, int scanTimeSeconds = 5)
        {
            try
            {
                var seenAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                await ZeroconfResolver.ResolveAsync(
                    "_nanoleafapi._tcp.local.",
                    scanTime: TimeSpan.FromSeconds(scanTimeSeconds),
                    cancellationToken: cancellationToken,
                    callback: host =>
                    {
                        string address = host.IPAddress;
                        if (!string.IsNullOrEmpty(address) && seenAddresses.Add(address))
                            onDeviceFound((address, ExtractModelFromMdns(host)));
                    });
            }
            catch
            {
                // mDNS is optional — swallow errors silently
            }
        }

        /// <summary>
        /// Asynchronously discovers all Nanoleaf devices, invoking a callback as each device is found.
        /// SSDP and mDNS discovery run concurrently.
        /// </summary>
        /// <param name="onDeviceFound">Callback invoked for each unique device as it is discovered.</param>
        /// <param name="cancellationToken">Token to cancel the discovery early.</param>
        public static async Task DiscoverAllDevicesAsync(Action<(string address, string model)> onDeviceFound,
            CancellationToken cancellationToken = default)
        {
            var seenAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            object seenLock = new();

            void DeduplicatingCallback((string address, string model) device)
            {
                bool isNew;
                lock (seenLock)
                    isNew = seenAddresses.Add(device.address);

                if (isNew)
                    onDeviceFound(device);
            }

            await Task.WhenAll(
                DiscoverDevicesAsync(DeduplicatingCallback, cancellationToken),
                DiscoverMatterDevicesAsync(DeduplicatingCallback, cancellationToken)
            );
        }

        /// <summary>
        /// Extracts the model identifier from an mDNS host response.
        /// Falls back to "MATTER_ESSENTIALS" if not found.
        /// </summary>
        private static string ExtractModelFromMdns(IZeroconfHost host)
        {
            // The mDNS service instance name often contains the device name
            // Try to extract a model from TXT records if available
            foreach (var service in host.Services.Values)
            {
                foreach (var property in service.Properties)
                {
                    if (property.TryGetValue("md", out var model) && !string.IsNullOrEmpty(model))
                        return model;
                }
            }

            return "MATTER_ESSENTIALS";
        }

        /// <summary>
        /// Represents an SSDP response with headers.
        /// </summary>
        private class SsdpResponse
        {
            /// <summary>
            /// Gets or sets the headers of the SSDP response.
            /// </summary>
            public Dictionary<string, string> Headers { get; set; } = new();
        }

        /// <summary>
        /// Parses a raw SSDP response string into an <see cref="SsdpResponse"/> object.
        /// </summary>
        /// <param name="rawResponse">The raw SSDP response string.</param>
        /// <returns>An <see cref="SsdpResponse"/> object containing the parsed headers.</returns>
        private static SsdpResponse ParseSsdpResponse(string rawResponse)
        {
            var result = new SsdpResponse();
            string[] lines = rawResponse.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);

            for (int i = 1; i < lines.Length; i++)
            {
                int sepIndex = lines[i].IndexOf(':');
                if (sepIndex > -1)
                {
                    string headerName = lines[i].Substring(0, sepIndex).Trim();
                    string headerValue = lines[i].Substring(sepIndex + 1).Trim();
                    result.Headers[headerName] = headerValue;
                }
            }

            return result;
        }
    }
}