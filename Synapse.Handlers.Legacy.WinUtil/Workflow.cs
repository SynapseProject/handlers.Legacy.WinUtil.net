using System;
using System.Diagnostics;

using Synapse.Core;

namespace Synapse.Handlers.Legacy.WinCore
{
	public class Workflow
	{
		WorkflowParameters _wfp = null;
        HandlerStartInfo _startInfo = null;
        public Action<string, string, LogLevel, Exception> OnLogMessage;
        public Func<string, string, StatusType, long, int, bool, Exception, bool> OnProgress;

        /// <summary>
        /// Default ctor
        /// </summary>
        public Workflow() { }

		/// <summary>
		/// Initializes parameters.
		/// </summary>
		/// <param name="parameters">Initializes Parameters.</param>
		public Workflow(WorkflowParameters parameters)
		{
			_wfp = parameters;
		}

		/// <summary>
		/// Initializes parameters.
		/// </summary>
		/// <param name="parameters">Initializes Parameters.</param>
		public Workflow(WinProcTaskContainer task)
		{
			_wfp = task.tasks;
		}

		/// <summary>
		/// Initializes parameters.
		/// </summary>
		/// <param name="parameters">Initializes Parameters.</param>
		public Workflow(WinCoreContainer wpac)
		{
			_wfp = wpac.winProcAdapter.tasks;
		}

		/// <summary>
		/// Gets or sets the parameters for the Workflow.  Set ahead of ExecuteAction.
		/// </summary>
		public WorkflowParameters Parameters
		{
            get { return _wfp; }
            set { _wfp = value as WorkflowParameters; }
        }

        /// <summary>
        /// Executes the main workflow.
        /// </summary>
        public void ExecuteAction(HandlerStartInfo startInfo)
		{
			string context = "ExecuteAction";
            _startInfo = startInfo;

			string msg = Utils.GetHeaderMessage( string.Format( "Entering Main Workflow." ) );
			if( OnStepStarting( context, msg ) )
			{
				return;
			}

            OnStepProgress("ExecuteAction", Utils.CompressXml(startInfo.Parameters));

            Stopwatch clock = new Stopwatch();
            clock.Start();

            IProcessState state = new ServiceConfig();

            Exception ex = null;
            try
            {
                if (ValidateParameters())
                {
                    switch (_wfp.TargetType)
                    {
                        case ServiceType.Service:
                            {
                                state = ManageService(startInfo.IsDryRun);
                                break;
                            }
                        case ServiceType.AppPool:
                            {
                                state = ManageAppPool(startInfo.IsDryRun);
                                break;
                            }
                        case ServiceType.ScheduledTask:
                            {
                                state = ManageScheduledTask(startInfo.IsDryRun);
                                break;
                            }
                    }
                }
                else
                {
                    throw new Exception("Could not validate parameters.");
                }
            }
            catch (Exception exception)
            {
                ex = exception;
            }

            if (_wfp.IsValid)
            {
                OnStepProgress(context, "\r\n\r\n" + state.ToXml(true) + "\r\n");
            }

            bool ok = ex == null;
            OnProgress(context, state.State, ok ? StatusType.Complete : StatusType.Failed, _startInfo.InstanceId, int.MaxValue, false, ex);

        }

        #region Validate Parameters
        bool ValidateParameters()
		{
			string context = "ValidateParameters";

			OnStepProgress( context, Utils.GetHeaderMessage( "Begin [PrepareAndValidate]" ) );

			_wfp.PrepareAndValidate();

			OnStepProgress( context, "IsValid = " + _wfp.IsValid );
			OnStepProgress( context, Utils.GetHeaderMessage( "End [PrepareAndValidate]" ) );

			return _wfp.IsValid;
		}
		#endregion

		IProcessState ManageService(bool isDryRun)
		{
			string context = "ManageService";

			OnStepProgress( context, "Calling Service Action :[" + _wfp.Action + "]" );
            if (isDryRun)
            {
                OnProgress(context, "IsDryRun Flag Is Set.  Service Action Will Be Query Only.", StatusType.Running, _startInfo.InstanceId, _cheapSequence++, false, null);
                _wfp.Action = ServiceAction.Query;
            }

            switch ( _wfp.Action )
			{
				case ServiceAction.Query:
				{
					break;
				}
				case ServiceAction.Start:
				{
					ServiceUtil.Start( _wfp.TargetName, _wfp.ServerName, _wfp.ServiceStartTimeToMonitor, _wfp.ServiceStartModeOnStart );
					break;
				}
				case ServiceAction.Stop:
				{
					ServiceUtil.Stop( _wfp.TargetName, _wfp.ServerName, _wfp.ServiceStopTimeToTerminate, _wfp.ServiceStartModeOnStop );
					break;
				}
				case ServiceAction.Restart:
				{
					ServiceUtil.Stop( _wfp.TargetName, _wfp.ServerName, _wfp.ServiceStopTimeToTerminate, _wfp.ServiceStartModeOnStop );
					System.Threading.Thread.Sleep( 5000 );
					ServiceUtil.Start( _wfp.TargetName, _wfp.ServerName, _wfp.ServiceStartTimeToMonitor, _wfp.ServiceStartModeOnStart );
					break;
				}
				case ServiceAction.Create:
				{
					ServiceUtil.CreateService( _wfp.TargetName, _wfp.ServerName, _wfp.TargetName,
						_wfp.TargetPath, _wfp.ServiceStartModeOnStart, _wfp.TargetUserName, _wfp.TargetPassword, _wfp.ServiceParameters );
					break;
				}
				case ServiceAction.Delete:
				{
					ServiceUtil.Stop( _wfp.TargetName, _wfp.ServerName, _wfp.ServiceStopTimeToTerminate, _wfp.ServiceStartModeOnStop );
					ServiceUtil.DeleteService( _wfp.TargetName, _wfp.ServerName );
					break;
				}
			}

			return ServiceUtil.QueryStatus( _wfp.TargetName, _wfp.ServerName );
		}

