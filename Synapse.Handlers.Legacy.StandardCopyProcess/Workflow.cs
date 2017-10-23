using System;
using System.Collections.Generic;
using System.Diagnostics;
using io = System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Web.XmlTransform;

using Alphaleonis.Win32.Filesystem;
using Synapse.Core;
using Synapse.Handlers.Legacy.WinCore;
using System.Security.Cryptography.Utility;

using Synapse.Aws.Core;
using Amazon;

using settings = Synapse.Handlers.Legacy.StandardCopyProcess.Properties.Settings;

namespace Synapse.Handlers.Legacy.StandardCopyProcess
{
	public class Workflow
	{
		WorkflowParameters _wfp = null;
        HandlerStartInfo _startInfo = null;
        S3Client S3Client = null;
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
		/// Gets or sets the parameters for the Workflow.  Set ahead of ExecuteAction.
		/// </summary>
		public WorkflowParameters Parameters { get { return _wfp; } set { _wfp = value as WorkflowParameters; } }

		/// <summary>
		/// Executes the main workflow of: Backup, UpdateConfigValues, CopyContent, MoveToNext.
		/// </summary>
		public void ExecuteAction(HandlerStartInfo startInfo)
		{
			string context = "ExecuteAction";
            _startInfo = startInfo;

			string msg = Utils.GetHeaderMessage(
				string.Format( "Synapse Legacy Handler, Standard Copy Process Handler. {0}, Entering Main Workflow.", Utils.GetBuildDateVersion() ) );
			if( OnStepStarting( context, msg ) )
			{
				return;
			}

            OnStepProgress("ExecuteAction", Utils.CompressXml(startInfo.Parameters));

            Stopwatch clock = new Stopwatch();
            clock.Start();

            bool ok = true;
            Exception ex = null;
            try
            {
                ok = ValidateParameters();
                if (ok)
                {
                    ExecuteBackups();
                    UpdateConfigValues();
                    ExecuteCopyContentTasks();
                    UpdateConfigOriginals();

                    if (_wfp.MoveToNext)
                    {
                        if (_wfp.TruncateNextEnvironmentDirectory)
                        {
                            if (IsS3Url(_wfp.NextEnvironmentSourceDirectory))
                            {
                                string[] nextEnvSrcDirUrl = SplitS3Url( _wfp.NextEnvironmentSourceDirectory );
                                S3Client.DeleteBucketObjects( nextEnvSrcDirUrl[0], nextEnvSrcDirUrl[1], LogFileCopyProgress );
                            }
                            else
                                DeleteFolder(_wfp.NextEnvironmentSourceDirectory, true);
                        }

                        MoveFolderContent(_wfp.SourceDirectory, _wfp.NextEnvironmentSourceDirectory);
                    }
                }
            }
            catch (Exception exception)
            {
                ex = exception;
            }

            ok = ok && ex == null;

            StatusType status = ok ? StatusType.Complete : StatusType.Failed; 
            if (status == StatusType.Complete && _wfp.DeleteManifest.HasExceptions)
            {
                status = _wfp.DeleteManifest.TreatExceptionsAsWarnings ?
                    StatusType.CompletedWithErrors : StatusType.Failed;
            }

            msg = Utils.GetHeaderMessage(string.Format("End Main Workflow: {0}, Total Execution Time: {1}",
                ok ? "Complete." : "One or more steps failed.", clock.ElapsedSeconds()));
            OnProgress(context, msg, status, _startInfo.InstanceId, int.MaxValue, false, ex);

        }

        public void LogFileCopyProgress(string context, string message)
        {
            OnLogMessage( context, message, LogLevel.Info, null );
        }

        #region Validate Parameters
        bool ValidateParameters()
		{
			string context = "ExecuteAction";
			const int padding = 50;

			OnStepProgress( context, Utils.GetHeaderMessage( "Begin [PrepareAndValidate]" ) );

			_wfp.PrepareAndValidate(this);

			OnStepProgress( context, Utils.GetMessagePadRight( "SourceDirectory", _wfp.SourceDirectory, padding ) );
			OnStepProgress( context, Utils.GetMessagePadRight( "IsSourceDirectoryValid", _wfp.IsSourceDirectoryValid, padding ) );
			OnStepProgress( context, Utils.GetMessagePadRight( "TargetRemoteDestination", _wfp.TargetRemoteDestination, padding ) );
			OnStepProgress( context, Utils.GetMessagePadRight( "IsTargetRemoteDestinationValid", _wfp.IsTargetRemoteDestinationValid, padding ) );
			OnStepProgress( context, Utils.GetMessagePadRight( "BackupRemoteDestination", _wfp.BackupRemoteDestination, padding ) );
			OnStepProgress( context, Utils.GetMessagePadRight( "IsBackupRemoteDestinationValid", _wfp.IsBackupRemoteDestinationValid, padding ) );
			OnStepProgress( context, Utils.GetMessagePadRight( "TargetServerDestination", _wfp.TargetServerDestination, padding ) );
			OnStepProgress( context, Utils.GetMessagePadRight( "IsTargetServerPathValid", _wfp.IsTargetServerPathValid, padding ) );
			OnStepProgress( context, Utils.GetMessagePadRight( "BackupServerDestination", _wfp.BackupServerDestination, padding ) );
			OnStepProgress( context, Utils.GetMessagePadRight( "IsBackupServerDestinationValid", _wfp.IsBackupServerDestinationValid, padding ) );
			if( _wfp.DeleteManifest.HasFileName )
			{
				OnStepProgress( context, Utils.GetMessagePadRight( "DeleteManifest", _wfp.DeleteManifest.FileName, padding ) );
				OnStepProgress( context, Utils.GetMessagePadRight( "IsDeleteManifestPathValid", _wfp.DeleteManifest.IsFileNameValid, padding ) );
			}

			OnStepProgress( context, Utils.GetMessagePadRight( "IsConfigTransformFileListValid", _wfp.IsConfigTransformFileListValid, padding ) );
			if( !_wfp.IsConfigTransformFileListValid )
			{
				foreach( ConfigFile cf in _wfp.ConfigsWithTransformFiles )
				{
					OnStepProgress( context, Utils.GetMessagePadRight( string.Format( "  Config File: {0}", cf.Name ), cf.NameExists, padding ) );
					OnStepProgress( context, Utils.GetMessagePadRight( string.Format( "  Transform File: {0}", cf.TransformFile ), cf.TransformFileExists, padding ) );
				}
			}

			OnStepProgress( context, Utils.GetMessagePadRight( "WorkflowParameters.IsValid",
				string.Format( "{0} [IsSourceDirectoryValid && IsTargetRemoteDestinationValid && IsBackupRemoteDestinationValid && IsTargetServerPathValid && IsBackupServerDestinationValid && IsDeleteManifestPathValid && IsConfigTransformFileListValid]", _wfp.IsValid ), padding ) );
			OnStepProgress( context, Utils.GetHeaderMessage( "End [PrepareAndValidate]" ) );

			return _wfp.IsValid;
		}
		#endregion

