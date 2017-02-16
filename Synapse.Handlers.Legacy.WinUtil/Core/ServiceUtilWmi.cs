using System;
using System.Threading;
using System.Management;
using System.Configuration;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Sentosa.CommandCenter.Adapters.WinProc.Core
{
	/// <summary>
	/// this class is deprecated and only in the project for code comparison; to be deleted
	/// </summary>
	internal class ServiceUtilWmi : WmiManagementBase
	{
		internal ServiceUtilWmi(string host)
			: base( host, WmiPath.Root )
		{
		}

		public string StartService(string serviceName, int timeout, int retryAttempts, int retryWaitMilliseconds)
		{
			string result = string.Empty;
			string host = Scope.Path.Server;
			ManagementObjectCollection queryCollection = null;

			string retCode = string.Empty;
			bool svcStarted = false;
			string svcStartMode = string.Empty;

			try
			{
				ObjectQuery query = new ObjectQuery( "SELECT Name,ProcessId,State,Started, StartMode FROM Win32_service WHERE name='" + serviceName + "'" );

				ManagementObjectCollection queryColl = Query( query, WmiPath.Root );
				int mgmtObjCount = queryColl.Count;
				queryCollection = Query( query, WmiPath.Root );

				if( mgmtObjCount > 0 )
				{

					foreach( ManagementObject service in queryCollection )
					{
						svcStartMode = service.GetValue( "StartMode" );
						if( service["Started"].Equals( false ) )
						{
							InvokeMethodOptions options = new InvokeMethodOptions();
							options.Timeout = System.TimeSpan.FromMinutes( 0 ); //spaul-changed this to 0 since we're manually handling it.
							ManagementBaseObject mbo = service.InvokeMethod( "StartService", null, options );


							retCode = Enum.GetName( typeof( ServiceReturnCode ), mbo["returnValue"] );

							result = string.Format( "{0}|{1}", retCode, service.GetValue( "StartMode" ) );
						}
						else
						{
							result = string.Format( "{0}|{1}", "ServiceAlreadyRunning", service.GetValue( "StartMode" ) );
						}
					}

					int retryCount = 0;
					string state = string.Empty;

					while( retryCount < retryAttempts && !svcStarted )
					{
						Thread.Sleep( retryWaitMilliseconds ); //sleeping for 5 seconds
						queryCollection = Query( query, WmiPath.Root );
						foreach( ManagementObject service in queryCollection )
						{
							state = service.GetValueLower( "State" );
						}
						if( (result.ToLower().Equals( "success" )) && (!state.Equals( "running" ) || !state.Equals( "started" )) )
						{
							svcStarted = false;
							retryCount++;
						}
						else
						{
							retCode = "Success";
							result = string.Format( "{0}|{1}", retCode, svcStartMode );
							svcStarted = true;
							break;
						}
						retryCount++;
						if( svcStarted )
						{
							break;
						}
					}
				}
				else
				{
					string requestStatus = Enum.GetName( typeof( ServiceReturnCode ), ServiceReturnCode.ServiceNotFound );
					result = string.Format( "{0}|{1}", requestStatus, "ServiceNotFound" );

				}
			}
			catch( InvalidOperationException ioex )
			{
				throw ioex;
			}
			catch( Exception ex )
			{
				throw ex;
			}
			return result;
		}

		/// <summary>
		/// StopService
		/// if timeout value is not provided- we'll wait for 120 seconds, before terminating the process behind the service
		/// if timeout value is 0 -   we'll never terminate the Process
		/// if timeout value is > 0 -   we'll  terminate the Process at teh timeoout value specified
		/// </summary>
		/// <param name="serviceName"></param>
		/// <param name="timeoutSeconds"></param>
		/// <returns></returns>
		public string StopService(string serviceName, int timeout, int retryAttempts, int retryWaitMilliseconds)
		{
			string result = string.Empty;
			string host = Scope.Path.Server;
			string processId = string.Empty;
			ObjectQuery query = null;
			bool serviceStopped = false;
			string state = string.Empty;
			string actionReturnCode = string.Empty;
			string processStartMode = string.Empty;
			bool canAcceptStop = false;
			ManagementObjectCollection queryCollection = null;
			string serviceStopResult = string.Empty;
			string requestStatus = string.Empty;

			try
			{
				query = new ObjectQuery( "SELECT Name,ProcessId,State,Started, StartMode, AcceptStop FROM Win32_Service WHERE name='" + serviceName + "'" );
				ManagementObjectCollection queryColl = Query( query, WmiPath.Root );
				int mgmtObjCount = queryColl.Count;
				queryCollection = Query( query, WmiPath.Root );
				if( mgmtObjCount <= 0 )
				{
					requestStatus = Enum.GetName( typeof( ServiceReturnCode ), ServiceReturnCode.ServiceNotFound );
					state = Enum.GetName( typeof( ServiceReturnCode ), ServiceReturnCode.ServiceNotFound );
					serviceStopResult = "ServiceStopFailed";
					result = string.Format( "{0}|{1}|{2}|{3}", requestStatus, processStartMode, state, serviceStopResult );
				}
				else
				{
					foreach( ManagementObject service in queryCollection )
					{
						processStartMode = service.GetValue( "StartMode" );
						state = service.GetValue( "State" );
						processId = service.GetValue( "ProcessId" );
						canAcceptStop = Boolean.Parse( service.GetValue( "AcceptStop" ) );
						if( canAcceptStop.Equals( true ) && (service.GetValueLower( "State" ).Equals( "running" ) || service.GetValueLower( "State" ).Equals( "started" )) )
						{
							InvokeMethodOptions options = new InvokeMethodOptions();
							options.Timeout = System.TimeSpan.FromMilliseconds( timeout );
							ManagementBaseObject mbo = service.InvokeMethod( "StopService", null, options );
							actionReturnCode = Enum.GetName( typeof( ServiceReturnCode ), mbo["returnValue"] );

							result = string.Format( "{0}|{1}|{2}|{3}", Enum.GetName( typeof( ServiceReturnCode ), mbo["returnValue"] ), processStartMode, state, state );
						}
						else if( service.GetValueLower( "State" ) == "stopped" )
						{
							requestStatus = "ServiceAlreadyStopped";
							serviceStopResult = "Stopped";
							result = string.Format( "{0}|{1}|{2}|{3}", requestStatus, processStartMode, state, serviceStopResult );
							serviceStopped = true;
						}
						else if( service.GetValueLower( "State" ).Replace( " ", "" ) == "stoppending" || service.GetValueLower( "State" ).Replace( " ", "" ) == "stopping" )
						{
							requestStatus = "StopPending";
							serviceStopResult = "ServiceStopping";
							result = string.Format( "{0}|{1}|{2}|{3}", requestStatus, processStartMode, state, serviceStopResult );
							serviceStopped = false;
						}
						else
						{
							requestStatus = "Servicecannotacceptstoprequestatthistime";
							serviceStopResult = "ServiceCannotBeStopped";
							result = string.Format( "{0}|{1}|{2}|{3}", requestStatus, processStartMode, state, serviceStopResult );
						}
					}

					if( !serviceStopped )
					{
						//poll-requery to check if the svc has stopped
						serviceStopped = PollForServiceStop( serviceName, timeout, retryAttempts, retryWaitMilliseconds, ref state, ref processStartMode );
						if( !state.Trim().ToLower().Equals( "stopped" ) )
						{
							if( timeout != 0 )
							{
								//Terminate Process , since the service has not been stopped yet
								if( processId != string.Empty )
								{
									result = TerminateProcess( "", processId.Trim() );
									//poll to check if service has been ternminated
									serviceStopped = PollForServiceStop( serviceName, timeout, retryAttempts, retryWaitMilliseconds, ref state, ref processStartMode );

									requestStatus = actionReturnCode;
									serviceStopResult = "Terminated";
									result = string.Format( "{0}|{1}|{2}|{3}", requestStatus, processStartMode, state, serviceStopResult );
								}
							}
							else
							{
								requestStatus = Enum.GetName( typeof( ServiceReturnCode ), ServiceReturnCode.ServiceRequestTimeout );
								serviceStopResult = "ServiceStopFailed";
								result = string.Format( "{0}|{1}|{2}|{3}", requestStatus, processStartMode, state, serviceStopResult );
							}
						}
					}
				}

			}
			catch( InvalidOperationException ioex )
			{
				throw ioex;
			}
			catch( Exception ex )
			{
				throw ex;
			}
			return result;

		}

		public List<string> GetAllProcess()
		{
			ObjectQuery query = null;

			List<string> result = new List<string>();

			try
			{
				query = new ObjectQuery( "SELECT ProcessID,PathName,State,StartMode,StartName FROM Win32_process" );


				ManagementObjectCollection collection = Query( query, WmiPath.Root );
				ManagementObjectCollection.ManagementObjectEnumerator en = collection.GetEnumerator();

				while( en.MoveNext() )
				{
					ManagementObject obj = (ManagementObject)en.Current;

					//ProcessID
					string processId = obj.GetValue( "ProcessId" );

					//processName from PathName
					string processName = string.Empty;
					string pathName = obj.GetValue( "PathName" );
					string[] s = new string[] { "\\" };
					string[] arrPath = pathName.Split( s, StringSplitOptions.RemoveEmptyEntries );
					foreach( string tempStr in arrPath )
					{
						if( tempStr.Contains( ".exe" ) )
						{
							processName = tempStr.Substring( Int32.Parse( "0" ), tempStr.IndexOf( ".exe" ) + Int32.Parse( "4" ) );
						}
					}
					//State
					string state = obj.GetValue( "State" );

					//StartMode
					string startMode = obj.GetValue( "StartMode" );

					//StartName
					string startName = obj.GetValue( "StartName" );

					result.Add( processId + "|" + processName );
				}
				return result;
			}
			catch( Exception ex )
			{
				throw ex;
			}
		}

		public string GetProcessId(string serviceName)
		{
			string processid = string.Empty;
			string processname = string.Empty;
			this.GetProcess( serviceName, out processid, out processname );

			return processid;
		}

		public string GetProcessName(string serviceName)
		{
			string processid = string.Empty;
			string processname = string.Empty;
			this.GetProcess( serviceName, out processid, out processname );

			return processname;
		}

		void GetProcess(string serviceName, out string processId, out string processName)
		{
			string host = Scope.Path.Server;
			processId = string.Empty;
			processName = string.Empty;

			try
			{
				ObjectQuery query = new ObjectQuery( "SELECT ProcessId,PathName FROM Win32_Service WHERE name='" + serviceName + "'" );
				ManagementObjectCollection queryCollection = Query( query, WmiPath.Root );
				ManagementObjectCollection.ManagementObjectEnumerator en = queryCollection.GetEnumerator();

				while( en.MoveNext() )
				{
					ManagementObject obj = (ManagementObject)en.Current;
					processId = obj.GetValue( "ProcessId" );
					string pathName = obj.GetValue( "PathName" );

					string[] s = new string[] { "\\" };
					string[] arrPath = pathName.Split( s, StringSplitOptions.RemoveEmptyEntries );

					foreach( string tempStr in arrPath )
					{
						if( tempStr.Contains( ".exe" ) )
						{
							processName = tempStr.Substring( Int32.Parse( "0" ), tempStr.IndexOf( ".exe" ) + Int32.Parse( "4" ) );
						}
					}
				}
			}
			catch( Exception ex )
			{
				throw ex;
			}
		}

		public string GetProcessStatus(string serviceName)
		{
			string host = Scope.Path.Server;
			string processId = string.Empty;
			string processName = string.Empty;
			string processState = string.Empty;
			string startMode = string.Empty;
			ManagementObjectCollection queryCollection = null;
			try
			{
				ObjectQuery query = new ObjectQuery( "SELECT ProcessId,PathName,State,StartMode,Status FROM Win32_Service WHERE name='" + serviceName + "'" );
				ManagementObjectCollection queryColl = Query( query, WmiPath.Root );
				int mgmtObjCount = queryColl.Count;
				queryCollection = Query( query, WmiPath.Root );
				if( mgmtObjCount > 0 )
				{
					ManagementObjectCollection.ManagementObjectEnumerator en = queryCollection.GetEnumerator();
					while( en.MoveNext() )
					{
						ManagementObject obj = (ManagementObject)en.Current;
						processId = obj.GetValue( "ProcessId" );
						string pathName = obj.GetValue( "PathName" );
						processState = obj.GetValue( "State" );
						if( obj.GetValue( "StartMode" ) != "" )
						{
							startMode = obj.GetValue( "StartMode" );
						}
						string[] s = new string[] { "\\" };
						string[] arrPath = pathName.Split( s, StringSplitOptions.RemoveEmptyEntries );
						foreach( string tempStr in arrPath )
						{
							if( tempStr.Contains( ".exe" ) )
							{
								processName = tempStr.Substring( Int32.Parse( "0" ), tempStr.IndexOf( ".exe" ) + Int32.Parse( "4" ) );
							}
						}
					}
				}
				else
				{
					processState = Enum.GetName( typeof( ServiceReturnCode ), ServiceReturnCode.ServiceNotFound );
					processName = serviceName;
					startMode = "ServiceNotFound";
				}
				return string.Format( "{0}|{1}|{2}", processName, processState, startMode );
			}
			catch( Exception ex )
			{
				throw ex;
			}
		}

		public string GetProcessDetailsByServiceName(string serviceName)
		{
			string host = Scope.Path.Server;

			string processId = string.Empty;
			string processName = string.Empty;
			string processState = string.Empty;
			string startMode = string.Empty;


			try
			{
				ObjectQuery query = new ObjectQuery( "SELECT ProcessId,PathName,State,StartMode,Status FROM Win32_Service WHERE name='" + serviceName + "'" );
				ManagementObjectCollection queryCollection = Query( query, WmiPath.Root );
				ManagementObjectCollection.ManagementObjectEnumerator en = queryCollection.GetEnumerator();

				while( en.MoveNext() )
				{
					ManagementObject obj = (ManagementObject)en.Current;
					processId = obj.GetValue( "ProcessId" );
					string pathName = obj.GetValue( "PathName" );
					processState = obj.GetValue( "State" );
					if( obj.GetValue( "StartMode" ) != "" )
					{
						startMode = obj.GetValue( "StartMode" );
					}

					string[] s = new string[] { "\\" };
					string[] arrPath = pathName.Split( s, StringSplitOptions.RemoveEmptyEntries );

					foreach( string tempStr in arrPath )
					{
						if( tempStr.Contains( ".exe" ) )
						{
							processName = tempStr.Substring( Int32.Parse( "0" ), tempStr.IndexOf( ".exe" ) + Int32.Parse( "4" ) );
						}
					}
				}


				ObjectQuery queryProcess = null;

				if( !processId.Equals( string.Empty ) && !processName.Equals( string.Empty ) )
				{
					queryProcess = new ObjectQuery( "SELECT * FROM Win32_Process WHERE name='" + processName + "' AND ProcessId=" + processId );
				}
				else if( processId.Equals( string.Empty ) && !processName.Equals( string.Empty ) )
				{
					queryProcess = new ObjectQuery( "SELECT * FROM Win32_Process WHERE name='" + processName + "'" );
				}
				else if( !processId.Equals( string.Empty ) && processName.Equals( string.Empty ) )
				{
					queryProcess = new ObjectQuery( "SELECT * FROM Win32_Process WHERE ProcessId='" + processId + "'" );
				}
				else
				{
					throw new Exception( "ProcessName and Processid are empty" );
				}


				ManagementObjectCollection queryProcessCollection = Query( queryProcess, WmiPath.Root );

				foreach( ManagementObject process in queryProcessCollection )
				{
					foreach( PropertyData propData in process.Properties )
					{
						//log.Info( string.Format( "\n ProcessProperties {0} : {1}", propData.Name.ToString(), propData.Value.ToString() ) );
					}
				}
				return string.Format( "{0}|{1}|{2}", processName, processState, startMode );
			}
			catch( Exception ex )
			{
				throw ex;
			}
		}

		public string TerminateProcess(string processName, string processId)
		{
			string result = string.Empty;
			string host = Scope.Path.Server;
			try
			{
				ObjectQuery query;
				if( !processId.Equals( string.Empty ) && !processName.Equals( string.Empty ) )
				{
					query = new ObjectQuery( "SELECT * FROM Win32_Process WHERE name='" + processName + "' AND ProcessId=" + processId );
				}
				else if( processId.Equals( string.Empty ) && !processName.Equals( string.Empty ) )
				{
					query = new ObjectQuery( "SELECT * FROM Win32_Process WHERE name='" + processName + "'" );
				}
				else if( !processId.Equals( string.Empty ) && processName.Equals( string.Empty ) )
				{
					query = new ObjectQuery( "SELECT * FROM Win32_Process WHERE ProcessId='" + processId + "'" );
				}
				else
				{
					throw new Exception( "ProcessName and Processid are empty" );
				}
				ManagementObjectCollection queryCollection = Query( query, WmiPath.Root );
				foreach( ManagementObject process in queryCollection )
				{
					ManagementBaseObject inParams = process.GetMethodParameters( "Terminate" );
					InvokeMethodOptions options = new InvokeMethodOptions();
					inParams["Reason"] = 0;
					options.Timeout = System.TimeSpan.FromMinutes( Convert.ToDouble( 0 ) ); //spaul-changed this - handling this in code
					ManagementBaseObject mbo = process.InvokeMethod( "Terminate", inParams, options );
					result = Enum.GetName( typeof( ServiceReturnCode ), mbo["returnValue"] );
				}
			}
			catch( InvalidOperationException ioex )
			{
				throw ioex;
			}
			catch( Exception ex )
			{
				throw ex;
			}

			return result;
		}

		bool PollForServiceStop(string svcName, int timeout, int retryAttempts, int retryWaitMilliseconds, ref string svcState, ref string svcStartMode)
		{
			bool svcStopped = false;
			string host = Scope.Path.Server;
			int retryCount = 0;

			ObjectQuery query = new ObjectQuery( "SELECT Name,ProcessId,State,Started,StartMode, AcceptStop FROM Win32_Service WHERE name='" + svcName + "'" );


			ManagementObjectCollection queryCollection = null;

			while( retryCount < retryAttempts && !svcStopped )
			{
				Thread.Sleep( retryWaitMilliseconds );
				queryCollection = Query( query, WmiPath.Root );

				foreach( ManagementObject service in queryCollection )
				{
					svcState = service.GetValueLower( "State" );
					svcStartMode = service.GetValueLower( "StartMode" );
				}
				if( !svcState.Equals( "stopped" ) || svcState.Replace( " ", "" ).Equals( "stoppending" ) )
				{
					svcStopped = false;
				}
				else if( svcState.Equals( "stopped" ) )
				{
					svcStopped = true;
					break;
				}
				else
				{
					svcStopped = false;
				}
				retryCount++;
				if( svcStopped )
				{
					break;
				}
			}

			return svcStopped;
		}


		public ServiceReturnCode ChangeStartMode(string svcName, ServiceStartMode startMode)
		{
			ServiceReturnCode retCode = ServiceReturnCode.UnknownFailure;
			string host = Scope.Path.Server;
			try
			{
				ObjectQuery query = new ObjectQuery( "SELECT Name,ProcessId,State,Started, StartMode FROM Win32_service WHERE name='" + svcName + "'" );
				ManagementObjectCollection queryCollection = Query( query, WmiPath.Root );

				foreach( ManagementObject service in queryCollection )
				{
					ManagementBaseObject mboIn = service.GetMethodParameters( "ChangeStartMode" );
					mboIn["StartMode"] = startMode.ToString();
					ManagementBaseObject mbo = service.InvokeMethod( "ChangeStartMode", mboIn, null );
					retCode = (ServiceReturnCode)Enum.Parse( typeof( ServiceReturnCode ), mbo["ReturnValue"].ToString() );
				}
			}
			catch( Exception ex )
			{
				throw ex;
			}
			return retCode;
		}
	}

	public static class ManagementObjectUtil
	{
		public static string GetValue(this ManagementObject obj, string propertyName)
		{
			return obj[propertyName].ToString();
		}

		public static string GetValueLower(this ManagementObject obj, string propertyName)
		{
			return obj[propertyName].ToString().Trim().ToLower();
		}
	}
}