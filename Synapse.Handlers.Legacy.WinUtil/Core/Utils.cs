using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.Text.RegularExpressions;

namespace Synapse.Handlers.Legacy.WinCore
{
	internal static class Utils
	{
		const string _lines = "--------------------------";

		public static double ElapsedSeconds(this Stopwatch stopwatch)
		{
			return TimeSpan.FromMilliseconds( stopwatch.ElapsedMilliseconds ).TotalSeconds;
		}

		public static string GetMessagePadLeft(string header, object message, int width)
		{
			return string.Format( "{0}: {1}", header.PadLeft( width, '.' ), message );
		}

		public static string GetMessagePadRight(string header, object message, int width)
		{
			return string.Format( "{0}: {1}", header.PadRight( width, '.' ), message );
		}

		public static string GetHeaderMessage(string header)
		{
			return string.Format( "{1}  {0}  {1}", header, _lines );
		}
        public static string CompressXml(string xml)
        {
            string str = Regex.Replace(xml, @"(>\s*<)", @"><");
            return str;
        }

        //stolen from Suplex.General.XmlUtils
        public static string Serialize<T>(object data, bool indented = true, bool omitXmlDeclaration = true, bool omitXmlNamespace = true)
		{
			XmlWriterSettings settings = new XmlWriterSettings();
			settings.OmitXmlDeclaration = omitXmlDeclaration;
			settings.ConformanceLevel = ConformanceLevel.Auto;
			settings.CloseOutput = false;
			settings.Encoding = Encoding.Unicode;
			settings.Indent = indented;

			MemoryStream ms = new MemoryStream();
			XmlSerializer s = new XmlSerializer( typeof( T ) );
			XmlWriter w = XmlWriter.Create( ms, settings );
			if( omitXmlNamespace )
			{
				XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
				ns.Add( "", "" );
				s.Serialize( w, data, ns );
			}
			else
			{
				s.Serialize( w, data );
			}
			string result = Encoding.Unicode.GetString( ms.GetBuffer(), 0, (int)ms.Length );
			w.Close();
			return result;
		}
	}
}