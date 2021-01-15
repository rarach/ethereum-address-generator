using System;

using Nethereum.Signer;
using Nethereum.Util;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;


namespace EthereumAddressGenerator
{
    internal class KeyGenerator
    {
        private ECKeyPairGenerator _keyPairGenerator = new ECKeyPairGenerator("EC");
        private Sha3Keccack _sha3Keccack = new Sha3Keccack();
        private byte[] _lastBytes = new byte[32];
        private readonly int _chunkSize;
        private int _chunkCounter;


        internal KeyGenerator(int chunkSize)
        {
            _chunkSize = chunkSize;
            _chunkCounter = chunkSize;
            _keyPairGenerator.Init(new KeyGenerationParameters(new Org.BouncyCastle.Security.SecureRandom(), 256));
        }


        internal EthECKey GenerateKey()
        {
            if (_chunkCounter++ >= _chunkSize)
            {
                _chunkCounter = 0;
                do
                {
                    var privateKeyParams = (ECPrivateKeyParameters)_keyPairGenerator.GenerateKeyPair().Private;
                    _lastBytes = privateKeyParams.D.ToByteArray();
                }
                while (_lastBytes.Length != 32);
            }
            else
            {
                IncrementBytes(ref _lastBytes);
            }

            return new EthECKey(_lastBytes, true);
        }

        internal string GetStartLowerCase(EthECKey key, int length)
        {
            byte[] hash = _sha3Keccack.CalculateHash(key.GetPubKeyNoPrefix());
            byte[] numArray = new byte[hash.Length - 12];
            Array.Copy(hash, 12, numArray, 0, hash.Length - 12);

            string address = "0x";

            for (int i=0; i < numArray.Length; i++)
            {
                address += numArray[i].ToString("x2");
                if (address.Length >= length)
                {
                    return address;
                }
            }

            return address;
        }


        /// <summary>
        /// Increment some bytes of given array
        /// </summary>
        private static void IncrementBytes(ref byte[] bytes)
        {
            int length = bytes.Length;

            for (int i = length - 1; i >= 0; i--)
            {
                if (bytes[i] < 128)
                {
                    bytes[i]++;
                    break;
                }
            }
        }
    }
}
