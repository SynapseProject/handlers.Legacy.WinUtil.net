using System;
using System.Diagnostics;
using System.Management;
using System.ServiceProcess;

using Microsoft.Win32.TaskScheduler;

namespace Synapse.Handlers.Legacy.WinCore
{
	public class ScheduledTaskUtil
	{
		/// <summary>
		/// Queries the status and other configuration properties of a Windows ScheduledTask.
		/// </summary>
		/// <param name="taskName">The name of the task as shown in the TaskScheduler Contol Panel applet.</param>
		/// <param name="machineName">The server on which the service is running.</param>
		/// <returns>The service configuration details.</returns>
		public static ScheduledTaskConfig QueryStatus(string taskName, string machineName)
		{
			TaskService ts = new TaskService( machineName );
			Task task = ts.FindTask( taskName );

			ScheduledTaskConfig config = new ScheduledTaskConfig();
			config.TaskName = taskName;
			config.ServerName = machineName;
			config.State = task.State.ToString();
			config.TaskDefinition = task.Definition;

			return config;
		}

		/// <summary>
		/// Enables a Windows ScheduledTask.
		/// </summary>
		/// <param name="taskName">The name of the task as shown in the TaskScheduler Contol Panel applet.</param>
		/// <param name="machineName">The server on which the service is running.</param>
		/// <returns>True if status == Running, otherwise false.</returns>
		public static bool Start(string taskName, string machineName)
		{
			TaskService ts = new TaskService( machineName );
			Task task = ts.FindTask( taskName );

			if( !task.IsActive )
			{
				task.Enabled = true;
				task = ts.FindTask( taskName );
			}

			return task.IsActive;
		}

		/// <summary>
		/// Stops and disables a Windows ScheduledTask.
		/// </summary>
		/// <param name="taskName">The name of the task as shown in the TaskScheduler Contol Panel applet.</param>
		/// <param name="machineName">The server on which the service is running.</param>
		/// <returns>True if status == Stopped, otherwise false.</returns>
		public static bool Stop(string taskName, string machineName)
		{
			TaskService ts = new TaskService( machineName );
			Task task = ts.FindTask( taskName );

			if( task.IsActive )
			{
				task.Stop();
				task.Enabled = false;
				task = ts.FindTask( taskName );
			}

			return task.IsActive;
		}
	}
}