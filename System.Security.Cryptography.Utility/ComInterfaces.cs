using System;
using System.Runtime.InteropServices;

namespace System.Security.Cryptography.Utility
{
	[Guid( "F94CEB67-EE6E-42CA-B759-1E606F49C3D7" )]
	interface ICipher
	{
		[DispId( 1 )]
		string Encrypt(string plainText);
		[DispId( 2 )]
		string Decrypt(string cipherText);
		[DispId( 3 )]
		string InitVector { get; set; }
		[DispId( 4 )]
		string PassPhrase { get; set; }
		[DispId( 5 )]
		string SaltValue { get; set; }
	}
}