		IProcessState ManageAppPool(bool isDryRun)
		{
			string context = "ManageAppPool";

			OnStepProgress( context, "Calling AppPool Action :[" + _wfp.Action + "]" );
            if (isDryRun)
            {
                OnProgress(context, "IsDryRun Flag Is Set.  AppPool Action Will Be Query Only.", StatusType.Running, _startInfo.InstanceId, _cheapSequence++, false, null);
                _wfp.Action = ServiceAction.Query;
            }

            switch ( _wfp.Action )
			{
				case ServiceAction.Query:
				{
					break;
				}
				case ServiceAction.Start:
				{
					AppPoolUtil.Start( _wfp.TargetName, _wfp.ServerName, 10000 );
					break;
				}
				case ServiceAction.Stop:
				{
					AppPoolUtil.Stop( _wfp.TargetName, _wfp.ServerName, 10000, 3, 10000 );
					break;
				}
				case ServiceAction.Restart:
				{
					AppPoolUtil.Recycle( _wfp.TargetName, _wfp.ServerName, 10000 );
					break;
				}
			}

			return AppPoolUtil.QueryStatus( _wfp.TargetName, false, _wfp.ServerName );
		}

		IProcessState ManageScheduledTask(bool isDryRun)
		{
			string context = "ManageScheduledTask";

			OnStepProgress( context, "Calling Scheduled Task Action :[" + _wfp.Action + "]" );
            if (isDryRun)
            {
                OnProgress(context, "IsDryRun Flag Is Set.  Scheduled Task Action Will Be Query Only.", StatusType.Running, _startInfo.InstanceId, _cheapSequence++, false, null);
                _wfp.Action = ServiceAction.Query;
            }
            switch ( _wfp.Action )
			{
				case ServiceAction.Query:
				{
					break;
				}
				case ServiceAction.Start:
				{
					ScheduledTaskUtil.Start( _wfp.TargetName, _wfp.ServerName );
					break;
				}
				case ServiceAction.Stop:
				{
					ScheduledTaskUtil.Stop( _wfp.TargetName, _wfp.ServerName );
					break;
				}
				case ServiceAction.Restart:
				{
					ScheduledTaskUtil.Stop( _wfp.TargetName, _wfp.ServerName );
					System.Threading.Thread.Sleep( 5000 );
					ScheduledTaskUtil.Start( _wfp.TargetName, _wfp.ServerName );
					break;
				}
			}

			return ScheduledTaskUtil.QueryStatus( _wfp.TargetName, _wfp.ServerName );
		}


		#region NotifyProgress Events
		public int _cheapSequence = 0;

		/// <summary>
		/// Notify of step beginning. If return value is True, then cancel operation.
		/// Defaults: PackageStatus.Running, Id = _cheapSequence++, Severity = 0, Exception = null.
		/// </summary>
		/// <param name="context">The method name.</param>
		/// <param name="message">Descriptive message.</param>
		/// <returns>AdapterProgressCancelEventArgs.Cancel value.</returns>
		bool OnStepStarting(string context, string message)
		{
            OnProgress(context, message, StatusType.Running, _startInfo.InstanceId, _cheapSequence++, false, null);
			return false;
		}

		/// <summary>
		/// Notify of step progress.
		/// Defaults: PackageStatus.Running, Id = _cheapSequence++, Severity = 0, Exception = null.
		/// </summary>
		/// <param name="context">The method name.</param>
		/// <param name="message">Descriptive message.</param>
		void OnStepProgress(string context, string message)
		{
            OnProgress(context, message, StatusType.Running, _startInfo.InstanceId, _cheapSequence++, false, null);
		}

		/// <summary>
		/// Notify of step completion.
		/// Defaults: PackageStatus.Running, Id = _cheapSequence++, Severity = 0, Exception = null.
		/// </summary>
		/// <param name="context">The method name or workflow activty.</param>
		/// <param name="message">Descriptive message.</param>
		void OnStepFinished(string context, string message)
		{
            OnProgress(context, message, StatusType.Running, _startInfo.InstanceId, _cheapSequence++, false, null);
		}
		#endregion
	}
}