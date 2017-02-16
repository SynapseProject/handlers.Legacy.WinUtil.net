using System;
using System.Xml.Serialization;
using System.Xml;
using System.IO;

namespace Synapse.Handlers.Legacy.WinCore
{
	[XmlRoot( "Task" )]
	public class WorkflowParameters
	{
		public WorkflowParameters()
		{
		}

		[XmlAttribute()]
		public string ServerName { get; set; }
		[XmlAttribute()]
		public string TargetName { get; set; }
		[XmlAttribute()]
		public string TargetPath { get; set; }
		[XmlAttribute()]
		public string TargetUserName { get; set; }
		[XmlAttribute()]
		public string TargetPassword { get; set; }
		[XmlAttribute()]
		public ServiceAction Action { get; set; }
		[XmlAttribute()]
		public ServiceType TargetType { get; set; }
		[XmlAttribute()]
		public int ServiceStopTimeToTerminate { get; set; }
		[XmlAttribute()]
		public int ServiceStartTimeToMonitor { get; set; }
		[XmlAttribute()]
		public ServiceStartMode ServiceStartModeOnStart { get; set; }
		[XmlAttribute()]
		public ServiceStartMode ServiceStartModeOnStop { get; set; }
        [XmlAttribute()]
        public string ServiceParameters { get; set; }

		public void PrepareAndValidate()
		{
			IsValid = !string.IsNullOrWhiteSpace( ServerName ) &&
				!string.IsNullOrWhiteSpace( TargetName );
		}

		public bool IsValid { get; set; }

		public static WorkflowParameters Deserialize(XmlElement el)
		{
			XmlSerializer s = new XmlSerializer( typeof( WorkflowParameters ) );
			return (WorkflowParameters)s.Deserialize( new System.IO.StringReader( el.OuterXml ) );
		}

		public static WorkflowParameters Deserialize(string filePath)
		{
			using( FileStream fs = new FileStream( filePath, FileMode.Open, FileAccess.Read ) )
			{
				XmlSerializer s = new XmlSerializer( typeof( WorkflowParameters ) );
				return (WorkflowParameters)s.Deserialize( fs );
			}
		}

		public string Serialize(bool indented = false)
		{
			return Utils.Serialize<WorkflowParameters>( this, indented );
		}


		#region WorkflowParameters Members

		public WorkflowParameters FromXmlElement(XmlElement el)
		{
			throw new NotImplementedException();
		}

		#endregion
	}

	[XmlRoot( "Tasks" )]
	public class WinProcTaskContainer : WorkflowParameters
	{
		[XmlElement( "Task" )]
		public WorkflowParameters tasks;

		public static new WinProcTaskContainer Deserialize(XmlElement el)
		{
			XmlSerializer s = new XmlSerializer( typeof( WorkflowParameters ) );
			return (WinProcTaskContainer)s.Deserialize( new System.IO.StringReader( el.OuterXml ) );
		}

		public static new WinProcTaskContainer Deserialize(string filePath)
		{
			using( FileStream fs = new FileStream( filePath, FileMode.Open, FileAccess.Read ) )
			{
				XmlSerializer s = new XmlSerializer( typeof( WinProcTaskContainer ) );
				return (WinProcTaskContainer)s.Deserialize( fs );
			}
		}

		public new string Serialize(bool indented = false)
		{
			return Utils.Serialize<WinProcTaskContainer>( this, indented );
		}

		#region WorkflowParameters Members

		public new WorkflowParameters FromXmlElement(XmlElement el)
		{
			throw new NotImplementedException();
		}

		#endregion
	}

	[XmlRoot( "WinProcAdapter" )]
	public class WinProcAdapterContainer : WorkflowParameters
	{
		/// <summary>
		/// default ctor
		/// </summary>
		public WinProcAdapterContainer() { }

		[XmlElement( "Tasks" )]
		public WinProcTaskContainer winProcAdapter;

		public new static WinProcAdapterContainer Deserialize(XmlElement el)
		{
			XmlSerializer s = new XmlSerializer( typeof( WinProcAdapterContainer ) );
			return (WinProcAdapterContainer)s.Deserialize( new System.IO.StringReader( el.OuterXml ) );
		}

		public new static WinProcAdapterContainer Deserialize(string filePath)
		{
			using( FileStream fs = new FileStream( filePath, FileMode.Open, FileAccess.Read ) )
			{
				XmlSerializer s = new XmlSerializer( typeof( WinProcAdapterContainer ) );
				return (WinProcAdapterContainer)s.Deserialize( fs );
			}
		}

		public new string Serialize(bool indented = false)
		{
			return Utils.Serialize<WinProcAdapterContainer>( this, indented );
		}

		public new WorkflowParameters FromXmlElement(XmlElement el)
		{
			XmlSerializer s = new XmlSerializer( typeof( WinProcAdapterContainer ) );
			return (WinProcAdapterContainer)s.Deserialize( new System.IO.StringReader( el.OuterXml ) );
		}
	}
}