		#region ExecuteBackups
		/// <summary>
		/// Backs-up remote target and the first server in the servers list.
		/// </summary>
		void ExecuteBackups()
		{
			string context = "ExecuteBackups";
			string msg = Utils.GetHeaderMessage( "Beginning content backup." );
			if( OnStepStarting( context, msg ) )
			{
				return;
			}

			Stopwatch clock = new Stopwatch();
			clock.Start();

			Dictionary<string, string> backupTargets = new Dictionary<string, string>();

            string[] files = null;

            if ( IsS3Url( _wfp.SourceDirectory ) )
            {
                string[] url = SplitS3Url( _wfp.SourceDirectory );
                files = S3Client.GetFiles( url[0], url[1] );
            }
            else
                files = Directory.GetFiles( _wfp.SourceDirectory, "*", io.SearchOption.AllDirectories );

			if( _wfp.HasBackupRemoteDestination && _wfp.IsBackupRemoteDestinationValid )
			{
				OnStepProgress( context, "Backing-up remote destination." );
				backupTargets.Add( _wfp.TargetRemoteDestination, _wfp.BackupRemoteDestination );
			}

			if( _wfp.HasBackupServerDestination && _wfp.IsBackupServerDestinationValid )
			{
				if( _wfp.HasServerList ) //this is a redundant test, covered in PrepareAndValidate()
				{
					OnStepProgress( context, "Backing-up server destination." );
					string serverName = _wfp.Servers[0];
					string targetDest = Utils.GetServerLongPath( serverName, _wfp.TargetServerDestination );
					backupTargets.Add( targetDest, _wfp.BackupServerDestination );
				}
			}

			try
			{
				//key->targetDestination, value->backupDestination
				Parallel.ForEach( backupTargets.Keys, key => BackupContent( files, key, backupTargets[key] ) );

				clock.Stop();
				msg = Utils.GetHeaderMessage(
					string.Format( "End content backup, Total Execution Time: {0}", clock.ElapsedSeconds() ) );
				OnStepFinished( context, msg );
			}
			catch( Exception ex )
			{
				//throw the AggregateException
				throw ex;
			}
		}

		/// <summary>
		/// Backs-up only files in the list "files" and that exist on the targetDestination.
		/// </summary>
		/// <param name="files">The list of files to backup.</param>
		/// <param name="targetDestination">The target for deployment; files backed-up from here.</param>
		/// <param name="backupDestination">The destination for the backed-up files.</param>
		void BackupContent(string[] files, string targetDestination, string backupDestination)
		{
			foreach( string file in files )
			{
				string unrootedFile = file.Replace( _wfp.SourceDirectory, string.Empty );
                string sourceFile = Utils.PathCombine( targetDestination, unrootedFile.Replace( "/", "\\" ));
                if ( IsS3Url( targetDestination ) )
                    sourceFile = Utils.PathCombineS3( targetDestination, unrootedFile );
				string destinationFile = Utils.PathCombine( backupDestination, unrootedFile.Replace( "/", "\\" ) );
                if ( IsS3Url( backupDestination) )
                    destinationFile = Utils.PathCombineS3( backupDestination, unrootedFile );
                string sourceDirectoryUrl = sourceFile.Replace( unrootedFile, string.Empty );

                if ( _startInfo.IsDryRun )
                    OnProgress( "DryRun:BackupContent", "Backing Up File : " + sourceFile + " To " + destinationFile, StatusType.Running, _startInfo.InstanceId, _cheapSequence++, false, null );
                else if ( IsS3Url( sourceFile ) )
                {
                    string[] sourceUrl = SplitS3Url( sourceFile );
                    if ( S3Client.Exists(sourceUrl[0], sourceUrl[1] ))
                    {
                        if ( IsS3Url( destinationFile ) )
                        {
                            string[] destUrl = SplitS3Url( destinationFile );
                            S3Client.CopyObject( sourceUrl[0], sourceUrl[1], destUrl[0], destUrl[1], sourceUrl[1], LogFileCopyProgress );
                        }
                        else
                        {
                            S3Client.CopyObjectToLocal( sourceUrl[0], sourceUrl[1], destinationFile, sourceUrl[1], LogFileCopyProgress );
                        }
                    }
                }
                else
                {
                    if ( File.Exists( sourceFile ) )
                    {
                        if ( IsS3Url( destinationFile ) )
                        {
                            string[] destUrl = SplitS3Url( destinationFile );
                            S3Client.UploadToBucket( sourceFile, destUrl[0], destUrl[1].Replace(unrootedFile, string.Empty), sourceDirectoryUrl, LogFileCopyProgress );
                        }
                        else
                        {
                            string destDir = Path.GetDirectoryName( destinationFile );
                            if ( !Directory.Exists( destDir ) )
                            {
                                Directory.CreateDirectory( destDir );
                            }

                            //CopyOptions.None->Allow overwrite if file exists in backup destination
                            //true->preserveDates
                            File.Copy( sourceFile, destinationFile,
                                CopyOptions.None, true, CopyMoveProgressHandler, null, PathFormat.FullPath );
                        }
                    }
                }
			}
		}
		#endregion

