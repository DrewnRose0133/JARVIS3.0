using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace JARVIS.Devices.OAuth
{
    public class SmartThingsOAuth
    {
        private const string AuthorizeUrl = "https://auth-global.api.smartthings.com/oauth/authorize";
        private const string TokenUrl = "https://auth-global.api.smartthings.com/oauth/token";
        private const string RedirectUri = "http://localhost:5000/callback";

        private readonly string _clientId;
        private readonly string _clientSecret;

        public SmartThingsOAuth(string clientId, string clientSecret)
        {
            _clientId = clientId;
            _clientSecret = clientSecret;
        }

        /// <summary>
        /// One-time code-grant flow: opens browser, awaits the callback, exchanges code → tokens.
        /// </summary>
        public async Task<(string accessToken, string refreshToken)> AuthorizeAsync()
        {
            var listener = new HttpListener();
            listener.Prefixes.Add(RedirectUri + "/");
            listener.Start();

            var url = $"{AuthorizeUrl}?response_type=code" +
                      $"&client_id={Uri.EscapeDataString(_clientId)}" +
                      $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
                      $"&scope={Uri.EscapeDataString("r:devices:* x:devices:*")}";

            Console.WriteLine("Opening browser for SmartThings login...");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url)
            {
                UseShellExecute = true
            });

            var context = await listener.GetContextAsync(); // wait for code
            var code = context.Request.QueryString["code"];
            var responseHtml = "<html><body>Authentication complete. You can close this window.</body></html>";
            var buffer = System.Text.Encoding.UTF8.GetBytes(responseHtml);
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer);
            context.Response.Close();
            listener.Stop();

            using var http = new HttpClient();
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string>("grant_type",    "authorization_code"),
                new KeyValuePair<string,string>("client_id",     _clientId),
                new KeyValuePair<string,string>("client_secret", _clientSecret),
                new KeyValuePair<string,string>("code",          code),
                new KeyValuePair<string,string>("redirect_uri",  RedirectUri)
            });
            var tokenResp = await http.PostAsync(TokenUrl, form);
            tokenResp.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await tokenResp.Content.ReadAsStringAsync());
            var at = doc.RootElement.GetProperty("access_token").GetString()!;
            var rt = doc.RootElement.GetProperty("refresh_token").GetString()!;
            return (at, rt);
        }

        /// <summary>
        /// Use your stored refreshToken to get a new accessToken (and refreshToken).
        /// </summary>
        public async Task<(string accessToken, string refreshToken)> RefreshAsync(string refreshToken)
        {
            using var http = new HttpClient();
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string>("grant_type",    "refresh_token"),
                new KeyValuePair<string,string>("client_id",     _clientId),
                new KeyValuePair<string,string>("client_secret", _clientSecret),
                new KeyValuePair<string,string>("refresh_token", refreshToken)
            });
            var resp = await http.PostAsync(TokenUrl, form);
            resp.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var newAt = doc.RootElement.GetProperty("access_token").GetString()!;
            var newRt = doc.RootElement.GetProperty("refresh_token").GetString()!;
            return (newAt, newRt);
        }
    }
}
