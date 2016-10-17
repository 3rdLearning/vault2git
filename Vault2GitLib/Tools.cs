using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Vault2Git.Lib
{
	public static class Tools
	{
		public static void SaveMapping<TKey, TValue>(IDictionary<TKey, TValue> mappingDictionary, string fileName)
		{
			Dictionary2Xml(mappingDictionary).Save(fileName);
		}

		public static XElement Dictionary2Xml<TKey, TValue>(IDictionary<TKey, TValue> input)
		{
			return new XElement("dictionary", new XAttribute("keyType", typeof(TKey).FullName),
				new XAttribute("valueType", typeof(TValue).FullName),
				input.Select(kp => new XElement("entry", new XAttribute("key", kp.Key), kp.Value)));
		}

		public static Dictionary<string, string> XElement2Dictionnary(XElement source)
		{
			return source.Descendants("entry").ToDictionary(xe => xe.Attribute("key").Value, xe => xe.Value);
		}

		public static Dictionary<string, string> ReadFromXml(string saveFileName)
		{
			if (!File.Exists(saveFileName))
				return null;
			try
			{
				return XElement2Dictionnary(XDocument.Load(saveFileName).Root);
			}
			catch (Exception e)
			{
				return null;
			}
		}

		public static void WriteProgressInfo(string message, TimeSpan processingTime, int completedVersion, int totalVersion,
			TimeSpan totalProcessingTime)
		{
			try
			{
				var percentage = Math.Round((decimal)(completedVersion / totalVersion) * 100, 0);
				var averageProcessingTime = TimeSpan.FromSeconds((int)(totalProcessingTime.TotalSeconds / completedVersion));
				var timeLeft = TimeSpan.FromSeconds(averageProcessingTime.TotalSeconds*(totalVersion - completedVersion));
				var etc = DateTime.Now + timeLeft;
				Console.WriteLine(
					$"[{DateTime.Now.ToLocalTime()}] - Processed version {completedVersion} of {totalVersion} ({percentage}%) in {processingTime}. ETC: {etc} (in {timeLeft} at {averageProcessingTime}/version)");
			}
			catch (Exception e)
			{
				Console.Error.WriteLine($"Unable to dump progress information: {e.Message}");
			}
		}
	}
}
