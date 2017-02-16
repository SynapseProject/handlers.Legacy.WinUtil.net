using System;
using System.Diagnostics;
using System.Management;
using System.ServiceProcess;

namespace Synapse.Handlers.Legacy.WinCore
{
	public class ServiceUtil
	{
		/// <summary>
		/// Queries the status and other configuration properties of a service.
		/// </summary>
		/// <param name="serviceName">The internal service name. Ex:"Print Spooler" is "Spooler".</param>
		/// <param name="machineName">The server on which the service is running.</param>
		/// <returns>The service configuration details.</returns>
		public static ServiceConfig QueryStatus(string serviceName, string machineName)
		{
			ServiceConfig config = new ServiceConfig()
			{
				ServiceName = serviceName,
				ServerName = machineName
			};

			ManagementObjectCollection services = GetServicesByServiceName( serviceName, machineName );
			foreach( ManagementObject service in services )
			{
				config.State = service.GetPropertyValue( "State" ).ToString();
				config.AcceptStop = bool.Parse( service.GetPropertyValue( "AcceptStop" ).ToString() );
				config.DisplayName = service.GetPropertyValue( "DisplayName" ).ToString();
				config.LogOnAs = service.GetPropertyValue( "StartName" ).ToString();
				config.PathName = service.GetPropertyValue( "PathName" ).ToString();
				config.ProcessId = int.Parse( service.GetPropertyValue( "ProcessId" ).ToString() );

				string startMode = service.GetPropertyValue( "StartMode" ).ToString();
				if( startMode == "Auto" ) { startMode = "Automatic"; }
				config.StartMode = (ServiceStartMode)Enum.Parse( typeof( ServiceStartMode ), startMode );
			}

			try
			{
				ServiceUtilWin32Helper svcUtil = new ServiceUtilWin32Helper( machineName );
				config.Description = svcUtil.GetServiceDescription( serviceName );
			}
			catch { } //eat the error

			return config;
		}

		/// <summary>
		/// Starts a Windows service.
		/// </summary>
		/// <param name="serviceName">The internal service name. Ex:"Print Spooler" is "Spooler".</param>
		/// <param name="machineName">The server on which the service is running.</param>
		/// <param name="millisecondsTimeout">Timeout on waiting for Running status.  Values less than or equal to zero will wait infinitely, positive value equals milliseconds to wait.</param>
		/// <param name="desiredStartMode">Sets the StartMode of the service.</param>
		/// <returns>True if status == Running, otherwise false.</returns>
		public static bool Start(string serviceName, string machineName, int millisecondsTimeout, ServiceStartMode desiredStartMode = ServiceStartMode.Unchanged)
		{
			ServiceController sc = new ServiceController( serviceName, machineName );
			try
			{
				sc.Start();
				if( millisecondsTimeout > 0 )
				{
					sc.WaitForStatus( ServiceControllerStatus.Running, TimeSpan.FromMilliseconds( millisecondsTimeout ) );
				}
				else
				{
					sc.WaitForStatus( ServiceControllerStatus.Running );
				}
			}
			catch( Exception )
			{
				//this just eats the exception and returns bool for success state.
				//possible exceptions are service is pending start or already running
			}

			if( desiredStartMode != ServiceStartMode.Unchanged )
			{
				ChangeStartMode( sc.ServiceName, machineName, desiredStartMode );
			}


			return sc.Status == ServiceControllerStatus.Running;
		}

		/// <summary>
		/// Stops a Windows service.
		/// </summary>
		/// <param name="serviceName">The internal service name. Ex:"Print Spooler" is "Spooler".</param>
		/// <param name="machineName">The server on which the service is running.</param>
		/// <param name="millisecondsTimeout">Timeout until PId kill.  Negative value will issue immediate kill, 0 will wait infinitely, positive value equals milliseconds to wait.</param>
		/// <param name="desiredStartMode">Sets the StartMode of the service.</param>
		/// <returns>True if status == Stopped, otherwise false.</returns>
		public static bool Stop(string serviceName, string machineName, int millisecondsTimeout, ServiceStartMode desiredStartMode = ServiceStartMode.Unchanged)
		{
			ServiceController sc = new ServiceController( serviceName, machineName );
			ServiceConfig svc = QueryStatus( serviceName, machineName );

			Stopwatch waitTimeElapsed = new Stopwatch();

			if( millisecondsTimeout < 0 )
			{
				KillProcesses( svc.ProcessId, machineName );
			}
			else
			{
				if( sc.CanStop )
				{
					sc.Stop();
					if( millisecondsTimeout > 0 )
					{
						try
						{
							waitTimeElapsed.Start();
							sc.WaitForStatus( ServiceControllerStatus.Stopped, TimeSpan.FromMilliseconds( millisecondsTimeout ) );
							waitTimeElapsed.Stop();
						}
						catch( Exception )
						{
							waitTimeElapsed.Stop();
							KillProcesses( svc.ProcessId, machineName );
						}
					}
					else
					{
						sc.WaitForStatus( ServiceControllerStatus.Stopped );
					}
				}
			}

			//if the PId is still around, run out the clock and kill it.
			string processName = System.IO.Path.GetFileName( svc.PathName.Replace( "\"", string.Empty ) );
			int pid = GetProcessesByProcessNamePId( processName, svc.ProcessId, machineName );
			long waitTimeRemaining = (long)millisecondsTimeout - waitTimeElapsed.ElapsedMilliseconds;
			if( pid != 0 )
			{
				if( waitTimeRemaining > 0 )
				{
					System.Threading.Thread.Sleep( (int)waitTimeRemaining );
				}

				pid = GetProcessesByProcessNamePId( processName, svc.ProcessId, machineName );
				if( pid != 0 )
				{
					KillProcesses( svc.ProcessId, machineName );
				}
			}

			if( desiredStartMode != ServiceStartMode.Unchanged )
			{
				ChangeStartMode( sc.ServiceName, machineName, desiredStartMode );
			}

			return sc.Status == ServiceControllerStatus.Stopped;
		}

