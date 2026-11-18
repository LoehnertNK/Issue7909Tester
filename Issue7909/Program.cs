using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using TwinCAT;
using TwinCAT.Ads;
using TwinCAT.TypeSystem;
using YamlDotNet.Serialization;

namespace Issue7909
{
    internal class Program
    {
        private static void Main(string[] args)
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
            using (var session = new AdsSession(new AmsAddress(config.AmsNetID, config.Port)))
            {
                var connection = (IAdsConnection)session.Connect();
                var symbolserver = ((ISymbolServerProvider)session).SymbolServer;
                var symbols = new SimpleSymbolCollection(((ISymbolServerProvider)connection.Session).SymbolServer.Symbols);

                var variable = config.Variable;
                uint? handle = null;
                try
                {
                    if (variable.AccessType == AccessType.Name)
                    {
                        handle = connection.AddDeviceNotification(variable.Name, variable.Size, new NotificationSettings(AdsTransMode.Cyclic, TimeSpan.Zero, TimeSpan.FromMinutes(1)), variable);
                    }
                    else if (variable.AccessType == AccessType.IndexGroupIndexOffset)
                    {
                        var adsSymbol = GetAdsSymbol(symbols[variable.Name]);
                        Console.WriteLine($"Variable: {variable.Name}, IndexGroup: {adsSymbol.IndexGroup}, IndexOffset: {adsSymbol.IndexOffset}");
                        handle = connection.AddDeviceNotification(adsSymbol.IndexGroup, adsSymbol.IndexOffset, variable.Size, new NotificationSettings(AdsTransMode.Cyclic, TimeSpan.Zero, TimeSpan.FromMinutes(1)), variable);
                    }
                    connection.AdsSumNotification += AdsSumNotified;
                    Console.ReadKey();
                }
                finally
                {
                    if (handle.HasValue)
                    {
                        connection.DeleteDeviceNotification(handle.Value);
                    }

                    connection.AdsSumNotification -= AdsSumNotified;
                }
            }
        }

        private IProcessImageAddress GetAdsSymbol(ISymbol symbol)
        {
            // The SymbolProvider returns DynamicSymbols which doesn't contain index group and offset.
            // But the wrapped symbol IProcessImageAddress IAdsSymbol.
            return (IProcessImageAddress)((IDynamicSymbol)symbol).Unwrap();
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

    internal class SimpleSymbolCollection
    {
        private IDictionary<string, ISymbol> _symbolsByKey;

        public SimpleSymbolCollection(IEnumerable<ISymbol> symbols)
        {
            _symbolsByKey = GetAllSymbolsRecursive(symbols);
        }

        public ISymbol this[string key]
        {
            get
            {
                if (!_symbolsByKey.ContainsKey(key))
                {
                    throw new KeyNotFoundException(
                        $"The symbol '{key}' was not found. " +
                        $"Please verify that the symbol exists in the PLC and that the TwinCAT IDE language is set to English. " +
                        $"For more information, see: https://docs.lisrt.de/articles/Loehnert.Lisrt.IO.Beckhoff/GettingStartedWithTwinCat3.html");
                }

                return _symbolsByKey[key];
            }
        }

        private static IDictionary<string, ISymbol> GetAllSymbolsRecursive(IEnumerable<ISymbol> symbols)
        {
            var paths = new Dictionary<string, ISymbol>();
            foreach (var symbol in symbols)
            {
                paths[symbol.InstancePath] = symbol;
                if (symbol.SubSymbols.Count > 0)
                {
                    foreach (var subSymbol in GetAllSymbolsRecursive(symbol.SubSymbols))
                    {
                        paths[subSymbol.Key] = subSymbol.Value;
                    }
                }
            }

            return paths;
        }
    }

    internal class Options
    {
        [Option('c', "config", Required = false, HelpText = "Path to the configuration file.")]
        public string ConfigFile { get; set; } = "config.yml";
    }
}