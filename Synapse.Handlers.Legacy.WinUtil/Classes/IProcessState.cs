using System;

namespace Synapse.Handlers.Legacy.WinCore
{
	public interface IProcessState
	{
		string ServerName { get; set; }
		string State { get; set; }

		string ToXml(bool indent);
	}
}