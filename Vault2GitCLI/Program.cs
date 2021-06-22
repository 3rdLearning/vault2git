using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Vault2Git.Lib.New;


namespace Vault2Git.CLI
{
	static class Program
	{
		struct RepoConfig
		{
            public string GitRepoName { get; }
            public string GitRepoPath { get; }
			public string VaultRepoPath  { get; }

            public RepoConfig(string gitRepoName, string gitRepoPath, string vaultRepoPath) : this()
            {
                GitRepoName = gitRepoName;
				GitRepoPath = gitRepoPath;
				VaultRepoPath = vaultRepoPath;
            }
        }

		class Params
		{
			public int Limit { get; protected set; }
			public bool UseConsole { get; protected set; }
			public bool UseCapsLock { get; protected set; }
			public bool SkipEmptyCommits { get; protected set; }
			public bool IgnoreLabels { get; protected set; }
            public List<string> Branches;
			public IEnumerable<string> Errors;

			protected Params()
			{
			}

			private const string LimitParam = "--limit=";
			private const string BranchParam = "--branch=";

			public static Params Parse(string[] args)
			{
				var errors = new List<string>();
                var branches = new List<string>();
                
                var p = new Params();
				foreach (var o in args)
				{
					if (o.Equals("--console-output"))
						p.UseConsole = true;
                    else if (o.Equals("--caps-lock"))
                        p.UseCapsLock = true;
					else if (o.Equals("--skip-empty-commits"))
						p.SkipEmptyCommits = true;
					else if (o.Equals("--ignore-labels"))
						p.IgnoreLabels = true;
					else if (o.Equals("--help"))
					{
						errors.Add("Usage: vault2git [options]");
						errors.Add("options:");
						errors.Add("   --help                  This screen");
						errors.Add("   --console-output        Use console output (default=no output)");
						errors.Add("   --caps-lock             Use caps lock to stop at the end of the cycle with proper finalizers (default=no caps-lock)");
						errors.Add("   --branch=<branch>       Process branches specified. Default=all branches specified in config");
						errors.Add("   --limit=<n>             Max number of versions to take from Vault for each branch");
						errors.Add("   --skip-empty-commits    Do not create empty commits in Git");
						errors.Add("   --ignore-labels         Do not create Git tags from Vault labels");
					}
					else if (o.StartsWith(LimitParam))
					{
						var l = o.Substring(LimitParam.Length);
                        if (int.TryParse(l, out int max))
                            p.Limit = max;
                        else
                            errors.Add(string.Format("Incorrect limit ({0}). Use integer.", l));
                    }
					else if (o.StartsWith(BranchParam))
					{
						var b = o.Substring(BranchParam.Length);
                        if (!branches.Contains(b))
                            branches.Add(b);
                        else
                            errors.Add(string.Format("Unknown branch {0}. Use one specified in .config", b));
					}
					else
                        errors.Add(string.Format("Unknown option {0}", o));
				}
                p.Branches = branches;
				p.Errors = errors;
				return p;
			}
		}

		private static bool _useCapsLock;
		private static bool _useConsole;
		private static bool _ignoreLabels;

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		//[STAThread]
		static void Main(string[] args)
		{
			Console.WriteLine("Vault2Git -- converting history from Vault repositories to Git");
			Console.InputEncoding = Encoding.UTF8;
			
			//parse params
			var param = Params.Parse(args);

			//get count from param
			if (param.Errors.Count() > 0)
			{
				foreach (var e in param.Errors)
					Console.WriteLine(e);

				Console.WriteLine("   use Vault2Git --help to get additional info");
				return;
			}

			_useConsole = param.UseConsole;
			_useCapsLock = param.UseCapsLock;
			_ignoreLabels = param.IgnoreLabels;

			if (string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["MappingSaveLocation"]))
			{
				Console.Error.WriteLine("Configuration MappingSaveLocation is not defined into application' settings. Please set a valid value.");
			}

			//get repoconfig info
			List<RepoConfig> repoConfigs = ParseRepoConfigFile(ConfigurationManager.AppSettings["Convertor.RepoConfigFile"]);
			bool composePaths=true;

