using System;
using System.Collections.Generic;
using io = System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

using Alphaleonis.Win32.Filesystem;
using Synapse.Handlers.Legacy.WinCore;

namespace Synapse.Handlers.Legacy.StandardCopyProcess
{
	[Serializable, XmlRoot( "StandardCopy" )]
	public class WorkflowParameters
	{
		/// <summary>
		/// Default ctor
		/// </summary>
		public WorkflowParameters() { }

		#region properties
		string _user = string.Empty;

		public string DeploymentRoot { get; set; }
		public string SourceDirectory { get; set; }
		[XmlIgnore()]
		public bool IsSourceDirectoryValid { get; private set; }

		public string NextEnvironmentSourceDirectory { get; set; }
		[XmlIgnore()]
		public bool MoveToNext { get { return !string.IsNullOrWhiteSpace( NextEnvironmentSourceDirectory ); } }
		[XmlIgnore()]
        public bool IsNextEnvironmentSourceDirectoryValid { get; private set; }
        public bool TruncateNextEnvironmentDirectory { get; set; }

		public string TargetRemoteDestination { get; set; }
		[XmlIgnore()]
		public bool HasTargetRemoteDestination { get { return !string.IsNullOrWhiteSpace( TargetRemoteDestination ); } }
		[XmlIgnore()]
		public bool IsTargetRemoteDestinationValid { get; private set; }

		public string BackupRemoteDestination { get; set; }
		[XmlIgnore()]
		public bool HasBackupRemoteDestination { get { return !string.IsNullOrWhiteSpace( BackupRemoteDestination ); } }
		[XmlIgnore()]
		public bool IsBackupRemoteDestinationValid { get; private set; }

		[XmlArrayItem( ElementName = "Server" )]
		public List<string> Servers { get; set; }
		[XmlIgnore()]
		public bool HasServerList { get { return Servers.Count > 0; } }
		public string TargetServerDestination { get; set; }
		[XmlIgnore()]
		public bool HasTargetServerDestination { get { return !string.IsNullOrWhiteSpace( TargetServerDestination ); } }
		[XmlIgnore()]
		public bool IsTargetServerPathValid { get; private set; }
		[XmlIgnore()]
		public bool CreateTargetServerPath { get; private set; }

		public string BackupServerDestination { get; set; }
		[XmlIgnore()]
		public bool HasBackupServerDestination { get { return !string.IsNullOrWhiteSpace( BackupServerDestination ); } }
		[XmlIgnore()]
		public bool IsBackupServerDestinationValid { get; private set; }

		public bool TruncateTargetDirectory { get; set; }

		public DeleteManifestFile DeleteManifest { get; set; }


		public List<Service> Services { get; set; }
		public List<AppPool> AppPools { get; set; }


		[XmlArrayItem( ElementName = "ConfigFile" )]
		public List<ConfigFile> ConfigFiles { get; set; }
		[XmlIgnore()]
		public bool IsConfigTransformFileListValid { get; private set; }
		[XmlIgnore()]
		internal List<ConfigFile> ConfigsWithTransformFiles { get; set; }

		[XmlIgnore()]
		public bool IsValid
		{
			get
			{
				return IsSourceDirectoryValid && IsTargetRemoteDestinationValid &&
					IsTargetServerPathValid && DeleteManifest.IsFileNameValid &&
					IsBackupRemoteDestinationValid && IsBackupServerDestinationValid &&
					IsConfigTransformFileListValid;
			}
		}
		#endregion


