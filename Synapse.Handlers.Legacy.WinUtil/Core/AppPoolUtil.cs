using System;
using System.Management;
using System.Threading;


namespace Synapse.Handlers.Legacy.WinCore
{
	public class AppPoolUtil
	{
		/// <summary>
		/// Queries the status and other configuration properties of an AppPool.
		/// </summary>
		/// <param name="name">The AppPool name.</param>
		/// <param name="useWildcard">Uses a wildcard in searching for the AppPool by name ["%{0}%"].</param>
		/// <param name="machineName">The server on which the AppPool is running.</param>
		/// <returns></returns>
		public static AppPoolConfig QueryStatus(string name, bool useWildcard, string machineName)
		{
			AppPoolConfig config = new AppPoolConfig()
			{
				AppPoolName = name,
				ServerName = machineName
			};

			ManagementObjectCollection pools = GetAppPoolSettings( name, useWildcard, machineName );
			foreach( ManagementObject pool in pools )
			{
                String appPoolState = pool["AppPoolState"].ToString();
                try
                {
                    config.State = ((AppPoolReturnCode)Enum.Parse(typeof(AppPoolReturnCode), appPoolState, false)).ToString();
                }
                catch
                {
                    config.State = "Unknown State (" + appPoolState + ")";
                }
			}

			return config;
		}

		/// <summary>
		/// Starts an AppPool.
		/// </summary>
		/// <param name="name">The AppPool name.</param>
		/// <param name="machineName">The server on which the AppPool is running.</param>
		/// <param name="millisecondsTimeout">Timeout value on wait for Started status.</param>
		/// <returns>True if status == Started, otherwise false.</returns>
		public static bool Start(string name, string machineName, int millisecondsTimeout)
		{
			ManagementOptions options = new InvokeMethodOptions();
			ManagementObject appPool = GetAppPoolManagementObject( name, machineName, millisecondsTimeout, ref options );
			ManagementBaseObject mbo = appPool.InvokeMethod( "Start", null, (InvokeMethodOptions)options );

			ManagementObjectCollection settings = GetAppPoolSettings( name, false, machineName );
			if( !(GetAppPoolState( settings ) == (int)AppPoolReturnCode.Started) )
			{
				Thread.Sleep( millisecondsTimeout );
			}

			settings = GetAppPoolSettings( name, false, machineName );
			return GetAppPoolState( settings ) == (int)AppPoolReturnCode.Started;
		}

		/// <summary>
		/// Stops an AppPool.
		/// </summary>
		/// <param name="name">The AppPool name.</param>
		/// <param name="machineName">The server on which the AppPool is running.</param>
		/// <param name="millisecondsTimeout">Timeout value on wait for Stopped status.</param>
		/// <param name="retryAttempts">Number of attempts to poll for Stopped status.</param>
		/// <param name="retryWaitMilliseconds">Wait time in between polling attempts.</param>
		/// <returns>True if status == Stopped, otherwise false.</returns>
		public static bool Stop(string name, string machineName, int millisecondsTimeout, int retryAttempts, int retryWaitMilliseconds)
		{
			bool isStopped = false;

			ManagementObjectCollection settings = GetAppPoolSettings( name, false, machineName );
			isStopped = GetAppPoolState( settings ) == (int)AppPoolReturnCode.Stopped;

			if( !isStopped )
			{
				ManagementOptions options = new InvokeMethodOptions();
				ManagementObject appPool = GetAppPoolManagementObject( name, machineName, millisecondsTimeout, ref options );
				ManagementBaseObject mbo = appPool.InvokeMethod( "Stop", null, (InvokeMethodOptions)options );

				settings = GetAppPoolSettings( name, false, machineName );
				isStopped = GetAppPoolState( settings ) == (int)AppPoolReturnCode.Stopped;
				int retryCount = 0;
				while( retryCount < retryAttempts && isStopped == false )
				{
					Thread.Sleep( retryWaitMilliseconds );

					settings = GetAppPoolSettings( name, false, machineName );
					isStopped = GetAppPoolState( settings ) == (int)AppPoolReturnCode.Stopped;

					retryCount++;
				}
			}

			return isStopped;
		}

		/// <summary>
		/// Starts a stopped AppPool, or Stops/Starts a running AppPool.
		/// </summary>
		/// <param name="name">The AppPool name.</param>
		/// <param name="machineName">The server on which the AppPool is running.</param>
		/// <param name="millisecondsTimeout">Timeout value on wait for Stopped/Started status.</param>
		/// <returns>True if status == Started, otherwise false.</returns>
		public static bool Recycle(string name, string machineName, int millisecondsTimeout)
		{
			bool isStopped = false;

			ManagementObjectCollection settings = GetAppPoolSettings( name, false, machineName );
			isStopped = GetAppPoolState( settings ) == (int)AppPoolReturnCode.Stopped;

			if( isStopped )
			{
				Start( name, machineName, millisecondsTimeout );
			}
			else
			{
				ManagementOptions options = new InvokeMethodOptions();
				ManagementObject appPool = GetAppPoolManagementObject( name, machineName, millisecondsTimeout, ref options );
				ManagementBaseObject mbo = appPool.InvokeMethod( "Recycle", null, (InvokeMethodOptions)options );
			}

			settings = GetAppPoolSettings( name, false, machineName );
			return GetAppPoolState( settings ) == (int)AppPoolReturnCode.Started;
		}

