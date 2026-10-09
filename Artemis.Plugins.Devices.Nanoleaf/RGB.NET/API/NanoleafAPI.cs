using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Artemis.Plugins.Devices.Nanoleaf.RGB.NET.Enum;
using Serilog;

namespace Artemis.Plugins.Devices.Nanoleaf.RGB.NET.API
{
    /// <summary>
    /// Partial implementation of the Nanoleaf-JSON-API
    /// </summary>
    public static class NanoleafAPI
    {
        /// <summary>
        /// The port the Nanoleaf Open API listens on.
        /// </summary>
        public const int API_PORT = 16021;

        /// <summary>
        /// The default UDP port used for external control streaming.
        /// </summary>
        public const ushort DEFAULT_STREAM_PORT = 60222;

        /// <summary>
        /// Timeout applied to regular API requests. Devices answer within milliseconds when healthy,
        /// so a short timeout keeps an unresponsive device from stalling Artemis.
        /// </summary>
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(3);

        /// <summary>
        /// A single shared client avoids socket exhaustion from creating one client per request.
        /// The timeout is applied per request so pairing can use a longer one.
        /// </summary>
        private static readonly HttpClient Client = new(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(1),
            ConnectTimeout = RequestTimeout
        })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        /// <summary>
        /// Logger used to report failed requests. Optional — logging is skipped when null.
        /// </summary>
        internal static ILogger? Logger { get; set; }