		#region Config Munge
		/// <summary>
		/// ForEach ConfigFile with an associated Transform file, execute the XmlTransform.
		/// </summary>
		void UpdateConfigValues()
		{
			string context = "UpdateConfigValues";
			string msg = Utils.GetHeaderMessage( "Updating config files." );
			if( OnStepStarting( context, msg ) )
			{
				return;
			}

			Stopwatch clock = new Stopwatch();
			clock.Start();

			foreach( ConfigFile cf in _wfp.ConfigsWithTransformFiles )
			{
  				ExecuteXmlTransformation( _wfp.SourceDirectory, cf );
			}

			clock.Stop();
			msg = Utils.GetHeaderMessage(
				string.Format( "End config files updates, Total Execution Time: {0}", clock.ElapsedSeconds() ) );
			OnStepFinished( context, msg );
		}

		/// <summary>
		/// Applies the TransformFile to the ConfigFile.
		/// </summary>
		/// <param name="sourceFolderPath">The Workflow parameters SourceDirectory</param>
		/// <param name="configFileName">The source config file</param>
		/// <param name="transformFileName">The source transform file</param>
		/// <param name="configTempPath">Temp processing folder root [delete on exit?]</param>
		void ExecuteXmlTransformation(string sourceFolderPath, ConfigFile cf)
		{
            if (_startInfo.IsDryRun)
            {
                OnStepProgress("DryRun:ExecuteXmlTransformation", string.Format("Executing XmlTransformation on [{0}] with [{1}]", cf.Name, cf.TransformFile));
                return;     // Do Nothing
            }
            else
                OnStepProgress("ExecuteXmlTransformation", string.Format("Executing XmlTransformation on [{0}] with [{1}]", cf.Name, cf.TransformFile));

			string tempFileToSave = Path.GetRandomFileName();

            System.IO.Stream transformFileOriginalStream = null;
            System.IO.Stream tempFileStream = null;
            System.IO.Stream transformFileStream = null;

            try
            {
                if ( IsS3Url( sourceFolderPath ) )
                {
                    cf.TransformFileOriginalName = Utils.PathCombineS3( sourceFolderPath, cf.Name );
                    string[] tfOriginalUrl = SplitS3Url( cf.TransformFileOriginalName );
                    transformFileOriginalStream = S3Client.GetObjectStream( tfOriginalUrl[0], tfOriginalUrl[1], S3FileMode.Open, S3FileAccess.Read );
                    tempFileToSave = Utils.PathCombineS3( cf.TransformFileOriginalName.Substring(0, cf.TransformFileOriginalName.LastIndexOf('/')), tempFileToSave );
                    string[] tempUrl = SplitS3Url( tempFileToSave );
                    tempFileStream = S3Client.GetObjectStream( tempUrl[0], tempUrl[1], S3FileMode.OpenOrCreate, S3FileAccess.Write );
                }
                else
                {
                    cf.TransformFileOriginalName = Path.GetLongFrom83ShortPath( Utils.PathCombine( sourceFolderPath, cf.Name ) );
                    transformFileOriginalStream = File.Open( cf.TransformFileOriginalName, System.IO.FileMode.Open, System.IO.FileAccess.Read );
                    tempFileToSave = Utils.PathCombine( Path.GetDirectoryName( cf.TransformFileOriginalName ), tempFileToSave );
                    tempFileStream = File.Open( tempFileToSave, io.FileMode.OpenOrCreate, io.FileAccess.Write );
                }

                //execute the transform
                string transformFile = null;
                if ( IsS3Url( sourceFolderPath ) )
                {
                    // Transform File Must Be Open Read/Write, Which S3 Does Not Support.
                    // Read S3 Stream Into A MemoryStream And Use That Instead.
                    transformFile = Utils.PathCombineS3( sourceFolderPath, cf.TransformFile );
                    string[] tfUrl = SplitS3Url( transformFile );
                    System.IO.Stream xformContent = S3Client.GetObjectStream( tfUrl[0], tfUrl[1], S3FileMode.OpenOrCreate, S3FileAccess.Read );
                    System.IO.MemoryStream ms = new System.IO.MemoryStream();
                    xformContent.CopyTo( ms );
                    xformContent.Close();

                    ms.Position = 0;
                    transformFileStream = ms;
                }
                else
                {
                    transformFile = Path.GetLongFrom83ShortPath( Utils.PathCombine( sourceFolderPath, cf.TransformFile ) );
                    transformFileStream = File.Open( transformFile, System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite );
                }

                using ( XmlTransformableDocument doc = new XmlTransformableDocument() )
				{
					doc.PreserveWhitespace = true;

					using( io.StreamReader sr = new io.StreamReader( transformFileOriginalStream ) )
					{
						doc.Load( sr );
					}

                    //doc.Load(cf.TransformFileOriginalName);

                    using ( XmlTransformation xt = new XmlTransformation( transformFileStream, null ) )
                    {
                        xt.Apply( doc );
                        doc.Save( tempFileStream );
                    }

                    cf.TransformOutFileFullPath = tempFileToSave;
                    if ( IsS3Url( tempFileToSave ) )
                    {
                        cf.TransformOutFileName = tempFileToSave.Substring( tempFileToSave.LastIndexOf( '/' ) + 1 );
                    }
                    else
                        cf.TransformOutFileName = (new FileInfo( tempFileToSave )).Name;
                }
            }
			catch( Exception ex )
			{
				string msg = string.Format( "ExecuteXmlTransformation failed on: configFile:[{0}], transformFileName:[{1}]", cf.TransformFileOriginalName, cf.TransformFile );
                OnProgress(msg, ex.Message, StatusType.Running, _startInfo.InstanceId, _cheapSequence++, false, ex);
				throw ex;
			}
            finally
            {
                transformFileOriginalStream.Close();
                tempFileStream.Close();
                transformFileStream.Close();
            }
        }

