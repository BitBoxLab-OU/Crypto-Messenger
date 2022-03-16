//using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using NBitcoin;
//using static NBitcoin.BitcoinSerializableExtensions;

namespace EncryptedMessaging
{
	/// <summary>
	/// This class handles the encryption and decryption of the passphrases and validations.
	/// </summary>
	public class CryptoServiceProvider : System.IDisposable
	{
		/// <summary>
		/// The ImportCspBlob method initializes the key data of an AsymmetricAlgorithm object using a blob that is compatible with the unmanaged Microsoft Cryptographic API (CAPI).
		/// </summary>
		/// <param name="key"></param>
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
					ImportCspBlob(System.Convert.FromBase64String(passphrase));
				}
			}
			catch (System.Exception)
			{
				Debugger.Break(); // passphrase wrong
			}
		}

		/// <summary>
		/// Gets the private key through wallet import format. 
		/// </summary>
		/// <returns></returns>
		public string GetPrivateKeyBase58()
		{
			return _privateKey.GetBitcoinSecret(Network.Main).ToString();
		}

		/// <summary>
		/// Verifies that a digital signature is valid by determining the hash value in the signature using the specified hash algorithm and padding, and comparing it to the provided hash value.
		/// </summary>
		/// <param name="hash256">The hash value of the signed data.</param>
		/// <param name="signature">The signature data to be verified.</param>
		/// <returns></returns>
		public bool VerifyHash(byte[] hash256, byte[] signature)
		{
			try
			{
				var sign = new NBitcoin.Crypto.ECDSASignature(signature);
				return _pubKey.Verify(new uint256(hash256), sign);
			}
			catch (System.Exception)
			{
				return false;
			}
		}

		/// <summary>
		/// Generates a digital signature for the specified hash value.
		/// </summary>
		/// <param name="hash256">The hash value of the data that is being signed.</param>
		/// <returns></returns>
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

		/// <summary>
		/// Exports a blob that contains the key information associated with an AsymmetricAlgorithm privatekey.
		/// </summary>
		/// <param name="includePrivateKey"></param>
		/// <returns></returns>
		public byte[] ExportCspBlob(bool includePrivateKey)
        {
			return _privateKey != null ? includePrivateKey ? _privateKey.ToBytes() : _privateKey.PubKey.ToBytes() : _pubKey?.ToBytes();
        }

		/// <summary>
		/// Get the passPhrase property, If the string is empty return null.
		/// </summary>
		/// <returns></returns>
		public string GetPassphrase()
		{
			if (_mnemo != null)
				return _mnemo.ToString();
			else if (_privateKey != null)
				return System.Convert.ToBase64String(_privateKey.ToBytes());
			return null;
		}

		/// <summary>
		/// The ImportCspBlob method initializes the key data of an AsymmetricAlgorithm object using a blob that is compatible with the unmanaged Microsoft Cryptographic API (CAPI).
		/// </summary>
		/// <param name="key">A byte array that represents an asymmetric key blob.</param>
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

		/// <summary>
		/// Encrypts the private key. 
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		public byte[] Encrypt(byte[] data) => _pubKey.Encrypt(data);

		/// <summary>
		/// Decrypts the private key. 
		/// </summary>
		/// <param name="encryptedData"></param>
		/// <returns></returns>
		public byte[] Decrypt(byte[] encryptedData) => _privateKey.Decrypt(encryptedData);

		private Mnemonic _mnemo;
		private ExtKey _hdRoot;
		private Key _privateKey;
		private PubKey _pubKey;

		/// <summary>
		/// Checks validity and returns boolean.
		/// </summary>
		/// <returns></returns>
		public bool IsValid()
		{
			return _privateKey != null || _pubKey != null;
		}

		// To detect redundant calls
		private bool _disposed = false;

		// Public implementation of Dispose pattern callable by consumers.
		/// <summary>
		///Public implementation of Dispose pattern callable by consumers. 
		/// </summary>
		public void Dispose() => Dispose(true);

		// Protected implementation of Dispose pattern.
		/// <summary>
		/// Protected implementation of Dispose pattern.
		/// </summary>
		/// <param name="disposing"></param>
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

		/// <summary>
		/// Computes the hash value for the specified byte array.
		/// </summary>
		/// <param name="data">Combined packages</param>
		/// <returns>Byte array</returns>
		public static byte[] ComputeHash(byte[] data) => NBitcoin.Crypto.Hashes.DoubleSHA256(data).ToBytes();

	}
}
