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
    int seqNo = 0;
    public override ExecuteResult Execute(HandlerStartInfo startInfo)
    {
        XmlSerializer ser = new XmlSerializer(typeof(WinProcAdapterContainer));
        WinProcAdapterContainer wfp = new WinProcAdapterContainer();
        TextReader reader = new StringReader(startInfo.Parameters);
        wfp = (WinProcAdapterContainer)ser.Deserialize(reader);

        Workflow wf = new Workflow(wfp);

        wf.OnLogMessage = this.OnLogMessage;
        wf.OnProgress = this.OnProgress;

        seqNo = 0;
        OnProgress("Execute", "Starting", StatusType.Running, startInfo.InstanceId, seqNo++);
        wf.ExecuteAction();

        return new ExecuteResult() { Status = StatusType.Complete };
    }
}