		/// <summary>
		/// ForEach ConfigFile transformed-in-place, copy the temp as the original.
		/// ForEach ConfigFile /not/ transformed-in-place, delete the temp.
		/// </summary>
		void UpdateConfigOriginals()
		{
			string context = "UpdateConfigOriginals";
            if (_startInfo.IsDryRun)
            {
                context = "DryRun:UpdateConfigOriginals";
                OnStepProgress(context, "DryRun Flag Is Set.  No Files Will Be Modified.");
            }
			string msg = Utils.GetHeaderMessage( "Updating original config files." );
			if( OnStepStarting( context, msg ) )
			{
				return;
			}

			Stopwatch clock = new Stopwatch();
			clock.Start();

			foreach( ConfigFile cf in _wfp.ConfigsWithTransformFiles )
			{
				//TransformInPlace: rename the temp file to the original config name
				if( cf.TransformInPlace )
				{
					try
					{
						OnStepProgress( context,
							string.Format( "Executing update on: BackupName:[{0}], OriginalName:[{1}]", cf.TransformOutFileFullPath, cf.TransformFileOriginalName ) );
                        if ( !_startInfo.IsDryRun )
                        {
                            if ( IsS3Url( cf.TransformFileOriginalName ) )
                            {
                                string[] outUrl = SplitS3Url( cf.TransformOutFileFullPath );
                                string[] origUrl = SplitS3Url( cf.TransformFileOriginalName );
                                S3Client.MoveBucketObjects( outUrl[0], outUrl[1], origUrl[0], origUrl[1], false );
                            }
                            else
                                File.Move( cf.TransformOutFileFullPath, cf.TransformFileOriginalName, MoveOptions.ReplaceExisting );
                        }
					}
					catch( Exception ex )
					{
						msg = string.Format( "UpdateConfigOriginals failed on: BackupName:[{0}], OriginalName:[{1}]", cf.TransformOutFileFullPath, cf.TransformFileOriginalName );
                        OnProgress(msg, ex.Message, StatusType.Running, _startInfo.InstanceId, _cheapSequence++, false, ex);
						throw ex;
					}
				}
				//not TransformInPlace: So we don't end up cluttering up the higher environment
				//directories, delete the temp file from the source directory
				else
				{
					try
					{
                        if (!_startInfo.IsDryRun)
                            File.Delete( Utils.PathCombine( Path.GetDirectoryName( Utils.PathCombine( _wfp.SourceDirectory, cf.Name ) ), cf.TransformOutFileName ) );
					}
					catch { /* If this fails, so be it - do nothing */ }
				}
			}

			clock.Stop();
			msg = Utils.GetHeaderMessage(
				string.Format( "End config files update, Total Execution Time: {0}", clock.ElapsedSeconds() ) );
			OnStepFinished( context, msg );
		}
		#endregion

		#region CopyContent
		/// <summary>
		/// Runs "CopyContent" in a multithreaded operation against all server and remote targets.
		/// </summary>
		void ExecuteCopyContentTasks()
		{
			string context = "ExecuteCopyContentTasks";
			string msg = Utils.GetHeaderMessage( "Copying content to destinations." );
			if( OnStepStarting( context, msg ) )
			{
				return;
			}

			Stopwatch clock = new Stopwatch();
			clock.Start();

			IEnumerable<string> copyTargets = _wfp.Servers;
			if( _wfp.HasTargetRemoteDestination )
			{
				copyTargets = copyTargets.Concat( new string[] { null } );
			}

			try
			{
				Parallel.ForEach( copyTargets, serverName => CopyContent( serverName ) );

				clock.Stop();

				msg = Utils.GetHeaderMessage(
					string.Format( "End content copy: Total Execution Time: {0}", clock.ElapsedSeconds() ) );
				OnStepFinished( context, msg );
			}
			catch( Exception ex )
			{
				//throw the AggregateException
				throw ex;
			}
		}

