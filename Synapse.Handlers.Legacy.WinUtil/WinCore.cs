using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml;
using System.Xml.Serialization;
using System.IO;

using Synapse.Handlers.Legacy.WinCore;

using Synapse.Core;

public class WinCoreHandler : HandlerRuntimeBase
{
    public override ExecuteResult Execute(HandlerStartInfo startInfo)
    {
        XmlSerializer ser = new XmlSerializer(typeof(WinCoreContainer));
        WinCoreContainer wfp = new WinCoreContainer();
        TextReader reader = new StringReader(startInfo.Parameters);
        wfp = (WinCoreContainer)ser.Deserialize(reader);

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
        WinCoreContainer core = new WinCoreContainer();

        core.winProcAdapter = new WinProcTaskContainer();
        core.winProcAdapter.tasks = new WorkflowParameters();

        core.winProcAdapter.tasks.ServerName = @"localhost";
        core.winProcAdapter.tasks.TargetName = @"OracleServiceXE";
        core.winProcAdapter.tasks.TargetPath = @"c:\oraclexe\app\oracle\product\11.2.0\server\bin\ORACLE.EXE XE";
        core.winProcAdapter.tasks.TargetUserName = @"OracleUser";
        core.winProcAdapter.tasks.TargetPassword = @"MyPassword";
        core.winProcAdapter.tasks.Action = ServiceAction.Create;
        core.winProcAdapter.tasks.TargetType = ServiceType.Service;
        core.winProcAdapter.tasks.ServiceStopTimeToTerminate = 60000;
        core.winProcAdapter.tasks.ServiceStartTimeToMonitor = 30000;
        core.winProcAdapter.tasks.ServiceStartModeOnStart = ServiceStartMode.Automatic;
        core.winProcAdapter.tasks.ServiceStartModeOnStop = ServiceStartMode.Manual;
        core.winProcAdapter.tasks.ServiceParameters = @"-p1 param1 -p2 param2";

        core.winProcAdapter.tasks.IsValid = true;

        String xml = core.Serialize( false );
        xml = xml.Substring( xml.IndexOf( "<" ) );
        return xml;
    }
}
