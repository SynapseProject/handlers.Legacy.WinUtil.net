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

public class StandardCopyProcessHandler : HandlerRuntimeBase
{
    override public ExecuteResult Execute(HandlerStartInfo startInfo)
    {
        XmlSerializer ser = new XmlSerializer(typeof(WorkflowParameters));
        WorkflowParameters wfp = new WorkflowParameters();
        TextReader reader = new StringReader(startInfo.Parameters);
        wfp = (WorkflowParameters)ser.Deserialize(reader);

        Workflow wf = new Workflow(wfp);

        wf.OnLogMessage = this.OnLogMessage;
        wf.OnProgress = this.OnProgress;

        wf.ExecuteAction(startInfo);

        return new ExecuteResult() { Status = StatusType.Complete };
    }

    public override object GetConfigInstance()
    {
        return null;
    }

    public override object GetParametersInstance()
    {
        WorkflowParameters wfp = new WorkflowParameters();

        wfp.DeploymentRoot = @"C:\Temp";
        wfp.SourceDirectory = @"Source";
        wfp.NextEnvironmentSourceDirectory = @"NextSource";
        wfp.TruncateNextEnvironmentDirectory = true;
        wfp.TargetRemoteDestination = @"\\server\share\dir1\dir2\target";
        wfp.BackupRemoteDestination = @"BackupRemote";

        wfp.Servers = new List<string>();
        wfp.Servers.Add( "localhost" );

        wfp.TargetServerDestination = @"C:\Temp\Target";
        wfp.BackupServerDestination = @"BackupServer";
        wfp.TruncateTargetDirectory = false;

        wfp.DeleteManifest = new DeleteManifestFile();
        wfp.DeleteManifest.FileName = @"C:\Temp\DeleteFileManifest\DeleteMe.txt";
        wfp.DeleteManifest.TreatExceptionsAsWarnings = true;

        wfp.Services = new List<Service>();
        Service svc = new Service();
        svc.Name = @"MyServiceName";
        svc.StopTimeoutToTerminate = 30000;
        svc.StartTimeoutToMonitor = 60000;
        svc.StartModeOnStart = Synapse.Handlers.Legacy.WinCore.ServiceStartMode.Automatic;
        svc.StartModeOnStop = Synapse.Handlers.Legacy.WinCore.ServiceStartMode.Disabled;
        svc.StartService = true;
        svc.Path = @"C:\Temp\MyService.exe";
        svc.UserName = @"MyUserName";
        svc.Password = @"MyPassword";
        svc.Reprovision = true;
        svc.Parameters = "-p1 My -p2 Service -p3 Parameters";
        wfp.Services.Add( svc );

        wfp.AppPools = new List<AppPool>();
        AppPool pool = new AppPool();
        pool.Name = @"MyAppPool";
        pool.StartPool = true;
        wfp.AppPools.Add( pool );

        wfp.ConfigFiles = new List<ConfigFile>();
        ConfigFile config = new ConfigFile();
        config.Name = @"MyConfigFile.txt";
        config.TransformFile = @"MyTransformFile.xml";
        config.TransformInPlace = false;
        wfp.ConfigFiles.Add( config );

        String xml = wfp.Serialize( false );
        xml = xml.Substring( xml.IndexOf( "<" ) );
        return xml;

    }
}
