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
}
