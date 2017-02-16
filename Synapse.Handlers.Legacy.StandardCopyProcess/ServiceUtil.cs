using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sentosa.CommandCenter.Adapters.Deliverance.Core
{
	class ServiceUtil
	{
		internal static bool StartService(Service service)
		{
			return true;
		}

		internal static bool StopService(Service service, int p1, bool p2)
		{
			return true;
		}

		internal static bool StartAppPool(AppPool appPool)
		{
			return true;
		}

		internal static bool StopAppPool(AppPool appPool)
		{
			return true;
		}
	}
}