using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Artemis.Plugins.Devices.Nanoleaf.RGB.NET.Enum;

namespace Artemis.Plugins.Devices.Nanoleaf.RGB.NET.API
{
    /// <summary>
    /// Partial implementation of the Nanoleaf-JSON-API
    /// </summary>
    public static class NanoleafAPI
    {
        /// <summary>
        /// Creates a <see cref="StringContent"/> with pre-serialized JSON so that Content-Length
        /// is always set.  <see cref="JsonContent"/> serializes lazily and reports an unknown
        /// length, which causes <see cref="HttpClient"/> to use Transfer-Encoding: chunked.
        /// The Matter WiFi Essentials devices cannot parse chunked requests.
        /// </summary>
        private static StringContent JsonBody(object value) =>
            new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");
        /// <summary>
        /// Known model numbers for Nanoleaf Matter WiFi Essentials devices.
        /// </summary>
        private static readonly HashSet<string> MatterEssentialsModels = new(StringComparer.OrdinalIgnoreCase)
        {
            "NL71K1", // Essentials Holiday String Lights (2023)
            "NL71K2", // Essentials Holiday String Lights (2024)
            "NL72K1", // Essentials Indoor HD Lightstrip
            "NL72K3", // Essentials Indoor Lightstrip
            "NL72K4", // Floor Lamp
            "NL72K6", // Rope Lights
            "NL73K1", // Essentials Outdoor String Lights
            "NL73K3", // Essentials Permanent Outdoor Lights
            "NL75K1", // Essentials WiFi A19
        };

