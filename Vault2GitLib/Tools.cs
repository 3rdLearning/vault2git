using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace Vault2Git.Lib
{
	//public struct GitCommit
 //   {
	//	public GitCommitHash CommitHash;
	//	public List<GitCommitHash> ParentCommitHash;
	//	public string Comment;
	//	public VaultVersionInfo VaultInfo;
	//}

	//public struct GitVaultMessageTag
	//{
	//	public string VaultRepositoryPath;
	//	public string Branch;
	//	public string CommitHash;
	//	public long TxId;
	//}

	public static class Tools
	{
        static Dictionary<string, string> authors = new Dictionary<string, string>();
        static Dictionary<string, string> branches = new Dictionary<string, string>();

        public static void SaveMapping<TKey, TValue>(IDictionary<TKey, TValue> mappingDictionary, string fileName)
		{
			Dictionary2Xml(mappingDictionary).Save(fileName);
		}

		//public static void SaveMapping(List<VaultTx2GitTx> mappingDictionary, string fileName)
		//{
		//	VaultTx2Git2Xml(mappingDictionary).Save(fileName);
		//}

		//public static XElement VaultTx2Git2Xml(List<VaultTx2GitTx> input)
		//{
		//	//if (typeof(TValue) == typeof(VaultTx2GitTx))
		//	return new XElement("TransactionMap", new XAttribute("MappingType", typeof(VaultTx2GitTx).FullName),
		//		input.Select(kp => new XElement("entry", new XAttribute("TxId", kp.TxId), new XAttribute("Branch", kp.Branch), new XAttribute("GitHash", kp.GitHash.ToString())
		//			)));
		//	//else
		//	//TValue[] arr = input.Values.ToArray();

		//	////--return XElement.Parse(Encoding.ASCII.GetString(memoryStream.ToArray()));
		//	//return new XElement("dictionary", new XAttribute("keyType", typeof(TKey).FullName),
		//	//new XAttribute("valueType", typeof(TValue).FullName),
		//	//arr.Select(kp =>
		//	//{
		//	//	using (var memoryStream = new MemoryStream())
		//	//	{
		//	//		using (TextWriter streamWriter = new StreamWriter(memoryStream))
		//	//		{
		//	//			var xmlSerializer = new XmlSerializer(typeof(TValue));
		//	//			xmlSerializer.Serialize(streamWriter, kp);
		//	//			return XElement.Parse(Encoding.ASCII.GetString(memoryStream.ToArray()));
		//	//		}
		//	//	}
		//	//}));

		//}

		public static XElement Dictionary2Xml<TKey, TValue>(IDictionary<TKey, TValue> input)
		{
			//if (typeof(TValue) == typeof(VaultTx2GitTx))
				return new XElement("dictionary", new XAttribute("keyType", typeof(TKey).FullName),
					new XAttribute("valueType", typeof(TValue).FullName),
					input.Select(kp => new XElement("entry", new XAttribute("key", kp.Key), kp.Value)));
			//else
			//TValue[] arr = input.Values.ToArray();

			////--return XElement.Parse(Encoding.ASCII.GetString(memoryStream.ToArray()));
			//return new XElement("dictionary", new XAttribute("keyType", typeof(TKey).FullName),
			//new XAttribute("valueType", typeof(TValue).FullName),
			//arr.Select(kp =>
			//{
			//	using (var memoryStream = new MemoryStream())
			//	{
			//		using (TextWriter streamWriter = new StreamWriter(memoryStream))
			//		{
			//			var xmlSerializer = new XmlSerializer(typeof(TValue));
			//			xmlSerializer.Serialize(streamWriter, kp);
			//			return XElement.Parse(Encoding.ASCII.GetString(memoryStream.ToArray()));
			//		}
			//	}
			//}));
		
		}

		public static bool SaveFile(string saveFileName, string contents)
        {
			File.WriteAllText(saveFileName, contents);
			return true;
        }

        //public static Dictionary<long, VaultTx2GitTx> XElement2Dictionnary(XElement source)
        //{
        //    return source.Descendants("entry").ToDictionary(xe => long.Parse(xe.Attribute("TxId").Value), xe => VaultTx2GitTx.parse(xe));
        //}

        //public static Dictionary<long, VaultTx2GitTx> ReadFromXml(string saveFileName)
        //{
        //    if (!File.Exists(saveFileName))
        //        return null;
        //    try
        //    {
        //        return XElement2Dictionnary(XDocument.Load(saveFileName).Root);
        //    }
        //    catch (Exception e)
        //    {
        //        return null;
        //    }
        //}

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

        public static void CopyFile(string source, string dest)
        {
            if (File.Exists(source) && !File.Exists(dest))
                File.Copy(source, dest);
        }

        public static (Dictionary<string, String>, Dictionary<string,string>) ParseMapFile(string path)
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

        public static string GetGitAuthor(string vault_user)
        {
            vault_user = vault_user.ToLower();
            return authors.ContainsKey(vault_user) ? authors[vault_user] : null;
        }

        //public static string GetBranchMapping(string branch_name)
        //{
        //    branch_name = branch_name.ToLower().Replace(" ", string.Empty);
        //    return branches.ContainsKey(branch_name) ? branches[branch_name] : branch_name;
        //}
    }
}
