using NBitcoin;
using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace EncryptedMessaging
{
	public class CryptoServiceProvider : IDisposable
	{
		public CryptoServiceProvider(byte[] key = null)
		{
			ImportCspBlob(key);
		}

		/// <summary>
		/// Instance the object
		/// </summary>
		/// <param name="passphrase">Passphrase or private key base 64</param>
		public CryptoServiceProvider(string passphrase)
		{
			try
			{
				passphrase = passphrase.Trim();
				passphrase = passphrase.Replace(",", " ");
				passphrase = Regex.Replace(passphrase, @"\s+", " ");
				var words = passphrase.Split(' ');
				if (words.Length >= 12)
				{
					passphrase = passphrase.ToLower();
					_mnemo = new Mnemonic(passphrase, Wordlist.AutoDetect(passphrase));
					_hdRoot = _mnemo.DeriveExtKey();
					_privateKey = _hdRoot.PrivateKey;
					_pubKey = _privateKey.PubKey;
				}
				else if (words.Length == 1)
				{
					ImportCspBlob(Convert.FromBase64String(passphrase));
				}
			}
			catch (Exception)
			{
				Debugger.Break(); // passphrase wrong
			}
		}

		public string GetPrivateKeyBase58()
		{
			return _privateKey.GetBitcoinSecret(Network.Main).ToString();
		}

		public bool VerifyHash(byte[] hash256, byte[] signature)
		{
			try
			{
				var sign = new NBitcoin.Crypto.ECDSASignature(signature);
				return _pubKey.Verify(new uint256(hash256), sign);
			}
			catch (Exception)
			{
				return false;
			}
		}

		public byte[] SignHash(byte[] hash256)
		{
			var sign = _privateKey.Sign(new uint256(hash256));
			return sign.ToDER();
		}

		//public void SetPrivateKeyBase58(string base58)
		//{
		//	var x = new BitcoinSecret(base58, Network.Main);
		//	_privateKey = x.PrivateKey;
		//	_pubKey = _privateKey.PubKey;
		//}

		public byte[] ExportCspBlob(bool includePrivateKey) => _privateKey != null ? includePrivateKey ? _privateKey.ToBytes() : _privateKey.PubKey.ToBytes() : (_pubKey?.ToBytes());
		public string GetPassphrase()
		{
			if (_mnemo != null)
				return _mnemo.ToString();
			else if (_privateKey != null)
				return Convert.ToBase64String(_privateKey.ToBytes());
			return null;
		}

		public void ImportCspBlob(byte[] key)
		{
			_privateKey = null;
			_mnemo = null;
			_hdRoot = null;
			if (key == null)
			{
				// generate a random private key
				_mnemo = new Mnemonic(Wordlist.English, WordCount.Twelve);
				_hdRoot = _mnemo.DeriveExtKey();
				_privateKey = _hdRoot.PrivateKey;
				_pubKey = _privateKey.PubKey;
			}
			else
			{
				if (key.Length == 32) // is a private key
				{
					_privateKey = new Key(key);
					_pubKey = _privateKey.PubKey;
				}
				else if (key.Length == 33) // is a pub key
					_pubKey = new PubKey(key); // load
			}
		}

		public byte[] Encrypt(byte[] data) => _pubKey.Encrypt(data);

		public byte[] Decrypt(byte[] encryptedData) => _privateKey.Decrypt(encryptedData);

		private Mnemonic _mnemo;
		private ExtKey _hdRoot;
		private Key _privateKey;
		private PubKey _pubKey;

		public bool IsValid()
		{
			return _privateKey != null || _pubKey != null;
		}

		// To detect redundant calls
		private bool _disposed = false;

		// Public implementation of Dispose pattern callable by consumers.
		public void Dispose() => Dispose(true);

		// Protected implementation of Dispose pattern.
		protected virtual void Dispose(bool disposing)
		{
			if (_disposed)
			{
				return;
			}

			if (disposing)
			{
				// Dispose managed state (managed objects).
				_privateKey?.Dispose();
			}
			_disposed = true;
		}

		public static byte[] ComputeHash(byte[] data) => NBitcoin.Crypto.Hashes.DoubleSHA256(data).ToBytes();

	}
}
