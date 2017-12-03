using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml;
using System.Xml.Serialization;
using System.IO;

using Synapse.Handlers.Legacy.StandardCopyProcess;

using Synapse.Core;
using Synapse.Core.Utilities;

public class StandardCopyProcessHandler : HandlerRuntimeBase
{
    public HandlerConfig config = null;

    public override IHandlerRuntime Initialize(string config)
    {
        this.config = DeserializeOrNew<HandlerConfig>( config );
        return this;
    }

    override public ExecuteResult Execute(HandlerStartInfo startInfo)
    {
        XmlSerializer ser = new XmlSerializer(typeof(WorkflowParameters));
        WorkflowParameters wfp = new WorkflowParameters();
        TextReader reader = new StringReader(startInfo.Parameters);
        wfp = (WorkflowParameters)ser.Deserialize(reader);

        Workflow wf = new Workflow(wfp);

        wf.OnLogMessage = this.OnLogMessage;
        wf.OnProgress = this.OnProgress;

        if ( config.Aws != null )
            wf.InitializeS3Client( config.Aws.AccessKey, config.Aws.SecretKey, config.Aws.AwsRegion );

        wf.ExecuteAction(startInfo);

        return new ExecuteResult() { Status = StatusType.Complete };
    }

    public override object GetConfigInstance()
    {
        return null;
    }

    public override object GetParametersInstance()
    {
        WorkflowParameters wfp = new WorkflowParameters
        {
            DeploymentRoot = @"C:\Temp",
            SourceDirectory = @"Source",
            NextEnvironmentSourceDirectory = @"NextSource",
            TruncateNextEnvironmentDirectory = true,
            TargetRemoteDestination = @"\\server\share\dir1\dir2\target",
            BackupRemoteDestination = @"BackupRemote",

            TargetServerDestination = @"C:\Temp\Target",
            BackupServerDestination = @"BackupServer",
            TruncateTargetDirectory = false,

            Servers = new List<string>()
        };
        wfp.Servers.Add( "localhost" );

        wfp.DeleteManifest = new DeleteManifestFile
        {
            FileName = @"C:\Temp\DeleteFileManifest\DeleteMe.txt",
            TreatExceptionsAsWarnings = true
        };

        wfp.Services = new List<Service>();
        Service svc = new Service
        {
            Name = @"MyServiceName",
            StopTimeoutToTerminate = 30000,
            StartTimeoutToMonitor = 60000,
            StartModeOnStart = Synapse.Handlers.Legacy.WinCore.ServiceStartMode.Automatic,
            StartModeOnStop = Synapse.Handlers.Legacy.WinCore.ServiceStartMode.Disabled,
            StartService = true,
            Path = @"C:\Temp\MyService.exe",
            UserName = @"MyUserName",
            Password = @"MyPassword",
            Reprovision = true,
            Parameters = "-p1 My -p2 Service -p3 Parameters"
        };
        wfp.Services.Add( svc );

        wfp.AppPools = new List<AppPool>();
        AppPool pool = new AppPool
        {
            Name = @"MyAppPool",
            StartPool = true
        };
        wfp.AppPools.Add( pool );

        wfp.ConfigFiles = new List<ConfigFile>();
        ConfigFile config = new ConfigFile
        {
            Name = @"MyConfigFile.txt",
            TransformFile = @"MyTransformFile.xml",
            TransformInPlace = false
        };
        wfp.ConfigFiles.Add( config );

        string xml = wfp.Serialize( indented: true );
        xml = xml.Replace( "\r\n", "\n" ); //this is only to make the XML pretty, like me
        return xml;
    }
}