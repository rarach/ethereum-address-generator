using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Configuration;

using Nethereum.Signer;


namespace EthereumAddressGenerator
{
    class Program
    {
        private static IEnumerable<string> _prefixes;
        private const int OUTPUT_INTERVAL = 5000;
        private static bool _terminate = false;
        private static int _maxPrefixLength = 0;


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
            _prefixes = config.GetSection("Prefixes").GetChildren().Select(section => SanitizePrefix(section.Value));
            foreach (string prefix in _prefixes)
            {
                if (prefix.Length > _maxPrefixLength)
                {
                    _maxPrefixLength = prefix.Length;
                }
            }
            bool dummy;
            bool caseSensitive = bool.TryParse(config["CaseSensitive"], out dummy) && dummy;

            Console.WriteLine("Starting ETH address generator with configuration:");
            Console.WriteLine(" threads: " + confThreads);
            Console.WriteLine(" prefixes: " + string.Join(", ", _prefixes));
            Console.WriteLine(" case sensitive: " + (caseSensitive ? "yes" : "no"));
            Console.WriteLine(new string('=', 80));

            int threadCount = int.Parse(confThreads);
            for (int i = 1; i <= threadCount; i++)
            {
                int threadId = i;
                string outFile = $"{confOutDir}eth_keys_{threadId}.txt";
                var thread = new Thread(() => GenerateAddress(threadId, caseSensitive, outFile));
                thread.Start();
            }

            Console.ReadLine();
            _terminate = true;
        }


        private static string SanitizePrefix(string prefix)
        {
            var invalid = new System.Text.RegularExpressions.Regex("[^a-fA-F0-9]");
            if (invalid.IsMatch(prefix))
            {
                throw new Exception($"Invalid prefix: {prefix}{Environment.NewLine}Must use only chars a-f, A-F, 0-9.");
            }

            if (!prefix.StartsWith("0x"))
            {
                prefix = "0x" + prefix;
            }
            return prefix;
        }

        private static void GenerateAddress(int threadId, bool caseSensitive, string outputFilePath)
        {
            var keyGenerator = new KeyGenerator(400);
            int loopCounter = 0;
            ulong outputCounter = 0;
            double runTimeSeconds = 0.0;
            StringComparison comparisonType = caseSensitive ? StringComparison.InvariantCulture : StringComparison.InvariantCultureIgnoreCase;
            DateTime start = DateTime.Now;

            using (StreamWriter fileWriter = File.AppendText(outputFilePath))
            {
                while (!_terminate)
                {
                    EthECKey ecKey = keyGenerator.GenerateKey();
                    string beginning = keyGenerator.GetStartLowerCase(ecKey, _maxPrefixLength);

                    foreach (string prefix in _prefixes)
                    {
                        if (beginning.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase))
                        {
                            string address = ecKey.GetPublicAddress();
                            if (address.StartsWith(prefix, comparisonType))
                            {
                                string privateKey = ecKey.GetPrivateKey();
                                fileWriter.WriteLine($"{address} - {privateKey}");
                                fileWriter.Flush();
                                Console.Beep();
                                Console.ForegroundColor = ConsoleColor.Magenta;
                                Console.WriteLine($"MATCH: {address} {privateKey}");
                                Console.ResetColor();
                                Console.Title = "MATCH!";
                            }
                            else
                            {
                                Console.WriteLine($"Case insensitive match: {address} ({ecKey.GetPrivateKey()})");
                                Console.Title = "ci_match";
                            }
                        }
                    }

                    if (++loopCounter == OUTPUT_INTERVAL)
                    {
                        loopCounter = 0;
                        outputCounter++;
                        TimeSpan duration = DateTime.Now - start;
                        runTimeSeconds += duration.TotalSeconds;
                        double avgSeconds = runTimeSeconds / outputCounter;
                        Console.WriteLine($"{OUTPUT_INTERVAL} more keys checked on thread {threadId} in ~{(int)duration.TotalSeconds}sec (avg. {avgSeconds:0.00}sec)");
                        Console.WriteLine($"Last: {ecKey.GetPublicAddress()} ({ecKey.GetPrivateKey()})");
                        Console.WriteLine(new string('=', 80));
                        start = DateTime.Now;
                    }
                }
            }
        }
    }
}
