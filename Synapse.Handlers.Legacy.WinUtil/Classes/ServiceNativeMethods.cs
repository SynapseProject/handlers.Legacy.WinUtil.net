using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text;

namespace Synapse.Handlers.Legacy.WinCore
{
	public class ServiceWin32Api
	{
		#region Win32 API enums/structs
		[Flags]
		public enum ServiceControlAccessRights : int
		{
			SC_MANAGER_CONNECT = 0x0001, // Required to connect to the service control manager. 
			SC_MANAGER_CREATE_SERVICE = 0x0002, // Required to call the CreateService function to create a service object and add it to the database. 
			SC_MANAGER_ENUMERATE_SERVICE = 0x0004, // Required to call the EnumServicesStatusEx function to list the services that are in the database. 
			SC_MANAGER_LOCK = 0x0008, // Required to call the LockServiceDatabase function to acquire a lock on the database. 
			SC_MANAGER_QUERY_LOCK_STATUS = 0x0010, // Required to call the QueryServiceLockStatus function to retrieve the lock status information for the database
			SC_MANAGER_MODIFY_BOOT_CONFIG = 0x0020, // Required to call the NotifyBootConfigStatus function. 
			SC_MANAGER_ALL_ACCESS = 0xF003F // Includes STANDARD_RIGHTS_REQUIRED, in addition to all access rights in this table. 
		}

		[Flags]
		public enum ServiceAccessRights : int
		{
			SERVICE_QUERY_CONFIG = 0x0001, // Required to call the QueryServiceConfig and QueryServiceConfig2 functions to query the service configuration. 
			SERVICE_CHANGE_CONFIG = 0x0002, // Required to call the ChangeServiceConfig or ChangeServiceConfig2 function to change the service configuration. Because this grants the caller the right to change the executable file that the system runs, it should be granted only to administrators. 
			SERVICE_QUERY_STATUS = 0x0004, // Required to call the QueryServiceStatusEx function to ask the service control manager about the status of the service. 
			SERVICE_ENUMERATE_DEPENDENTS = 0x0008, // Required to call the EnumDependentServices function to enumerate all the services dependent on the service. 
			SERVICE_START = 0x0010, // Required to call the StartService function to start the service. 
			SERVICE_STOP = 0x0020, // Required to call the ControlService function to stop the service. 
			SERVICE_PAUSE_CONTINUE = 0x0040, // Required to call the ControlService function to pause or continue the service. 
			SERVICE_INTERROGATE = 0x0080, // Required to call the ControlService function to ask the service to report its status immediately. 
			SERVICE_USER_DEFINED_CONTROL = 0x0100, // Required to call the ControlService function to specify a user-defined control code.
			SERVICE_ALL_ACCESS = 0xF01FF // Includes STANDARD_RIGHTS_REQUIRED in addition to all access rights in this table. 
		}

		public enum ServiceConfig2InfoLevel : int
		{
			SERVICE_CONFIG_DESCRIPTION = 0x00000001, // The lpBuffer parameter is a pointer to a SERVICE_DESCRIPTION structure.
			SERVICE_CONFIG_FAILURE_ACTIONS = 0x00000002 // The lpBuffer parameter is a pointer to a SERVICE_FAILURE_ACTIONS structure.
		}

		public enum SC_ACTION_TYPE : uint
		{
			SC_ACTION_NONE = 0x00000000, // No action.
			SC_ACTION_RESTART = 0x00000001, // Restart the service.
			SC_ACTION_REBOOT = 0x00000002, // Reboot the computer.
			SC_ACTION_RUN_COMMAND = 0x00000003 // Run a command.
		}

		public struct SERVICE_FAILURE_ACTIONS
		{
			[MarshalAs( UnmanagedType.U4 )]
			public UInt32 dwResetPeriod;
			[MarshalAs( UnmanagedType.LPStr )]
			public String lpRebootMsg;
			[MarshalAs( UnmanagedType.LPStr )]
			public String lpCommand;
			[MarshalAs( UnmanagedType.U4 )]
			public UInt32 cActions;
			public IntPtr lpsaActions;
		}

		public struct SC_ACTION
		{
			[MarshalAs( UnmanagedType.U4 )]
			public SC_ACTION_TYPE Type;
			[MarshalAs( UnmanagedType.U4 )]
			public UInt32 Delay;
		}
		#endregion

		[DllImport( "advapi32.dll", EntryPoint = "OpenSCManager" )]
		public static extern IntPtr OpenSCManager(
			string machineName,
			string databaseName,
			ServiceControlAccessRights desiredAccess);

		[DllImport( "advapi32.dll", EntryPoint = "CloseServiceHandle" )]
		public static extern int CloseServiceHandle(IntPtr hSCObject);

		[DllImport( "advapi32.dll", EntryPoint = "OpenService" )]
		public static extern IntPtr OpenService(
			IntPtr hSCManager,
			string serviceName,
			ServiceAccessRights desiredAccess);

		[DllImport( "advapi32.dll", EntryPoint = "QueryServiceConfig2" )]
		public static extern int QueryServiceConfig2(
			IntPtr hService,
			ServiceConfig2InfoLevel dwInfoLevel,
			IntPtr lpBuffer,
			int cbBufSize,
			out int pcbBytesNeeded);

		[StructLayout( LayoutKind.Sequential, CharSet = CharSet.Ansi )]
		public struct SERVICE_DESCRIPTION
		{
			public string lpDescription;
		}

		[DllImport( "advapi32.dll", EntryPoint = "ChangeServiceConfig2", SetLastError = true, CharSet = CharSet.Ansi )]
		[return: MarshalAs( UnmanagedType.Bool )]
		public static extern bool ChangeServiceConfig2(IntPtr hService,
			int dwInfoLevel, [MarshalAs( UnmanagedType.Struct )] ref SERVICE_DESCRIPTION lpInfo);