		/// <summary>
		/// Finds a service by name, gets it's PId, kills the PId.
		/// </summary>
		/// <param name="serviceName">The internal service name. Ex:"Print Spooler" is "Spooler".</param>
		/// <param name="machineName">The server on which the service is running.</param>
		static void KillProcesses(int pid, string machineName)
		{
			try
			{
				ManagementObjectCollection processes = GetProcessesByPId( pid, machineName );
				foreach( ManagementObject process in processes )
				{
					KillProcessTree( process, machineName );
				}
			}
			catch( ManagementException mex )
			{
				//if ErrorCode == ManagementStatus.NotFound, eat the error.
				//Somehow the PId is not there anymore.
				if( mex.ErrorCode != ManagementStatus.NotFound )
				{
					throw mex;
				}
			}
		}

		static void KillProcessTree(ManagementObject process, string machineName)
		{
			ManagementObjectCollection processes = GetProcessesByParentId( Convert.ToInt32( process["ProcessId"].ToString() ), machineName );
			foreach( ManagementObject child in processes )
			{
				KillProcessTree( child, machineName );
			}

			ManagementBaseObject inParams = process.GetMethodParameters( "Terminate" );
			InvokeMethodOptions options = new InvokeMethodOptions();
			inParams["Reason"] = 0;
			options.Timeout = System.TimeSpan.FromMinutes( 0 );
			ManagementBaseObject mbo = process.InvokeMethod( "Terminate", inParams, options );
		}

		/// <summary>
		/// Sets the StartMode of a Windows service.
		/// </summary>
		/// <param name="serviceName">The internal service name. Ex:"Print Spooler" is "Spooler".</param>
		/// <param name="machineName">The server on which the service is running.</param>
		/// <param name="startMode">The value to set.</param>
		/// <returns>Enumerated value as returned from Wmi call.</returns>
		public static ServiceReturnCode ChangeStartMode(string serviceName, string machineName, ServiceStartMode startMode)
		{
			ServiceReturnCode result = ServiceReturnCode.UnknownFailure;

			ManagementObjectCollection services = GetServicesByServiceName( serviceName, machineName );
			foreach( ManagementObject service in services )
			{
				ManagementBaseObject inparms = service.GetMethodParameters( "ChangeStartMode" );
				inparms["StartMode"] = startMode.ToString();
				ManagementBaseObject mbo = service.InvokeMethod( "ChangeStartMode", inparms, null );
				result = (ServiceReturnCode)Enum.Parse( typeof( ServiceReturnCode ), mbo["ReturnValue"].ToString() );
			}

			return result;
		}

		public static ServiceReturnCode CreateService(string serviceName, string machineName, string displayName, string pathName, ServiceStartMode startMode,
			string startName = null, string password = null, string parameters = null, 
			WindowsServiceType serviceType = WindowsServiceType.OwnProcess, ErrorControlAction errorControl = ErrorControlAction.UserIsNotified,
			bool interactWithDesktop = false, string loadOrderGroup = null,
			string[] loadOrderGroupDependencies = null, string[] svcDependencies = null)
		{
			ServiceReturnCode result = ServiceReturnCode.UnknownFailure;

			ServiceUtilWmiHelper helper = new ServiceUtilWmiHelper( machineName );
			ManagementClass mc = helper.GetManagementClass( "Win32_Service" );
			ManagementBaseObject inparms = mc.GetMethodParameters( "Create" );
            string execPath = pathName;

			if( startMode == ServiceStartMode.Unchanged )
			{
				startMode = ServiceStartMode.Automatic;
			}

            if (!string.IsNullOrEmpty(parameters))
            {
                execPath = string.Format("{0} {1}", pathName, parameters);
            }

			inparms["Name"] = serviceName;
			inparms["DisplayName"] = displayName;
			inparms["PathName"] = execPath;
			inparms["ServiceType"] = serviceType;
			inparms["ErrorControl"] = errorControl;
			inparms["StartMode"] = startMode.ToString();
			inparms["DesktopInteract"] = interactWithDesktop;
			inparms["StartName"] = startName;
			inparms["StartPassword"] = password;
			inparms["LoadOrderGroup"] = loadOrderGroup;
			inparms["LoadOrderGroupDependencies"] = loadOrderGroupDependencies;
			inparms["ServiceDependencies"] = svcDependencies;

			try
			{
				ManagementBaseObject mbo = mc.InvokeMethod( "Create", inparms, null );
				result = (ServiceReturnCode)Enum.Parse( typeof( ServiceReturnCode ), mbo["ReturnValue"].ToString() );

				if( result == ServiceReturnCode.Success )
				{
					UpdateServiceDescriptionWithVersion( serviceName, machineName, pathName );
				}
			}
			catch( Exception ex )
			{
				throw ex;
			}

			return result;
		}

