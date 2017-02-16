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
	[Serializable, XmlRoot( "DeliveranceAdapter" )]
	public class WorkflowParameters
	{
		/// <summary>
		/// Default ctor
		/// </summary>
		public WorkflowParameters() { }

		#region properties
		string _user = string.Empty;

		public string PackageKey { get; set; }
		public string RequestNumber { get; set; }
		public string EncryptedUser { get; set; }
		public string User
		{
			get { return _user; }
			set
			{
				string[] u = value.Split( '\\' );
				_user = (u.Length > 1) ? u[1] : u[0];
			}
		}
		public string LogPath { get; set; }

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


		//the parameters not currently in use, possible future use
		public List<KeyValue> KeyValueMap { get; set; }
		public string CreateRemedyTicket { get; set; }
		public string NotificationGroup { get; set; }
		public string ApplicationName { get; set; }
		public string ControlledMigrationFlag { get; set; }
		public string CachedFilesFolder { get; set; }
		public string StagingFolder { get; set; }
		public string ProcessedFolder { get; set; }
		public string EnvironmentFolder { get; set; }
		public string MigrationType { get; set; }



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


		public void PrepareAndValidate()
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
			if( KeyValueMap == null )
			{
				KeyValueMap = new List<KeyValue>();
			}
			#endregion

			#region IsSourceDirectoryValid
			SourceDirectory = Utils.PathCombine( DeploymentRoot, SourceDirectory );
			IsSourceDirectoryValid = Directory.Exists( SourceDirectory );
			if( MoveToNext )
			{
				NextEnvironmentSourceDirectory = Utils.PathCombine( DeploymentRoot, NextEnvironmentSourceDirectory );
				IsNextEnvironmentSourceDirectoryValid = Directory.Exists( NextEnvironmentSourceDirectory );
			}
			#endregion

			#region IsTargetRemoteDestinationValid
			IsTargetRemoteDestinationValid = true;
			if( HasTargetRemoteDestination )
			{
				IsTargetRemoteDestinationValid = Directory.Exists( TargetRemoteDestination );
			}
			#endregion

			#region IsBackupRemoteDestinationValid
			IsBackupRemoteDestinationValid = true;
			if( HasBackupRemoteDestination )
			{
				BackupRemoteDestination = Utils.PathCombine( DeploymentRoot, BackupRemoteDestination );
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
								BackupServerDestination = Utils.PathCombine( DeploymentRoot, BackupServerDestination );
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
			DeleteManifest.Validate( SourceDirectory );
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

					cf.NameExists = File.Exists( configFile );
					cf.TransformFileExists = File.Exists( transformFile );
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


		public void Validate(string sourceDirectory)
		{
			IsFileNameValid = true;
			if( HasFileName )
			{
				FileName = Utils.PathCombine( sourceDirectory, FileName );
				IsFileNameValid = File.Exists( FileName );
				if( IsFileNameValid )
				{
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