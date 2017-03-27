using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml;

namespace Vault2Git.Lib
{
	public static class Tools
	{
        static Dictionary<string, string> authors = new Dictionary<string, string>();
        static Dictionary<string, string> branches = new Dictionary<string, string>();

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
				var percentage = Math.Round(100 * Convert.ToDouble(completedVersion) / Convert.ToDouble(totalVersion),1);
				var averageProcessingTime = TimeSpan.FromSeconds((int)(totalProcessingTime.TotalSeconds / completedVersion));
				var timeLeft = TimeSpan.FromSeconds(averageProcessingTime.TotalSeconds * (totalVersion - completedVersion));
				var etc = DateTime.Now + timeLeft;
				Console.WriteLine(
					$"[{DateTime.Now.ToLocalTime()}] - Processed version {completedVersion} of {totalVersion} ({percentage}%) in {processingTime}. ETC: {etc} (in {timeLeft} at {averageProcessingTime}/version)");
			}
			catch (Exception e)
			{
				Console.Error.WriteLine($"Unable to dump progress information: {e.Message}");
			}
		}

        public static void ParseMapFile(string path)
        {

            if (File.Exists(path))
            {
                XmlDocument xml = new XmlDocument();
                xml.Load(path);

                foreach (XmlElement element in xml.GetElementsByTagName("author"))
                {
                    string vaultname = element.Attributes["vaultname"].Value;
                    string gitname = element.Attributes["name"].Value
                        + ":"
                        + element.Attributes["email"].Value;

                    authors.Add(vaultname.ToLower(), gitname);
                }

                foreach (XmlElement element in xml.GetElementsByTagName("branch"))
                {
                    string vaultname = element.Attributes["vaultname"].Value;
                    string gitname = element.Attributes["name"].Value;

                    branches.Add(vaultname.ToLower(), gitname);
                }
            }
        }

        public static string GetGitAuthor(string vault_user)
        {
            vault_user = vault_user.ToLower();
            return authors.ContainsKey(vault_user) ? authors[vault_user] : null;
        }

        public static string GetBranchMapping(string branch_name)
        {
            branch_name = branch_name.ToLower().Replace(" ", string.Empty);
            return branches.ContainsKey(branch_name) ? branches[branch_name] : branch_name;
        }
    }
}
