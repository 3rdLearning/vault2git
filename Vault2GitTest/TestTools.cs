using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Vault2Git.Lib;

namespace Vault2GitTest
{
	[TestFixture]
	public class TestTools
	{
		public static readonly Dictionary<long, string> Mapping = new Dictionary<long, string>
		{
			{ 0L,"FirstEntry"},
			{ 2L,"SecondEntry"},
			{ 3L,"ThirdEntry"}
		};

		private static string _saveFileName;
		#region Serialization
		[Test]
		public void WhenSavingADictionaryDoesntExpectsError()
		{
			_saveFileName = GetTempXmlFile();
			Tools.SaveMapping(Mapping, _saveFileName);
		}

		[Test]
		public void WhenConvertingADictionaryToXmlExpectsContent()
		{
			Assert.False(Tools.Dictionary2Xml(Mapping).IsEmpty);
		}

		[Test]
		public void WhenSavingADictionaryExpectFileSizeToIncrease()
		{
			_saveFileName = GetTempXmlFile();
			var fInfo = new FileInfo(_saveFileName);
			var fileSizeBefore = fInfo.Exists ? fInfo.Length : 0L;
			Tools.SaveMapping(Mapping, _saveFileName);
			fInfo.Refresh();
			var fileSizeAfter = fInfo.Exists ? fInfo.Length : 0L;
			Assert.IsTrue(fileSizeAfter > fileSizeBefore, $"File {_saveFileName} size has not increased. Was {fileSizeBefore} and is {fileSizeAfter}");
		}
		#endregion
		#region Read

		[Test]
		public void WhenSaveAndReadingMappingExpectsSameContent()
		{
			//_saveFileName = GetTempXmlFile();
			//var fInfo = new FileInfo(_saveFileName);
			//Tools.SaveMapping(Mapping, _saveFileName);
			//var readDictionary = Tools.ReadFromXml(_saveFileName);
			//Assert.IsTrue(readDictionary != null, "Returned dictionary is nulll");
			//Assert.IsTrue(readDictionary.Count == Mapping.Count, "Returned dictionary doesn't contains the same amount of entries.");
		}
		#endregion
		private string GetTempXmlFile()
		{
			return Path.GetTempFileName() + ".xml";
		}
	}
}