        /// <summary>
        /// Determines whether the specified model number corresponds to a Matter WiFi Essentials device.
        /// </summary>
        /// <param name="model">The model number to check.</param>
        /// <returns><c>true</c> if the model is a Matter WiFi Essentials device; otherwise, <c>false</c>.</returns>
        public static bool IsMatterEssentialsDevice(string? model)
        {
            return !string.IsNullOrEmpty(model) && MatterEssentialsModels.Contains(model);
        }
        /// <summary>
        /// Gets the data returned by the 'info' endpoint of the Nanoleaf-device.
        /// </summary>
        /// <param name="address">The address of the device to request data from.</param>
        /// <param name="authToken">The authentication token of the device to request data from.</param>
        /// <returns>The data returned by the Nanoleaf-device.</returns>
        public static NanoleafInfo? Info(string address, string authToken)
        {
            if (string.IsNullOrEmpty(address)) return null;

            using HttpClient client = new();
            try
            {
                var uri = new UriBuilder("http", address, 16021, $"/api/v1/{authToken}/").Uri;
                return client.Send(new HttpRequestMessage(HttpMethod.Get, uri))
                    .Content
                    .ReadFromJsonAsync<NanoleafInfo>()
                    .Result;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Authenticates with the Nanoleaf device and retrieves an authentication token.
        /// </summary>
        /// <param name="address">The address of the device to authenticate with.</param>
        /// <returns>The authentication token, or null if authentication fails.</returns>
        public static string? Authenticate(string address)
        {
            if (string.IsNullOrEmpty(address)) return null;

            using HttpClient client = new();
            try
            {
                var uri = new UriBuilder("http", address, 16021, "/api/v1/new").Uri;
                return client.Send(new HttpRequestMessage(HttpMethod.Post, uri))
                        .Content
                        .ReadFromJsonAsync<NanoleafAuthenticationResponse>()
                        .Result?
                        .AuthToken
                    ?? string.Empty;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Sets the brightness of the Nanoleaf device.
        /// </summary>
        /// <param name="address">The address of the device to set the brightness for.</param>
        /// <param name="authToken">The authentication token of the device.</param>
        /// <param name="brightness">The brightness value to set (0-100).</param>
        public static void SetBrightness(string address, string authToken, byte brightness)
        {
            if (string.IsNullOrEmpty(address) || string.IsNullOrEmpty(authToken) || brightness > 100) return;

            using HttpClient client = new();
            try
            {
                var uri = new UriBuilder("http", address, 16021, $"/api/v1/{authToken}/state").Uri;
                var request = new HttpRequestMessage(HttpMethod.Put, uri)
                {
                    Content = JsonBody(new { brightness = new { value = brightness } })
                };
                client.Send(request);
            }
            catch
            {
                // ignored
            }
        }
        
        /// <summary>
        /// Sets the on/off state of the Nanoleaf device.
        /// </summary>
        /// <param name="address">The address of the device to set the state for.</param>
        /// <param name="authToken">The authentication token of the device.</param>
        /// <param name="on">True to turn the device on, false to turn it off.</param>
        public static void SetOnOff(string address, string authToken, bool on)
        {
            if (string.IsNullOrEmpty(address) || string.IsNullOrEmpty(authToken)) return;

            using HttpClient client = new();
            try
            {
                var uri = new UriBuilder("http", address, 16021, $"/api/v1/{authToken}/state").Uri;
                var request = new HttpRequestMessage(HttpMethod.Put, uri)
                {
                    Content = JsonBody(new { on = new { value = on } })
                };
                client.Send(request);
            }
            catch
            {
                // ignored
            }
        }
        
        /// <summary>
        /// Sets the effect of the Nanoleaf device.
        /// </summary>
        /// <param name="address">The address of the device to set the effect for.</param>
        /// <param name="authToken">The authentication token of the device.</param>
        /// <param name="effectName">The name of the effect to set.</param>
        public static void SetEffect(string address, string authToken, string effectName)
        {
            if (string.IsNullOrEmpty(address) || string.IsNullOrEmpty(authToken) || string.IsNullOrEmpty(effectName)) return;

            using HttpClient client = new();
            try
            {
                var uri = new UriBuilder("http", address, 16021, $"/api/v1/{authToken}/effects").Uri;
                var request = new HttpRequestMessage(HttpMethod.Put, uri)
                {
                    Content = JsonBody(new { select = effectName })
                };
                client.Send(request);
            }
            catch
            {
                // ignored
            }
        }

        /// <summary>
        /// Sets the state of the Nanoleaf device.
        /// </summary>
        /// <param name="address">The address of the device to set the state for.</param>
        /// <param name="authToken">The authentication token of the device.</param>
        /// <param name="stateInfo">The state information to set.</param>
        public static void SetState(string address, string authToken, NanoleafInfo.StateInfo? stateInfo)
        {
            if (string.IsNullOrEmpty(address) || string.IsNullOrEmpty(authToken) || stateInfo == null) return;
            using HttpClient client = new();
            try
            {
                var uri = new UriBuilder("http", address, 16021, $"/api/v1/{authToken}/state").Uri;
                var request = new HttpRequestMessage(HttpMethod.Put, uri)
                {
                    Content = JsonBody(new
                    {
                        brightness = new { value = stateInfo.Brightness.Value },
                        ct = new { value = stateInfo.Ct.Value },
                        hue = new { value = stateInfo.Hue.Value },
                        sat = new { value = stateInfo.Sat.Value },
                        on = new { value = stateInfo.On.Value }
                    })
                };
                client.Send(request);
            }
            catch
            {
                // ignored
            }
        }
        
        /// <summary>
        /// Starts external control on the Nanoleaf device.
        /// </summary>
        /// <param name="address">The address of the device to control.</param>
        /// <param name="authToken">The authentication token of the device.</param>
        /// <param name="version">The version of the external control protocol.</param>
        /// <returns>A tuple containing the address and port for external control.</returns>
        public static (string address, ushort port) StartExternalControl(string address, string authToken,
            ExtControlVersion? version)
        {
            if (string.IsNullOrEmpty(address)) return (string.Empty, 0);

            using HttpClient client = new();
            try
            {
                var uri = new UriBuilder("http", address, 16021, $"/api/v1/{authToken}/effects").Uri;
                var request = new HttpRequestMessage(HttpMethod.Put, uri)
                {
                    Content = JsonBody(new
                    {
                        write = new
                        {
                            command = "display", animType = "extControl",
                            extControlVersion = System.Enum.GetName(version ?? ExtControlVersion.v1)
                        }
                    })
                };

                string responseContent = client.Send(request).Content.ReadAsStringAsync().Result;
                var response = string.IsNullOrWhiteSpace(responseContent)
                    ? null
                    : JsonSerializer.Deserialize<NanoleafExternalControlResponse>(responseContent);
                return (response?.Address ?? address, response?.Port ?? 60222);
            }
            catch
            {
                return (string.Empty, 0);
            }
        }

        /// <summary>
        /// Represents the response from the Nanoleaf device for external control.
        /// </summary>
        private class NanoleafExternalControlResponse
        {
            /// <summary>
            /// Gets or sets the address for external control.
            /// </summary>
            [JsonPropertyName("streamControlIpAddr")]
            public string? Address { get; init; }

            /// <summary>
            /// Gets or sets the port for external control.
            /// </summary>
            [JsonPropertyName("streamControlPort")]
            public ushort? Port { get; init; }

            /// <summary>
            /// Gets or sets the protocol for external control.
            /// </summary>
            [JsonPropertyName("streamControlProtocol")]
            public string? Protocol { get; init; }
        }

        /// <summary>
        /// Represents the response from the Nanoleaf device for authentication.
        /// </summary>
        private class NanoleafAuthenticationResponse
        {
            /// <summary>
            /// Gets or sets the authentication token.
            /// </summary>
            [JsonPropertyName("auth_token")]
            // ReSharper disable once UnusedAutoPropertyAccessor.Local
            public string? AuthToken { get; init; }
        }

        /// <summary>
        /// Gets the number of LEDs on a Matter WiFi Essentials device.
        /// </summary>
        /// <param name="address">The address of the device to query.</param>
        /// <param name="authToken">The authentication token of the device.</param>
        /// <returns>The number of LEDs, or 0 if the request fails.</returns>
        public static int GetLedCount(string address, string authToken)
        {
            if (string.IsNullOrEmpty(address) || string.IsNullOrEmpty(authToken)) return 0;

            using HttpClient client = new();
            try
            {
                var uri = new UriBuilder("http", address, 16021, $"/api/v1/{authToken}/length").Uri;
                var response = client.Send(new HttpRequestMessage(HttpMethod.Get, uri))
                    .Content
                    .ReadFromJsonAsync<LedCountResponse>()
                    .Result;
                return response?.NumLEDs ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Represents the response from the length endpoint.
        /// </summary>
        private class LedCountResponse
        {
            [JsonPropertyName("numLEDs")]
            public int NumLEDs { get; init; }
        }
    }
}