        /// <summary>
        /// Creates a <see cref="StringContent"/> with pre-serialized JSON so that Content-Length
        /// is always set.  <see cref="System.Net.Http.Json.JsonContent"/> serializes lazily and reports an unknown
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
            if (string.IsNullOrEmpty(address) || string.IsNullOrEmpty(authToken)) return null;
            return Get<NanoleafInfo>(address, authToken, "", "info");
        }

        /// <summary>
        /// Asynchronously gets the data returned by the 'info' endpoint of the Nanoleaf-device.
        /// </summary>
        /// <param name="address">The address of the device to request data from.</param>
        /// <param name="authToken">The authentication token of the device to request data from.</param>
        /// <returns>The data returned by the Nanoleaf-device, or null if the request fails.</returns>
        public static async Task<NanoleafInfo?> InfoAsync(string address, string authToken)
        {
            if (string.IsNullOrEmpty(address) || string.IsNullOrEmpty(authToken)) return null;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, BuildUri(address, authToken, ""));
                using var cts = new CancellationTokenSource(RequestTimeout);
                using HttpResponseMessage response = await Client.SendAsync(request, cts.Token);
                if (!CheckResponse(response, address, "info"))
                    return null;

                await using Stream stream = await response.Content.ReadAsStreamAsync(cts.Token);
                return await JsonSerializer.DeserializeAsync<NanoleafInfo>(stream, cancellationToken: cts.Token);
            }
            catch (Exception e)
            {
                LogFailure(e, address, "info");
                return null;
            }
        }

        /// <summary>
        /// Authenticates with the Nanoleaf device and retrieves an authentication token.
        /// The device must be in pairing mode, otherwise it responds with 403 Forbidden.
        /// </summary>
        /// <param name="address">The address of the device to authenticate with.</param>
        /// <returns>The authentication token, or null if authentication fails.</returns>
        public static async Task<string?> AuthenticateAsync(string address)
        {
            if (string.IsNullOrEmpty(address)) return null;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(address, null, "new"));
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                using HttpResponseMessage response = await Client.SendAsync(request, cts.Token);
                if (!CheckResponse(response, address, "authenticate"))
                    return null;

                await using Stream stream = await response.Content.ReadAsStreamAsync(cts.Token);
                var result = await JsonSerializer.DeserializeAsync<NanoleafAuthenticationResponse>(stream,
                    cancellationToken: cts.Token);
                return string.IsNullOrEmpty(result?.AuthToken) ? null : result.AuthToken;
            }
            catch (Exception e)
            {
                LogFailure(e, address, "authenticate");
                return null;
            }
        }

        /// <summary>
        /// Sets the brightness of the Nanoleaf device.
        /// </summary>
        /// <param name="address">The address of the device to set the brightness for.</param>
        /// <param name="authToken">The authentication token of the device.</param>
        /// <param name="brightness">The brightness value to set (0-100).</param>
        /// <returns><c>true</c> if the device accepted the request; otherwise, <c>false</c>.</returns>
        public static bool SetBrightness(string address, string authToken, byte brightness)
        {
            if (string.IsNullOrEmpty(address) || string.IsNullOrEmpty(authToken) || brightness > 100) return false;
            return Put(address, authToken, "state", new { brightness = new { value = brightness } }, "set brightness");
        }

        /// <summary>
        /// Sets the on/off state of the Nanoleaf device.
        /// </summary>
        /// <param name="address">The address of the device to set the state for.</param>
        /// <param name="authToken">The authentication token of the device.</param>
        /// <param name="on">True to turn the device on, false to turn it off.</param>
        /// <returns><c>true</c> if the device accepted the request; otherwise, <c>false</c>.</returns>
        public static bool SetOnOff(string address, string authToken, bool on)
        {
            if (string.IsNullOrEmpty(address) || string.IsNullOrEmpty(authToken)) return false;
            return Put(address, authToken, "state", new { on = new { value = on } }, "set on/off");
        }

        /// <summary>
        /// Sets the effect of the Nanoleaf device.
        /// </summary>
        /// <param name="address">The address of the device to set the effect for.</param>
        /// <param name="authToken">The authentication token of the device.</param>
        /// <param name="effectName">The name of the effect to set.</param>
        /// <returns><c>true</c> if the device accepted the request; otherwise, <c>false</c>.</returns>
        public static bool SetEffect(string address, string authToken, string effectName)
        {
            if (string.IsNullOrEmpty(address) || string.IsNullOrEmpty(authToken) || string.IsNullOrEmpty(effectName)) return false;
            return Put(address, authToken, "effects", new { select = effectName }, "set effect");
        }

        /// <summary>
        /// Sets the state of the Nanoleaf device.
        /// </summary>
        /// <param name="address">The address of the device to set the state for.</param>
        /// <param name="authToken">The authentication token of the device.</param>
        /// <param name="stateInfo">The state information to set.</param>
        /// <returns><c>true</c> if the device accepted the request; otherwise, <c>false</c>.</returns>
        public static bool SetState(string address, string authToken, NanoleafInfo.StateInfo? stateInfo)
        {
            if (string.IsNullOrEmpty(address) || string.IsNullOrEmpty(authToken) || stateInfo == null) return false;
            return Put(address, authToken, "state", BuildStateBody(stateInfo), "set state");
        }

        /// <summary>
        /// Builds the body for a state request. Only the color parameters relevant to the original
        /// color mode are included: sending both ct and hue/sat simultaneously can cause the device
        /// to pick hue/sat (often hue=0 = red) instead of restoring the intended color.
        /// </summary>
        internal static object BuildStateBody(NanoleafInfo.StateInfo stateInfo)
        {
            return string.Equals(stateInfo.ColorMode, "ct", StringComparison.OrdinalIgnoreCase)
                ? new
                {
                    brightness = new { value = stateInfo.Brightness.Value },
                    ct = new { value = stateInfo.Ct.Value },
                    on = new { value = stateInfo.On.Value }
                }
                : new
                {
                    brightness = new { value = stateInfo.Brightness.Value },
                    hue = new { value = stateInfo.Hue.Value },
                    sat = new { value = stateInfo.Sat.Value },
                    on = new { value = stateInfo.On.Value }
                };
        }

        /// <summary>
        /// Starts external control on the Nanoleaf device.
        /// </summary>
        /// <param name="address">The address of the device to control.</param>
        /// <param name="authToken">The authentication token of the device.</param>
        /// <param name="version">The version of the external control protocol.</param>
        /// <returns>
        /// The address and port to stream to, or <c>null</c> if the device rejected the request.
        /// Devices that answer without a body (v2, Matter) stream on their own address and the default port.
        /// </returns>
        public static (string address, ushort port)? StartExternalControl(string address, string authToken,
            ExtControlVersion version)
        {
            if (string.IsNullOrEmpty(address) || string.IsNullOrEmpty(authToken)) return null;

            var body = new
            {
                write = new
                {
                    command = "display", animType = "extControl",
                    extControlVersion = System.Enum.GetName(version)
                }
            };

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Put, BuildUri(address, authToken, "effects"))
                {
                    Content = JsonBody(body)
                };
                using HttpResponseMessage response = Send(request);
                if (!CheckResponse(response, address, "start external control"))
                    return null;

                string responseContent = ReadString(response);
                var result = string.IsNullOrWhiteSpace(responseContent)
                    ? null
                    : JsonSerializer.Deserialize<NanoleafExternalControlResponse>(responseContent);

                string streamAddress = string.IsNullOrEmpty(result?.Address) ? address : result.Address;
                ushort streamPort = result?.Port is > 0 ? result.Port.Value : DEFAULT_STREAM_PORT;
                return (streamAddress, streamPort);
            }
            catch (Exception e)
            {
                LogFailure(e, address, "start external control");
                return null;
            }
        }

        /// <summary>
        /// Gets the currently selected effect on the Nanoleaf device.
        /// Useful for Matter WiFi Essentials devices whose root info endpoint does not include an effects section.
        /// </summary>
        /// <param name="address">The address of the device.</param>
        /// <param name="authToken">The authentication token of the device.</param>
        /// <returns>The effect name, or null if unavailable.</returns>
        public static string? GetCurrentEffect(string address, string authToken)
        {
            if (string.IsNullOrEmpty(address) || string.IsNullOrEmpty(authToken)) return null;
            return Get<string>(address, authToken, "effects/select", "get current effect");
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
            return Get<LedCountResponse>(address, authToken, "length", "get LED count")?.NumLEDs ?? 0;
        }

        #region Request helpers

        /// <summary>
        /// Builds the URI of an API endpoint. The token is part of the path, so the resulting URI must never be logged.
        /// </summary>
        private static Uri BuildUri(string address, string? authToken, string path)
        {
            string fullPath = authToken == null ? $"/api/v1/{path}" : $"/api/v1/{authToken}/{path}";
            return new UriBuilder("http", address, API_PORT, fullPath).Uri;
        }

        private static HttpResponseMessage Send(HttpRequestMessage request)
        {
            using var cts = new CancellationTokenSource(RequestTimeout);
            return Client.Send(request, cts.Token);
        }

        private static string ReadString(HttpResponseMessage response)
        {
            using var reader = new StreamReader(response.Content.ReadAsStream());
            return reader.ReadToEnd();
        }

        private static T? Get<T>(string address, string authToken, string path, string operation)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, BuildUri(address, authToken, path));
                using HttpResponseMessage response = Send(request);
                if (!CheckResponse(response, address, operation))
                    return default;

                using Stream stream = response.Content.ReadAsStream();
                return JsonSerializer.Deserialize<T>(stream);
            }
            catch (Exception e)
            {
                LogFailure(e, address, operation);
                return default;
            }
        }

        private static bool Put(string address, string authToken, string path, object body, string operation)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Put, BuildUri(address, authToken, path))
                {
                    Content = JsonBody(body)
                };
                using HttpResponseMessage response = Send(request);
                return CheckResponse(response, address, operation);
            }
            catch (Exception e)
            {
                LogFailure(e, address, operation);
                return false;
            }
        }

        private static bool CheckResponse(HttpResponseMessage response, string address, string operation)
        {
            if (response.IsSuccessStatusCode)
                return true;

            Logger?.Warning("Nanoleaf request '{operation}' to {address} failed with HTTP {statusCode}",
                operation, address, (int)response.StatusCode);
            return false;
        }

        private static void LogFailure(Exception e, string address, string operation)
        {
            // Only log the exception type and message: some exception messages may include the request URI,
            // which contains the auth token.
            string reason = e is OperationCanceledException ? "timed out" : e.GetType().Name;
            Logger?.Warning("Nanoleaf request '{operation}' to {address} failed: {reason}", operation, address, reason);
        }

        #endregion

        #region Response models

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
        /// Represents the response from the length endpoint.
        /// </summary>
        private class LedCountResponse
        {
            [JsonPropertyName("numLEDs")]
            public int NumLEDs { get; init; }
        }

        #endregion
    }
}