			if (!repoConfigs.Any())
			{
				var repoName = ConfigurationManager.AppSettings["Convertor.WorkingFolder"].Split('\\').Last();
				var repoFolder = ConfigurationManager.AppSettings["Convertor.WorkingFolder"].Replace($"\\{repoName}", string.Empty);
				
				repoConfigs.Add(new RepoConfig(
					gitRepoName: repoName,
                    gitRepoPath: repoFolder,
                    vaultRepoPath: ConfigurationManager.AppSettings["Convertor.Paths"]
				));

				composePaths = false;
			}
			var processor = new Processor
			{
				GitCmd = ConfigurationManager.AppSettings["Convertor.GitCmd"],
				GitDomainName = ConfigurationManager.AppSettings["Git.DomainName"],
				VaultServer = ConfigurationManager.AppSettings["Vault.Server"],
				VaultUseSsl = (ConfigurationManager.AppSettings["Vault.UseSSL"].ToLower() == "true"),
				VaultRepository = ConfigurationManager.AppSettings["Vault.Repo"],
				VaultUser = ConfigurationManager.AppSettings["Vault.User"],
				VaultPassword = ConfigurationManager.AppSettings["Vault.Password"],
				RevisionStartDate = ConfigurationManager.AppSettings["RevisionStartDate"] ?? "2005-01-01",
				RevisionEndDate = ConfigurationManager.AppSettings["RevisionEndDate"] ?? "2030-12-31",
				Vault2GitAuthorMappingFilePath = ConfigurationManager.AppSettings["Vault2GitAuthorMappingFilePath"],
				Progress = ShowProgress,
				SkipEmptyCommits = param.SkipEmptyCommits,
			};

			foreach (RepoConfig repo in repoConfigs)
            {
				Console.WriteLine("");
				Console.WriteLine($"Starting {repo.GitRepoName}");
				List<string> branches = repo.VaultRepoPath.TrimEnd(';').Split(';').ToList();

				
				processor.WorkingFolder = $"{repo.GitRepoPath}\\{repo.GitRepoName}";
				processor.MappingSaveLocation = composePaths ? $"{ConfigurationManager.AppSettings["MappingSaveLocation"]}\\vault2git.{repo.GitRepoName}.xml" : ConfigurationManager.AppSettings["MappingSaveLocation"];
				processor.BranchRenameFilePath = composePaths ? $"{ConfigurationManager.AppSettings["CustomMapPath"]}\\mapfile.{repo.GitRepoName}.xml" : (ConfigurationManager.AppSettings["CustomMapPath"] ?? "c:\\temp\\mapfile.xml");
				processor.GitCommitMessageTempFile = composePaths ? $"{ConfigurationManager.AppSettings["GitCommitMessageTempFilePath"]}\\CommitMessage-{repo.GitRepoName}.tmp" : (ConfigurationManager.AppSettings["GitCommitMessageTempFilePath"] ?? "c:\\temp\\commitmessage.tmp");


				processor.Pull
					(
						branches
						, 0 == param.Limit ? int.MaxValue : param.Limit
						, _ignoreLabels
					);

				if (!_ignoreLabels)
					processor.CreateTagsFromLabels();

			}

			processor.Finish();
#if DEBUG
            Console.WriteLine("Press ENTER");
			Console.ReadLine();
#endif
		}

		static bool ShowProgress(long currentVersion, long totalVersion, int ticks)
		{
			var timeSpan = TimeSpan.FromMilliseconds(ticks);
			if (_useConsole)
			{

				if (Processor._progressSpecialVersionInit == currentVersion)
                    Console.WriteLine("init took {0}", timeSpan);
                else if (Processor._progressSpecialVersionGc == currentVersion)
                    Console.WriteLine("gc took {0}", timeSpan);
                else if (Processor._progressSpecialVersionFinalize == currentVersion)
                    Console.WriteLine("finalization took {0}", timeSpan);
                else if (Processor._progressSpecialVersionTags == currentVersion)
                    Console.WriteLine("tags creation took {0}", timeSpan);
                else
                    Console.WriteLine("processing version {0}/{2} took {1}", currentVersion, timeSpan, totalVersion);
            }

            return _useCapsLock && Console.CapsLock; //cancel flag
		}
		static List<RepoConfig> ParseRepoConfigFile(string path)
        {
            List<RepoConfig> repoConfigs = new List<RepoConfig>();

            if (File.Exists(path))
            {
                XmlDocument xml = new XmlDocument();
                xml.Load(path);

                foreach (XmlElement element in xml.GetElementsByTagName("repo"))
                {
                    repoConfigs.Add(new RepoConfig(
                        gitRepoName: element.Attributes["GitRepoName"].Value,
                        gitRepoPath: element.Attributes["GitRepoPath"].Value,
                        vaultRepoPath: element.Attributes["VaultRepoPath"].Value
                    ));
                }
            }
            return repoConfigs;
        }
	}
}
