using System;
using System.Collections.Generic;
using System.Text;
using System.Configuration;
using System.Security.Cryptography;
using System.IO;
using System.Collections.Specialized;
using System.Runtime.InteropServices;

namespace System.Security.Cryptography.Utility
{
	[Guid( "E6393542-1B43-4636-9EA8-D5DEEF85112C" ),
	ClassInterface( ClassInterfaceType.None )]
	public class Cipher : ICipher
	{
		private string _passPhrase = string.Empty;
		private string _saltValue = string.Empty;
		private string _initVector = string.Empty;
		private int _keySize = 256;
		private string _hashAlgorithm = "SHA1";
		private int _pwdIterations = 2;


		#region ctors
		public Cipher()
		{
			this.InitCipher( this.GetType().Assembly.Location );
		}

		public Cipher(string exePath)
		{
			this.InitCipher( exePath );
		}

		public Cipher(System.Configuration.Configuration config)
		{
			this.InitCipher( config );
		}

		public Cipher(NameValueCollection settings)
		{
			this.InitCipher( settings );
		}


		private void InitCipher(string exePath)
		{
			this.InitCipher( ConfigurationManager.OpenExeConfiguration( exePath ) );
		}

		private void InitCipher(System.Configuration.Configuration config)
		{
			if( config != null && config.AppSettings.Settings != null )
			{
				this.InitCipher( config.AppSettings.Settings );
			}
		}

		private void InitCipher(KeyValueConfigurationCollection settings)
		{
			if( settings != null )
			{
				if( settings["passPhrase"] != null )
				{
					_passPhrase = settings["passPhrase"].Value;
				}
				if( settings["saltValue"] != null )
				{
					_saltValue = settings["saltValue"].Value;
				}
				if( settings["initVector"] != null )
				{
					_initVector = settings["initVector"].Value;
				}
			}
		}

		private void InitCipher(NameValueCollection settings)
		{
			if( settings != null )
			{
				if( settings["passPhrase"] != null )
				{
					_passPhrase = settings["passPhrase"];
				}
				if( settings["saltValue"] != null )
				{
					_saltValue = settings["saltValue"];
				}
				if( settings["initVector"] != null )
				{
					_initVector = settings["initVector"];
				}
			}
		}

		public Cipher(string passPhrase, string saltValue, string initVector)
		{
			_passPhrase = passPhrase;
			_saltValue = saltValue;
			_initVector = initVector;
		}
		#endregion


		#region props
		public string PassPhrase
		{
			get { return _passPhrase; }
			set { _passPhrase = value; }
		}

		public string SaltValue
		{
			get { return _saltValue; }
			set { _saltValue = value; }
		}

		public string InitVector
		{
			get { return _initVector; }
			set { _initVector = value; }
		}
		#endregion


		#region methods
		public string Encrypt(string plainText)
		{
			try
			{
				// Convert strings into byte arrays.
				// Let us assume that strings only contain ASCII codes.
				// If strings include Unicode characters, use Unicode, UTF7, or UTF8
				// encoding.
				byte[] initVectorBytes = Encoding.ASCII.GetBytes( _initVector );
				byte[] saltValueBytes = Encoding.ASCII.GetBytes( _saltValue );

				// Convert our plaintext into a byte array.
				// Let us assume that plaintext contains UTF8-encoded characters.
				byte[] plainTextBytes = Encoding.UTF8.GetBytes( plainText );

				// First, we must create a password, from which the key will be derived.
				// This password will be generated from the specified passphrase and
				// salt value. The password will be created using the specified hash
				// algorithm. Password creation can be done in several iterations.
				PasswordDeriveBytes password = new PasswordDeriveBytes(
					_passPhrase,
					saltValueBytes,
					_hashAlgorithm,
					_pwdIterations );

				// Use the password to generate pseudo-random bytes for the encryption
				// key. Specify the size of the key in bytes (instead of bits).
				byte[] keyBytes = password.GetBytes( _keySize / 8 );

				// Create uninitialized Rijndael encryption object.
				RijndaelManaged symmetricKey = new RijndaelManaged();

				// It is reasonable to set encryption mode to Cipher Block Chaining
				// (CBC). Use default options for other symmetric key parameters.
				symmetricKey.Mode = CipherMode.CBC;

				// Generate encryptor from the existing key bytes and initialization
				// vector. Key size will be defined based on the number of the key
				// bytes.
				ICryptoTransform encryptor = symmetricKey.CreateEncryptor(
					keyBytes,
					initVectorBytes );

				// Define memory stream which will be used to hold encrypted data.
				MemoryStream memoryStream = new MemoryStream();

				// Define cryptographic stream (always use Write mode for encryption).
				CryptoStream cryptoStream = new CryptoStream( memoryStream,
					encryptor,
					CryptoStreamMode.Write );
				// Start encrypting.
				cryptoStream.Write( plainTextBytes, 0, plainTextBytes.Length );

				// Finish encrypting.
				cryptoStream.FlushFinalBlock();

				// Convert our encrypted data from a memory stream into a byte array.
				byte[] cipherTextBytes = memoryStream.ToArray();

				// Close both streams.
				memoryStream.Close();
				cryptoStream.Close();

				// Convert encrypted data into a base64-encoded string.
				string cipherText = Convert.ToBase64String( cipherTextBytes );

				// Return encrypted string.
				return cipherText;
			}
			catch( Exception err )
			{
				return "UNABLE TO ENCRYPT - Error: " + err.Message;
			}
		}

