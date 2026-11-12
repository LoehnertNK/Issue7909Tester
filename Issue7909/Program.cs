using CommandLine;
using System;
using System.IO;
using TwinCAT.Ads;
using YamlDotNet.Serialization;

namespace Issue7909
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var options = Parser.Default.ParseArguments<Options>(args).Value;
            var config = new Deserializer().Deserialize<Configuration>(File.ReadAllText(options.ConfigFile));
            _ = new AdsNotificationLogger(config);
        }
    }

    internal class AdsNotificationLogger
    {   
        private readonly Configuration _config;
        private readonly string _logFile;
        private int _sampleCount;

        public AdsNotificationLogger(Configuration config)
        {
            _config = config;
            _logFile = $"Log_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}.txt";
            using (var client = new AdsClient())
            {
                client.Connect(config.AmsNetID, config.Port);
                var variable = config.Variable;
                uint handle = 0;
                try
                {
                    handle = client.AddDeviceNotification(variable.Name, variable.Size, new NotificationSettings(AdsTransMode.Cyclic, TimeSpan.Zero, TimeSpan.FromMinutes(1)), variable);
                    client.AdsSumNotification += AdsSumNotified;
                    Console.ReadKey();
                }
                finally
                {
                    client.DeleteDeviceNotification(handle);
                    client.AdsSumNotification -= AdsSumNotified;
                }
            }
        }

        private void AdsSumNotified(object sender, AdsSumNotificationEventArgs e)
        {
            _sampleCount++;
            if (_sampleCount > _config.UnderSampling)
            {
                _sampleCount = 0;
                foreach (var n in e.Notifications)
                {
                    var variable = n.UserData as Variable;
                    string log = $"{variable?.Name}; {n.TimeStamp}; {BytesToHex(n.Data)}";
                    Console.WriteLine(log);
                    File.AppendAllLines(_logFile, new[] { log });
                }
            }
        }

        private static string BytesToHex(ReadOnlyMemory<byte> bytes)
        {
            if (bytes.Length == 0)
            {
                return string.Empty;
            }

            return BitConverter.ToString(bytes.ToArray());
        }
    }

    internal class Options
    {
        [Option('c', "config", Required = false, HelpText = "Path to the configuration file.")]
        public string ConfigFile { get; set; } = "config.yml";
    }
}
