using System;
using System.Xml.Serialization;

namespace Synapse.Handlers.Legacy.WinCore
{
	[Serializable]
	public class AppPoolConfig : IProcessState
	{
		public AppPoolConfig()
		{
			ServerName = "Unknown";
			State = "Unknown";
			AppPoolName = "Unknown";
		}

		[XmlElement]
		public string ServerName { get; set; }
		[XmlElement]
		public string AppPoolName { get; set; }
		[XmlElement]
		public string LogOnAs { get; set; }
		[XmlElement]
		public string State { get; set; }


		public string ToXml(bool indent)
		{
			return Utils.Serialize<AppPoolConfig>( this, indent );
		}
	}
}