		static void UpdateServiceDescriptionWithVersion(string serviceName, string machineName, string path)
		{
			const string sv = "Service Version: ";
			string filepath =
				Alphaleonis.Win32.Filesystem.Path.GetFullPath( "\\\\" + machineName + "\\" + path.Replace( ':', '$' ) );
			FileVersionInfo fvi = FileVersionInfo.GetVersionInfo( filepath );

			ServiceUtilWin32Helper svcUtil = new ServiceUtilWin32Helper( machineName );
			string desc = svcUtil.GetServiceDescription( serviceName );

			if( !string.IsNullOrWhiteSpace( desc ) )
			{
				int pos = desc.IndexOf( sv );
				if( pos >= 0 )
				{
					desc = desc.Remove( pos, desc.Length - pos );
				}
				if( desc.Length > 0 && desc[desc.Length - 1] != ' ' )
				{
					desc += " ";
				}
			}

			desc = string.Format( "{0}{1}{2}", desc, sv, fvi.FileVersion );
			svcUtil.SetServiceDescription( serviceName, desc );
		}

		public static ServiceReturnCode DeleteService(string serviceName, string machineName)
		{
			ServiceReturnCode result = ServiceReturnCode.UnknownFailure;

			ServiceUtilWmiHelper helper = new ServiceUtilWmiHelper( machineName );
			ManagementObject service = helper.GetInstance( string.Format( "Win32_Service.Name='{0}'", serviceName ) );

			try
			{
				ManagementBaseObject mbo = service.InvokeMethod( "Delete", null, null );

				result = (ServiceReturnCode)Enum.Parse( typeof( ServiceReturnCode ), mbo["ReturnValue"].ToString() );
			}
			catch( ManagementException mex )
			{
				//if ErrorCode == ManagementStatus.NotFound, eat the error.
				result = ServiceReturnCode.ServiceNotFound;

				//otherwise, throw whatever happened
				if( mex.ErrorCode != ManagementStatus.NotFound )
				{
					throw mex;
				}
			}

			return result;
		}

		#region utility functions
		static ManagementObjectCollection GetServicesByServiceName(string serviceName, string machineName)
		{
			ObjectQuery query = new ObjectQuery( string.Format(
				"SELECT Name, DisplayName, PathName, StartName, ProcessId, State, Started, StartMode, AcceptStop FROM Win32_Service WHERE name = '{0}'", serviceName ) );
			ServiceUtilWmiHelper helper = new ServiceUtilWmiHelper( machineName );
			return helper.Query( query, WmiPath.Root );
		}

		static ManagementObjectCollection GetProcessesByPId(int pid, string machineName)
		{
			ObjectQuery query = new ObjectQuery(
				string.Format( "SELECT * FROM Win32_Process WHERE ProcessId = '{0}'", (int)((UInt32)pid) ) );
			ServiceUtilWmiHelper helper = new ServiceUtilWmiHelper( machineName );
			return helper.Query( query, WmiPath.Root );
		}

		static ManagementObjectCollection GetProcessesByParentId(int pid, string machineName)
		{
			ObjectQuery query = new ObjectQuery(
				string.Format( "SELECT * FROM Win32_Process WHERE ParentProcessId = '{0}'", (int)((UInt32)pid) ) );
			ServiceUtilWmiHelper helper = new ServiceUtilWmiHelper( machineName );
			return helper.Query( query, WmiPath.Root );
		}

		//query by ProcessName _and_ PId to guarantee it's the exact same Process & PId
		//	(not a new PId with diff app, in case PId already got re-used).
		static int GetProcessesByProcessNamePId(string processName, int pid, string machineName)
		{
			int processId = 0;

			ObjectQuery query = new ObjectQuery(
				string.Format( "SELECT ProcessId FROM Win32_Process WHERE Name = '{0}' AND ProcessId = '{1}'", processName, pid ) );
			ServiceUtilWmiHelper helper = new ServiceUtilWmiHelper( machineName );
			ManagementObjectCollection processes = helper.Query( query, WmiPath.Root );

			foreach( ManagementObject process in processes )
			{
				object prId = process.GetPropertyValue( "ProcessId" );
				try { processId = (int)((UInt32)prId); }
				catch { }
			}

			return processId;
		}
		#endregion
	}

	class ServiceUtilWmiHelper : WmiManagementBase
	{
		public ServiceUtilWmiHelper(string host)
			: base( host, WmiPath.Root ) { }
	}
}