		/// <summary>
		/// Stops running processes*, copies source to destination, re-starts processes.  *Processes stop/start for servers only.
		/// </summary>
		/// <param name="serverName">The target server.  Pass null if for NAS (remote) target.</param>
		void CopyContent(string serverName)
		{
			string context = "CopyContent";

			Stopwatch clock = new Stopwatch();
			clock.Start();

			bool isServer = !string.IsNullOrWhiteSpace( serverName );

			string msg = string.Format( "Beginning copy for [{0}]", isServer ? serverName : "Remote" );
			OnStepProgress( context, msg );

			try
			{
				if( isServer )
				{
					string serverDest = Utils.GetServerLongPath( serverName, _wfp.TargetServerDestination );

					//this is required when Reprovisioning NT Services
					if( _wfp.CreateTargetServerPath &&
						!Directory.Exists( serverDest, PathFormat.LongFullPath ) )
					{
                        if (!_startInfo.IsDryRun)
    						Directory.CreateDirectory( serverDest, PathFormat.LongFullPath );
					}

					StopServerProcesses( serverName );
					if( _wfp.TruncateTargetDirectory )
					{
						TruncateTargetDirectory( serverDest );
					}
					CopyFolder( _wfp.SourceDirectory, serverDest );
					RenameConfigsAtTarget( serverDest );
					if( _wfp.DeleteManifest.HasPaths )
					{
						AggregateException dmEx = DeleteManifestPathsContent( serverDest );
						if( !_wfp.DeleteManifest.TreatExceptionsAsWarnings && dmEx != null )
						{
							//throw dmEx;
						}
					}
					StartServerProcesses( serverName );
				}
				else
				{
					if( _wfp.TruncateTargetDirectory )
					{
						TruncateTargetDirectory( _wfp.TargetRemoteDestination );
					}
					CopyFolder( _wfp.SourceDirectory, _wfp.TargetRemoteDestination );
					RenameConfigsAtTarget( _wfp.TargetRemoteDestination );
					if( _wfp.DeleteManifest.HasPaths )
					{
						AggregateException dmEx = DeleteManifestPathsContent( _wfp.TargetRemoteDestination );
						if( !_wfp.DeleteManifest.TreatExceptionsAsWarnings && dmEx != null )
						{
							//throw dmEx;
						}
					}
				}

				clock.Stop();
				msg = string.Format( "End copy for [{0}], Time: {1}", isServer ? serverName : "Remote", clock.ElapsedSeconds() );
				OnStepProgress( context, msg );
			}
			catch( Exception ex )
			{
				msg = string.Format( "CopyContent failed on: serverName:[{0}]", serverName );
                OnProgress(msg, ex.Message, StatusType.Running, _startInfo.InstanceId, _cheapSequence++, false, ex);
				throw ex;
			}
		}

		/// <summary>
		/// At the destination path (target folder), renames XmlTransform temp files to the proper names.
		/// </summary>
		/// <param name="source">The source directory path.</param>
		/// <param name="destination">The destination directory path.</param>
		void RenameConfigsAtTarget(string destination)
		{
            String ctx = "UpdateConfigsAtTarget";
            if (_startInfo.IsDryRun)
            {
                ctx = "DryRun:UpdateConfigsAtTarget";
                OnStepProgress(ctx, "DryRun Flag Is Set.  Config Files Will NOT Be Renamed.");
            }

            //Rename each temp transformed file to the correct config file name
            foreach ( ConfigFile cf in _wfp.ConfigsWithTransformFiles )
			{
				string destinationConfigName = Utils.PathCombine( destination, cf.Name );
				string destinationConfigPath = Path.GetDirectoryName( destinationConfigName );
				string destinationConfigBackupName = destinationConfigName + ".backup.original";
				string transformedConfigTempName = Utils.PathCombine( destinationConfigPath, cf.TransformOutFileName );

                if (!_startInfo.IsDryRun)
                {
                    try
                    {
                        //copy the untransformed "template" config to *.backup.original
                        File.Move(destinationConfigName, destinationConfigBackupName, MoveOptions.ReplaceExisting);
                    }
                    catch { /* If this fails, so be it - do nothing */ }


                    //copy the transformed file over the untransformed "template" config
                    File.Move(transformedConfigTempName, destinationConfigName, MoveOptions.ReplaceExisting);
                }

				OnProgress( ctx, string.Format( "Overwrote config file: {0}  [with]  {1}", destinationConfigName, transformedConfigTempName ),
					StatusType.Running, _startInfo.InstanceId, _cheapSequence++, false, null );
			}
		}
		#endregion

		#region Services/AppPools
		void StopServerProcesses(string serverName)
		{
			Parallel.ForEach( _wfp.Services, s => StopServices( serverName, s ) );
			Parallel.ForEach( _wfp.AppPools, a => StopAppPools( serverName, a ) );
		}

		void StopServices(string serverName, Service s)
		{
            String ctx = "StopServices";
            if (_startInfo.IsDryRun)
            {
                ctx = "DryRun:StopServices";
                OnStepProgress(ctx, "DryRun Flag Is Set.  Services Will NOT Be Stopped.");
            }

			OnStepProgress( ctx, string.Format( "{0}: {1}, {2}, {3}", serverName, s.Name, s.StopTimeoutToTerminate, s.StartModeOnStop ) );
			ServiceConfig sc = ServiceUtil.QueryStatus( s.Name, serverName );

            if (!_startInfo.IsDryRun)
            {
                if (sc.ProcessId > 0)
                {
                    //only cache the existing StartMode if it is not specified in the parms 
                    if (s.StartModeOnStart == ServiceStartMode.Unchanged)
                    {
                        s.StartModeOnStart = sc.StartMode;
                    }
                    ServiceUtil.Stop(s.Name, serverName, s.StopTimeoutToTerminate, s.StartModeOnStop);
                }

                if (s.Reprovision)
                {
                    ServiceReturnCode result = ServiceUtil.DeleteService(s.Name, serverName);

                    if (result != ServiceReturnCode.Success && result != ServiceReturnCode.ServiceNotFound)
                    {
                        throw new Exception(string.Format("Could not delete service {0} on {1}.  Result: {2}",
                            s.Name, serverName, result));
                    }
                    else
                    {
                        string msg = result == ServiceReturnCode.ServiceNotFound ? "not found" : "successfully deleted";
                        OnStepProgress(ctx, string.Format("Reprovision = true, Service [{0}] {1}, proceeding.", s.Name, msg));
                    }
                }

                sc = ServiceUtil.QueryStatus(s.Name, serverName);
            }
			OnStepProgress( ctx, sc.ToXml( false ) );
		}