		/// <summary>
		/// Creates a new AppPool.
		/// </summary>
		/// <param name="name">The AppPool name.</param>
		/// <param name="machineName">The server on which the AppPool will be created.</param>
		/// <param name="millisecondsTimeout">Timeout value on wait for Create to complete.</param>
		/// <returns>The Wmi ManagementPath</returns>
		public static ManagementPath Create(string name, string machineName, int millisecondsTimeout)
		{
			PutOptions options = new PutOptions();
			options.Timeout = System.TimeSpan.FromMinutes( Convert.ToDouble( millisecondsTimeout ) );

			IisAppPoolUtilWmiHelper helper = new IisAppPoolUtilWmiHelper( machineName );
			ManagementObject appPool = helper.CreateManagementObject( "IIsApplicationPoolSetting" );
			appPool.Properties["Name"].Value = string.Format( "W3SVC/AppPools/{0}", name );
			appPool.Properties["AppPoolIdentityType"].Value = 3;
			appPool.Properties["ManagedRuntimeVersion"].Value = "v4.0";

			return appPool.Put( options );
		}

		/// <summary>
		/// Deletes an AppPool.
		/// </summary>
		/// <param name="name">The AppPool name.</param>
		/// <param name="machineName">The server on which the AppPool is running.</param>
		/// <param name="millisecondsTimeout">Timeout value on wait for Delete to complete.</param>
		/// <returns>The Wmi ManagementPath</returns>
		public static void Delete(string name, string machineName, int millisecondsTimeout)
		{
			ManagementOptions options = new DeleteOptions();
			ManagementObject appPool = GetAppPoolManagementObject( name, machineName, millisecondsTimeout, ref options );
			appPool.Delete( (DeleteOptions)options );
		}

		/// <summary>
		/// Wmi query for IIsApplicationPool object.
		/// </summary>
		/// <param name="name">The AppPool name.</param>
		/// <param name="machineName">The server on which the AppPool is running.</param>
		/// <param name="millisecondsTimeout">Timeout value on ManagementOptions object.</param>
		/// <param name="options"></param>
		/// <returns>The Wmi AppPool ManagementObject</returns>
		static ManagementObject GetAppPoolManagementObject(string name, string machineName, int millisecondsTimeout, ref ManagementOptions options)
		{
			IisAppPoolUtilWmiHelper helper = new IisAppPoolUtilWmiHelper( machineName );
			ManagementObject appPool = helper.CreateManagementObject( "IIsApplicationPool" );
			appPool.Properties["Name"].Value = string.Format( "W3SVC/AppPools/{0}", name );
			options.Timeout = System.TimeSpan.FromMinutes( Convert.ToDouble( millisecondsTimeout ) );

			return appPool;
		}

		/// <summary>
		/// Wmi query for IIsApplicationPoolSetting for an AppPool.
		/// </summary>
		/// <param name="appPoolName">The AppPool name.</param>
		/// <param name="useWildcard">Uses a wildcard in searching for the AppPool by name ["%{0}%"].</param>
		/// <param name="machineName">The server on which the AppPool is running.</param>
		/// <returns>The Wmi ManagementObjectCollection for the AppPool.</returns>
		static ManagementObjectCollection GetAppPoolSettings(string appPoolName, bool useWildcard, string machineName)
		{
			if( useWildcard )
			{
				appPoolName = string.IsNullOrWhiteSpace( appPoolName ) ? "%" : string.Format( "%{0}%", appPoolName );
			}

			ObjectQuery query =
				new ObjectQuery( string.Format( "SELECT * FROM IIsApplicationPoolSetting WHERE Name = 'W3SVC/AppPools/{0}'", appPoolName ) );
			IisAppPoolUtilWmiHelper helper = new IisAppPoolUtilWmiHelper( machineName );

			return helper.Query( query, WmiPath.IIS );
		}

		/// <summary>
		/// Pulls AppPoolState from an IIsApplicationPoolSetting ManagementObjectCollection
		/// </summary>
		/// <param name="settings">An IIsApplicationPoolSetting ManagementObjectCollection</param>
		/// <returns>The integer value of AppPoolState</returns>
		static int GetAppPoolState(ManagementObjectCollection settings)
		{
			int state = 0;
			foreach( ManagementObject item in settings )
			{
				state = Convert.ToInt32( item["AppPoolState"] );
				break;
			}
			return state;
		}
	}

	class IisAppPoolUtilWmiHelper : WmiManagementBase
	{
		public IisAppPoolUtilWmiHelper(string host)
			: base( host, WmiPath.IIS ) { }
	}
}