		public void PrepareAndValidate(Workflow wf)
		{
			#region initialize lists if required
			if( Servers == null )
			{
				Servers = new List<string>();
			}
			if( Services == null )
			{
				Services = new List<Service>();
			}
			if( AppPools == null )
			{
				AppPools = new List<AppPool>();
			}
			if( DeleteManifest == null )
			{
				DeleteManifest = new DeleteManifestFile();
			}
			if( ConfigFiles == null )
			{
				ConfigFiles = new List<ConfigFile>();
			}
            //			if( KeyValueMap == null )
            //			{
            //				KeyValueMap = new List<KeyValue>();
            //			}
            #endregion

            #region IsSourceDirectoryValid
            if ( !String.IsNullOrWhiteSpace( DeploymentRoot ) )
                SourceDirectory = Utils.PathCombine( DeploymentRoot, SourceDirectory );

            if ( wf.IsS3Url( SourceDirectory ) )
                IsSourceDirectoryValid = wf.S3Exists( SourceDirectory );
            else
    			IsSourceDirectoryValid = Directory.Exists( SourceDirectory );
			if( MoveToNext )
			{
                if ( !String.IsNullOrWhiteSpace( DeploymentRoot ) )
                    NextEnvironmentSourceDirectory = Utils.PathCombine( DeploymentRoot, NextEnvironmentSourceDirectory );
                if ( wf.IsS3Url( NextEnvironmentSourceDirectory ) )
                    IsNextEnvironmentSourceDirectoryValid = wf.S3Exists( NextEnvironmentSourceDirectory );
                else
    				IsNextEnvironmentSourceDirectoryValid = Directory.Exists( NextEnvironmentSourceDirectory );
			}
			#endregion

			#region IsTargetRemoteDestinationValid
			IsTargetRemoteDestinationValid = true;
			if( HasTargetRemoteDestination )
			{
                if ( wf.IsS3Url( TargetRemoteDestination ) )
                    IsTargetRemoteDestinationValid = wf.S3Exists( TargetRemoteDestination );
                else
    				IsTargetRemoteDestinationValid = Directory.Exists( TargetRemoteDestination );
			}
			#endregion

			#region IsBackupRemoteDestinationValid
			IsBackupRemoteDestinationValid = true;
			if( HasBackupRemoteDestination )
			{
                if ( !String.IsNullOrWhiteSpace(DeploymentRoot) )
                    BackupRemoteDestination = Utils.PathCombine( DeploymentRoot, BackupRemoteDestination );
                if ( wf.IsS3Url( BackupRemoteDestination ) )
                    IsBackupRemoteDestinationValid = wf.S3Exists( BackupRemoteDestination );
                else
    				IsBackupRemoteDestinationValid = Directory.Exists( BackupRemoteDestination );
			}
			#endregion

			#region IsTargetServerPathValid/IsBackupServerDestinationValid
			IsTargetServerPathValid = true;
			IsBackupServerDestinationValid = true;

			CreateTargetServerPath = false;
			foreach(Service service in Services)
			{
				if( service.Reprovision )
				{
					CreateTargetServerPath = true;
					break;
				}
			}

			if( HasServerList && HasTargetServerDestination )
			{
				int c = 0;
				string serverDest = string.Empty;
				foreach( string server in Servers )
				{
					bool hasAccess = true; //{CheckAccessToServer??};
					if( hasAccess )
					{
						serverDest = Utils.GetServerLongPath( server, TargetServerDestination );
						if( !CreateTargetServerPath )
						{
							IsTargetServerPathValid &= Directory.Exists( serverDest, PathFormat.LongFullPath );
						}

						if( HasBackupServerDestination )
						{
							if( c == 0 )
							{
                                if ( !String.IsNullOrWhiteSpace( DeploymentRoot ) )
                                    BackupServerDestination = Utils.PathCombine( DeploymentRoot, BackupServerDestination );
                                if ( wf.IsS3Url( BackupServerDestination ) )
                                    IsBackupServerDestinationValid &= wf.S3Exists( BackupServerDestination );
                                else
                                    IsBackupServerDestinationValid &= Directory.Exists( BackupServerDestination, PathFormat.LongFullPath );
							}
						}
						else
						{
							IsBackupServerDestinationValid = true;
						}
					}
					else
					{
						IsTargetServerPathValid =
							IsBackupServerDestinationValid = false;
					}

					c++;
				}
			}
			else
			{
				IsTargetServerPathValid =
					IsBackupServerDestinationValid = true;
			}
			#endregion

			#region IsDeleteManifestPathValid/HasTargetDelete
			DeleteManifest.Validate( SourceDirectory, wf );
			#endregion

			#region IsConfigTransformFileListValid
			//check for presence of config/transforms
			IsConfigTransformFileListValid = true;
			ConfigsWithTransformFiles = new List<ConfigFile>();

			foreach( ConfigFile cf in ConfigFiles )
			{
				if( cf.HasTransformFile )
				{
					ConfigsWithTransformFiles.Add( cf );

					if( string.IsNullOrWhiteSpace( cf.Name ) ) { cf.Name = string.Empty; }
					string configFile = Path.GetLongPath( Utils.PathCombine( SourceDirectory, cf.Name ) );
					string transformFile = Path.GetLongPath( Utils.PathCombine( SourceDirectory, cf.TransformFile ) );
                    if ( wf.IsS3Url( SourceDirectory ) )
                    {
                        configFile = Utils.PathCombineS3( SourceDirectory, cf.Name );
                        transformFile = Utils.PathCombineS3( SourceDirectory, cf.TransformFile );

                        cf.NameExists = wf.S3Exists( configFile );
                        cf.TransformFileExists = wf.S3Exists( transformFile );
                    }
                    else
                    {
                        cf.NameExists = File.Exists( configFile );
                        cf.TransformFileExists = File.Exists( transformFile );
                    }

					if( !cf.IsValid )
					{
						IsConfigTransformFileListValid = false;
					}
				}
			}
			#endregion
		}


		public void Serialize(string filePath)
		{
			XmlSerializer s = new XmlSerializer( typeof( WorkflowParameters ) );
			XmlTextWriter w = new XmlTextWriter( filePath, Encoding.ASCII );
			w.Formatting = Formatting.Indented;
			s.Serialize( w, this );
			w.Close();
		}

        public string Serialize(bool indented = false)
        {
            return Utils.Serialize<WorkflowParameters>(this, indented);
        }


