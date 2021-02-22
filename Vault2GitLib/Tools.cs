using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

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

        public static bool SaveFile(string saveFileName, string contents)
        {
            File.WriteAllText(saveFileName, contents);
            return true;
        }

        public static void WriteProgressInfo(string message, TimeSpan processingTime, int completedVersion, int totalVersion,
           TimeSpan totalProcessingTime)
        {
            try
            {
                var percentage = Math.Round(100 * Convert.ToDouble(completedVersion) / Convert.ToDouble(totalVersion), 1);
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
        public static void CopyFile(string source, string dest)
        {
            if (File.Exists(source) && !File.Exists(dest))
                File.Copy(source, dest);
        }

        public static (Dictionary<string, String>, Dictionary<string, string>) ParseMapFile(string path)
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
            return (branches, authors);
        }
    }
}