		void StopAppPools(string serverName, AppPool a)
		{
            String ctx = "StopAppPools";
            if (_startInfo.IsDryRun)
            {
                ctx = "DryRun:StopAppPools";
                OnStepProgress(ctx, "DryRun Flag Is Set.  AppPools Will NOT Be Stopped.");
            }

            OnStepProgress( ctx, string.Format( "{0}: {1}", serverName, a.Name ) );
            if (!_startInfo.IsDryRun)
    			AppPoolUtil.Stop( a.Name, serverName, 30000, 3, 30000 );

			AppPoolConfig ap = AppPoolUtil.QueryStatus( a.Name, false, serverName );
			OnStepProgress( ctx, ap.ToXml( false ) );
		}

		void StartServerProcesses(string serverName)
		{
			Parallel.ForEach( _wfp.Services, s => StartServices( serverName, s ) );
			Parallel.ForEach( _wfp.AppPools, a => StartAppPools( serverName, a ) );
		}

		void StartServices(string serverName, Service s)
		{
            String ctx = "StartServices";
            if (_startInfo.IsDryRun)
            {
                ctx = "DryRun:StartServices";
                OnStepProgress(ctx, "DryRun Flag Is Set.  Services Will NOT Be Started.");
            }

            string msg = string.Format( "{0}: {1}", serverName, s.Name );
			OnStepProgress( ctx, string.Format( "{0}, StartModeOnStart: {1}, StartService: {2}", msg, s.StartModeOnStart, s.StartService ) );

            if (!_startInfo.IsDryRun)
            {
                if (s.Reprovision)
                {
                    string servicePath = Utils.PathCombine(_wfp.TargetServerDestination, s.Path);

                    string password = null;
                    if (!string.IsNullOrWhiteSpace(s.Password))
                    {
                        Cipher c = new Cipher(settings.Default.passwordPassPhrase, settings.Default.passwordSaltValue, settings.Default.passwordInitVector);
                        password = c.Decrypt(s.Password);
                        if (password.ToLower().StartsWith("unable"))
                        {
                            throw new Exception(string.Format("Unable to decrypt password for service configuration: {0}, {1}:[{2}]",
                                serverName, s.Name, servicePath));
                        }
                    }

                    ServiceReturnCode result = ServiceUtil.CreateService(s.Name, serverName, s.Name, servicePath,
                        s.StartModeOnStart, s.UserName, password, s.Parameters);
                    if (result != ServiceReturnCode.Success)
                    {
                        throw new Exception(string.Format("Could not create service [{0}] on {1}:[{2}] with {3}.  Result: {4}",
                            s.Name, serverName, servicePath, s.UserName, result));
                    }
                    else
                    {
                        OnStepProgress(ctx, string.Format("Reprovision = true, Service [{0}] on {1}:[{2}] with {3} successfully created.",
                            s.Name, serverName, servicePath, s.UserName));
                    }
                }

                if (s.StartService)
                {
                    ServiceUtil.Start(s.Name, serverName, s.StartTimeoutToMonitor, s.StartModeOnStart);
                }
            }

			ServiceConfig sc = ServiceUtil.QueryStatus( s.Name, serverName );
			OnStepProgress( ctx, sc.ToXml( false ) );
		}

		void StartAppPools(string serverName, AppPool a)
		{
            String ctx = "StartAppPools";
            if (_startInfo.IsDryRun)
            {
                ctx = "DryRun:StartAppPools";
                OnStepProgress(ctx, "DryRun Flag Is Set.  AppPools Will NOT Be Started.");
            }

            string msg = string.Format( "{0}: {1}", serverName, a.Name );
			OnStepProgress( ctx, string.Format( "{0}, StartPool: {1}", msg, a.StartPool ) );

			if( a.StartPool && !_startInfo.IsDryRun)
			{
				AppPoolUtil.Start( a.Name, serverName, 10000 );
			}

			AppPoolConfig ap = AppPoolUtil.QueryStatus( a.Name, false, serverName );
			OnStepProgress( ctx, ap.ToXml( false ) );
		}
		#endregion 

		#region File/Directory Stuff
		/// <summary>
		/// Callback handler from Directory.Copy/Move operations.
		/// </summary>
		/// <returns>Returns 'Continue' to localized error handling (in the source caller).</returns>
		CopyMoveProgressResult CopyMoveProgressHandler(long totalFileSize, long totalBytesTransferred,
			long streamSize, long streamBytesTransferred, int streamNumber,
			CopyMoveProgressCallbackReason callbackReason, object userData)
		{
			if( userData != null )
			{
				string[] files = userData.ToString().Split( '|' );
				OnProgress( "CopyMoveProgress",
					string.Format( "Copied file: {0}  [to]  {1}", files[0], files[1] ),
					StatusType.Running, _startInfo.InstanceId, _cheapSequence++, false, null );
			}

			return CopyMoveProgressResult.Continue;
		}

		/// <summary>
		/// Executes truncate against target path.
		/// </summary>
		/// <param name="targetPath">The name of the directory to truncate.</param>
		void TruncateTargetDirectory(string targetPath)
		{
			try
			{
                if ( IsS3Url( targetPath ) )
                {
                    string[] url = SplitS3Url( targetPath );
                    S3Client.DeleteBucketObjects( url[0], url[1], LogFileCopyProgress );
                }
                else
    				DeleteFolder( targetPath, true );
			}
			catch( Exception ex )
			{
				string msg = string.Format( "TruncateTargetDirectory failed on: targetPath:[{0}]", targetPath );
                OnProgress(msg, ex.Message, StatusType.Running, _startInfo.InstanceId, _cheapSequence++, false, ex);
				throw ex;
			}
		}

