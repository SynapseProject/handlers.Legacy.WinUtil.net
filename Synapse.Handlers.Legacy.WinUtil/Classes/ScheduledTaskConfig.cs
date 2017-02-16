using System;
using System.Text;
using System.Xml.Serialization;
using Microsoft.Win32.TaskScheduler;

namespace Synapse.Handlers.Legacy.WinCore
{
	[Serializable]
	public class ScheduledTaskConfig : IProcessState
	{
		string _replace = "<TaskInfo />";
		public ScheduledTaskConfig()
		{
			ServerName = "Unknown";
			TaskName = "Unknown";
			State = "Unknown";
			TaskInfo = string.Empty;
		}

		[XmlElement]
		public string ServerName { get; set; }
		[XmlElement]
		public string TaskName { get; set; }
		[XmlElement]
		public string State { get; set; }
		[XmlElement]
		public string TaskInfo { get; set; }

		[XmlIgnore]
		public TaskDefinition TaskDefinition { get; set; }


		public string ToXml(bool indent)
		{
			string xml = Utils.Serialize<ScheduledTaskConfig>( this, indent );

			//ss: Steve did this.  I know, it's stupid.  The TaskScheduler author didn;t include parameterless ctors on
			//    the TaskDefinition class or its members, and I didn;t want to manually serialize his stuff.  Faster
			//    to get his Xml and doctor the presentation.
			string[] xmlText = this.TaskDefinition.XmlText.Split( new string[] { "\r\n" }, StringSplitOptions.None );
			StringBuilder s = new StringBuilder();
			//skip the first line as it's the Xml declaration
			s.AppendLine( string.Format( "{0}", xmlText[1] ) );
			for( int c = 2; c < xmlText.Length - 1; c++ )
			{
				string line = xmlText[c];
				s.AppendLine( string.Format( "  {0}", line ) );
			}
			//don't Append[Line] the very last line
			s.Append( string.Format( "  {0}", xmlText[xmlText.Length - 1] ) );

			//replace the empty <TaskInfo /> element
			xml = xml.Replace( _replace, s.ToString() );

			return xml;
		}
	}
}