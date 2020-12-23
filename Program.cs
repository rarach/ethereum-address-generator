using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Configuration;

using Nethereum.Signer;
using Nethereum.Web3.Accounts;


namespace EthereumAddressGenerator
{
    class Program
    {
        private static IEnumerable<string> _prefixes;
        private const int OUTPUT_INTERVAL = 5000;
        private static bool _terminate = false;


        static void Main(string[] args)
        {
            IConfigurationBuilder builder = new ConfigurationBuilder().AddJsonFile("appSettings.json");
            IConfigurationRoot config = builder.Build();
            string confThreads = config["Threads"];
            string confOutDir = config["OutputDir"];
            if (!Directory.Exists(confOutDir))
            {
                throw new ApplicationException($"Output folder {confOutDir} not found");
            }
            if (!confOutDir.EndsWith(Path.DirectorySeparatorChar))
            {
                confOutDir += Path.DirectorySeparatorChar;
            }
            _prefixes = config.GetSection("Prefixes").GetChildren().Select(x => "0x" + x.Value);

            Console.WriteLine("Starting ETH address generator with configuration:");
            Console.WriteLine("threads=" + confThreads);
            Console.WriteLine("prefixes=" + string.Join(", ", _prefixes));
            Console.WriteLine(new string('=', 80));

            int threadCount = int.Parse(confThreads);
            for (int i = 1; i <= threadCount; i++)
            {
                int threadId = i;
                string outFile = $"{confOutDir}eth_keys_{threadId}.txt";
                var thread = new Thread(() => GenerateAddress(threadId, outFile));
                thread.Start();
            }

            Console.ReadLine();
            _terminate = true;
        }


        private static void GenerateAddress(int threadId, string outputFilePath)
        {
            int counter = 0;
            using (StreamWriter fileWriter = File.AppendText(outputFilePath))
            {
                while (!_terminate)
                {
                    EthECKey ecKey = EthECKey.GenerateKey();
                    byte[] privateKey = ecKey.GetPrivateKeyAsBytes();
                    var account = new Account(privateKey);
                    string address = account.Address;

                    foreach (string prefix in _prefixes)
                    {
                        if (address.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            fileWriter.WriteLine($"{address} - {account.PrivateKey}");
                            Console.Beep();
                            Console.ForegroundColor = ConsoleColor.Magenta;
                            Console.WriteLine($"MATCH: {address} {account.PrivateKey}");
                            Console.ResetColor();
                        }
                    }

                    if (++counter == OUTPUT_INTERVAL)
                    {
                        counter = 0;
                        Console.WriteLine($"Another {OUTPUT_INTERVAL} keys checked on thread {threadId}");
                        Console.WriteLine($"Last: {address} ({account.PrivateKey})");
                        Console.WriteLine(new string('=', 80));
                    }
                }
            }
        }
    }
}
