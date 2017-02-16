using System;
using System.Management;
using System.Configuration;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Synapse.Handlers.Legacy.WinCore
{
	public abstract class WmiManagementBase
	{
		protected ManagementScope Scope { get; private set; }

		protected WmiManagementBase(string host, WmiPath wmiPath)
		{
			switch( wmiPath )
			{
				case WmiPath.Root:
				{
					string path = string.Format( @"\\{0}\root\cimv2", host );
					Scope = new ManagementScope( path );
					break;
				}
				case WmiPath.IIS:
				{
					string path = string.Format( @"\\{0}\root\MicrosoftIISv2", host );
					Scope = new ManagementScope( path );
					break;
				}
			}

			Scope.Options.Impersonation = ImpersonationLevel.Impersonate;
			Scope.Options.EnablePrivileges = true;
			Scope.Options.Authentication = AuthenticationLevel.PacketPrivacy;
			Scope.Connect();
		}

		public ManagementObjectCollection Query(ObjectQuery query, WmiPath wmiPath)
		{
			ManagementObjectCollection mgmtObjs = null;

			switch( wmiPath )
			{
				case WmiPath.Root:
				{
					EnumerationOptions eo = new EnumerationOptions();
					eo.Rewindable = false;
					eo.ReturnImmediately = true;
					eo.EnumerateDeep = false;
					eo.EnsureLocatable = false;
					eo.DirectRead = true;
					eo.EnsureLocatable = false;
					eo.UseAmendedQualifiers = false;
					eo.BlockSize = 10;

					mgmtObjs = new ManagementObjectSearcher( Scope, query, eo ).Get();

					break;
				}
				case WmiPath.IIS:
				{
					mgmtObjs = new ManagementObjectSearcher( Scope, query ).Get();

					break;
				}
			}

			return mgmtObjs;
		}

		public ManagementClass GetManagementClass( string className )
		{
			return new ManagementClass( Scope, new ManagementPath( className ), null );
		}

		public ManagementObject CreateManagementObject( string className )
		{
			ManagementClass c = new ManagementClass( Scope, new ManagementPath( className ), null );
			return c.CreateInstance();
		}

		public ManagementObject GetInstance(string path)
		{
			return new ManagementObject( Scope, new ManagementPath( path ), null );
		}

		public void ApplySettings(ManagementObject target, Dictionary<string, object> properties)
		{
			foreach( KeyValuePair<string,object> kvp in properties )
			{
				target.Properties[kvp.Key].Value = kvp.Value;
			}
			target.Put();
		}
	}
}