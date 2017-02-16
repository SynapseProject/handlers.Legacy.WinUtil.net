using System;
using System.Diagnostics;
using System.Reflection;

using Alphaleonis.Win32.Filesystem;

namespace Synapse.Handlers.Legacy.StandardCopyProcess
{
	internal static class Utils
	{
		const string _lines = "--------------------------";
		public static string GetServerLongPath(string server, string localServerPath)
		{
			return Path.GetLongPath( "\\\\" + server + "\\" + localServerPath.Replace( ':', '$' ) );
		}

		public static double ElapsedSeconds(this Stopwatch stopwatch)
		{
			return TimeSpan.FromMilliseconds( stopwatch.ElapsedMilliseconds ).TotalSeconds;
		}

		public static string GetMessagePadLeft(string header, object message, int width)
		{
			return string.Format( "{0}: {1}", header.PadLeft( width, '.' ), message );
		}

		public static string GetMessagePadRight(string header, object message, int width)
		{
			return string.Format( "{0}: {1}", header.PadRight( width, '.' ), message );
		}

		public static string GetHeaderMessage(string header)
		{
			return string.Format( "{1}  {0}  {1}", header, _lines );
		}

		/// <summary>
		/// A wrapper on Path.Combine to correct for fronting/trailing backslashes that otherwise fail in Path.Combine.
		/// </summary>
		/// <param name="paths">An array of parts of the path.</param>
		/// <returns>The combined path</returns>
		public static string PathCombine(params string[] paths)
		{
			if( paths.Length > 0 )
			{
				int last = paths.Length - 1;
				for( int c = 0; c <= last; c++ )
				{
					if( c != 0 )
					{
						paths[c] = paths[c].Trim( Path.DirectorySeparatorChar );
					}
					if( c != last )
					{
						paths[c] = string.Format( "{0}\\", paths[c] );
					}
				}
			}
			else
			{
				return string.Empty;
			}

			return Path.Combine( paths );
		}

		//http://stackoverflow.com/questions/1600962/displaying-the-build-date
		//note: [assembly: AssemblyVersion("1.0.*")] // important: use wildcard for build and revision numbers!
		public static string GetBuildDateVersion()
		{
			Assembly assm = Assembly.GetExecutingAssembly();
			Version version = assm.GetName().Version;
			DateTime buildDateTime = new FileInfo( assm.Location ).LastWriteTime;

			return string.Format( "Version: {0}, Build DateTime: {1}", version, buildDateTime );

			//ToString( "yyMMdd.HHmm" )
//			return string.Format( "{0}.{1}.{2}.{3}", version.Major, version.Minor, buildDateTime.ToString( "yy" ), buildDateTime.DayOfYear.ToString( "D3" ) );
		}

	}
}