		/// <summary>
		/// Executes DeleteManifest against target path.
		/// </summary>
		/// <param name="rootPath">The name of the root directory; is prepended to the DeleteManifestPaths paths.</param>
		AggregateException DeleteManifestPathsContent(string rootPath)
		{
			string context = "DeleteManifest";
            if (_startInfo.IsDryRun)
            {
                context = "DryRun:DeleteManifest";
                OnStepProgress(context, "DryRun Flag Is Set.  Files Will NOT Be Deleted.");
            }

            List<Exception> exceptions = new List<Exception>();

			string relPath = string.Empty;	//just for error reporting
			string fullPath = string.Empty;
			foreach( string path in _wfp.DeleteManifest.Paths )
			{
				try
				{
					if( !string.IsNullOrEmpty( path.Trim() ) )
					{
                        if ( IsS3Url( rootPath ) )
                        {
                            fullPath = Utils.PathCombineS3( rootPath, path );
                            string[] url = SplitS3Url( fullPath );
                            S3Client.DeleteObject( url[0], url[1] );
                        }
                        else
                        {
                            relPath = path;
                            fullPath = Utils.PathCombine( rootPath, path );

                            bool isDir =
                                (File.GetAttributes( fullPath, PathFormat.FullPath ) & io.FileAttributes.Directory) ==
                                io.FileAttributes.Directory;
                            if ( isDir )
                            {
                                if ( !_startInfo.IsDryRun )
                                    DeleteFolder( fullPath, false );
                            }
                            else
                            {
                                if ( !_startInfo.IsDryRun )
                                    //true->ignoreReadOnly
                                    File.Delete( fullPath, true, PathFormat.FullPath );
                            }
                        }

						OnStepProgress( context, string.Format( "Deleted: [{0}]", fullPath ) );
					}
				}
				catch( Exception ex ) //catch and aggregate any exceptions
				{
					bool warn = _wfp.DeleteManifest.TreatExceptionsAsWarnings;
					_wfp.DeleteManifest.HasExceptions = true;
					string errType = warn ? "Warning" : "Error";
					string msg = string.Format( "{0}: DeleteManifestPaths failed on: [{1}], relativePath [{2}].  Exception: {3}",
						errType, fullPath, relPath, ex.Message );
					OnProgress( string.Format( "{0}:{1}", context, errType ),
						msg, StatusType.Running, _startInfo.InstanceId, _cheapSequence++, false, null ); //warn ? null : ex
					exceptions.Add( new Exception( msg, ex ) );
				}
			}

			if( exceptions.Count > 0 )
			{
				return new AggregateException( exceptions );
			}
			else
			{
				return null;
			}
		}

		/// <summary>
		/// Deletes the specified directory and its children, or deletes only the children.
		/// </summary>
		/// <param name="path">The name of the directory to delete.</param>
		/// <param name="enumerateChildrenForDelete">Pass true to delete only the children, false to delete the specified directory and all children.</param>
		void DeleteFolder(string path, bool enumerateChildrenForDelete)
		{
            if (_startInfo.IsDryRun)
                OnStepProgress("DryRun:DeleteFolder", "Dry Run Flag Is Set.  No Files Will Be Deleted.");
			try
			{
				if( enumerateChildrenForDelete )
				{
					string[] dirs = Directory.GetDirectories( path );
					foreach( string dir in dirs )
					{
                        if (_startInfo.IsDryRun)
                            OnStepProgress("DryRun:DeleteFolder", "Deleting Directory : " + dir);
                        else
                            //true->recursive, true->ignoreReadOnly
                            Directory.Delete( dir, true, true, PathFormat.FullPath );
					}

					string[] files = Directory.GetFiles( path );
					foreach( string file in files )
					{
                        if (_startInfo.IsDryRun)
                            OnStepProgress("DryRun:DeleteFolder", "Deleting File : " + file);
                        else
                            //true->ignoreReadOnly
                            File.Delete( file, true, PathFormat.FullPath );
					}
				}
				else
				{
                    //true->recursive, true->ignoreReadOnly
                    if (_startInfo.IsDryRun)
                        OnStepProgress("DryRun:DeleteFolder", "Deleting Directory : " + path);
                    else
                        Directory.Delete( path, true, true, PathFormat.FullPath );
				}
			}
			catch( Exception ex )
			{
				string msg = string.Format( "DeleteFolder failed on: path:[{0}], enumerateChildrenForDelete:[{1}]", path, enumerateChildrenForDelete );
                OnProgress(msg, ex.Message, StatusType.Running, _startInfo.InstanceId, _cheapSequence++, false, ex);
				throw ex;
			}
		}

		/// <summary>
		/// Copies a directory and its contents to a new location.  Notifies of each file processed via "CopyMoveProgressHandler".
		/// </summary>
		/// <param name="source">The source directory path.</param>
		/// <param name="destination">The destination directory path.</param>
		void CopyFolder(string source, string destination)
		{
			try
			{
                if ( _startInfo.IsDryRun )
                {
                    OnStepProgress( "DryRun:CopyFolder", "DryRun Flag Is Set.  No Files Will Be Copied." );
                    OnStepProgress( "DryRun:CopyFolder", "Copying From : " + source + " To " + destination );
                }
                else if ( IsS3Url( source ) )
                {
                    string[] url = SplitS3Url( source );
                    if ( IsS3Url( destination ) )
                    {
                        string[] destUrl = SplitS3Url( destination );
                        S3Client.CopyBucketObjects( url[0], url[1], destUrl[0], destUrl[1], false, LogFileCopyProgress );
                    }
                    else
                        S3Client.CopyBucketObjectsToLocal( url[0], destination, url[1], false, LogFileCopyProgress );
                }
                else
                {
                    if ( IsS3Url( destination ) )
                    {
                        string[] destUrl = SplitS3Url( destination );
                        S3Client.UploadFilesToBucket( source, destUrl[0], destUrl[1], LogFileCopyProgress );
                    }
                    else
                        //CopyOptions.None overrides CopyOptions.FailIfExists, meaning, overwrite any existing files
                        Directory.Copy( source, destination, CopyOptions.None, CopyMoveProgressHandler, null, PathFormat.FullPath );
                }
			}
			catch( Exception ex )
			{
				string msg = string.Format( "CopyFolder failed on: source:[{0}], destination:[{1}]", source, destination );
                OnProgress(msg, ex.Message, StatusType.Running, _startInfo.InstanceId, _cheapSequence++, false, ex);
				throw ex;
			}
		}