        public static WorkflowParameters Deserialize(string filePath)
		{
			using( io.FileStream fs = new io.FileStream( filePath, io.FileMode.Open, io.FileAccess.Read ) )
			{
				XmlSerializer s = new XmlSerializer( typeof( WorkflowParameters ) );
				return (WorkflowParameters)s.Deserialize( fs );
			}
		}

		public static WorkflowParameters Deserialize(XmlElement el)
		{
			XmlSerializer s = new XmlSerializer( typeof( WorkflowParameters ) );
			return (WorkflowParameters)s.Deserialize( new System.IO.StringReader( el.OuterXml ) );
		}

		public WorkflowParameters FromXmlElement(XmlElement el)
		{
			XmlSerializer s = new XmlSerializer( typeof( WorkflowParameters ) );
			return (WorkflowParameters)s.Deserialize( new System.IO.StringReader( el.OuterXml ) );
		}
	}

	[Serializable()]
	public class ConfigFile
	{
		//readonly string _backupExtension = ".original.backup";

		[XmlText()]
		public string Name { get; set; }

		[XmlIgnore]
		public bool NameExists { get; set; }

		[XmlAttribute( "TransformFile" )]
		public string TransformFile { get; set; }

		[XmlAttribute]
		public bool TransformInPlace { get; set; }

		[XmlIgnore]
		public bool HasTransformFile { get { return !string.IsNullOrWhiteSpace( TransformFile ); } }

		[XmlIgnore]
		public bool TransformFileExists { get; set; }

		#region working variables
		[XmlIgnore]
		public string TransformFileOriginalName { get; set; }

		[XmlIgnore]
		public string TransformOutFileName { get; set; }

		[XmlIgnore]
		public string TransformOutFileFullPath { get; set; }

		//[XmlIgnore]
		//public string TransformFileBackupName { get { return string.Format( "{0}{1}", TransformFileOriginalName, _backupExtension ); } }
		//
		#endregion

		[XmlIgnore]
		public bool IsValid
		{
			get
			{
				if( HasTransformFile )
				{
					return NameExists && TransformFileExists;
				}
				else
				{
					return NameExists;
				}
			}
		}

		public string ToExistsString()
		{
			return string.Format( "File: {0}, Exists: {1};  TransformFile: {2}, Exists: {3}", Name, NameExists, TransformFile, TransformFileExists );
		}
	}

	[Serializable()]
	public class Service
	{
		[XmlAttribute()]
		public bool StartService { get; set; }

		[XmlText()]
		public string Name { get; set; }

		[XmlAttribute()]
		public int StopTimeoutToTerminate { get; set; }

		[XmlAttribute()]
		public int StartTimeoutToMonitor { get; set; }

		[XmlAttribute()]
		public ServiceStartMode StartModeOnStart { get; set; }

		[XmlAttribute()]
		public ServiceStartMode StartModeOnStop { get; set; }

		[XmlAttribute()]
		public string Path { get; set; }

		[XmlAttribute()]
		public string UserName { get; set; }

		[XmlAttribute()]
		public string Password { get; set; }

		[XmlAttribute()]
		public bool Reprovision { get; set; }

        [XmlAttribute()]
        public string Parameters { get; set; }
	}

	[Serializable()]
	public class AppPool
	{
		[XmlAttribute( "StartPool" )]
		public bool StartPool { get; set; }

		[XmlText()]
		public string Name { get; set; }
	}

	[Serializable()]
	public class KeyValue
	{
		[XmlAttribute( "Key" )]
		public string Key { get; set; }

		[XmlAttribute( "Value" )]
		public string Value { get; set; }
	}

	[Serializable()]
	public class DeleteManifestFile
	{
		[XmlText()]
		public string FileName { get; set; }

		[XmlAttribute()]
		public bool TreatExceptionsAsWarnings { get; set; }

		[XmlAttribute()]
		public bool HasExceptions { get; set; }

		[XmlIgnore()]
		public bool HasFileName { get { return !string.IsNullOrWhiteSpace( FileName ); } }
		[XmlIgnore()]
		public bool IsFileNameValid { get; private set; }
		[XmlIgnore()]
		public string[] Paths { get; private set; }
		[XmlIgnore()]
		public bool HasPaths { get { return Paths != null && Paths.Length > 0; } }


		public void Validate(string sourceDirectory, Workflow wf)
		{
			IsFileNameValid = true;
			if( HasFileName )
			{
                if ( wf.IsS3Url( sourceDirectory ) )
                {
                    FileName = Utils.PathCombineS3( sourceDirectory, FileName );
                    IsFileNameValid = wf.S3Exists( FileName );
                    if (IsFileNameValid)
                        Paths = wf.S3ReadAllLines( FileName );
                }
                else
                {
                    FileName = Utils.PathCombine( sourceDirectory, FileName );
                    IsFileNameValid = File.Exists( FileName );
                    if ( IsFileNameValid )
                        Paths = File.ReadAllLines( FileName );
                }

            }
			else
			{
				Paths = new string[] { };
			}
		}
	}
}