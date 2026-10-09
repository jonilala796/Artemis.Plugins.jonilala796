using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Zeroconf;

namespace Artemis.Plugins.Devices.Nanoleaf.Helper
{
    /// <summary>
    /// Helper class for discovering Nanoleaf devices on the network using SSDP and mDNS.
    /// </summary>
    public static class NanoleafDiscoveryHelper
    {
        private static readonly IPEndPoint SsdpMulticastEndpoint = new(IPAddress.Parse("239.255.255.250"), 1900);

        /// <summary>
        /// SSDP search targets of the panel-based devices. Every family uses its own target and only answers to
        /// its own target or to ssdp:all.
        /// </summary>
        private static readonly string[] SsdpSearchTargets =
        [
            "nanoleaf_aurora:light", // Light Panels
            "nanoleaf:nl29", // Canvas
            "nanoleaf:nl42", // Shapes
            "nanoleaf:nl52", // Elements
            "nanoleaf:nl59", // Lines
            "ssdp:all" // Models without a known search target
        ];

        /// <summary>
        /// Asynchronously discovers panel-based Nanoleaf devices using SSDP, invoking a callback for each device found.
        /// The search is sent on every active IPv4 interface so devices behind a secondary network adapter are found too.
        /// </summary>
        /// <param name="onDeviceFound">Callback invoked for each device as it is discovered.</param>
        /// <param name="cancellationToken">Token to cancel the discovery early.</param>
        /// <param name="waitFor">The time to wait for SSDP responses in milliseconds.</param>
        public static async Task DiscoverDevicesAsync(Action<(string address, string model)> onDeviceFound,
            CancellationToken cancellationToken = default, int waitFor = 5000)
        {
            using var timeoutCts = new CancellationTokenSource(waitFor);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            List<UdpClient> clients = CreateSsdpClients();
            try
            {
                await Task.WhenAll(clients.Select(c => SearchAsync(c, onDeviceFound, linkedCts.Token)));
            }
            finally
            {
                foreach (UdpClient client in clients)
                    client.Dispose();
            }
        }

        private static async Task SearchAsync(UdpClient udpClient, Action<(string address, string model)> onDeviceFound,
            CancellationToken cancellationToken)
        {
            try
            {
                foreach (string searchTarget in SsdpSearchTargets)
                {
                    byte[] data = Encoding.UTF8.GetBytes(BuildSearchRequest(searchTarget));
                    await udpClient.SendAsync(data.AsMemory(), SsdpMulticastEndpoint, cancellationToken);
                }

                while (!cancellationToken.IsCancellationRequested)
                {
                    var result = await udpClient.ReceiveAsync(cancellationToken);
                    if (TryParseSsdpDevice(Encoding.UTF8.GetString(result.Buffer), out var device))
                        onDeviceFound(device);
                }
            }
            catch (OperationCanceledException)
            {
                // Scan time elapsed or discovery was cancelled
            }
            catch (SocketException)
            {
                // The interface went away or does not support multicast; other interfaces keep searching
            }
        }

        /// <summary>
        /// Creates one UDP client per active IPv4 interface, or a single unbound client if none can be determined.
        /// </summary>
        private static List<UdpClient> CreateSsdpClients()
        {
            List<UdpClient> clients = [];
            try
            {
                IEnumerable<IPAddress> localAddresses = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up
                                && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                                && n.SupportsMulticast)
                    .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                    .Select(a => a.Address)
                    .Where(a => a.AddressFamily == AddressFamily.InterNetwork);

                foreach (IPAddress localAddress in localAddresses)
                {
                    try
                    {
                        clients.Add(new UdpClient(new IPEndPoint(localAddress, 0)));
                    }
                    catch (SocketException)
                    {
                        // Skip addresses that cannot be bound
                    }
                }
            }
            catch (NetworkInformationException)
            {
                // Fall back to the default interface below
            }

            if (clients.Count == 0)
                clients.Add(new UdpClient());

            return clients;
        }

        internal static string BuildSearchRequest(string searchTarget) =>
            $"M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\nMAN: \"ssdp:discover\"\r\nMX: 3\r\nST: {searchTarget}\r\n\r\n";

        /// <summary>
        /// Parses an SSDP response and extracts the address and model of a Nanoleaf device.
        /// </summary>
        /// <returns><c>true</c> if the response came from a Nanoleaf device; otherwise, <c>false</c>.</returns>
        internal static bool TryParseSsdpDevice(string rawResponse, out (string address, string model) device)
        {
            device = default;
            Dictionary<string, string> headers = ParseSsdpHeaders(rawResponse);

            if (!headers.TryGetValue("ST", out string? st) && !headers.TryGetValue("NT", out st))
                return false;
            if (!st.Contains("nanoleaf", StringComparison.OrdinalIgnoreCase))
                return false;
            if (!headers.TryGetValue("Location", out string? location) ||
                !Uri.TryCreate(location, UriKind.Absolute, out Uri? uri) ||
                string.IsNullOrEmpty(uri.Host))
                return false;

            device = (uri.Host, GetModelFromSearchTarget(st));
            return true;
        }

        /// <summary>
        /// Gets the model number from an SSDP search target, e.g. <c>nanoleaf:nl42</c> becomes <c>NL42</c>.
        /// </summary>
        /// <returns>The model number, or an empty string if it is unknown.</returns>
        internal static string GetModelFromSearchTarget(string searchTarget)
        {
            if (searchTarget.Equals("nanoleaf_aurora:light", StringComparison.OrdinalIgnoreCase))
                return "NL22";

            int separator = searchTarget.IndexOf(':');
            if (separator < 0)
                return "";

            string model = searchTarget[(separator + 1)..].Trim();
            return model.StartsWith("nl", StringComparison.OrdinalIgnoreCase) ? model.ToUpperInvariant() : "";
        }

        /// <summary>
        /// Asynchronously discovers Nanoleaf devices using mDNS, invoking a callback for each device found.
        /// Matter WiFi Essentials devices can only be found this way, panel-based devices advertise the service too.
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
        /// Extracts the model identifier from the TXT records ("md") of an mDNS host response.
        /// </summary>
        /// <returns>The model number, or an empty string if it is unknown.</returns>
        private static string ExtractModelFromMdns(IZeroconfHost host)
        {
            foreach (var service in host.Services.Values)
            {
                foreach (var property in service.Properties)
                {
                    if (property.TryGetValue("md", out var model) && !string.IsNullOrEmpty(model))
                        return model;
                }
            }

            return "";
        }

        /// <summary>
        /// Parses the headers of a raw SSDP response. Header names are case-insensitive.
        /// </summary>
        private static Dictionary<string, string> ParseSsdpHeaders(string rawResponse)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string[] lines = rawResponse.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);

            // The first line is the status line
            for (int i = 1; i < lines.Length; i++)
            {
                int sepIndex = lines[i].IndexOf(':');
                if (sepIndex > 0)
                    headers[lines[i][..sepIndex].Trim()] = lines[i][(sepIndex + 1)..].Trim();
            }

            return headers;
        }
    }
}