		[DllImport( "advapi32.dll", EntryPoint = "ChangeServiceConfig2" )]
		public static extern int ChangeServiceConfig2(
			IntPtr hService,
			ServiceConfig2InfoLevel dwInfoLevel,
			IntPtr lpInfo);
	}

	public class ServiceUtilWin32Helper : IDisposable
	{
		private IntPtr _scManager;
		private bool disposed;

		/// <summary>
		/// Initializes a new instance of the <see cref="ServiceUtilWin32Helper"/> class.
		/// </summary>
		/// <exception cref="ComponentModel.Win32Exception">"Unable to open Service Control Manager."</exception>
		[SecurityPermission( SecurityAction.LinkDemand, UnmanagedCode = true )]
		public ServiceUtilWin32Helper(string machineName)
		{
			// Open the service control manager
			_scManager = ServiceWin32Api.OpenSCManager(
				machineName,
				null,
				ServiceWin32Api.ServiceControlAccessRights.SC_MANAGER_CONNECT );

			// Verify if the SC is opened
			if( _scManager == IntPtr.Zero )
			{
				throw new Win32Exception( Marshal.GetLastWin32Error(), "Unable to open Service Control Manager." );
			}
		}

		/// <summary>
		/// Calls the Win32 OpenService function and performs error checking.
		/// </summary>
		/// <exception cref="ComponentModel.Win32Exception">"Unable to open the requested Service."</exception>
		private IntPtr OpenService(string serviceName, ServiceWin32Api.ServiceAccessRights desiredAccess)
		{
			// Open the service
			IntPtr service = ServiceWin32Api.OpenService(
				_scManager,
				serviceName,
				desiredAccess );

			// Verify if the service is opened
			if( service == IntPtr.Zero )
			{
				throw new Win32Exception( Marshal.GetLastWin32Error(), "Unable to open the requested Service." );
			}

			return service;
		}

		public string GetServiceDescription(string serviceName)
		{
			const int bufferSize = 1024 * 8;

			IntPtr service = IntPtr.Zero;
			IntPtr bufferPtr = IntPtr.Zero;

			try
			{
				// Open the service
				service = OpenService( serviceName, ServiceWin32Api.ServiceAccessRights.SERVICE_QUERY_CONFIG );

				int dwBytesNeeded = 0;

				// Allocate memory for struct
				bufferPtr = Marshal.AllocHGlobal( bufferSize );
				int queryResult = ServiceWin32Api.QueryServiceConfig2(
					service,
					ServiceWin32Api.ServiceConfig2InfoLevel.SERVICE_CONFIG_DESCRIPTION,
					bufferPtr,
					bufferSize,
					out dwBytesNeeded );

				if( queryResult == 0 )
				{
					throw new Win32Exception( Marshal.GetLastWin32Error(), "Unable to query the Service configuration." );
				}

				// Cast the buffer to a SERVICE_DESCRIPTION struct
				ServiceWin32Api.SERVICE_DESCRIPTION desc =
					(ServiceWin32Api.SERVICE_DESCRIPTION)Marshal.PtrToStructure( bufferPtr, typeof( ServiceWin32Api.SERVICE_DESCRIPTION ) );

				return desc.lpDescription;
			}
			finally
			{
				// Clean up
				if( bufferPtr != IntPtr.Zero )
				{
					Marshal.FreeHGlobal( bufferPtr );
				}

				if( service != IntPtr.Zero )
				{
					ServiceWin32Api.CloseServiceHandle( service );
				}
			}
		}

		public void SetServiceDescription(string serviceName, string description)
		{
			IntPtr service = IntPtr.Zero;

			try
			{
				ServiceWin32Api.SERVICE_DESCRIPTION desc = new ServiceWin32Api.SERVICE_DESCRIPTION
				{
					lpDescription = description
				};

				// Open the service
				service = OpenService( serviceName, ServiceWin32Api.ServiceAccessRights.SERVICE_CHANGE_CONFIG );

				bool ok = ServiceWin32Api.ChangeServiceConfig2(
					service,
					(int)ServiceWin32Api.ServiceConfig2InfoLevel.SERVICE_CONFIG_DESCRIPTION,
					ref desc );

				// Check that the change occurred
				if( !ok )
				{
					throw new Win32Exception( Marshal.GetLastWin32Error(), "Unable to change the Service configuration." );
				}
			}
			finally
			{
				if( service != IntPtr.Zero )
				{
					ServiceWin32Api.CloseServiceHandle( service );
				}
			}
		}


		#region IDisposable Members
		/// <summary>
		/// See <see cref="IDisposable.Dispose"/>.
		/// </summary>
		public void Dispose()
		{
			Dispose( true );
			GC.SuppressFinalize( this );
		}

		/// <summary>
		/// Implements the Dispose(bool) pattern outlined by MSDN and enforced by FxCop.
		/// </summary>
		private void Dispose(bool disposing)
		{
			if( !this.disposed )
			{
				if( disposing )
				{
					// Dispose managed resources here
				}

				// Unmanaged resources always need disposing
				if( _scManager != IntPtr.Zero )
				{
					ServiceWin32Api.CloseServiceHandle( _scManager );
					_scManager = IntPtr.Zero;
				}
			}
			disposed = true;
		}

		/// <summary>
		/// Finalizer for the <see cref="ServiceUtilWin32Helper"/> class.
		/// </summary>
		~ServiceUtilWin32Helper()
		{
			Dispose( false );
		}
		#endregion
	}
}