		public string Decrypt(string cipherText)
		{
			try
			{
				// Convert strings defining encryption key characteristics into byte
				// arrays. Let us assume that strings only contain ASCII codes.
				// If strings include Unicode characters, use Unicode, UTF7, or UTF8
				// encoding.
				byte[] initVectorBytes = Encoding.ASCII.GetBytes( _initVector );
				byte[] saltValueBytes = Encoding.ASCII.GetBytes( _saltValue );

				// Convert our ciphertext into a byte array.
				byte[] cipherTextBytes = Convert.FromBase64String( cipherText );

				// First, we must create a password, from which the key will be
				// derived. This password will be generated from the specified
				// passphrase and salt value. The password will be created using
				// the specified hash algorithm. Password creation can be done in
				// several iterations.
				PasswordDeriveBytes password = new PasswordDeriveBytes(
					_passPhrase,
					saltValueBytes,
					_hashAlgorithm,
					_pwdIterations );

				// Use the password to generate pseudo-random bytes for the encryption
				// key. Specify the size of the key in bytes (instead of bits).
				byte[] keyBytes = password.GetBytes( _keySize / 8 );

				// Create uninitialized Rijndael encryption object.
				RijndaelManaged symmetricKey = new RijndaelManaged();

				// It is reasonable to set encryption mode to Cipher Block Chaining
				// (CBC). Use default options for other symmetric key parameters.
				symmetricKey.Mode = CipherMode.CBC;

				// Generate decryptor from the existing key bytes and initialization
				// vector. Key size will be defined based on the number of the key
				// bytes.
				ICryptoTransform decryptor = symmetricKey.CreateDecryptor(
					keyBytes,
					initVectorBytes );

				// Define memory stream which will be used to hold encrypted data.
				MemoryStream memoryStream = new MemoryStream( cipherTextBytes );

				// Define cryptographic stream (always use Read mode for encryption).
				CryptoStream cryptoStream = new CryptoStream( memoryStream,
					decryptor,
					CryptoStreamMode.Read );

				// Since at this point we don't know what the size of decrypted data
				// will be, allocate the buffer long enough to hold ciphertext;
				// plaintext is never longer than ciphertext.
				byte[] plainTextBytes = new byte[cipherTextBytes.Length];

				// Start decrypting.
				int decryptedByteCount = cryptoStream.Read( plainTextBytes,
					0,
					plainTextBytes.Length );

				// Close both streams.
				memoryStream.Close();
				cryptoStream.Close();

				// Convert decrypted data into a string.
				// Let us assume that the original plaintext string was UTF8-encoded.
				string plainText = Encoding.UTF8.GetString( plainTextBytes,
					0,
					decryptedByteCount );

				// Return decrypted string.
				return plainText;
			}
			catch( Exception err )
			{
				return "UNABLE TO DECRYPT - Error: " + err.Message;
			}
		}
		#endregion
	}
}