		/// <summary>
		/// Moves all children within source folder to the destination path.  Does not move the source folder itself.
		/// </summary>
		/// <param name="source">The source directory path.</param>
		/// <param name="destination">The destination directory path.</param>
		void MoveFolderContent(string source, string destination)
		{
			string context = "MoveFolderContent";
            if (_startInfo.IsDryRun)
            {
                context = "DryRun:MoveFolderContent";
                OnStepProgress(context, "Dry Run Flag Is Set.  No Files Will Be Moved.");
            }

            string msg = Utils.GetHeaderMessage(
				string.Format( "Moving content to next environment staging folder: [{0}  [to]  {1}]", source, destination ) );
			if( OnStepStarting( context, msg ) )
			{
				return;
			}

			Stopwatch clock = new Stopwatch();
			clock.Start();

			try
			{
                if ( IsS3Url( source ) )
                {
                    string[] sourceUrl = SplitS3Url( source );
                    if ( IsS3Url(destination) )
                    {
                        string[] destinationUrl = SplitS3Url( destination );
                        S3Client.MoveBucketObjects( sourceUrl[0], sourceUrl[1], destinationUrl[0], destinationUrl[1], false, LogFileCopyProgress );
                    }
                    else
                    {
                        S3Client.MoveBucketObjectsToLocal( sourceUrl[0], destination, sourceUrl[1], false, LogFileCopyProgress );
                    }
                }
                else
                {
                    if ( IsS3Url( destination ) )
                    {
                        string[] destinationUrl = SplitS3Url( destination );
                        S3Client.MoveFilesToBucket( source, destinationUrl[0], destinationUrl[1], LogFileCopyProgress );
                    }
                    else
                    {
                        string[] dirs = Directory.GetDirectories( source );
                        foreach ( string dir in dirs )
                        {
                            string folder = Path.GetDirectoryNameWithoutRoot( dir + @"\\" );
                            string dst = Utils.PathCombine( destination, folder );

                            if ( _startInfo.IsDryRun )
                                OnStepProgress( context, "Moving Folder : " + dir + " To " + dst );
                            else
                            {
                                //CopyOptions.None overrides CopyOptions.FailIfExists, meaning, overwrite any existing files
                                Directory.Copy( dir, dst, CopyOptions.None, CopyMoveProgressHandler, null, PathFormat.FullPath );
                                //true->recursive, true->ignoreReadOnly
                                Directory.Delete( dir, true, true, PathFormat.FullPath );

                                #region note from Steve: do not switch back to Directory.Move
                                //note: Directory.Move with MoveOptions.ReplaceExisting is implemented as a folder "overwrite" within
                                //		Alphaleonis, where the destinationPath is first _deleted_, then the source is copied to dest.
                                //		Deliverance specifications for MoveToNext are to implement a Directory _merge_, so I re-coded
                                //		as a Copy + Delete.  Original implementation was:
                                //Directory.Move( dir, dst,
                                //	MoveOptions.ReplaceExisting | MoveOptions.WriteThrough, PathFormat.FullPath );
                            }
                            #endregion
                        }

                        string[] files = Directory.GetFiles( source );
                        foreach ( string file in files )
                        {
                            string dst = Utils.PathCombine( destination, Path.GetFileName( file ) );
                            if ( _startInfo.IsDryRun )
                                OnStepProgress( context, "Moving File : " + file + " To " + dst );
                            else
                            {
                                File.Move( file, dst,
                                MoveOptions.ReplaceExisting | MoveOptions.WriteThrough, PathFormat.FullPath );
                            }
                        }
                    }
                }

                clock.Stop();
				msg = Utils.GetHeaderMessage( string.Format( "End move for: [{0}  [to]  {1}]: Total Execution Time: {2}",
					source, destination, clock.ElapsedSeconds() ) );
				OnStepFinished( context, msg );
			}
			catch( Exception ex )
			{
				msg = string.Format( "MoveFolderContent failed on: source:[{0}], destination:[{1}]", source, destination );
                OnProgress(msg, ex.Message, StatusType.Running, _startInfo.InstanceId, _cheapSequence++, false, ex);
				throw ex;
			}
		}
		#endregion

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

        #region S3 Functions

        public bool UsesAwsS3()
        {
            return (S3Client != null);
        }

        public bool IsS3Url(string url)
        {
            return url.StartsWith( @"s3://", StringComparison.OrdinalIgnoreCase );
        }

        public bool S3Exists(string s3Path)
        {
            string[] url = SplitS3Url( s3Path );
            return S3Client.Exists( url[0], url[1] );
        }

        public string[] S3ReadAllLines(string s3Path)
        {
            string[] url = SplitS3Url( s3Path );
            return S3Client.ReadAllLines( url[0], url[1] );
        }

        public string[] SplitS3Url(string url)
        {
            if ( url.StartsWith( @"s3://", StringComparison.OrdinalIgnoreCase ) )
            {
                char[] seperators = { '/', '\\' };
                String s3Path = url.Substring( 5 );
                return s3Path.Split( seperators, 2 );
            }
            else
                return null;
        }

        public void InitializeS3Client(string accessKey, string secretKey, RegionEndpoint endPoint)
        {
            if ( String.IsNullOrWhiteSpace( accessKey ) || String.IsNullOrWhiteSpace( secretKey ) )
                this.S3Client = new S3Client( endPoint );
            else
                this.S3Client = new S3Client( accessKey, secretKey, endPoint );
        }

        #endregion
    }
}