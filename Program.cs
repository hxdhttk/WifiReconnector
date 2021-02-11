using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.WiFi;
using Windows.Networking.Connectivity;
using Windows.Security.Credentials;

namespace WifiReconnector
{
    public static class Program
    {
        private static Config _config;

        private static AutoResetEvent _waiter;

        public static async Task Main()
        {
            _config = GetConfig();
            _waiter = new AutoResetEvent(false);

            NetworkInformation.NetworkStatusChanged += OnNetworkStatusChanged;

            await WaitForEventsAsync();
        }

        private static void OnNetworkStatusChanged(object _)
        {
            _waiter.Set();
        }

        private static Config GetConfig()
        {
            var configContent = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "config.json"));
            return JsonSerializer.Deserialize<Config>(configContent);
        }

        private static async Task WaitForEventsAsync()
        {
            while (_waiter.WaitOne())
            {
                await ReconnectAsync();
            }
        }

        private static async Task ReconnectAsync()
        {
            var profile = NetworkInformation.GetInternetConnectionProfile();
            var reconnect = profile == null || profile.GetNetworkConnectivityLevel() < NetworkConnectivityLevel.InternetAccess;
            if (reconnect)
            {
                Logger.Log("Lost connection, start to reconnect...");

                var adapters = await WiFiAdapter.FindAllAdaptersAsync();
                var adapter = adapters.First();
                var report = adapter.NetworkReport;
                foreach (var target in report.AvailableNetworks.OrderByDescending(n => n.NetworkRssiInDecibelMilliwatts))
                {
                    if (target.Ssid == _config.WifiSsid)
                    {
                        Logger.Log($"Reconnect to {target.Ssid}...");

                        var cred = new PasswordCredential();
                        cred.Password = _config.WifiPassword;

                        var result = await adapter.ConnectAsync(target, WiFiReconnectionKind.Automatic, cred);
                        if (result.ConnectionStatus == WiFiConnectionStatus.Success)
                        {
                            Logger.Log("Reconnection succeeded.");
                        }
                        else
                        {
                            Logger.Log($"Reconnection failed with error {result.ConnectionStatus}.");
                        }
                        break;
                    }
                }
            }
        }
    }

    public static class Logger
    {
        private static string _logPath = Path.Combine(Directory.GetCurrentDirectory(), @"log.txt");

        private static StreamWriter _logStream = CreateWriter();

        private static StreamWriter CreateWriter()
        {
            var logFile = new FileStream(_logPath, FileMode.Append, FileAccess.Write, FileShare.Read);
            return new StreamWriter(logFile);
        }

        public static void Log(string msg)
        {
            lock (_logStream)
            {
                _logStream.WriteLine($"{DateTime.Now} {msg}");
                _logStream.Flush();
            }
        }
    }

    public class Config
    {
        public string WifiSsid { get; set; }

        public string WifiPassword { get; set; }
    }
}
