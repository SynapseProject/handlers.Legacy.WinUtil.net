using System;
using System.Xml.Serialization;

namespace Synapse.Handlers.Legacy.WinCore
{
	[Serializable]
	public class ServiceConfig : IProcessState
	{
		public ServiceConfig()
		{
			ServerName = "Unknown";
			State = "Unknown";
			ServiceName = "Unknown";
		}

		[XmlElement]
		public string ServerName { get; set; }
		[XmlElement]
		public string DisplayName { get; set; }
		[XmlElement]
		public string ServiceName { get; set; }
		[XmlElement]
		public string Description { get; set; }
		[XmlElement]
		public string PathName { get; set; }
		[XmlElement]
		public string LogOnAs { get; set; }
		[XmlElement]
		public ServiceStartMode StartMode { get; set; }
		[XmlElement]
		public string State { get; set; }
		[XmlElement]
		public bool AcceptStop { get; set; }
		[XmlElement]
		public int ProcessId { get; set; }


		public string ToXml(bool indent)
		{
			return Utils.Serialize<ServiceConfig>( this, indent );
		}
	}
}