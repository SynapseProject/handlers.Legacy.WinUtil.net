using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel;

namespace Synapse.Handlers.Legacy.WinCore
{
	public enum WmiPath
	{
		Root,
		IIS
	}

	public enum ServiceType
	{
		Service,
		AppPool,
		ScheduledTask
	}

	public enum ServiceAction
	{
		Start,
		Stop,
		Restart,
		Create,
		Delete,
		Query,
		StartMode
	}

	//[DataContract( Name = "ServiceStartMode" )]
	public enum ServiceStartMode
	{
		Unchanged = 0,
		System = 1,
		Automatic = 2,
		Manual = 3,
		Disabled = 4,
		Boot = 5
	}

	public enum ServiceReturnCode
	{
		Success = 0,
		NotSupported = 1,
		AccessDenied = 2,
		DependentServicesRunningOrInsufficientPrivilege = 3,
		InvalidServiceControl = 4,
		ServiceCannotAcceptControl = 5,
		ServiceNotActive = 6,
		ServiceRequestTimeout = 7,
		UnknownFailure = 8,
		PathNotFound = 9,
		ServiceAlreadyRunning = 10,
		ServiceDatabaseLocked = 11,
		ServiceDependencyDeleted = 12,
		ServiceDependencyFailure = 13,
		ServiceDisabled = 14,
		ServiceLogonFailure = 15,
		ServiceMarkedForDeletion = 16,
		ServiceNoThread = 17,
		StatusCircularDependency = 18,
		StatusDuplicateName = 19,
		StatusInvalidName = 20,
		StatusInvalidParameter = 21,
		StatusInvalidServiceAccount = 22,
		StatusServiceExists = 23,
		ServiceAlreadyPaused = 24,
		ServiceNotFound = 350
	}

	public enum WindowsServiceType : uint
	{
		KernelDriver = 0x1,
		FileSystemDriver = 0x2,
		Adapter = 0x4,
		RecognizerDriver = 0x8,
		OwnProcess = 0x10,
		ShareProcess = 0x20,
		Interactive = 0x100
	}

	public enum ErrorControlAction
	{
		UserIsNotNotified = 0,
		UserIsNotified = 1,
		SystemRestartedLastGoodConfiguraion = 2,
		SystemAttemptStartWithGoodConfiguration = 3
	}

	public enum AppPoolReturnCode
	{
		Unknown = 0,
		Starting = 1,
		Started = 2,
		Stopping = 3,
		Stopped = 4,
        Pausing = 5,
        Paused = 6,
        Continuing = 7
	}
}