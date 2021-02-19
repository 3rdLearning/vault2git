using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Vault2Git.Lib;

namespace Vault2Git.CLI
{
	static class Program
	{

		class Params
		{
			public int Limit { get; protected set; }
			public bool UseConsole { get; protected set; }
			public bool UseCapsLock { get; protected set; }
            public bool BuildGraft { get; protected set; }
			public bool SkipEmptyCommits { get; protected set; }
			public bool IgnoreLabels { get; protected set; }
            public List<string> Branches;
			public IEnumerable<string> Errors;

			protected Params()
			{
			}

			private const string _limitParam = "--limit=";
			private const string _branchParam = "--branch=";

			public static Params Parse(string[] args, List<string> gitBranches)
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
                    else if (o.Equals("--build-graft"))
						p.BuildGraft = true;
					else if (o.Equals("--skip-empty-commits"))
						p.SkipEmptyCommits = true;
					else if (o.Equals("--ignore-labels"))
						p.IgnoreLabels = true;
					else if (o.Equals("--help"))
					{
						errors.Add("Usage: vault2git [options]");
						errors.Add("options:");
						errors.Add("   --help                  This screen");
                        errors.Add("   --build-graft           Build graft file from log");
						errors.Add("   --console-output        Use console output (default=no output)");
						errors.Add("   --caps-lock             Use caps lock to stop at the end of the cycle with proper finalizers (default=no caps-lock)");
						errors.Add("   --branch=<branch>       Process branches specified. Default=all branches specified in config");
						errors.Add("   --limit=<n>             Max number of versions to take from Vault for each branch");
						errors.Add("   --skip-empty-commits    Do not create empty commits in Git");
						errors.Add("   --ignore-labels         Do not create Git tags from Vault labels");
					}
					else if (o.StartsWith(_limitParam))
					{
						var l = o.Substring(_limitParam.Length);
                        int max;
                        if (int.TryParse(l, out max))
							p.Limit = max;
						else
							errors.Add(string.Format("Incorrect limit ({0}). Use integer.", l));
					}
					else if (o.StartsWith(_branchParam))
					{
						var b = o.Substring(_branchParam.Length);
                        if (gitBranches.Contains(b))
                            branches.Add(b);
                        else

                            errors.Add(string.Format("Unknown branch {0}. Use one specified in .config", b));
					}
					else
                        errors.Add(string.Format("Unknown option {0}", o));
				}
                p.Branches = 0 == branches.Count()
                    ? gitBranches
					: branches;
				p.Errors = errors;
				p.BuildGraft = false;
				return p;
			}
		}

		private static bool _useCapsLock = false;
		private static bool _useConsole = false;
		private static bool _ignoreLabels = false;
        private static bool _buildGraft = false;


		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		//[STAThread]
		static void Main(string[] args)
		{
			Console.WriteLine("Vault2Git -- converting history from Vault repositories to Git");
			System.Console.InputEncoding = System.Text.Encoding.UTF8;
			
			//get configuration for branches
			string paths = ConfigurationManager.AppSettings["Convertor.Paths"];
			List<string> branches = paths.TrimEnd(';').Split(';').ToList();


            //parse params
            var param = Params.Parse(args, branches);

			//get count from param
			if (param.Errors.Count() > 0)
			{
				foreach (var e in param.Errors)
					Console.WriteLine(e);
				return;
			}

			Console.WriteLine("   use Vault2Git --help to get additional info");

			_useConsole = param.UseConsole;
			_useCapsLock = param.UseCapsLock;
			_ignoreLabels = param.IgnoreLabels;
            _buildGraft = param.BuildGraft;

			if (string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["MappingSaveLocation"]))
			{
				Console.Error.WriteLine($"Configuration MappingSaveLocation is not defined into application' settings. Please set a valid value.");
			}

            var processor = new Vault2Git.Lib.New.Processor()
            {
                WorkingFolder = ConfigurationManager.AppSettings["Convertor.WorkingFolder"],
                GitCmd = ConfigurationManager.AppSettings["Convertor.GitCmd"],
                GitDomainName = ConfigurationManager.AppSettings["Git.DomainName"],
                VaultServer = ConfigurationManager.AppSettings["Vault.Server"],
                VaultUseSSL = (ConfigurationManager.AppSettings["Vault.UseSSL"].ToLower() == "true"),
                VaultRepository = ConfigurationManager.AppSettings["Vault.Repo"],
                VaultUser = ConfigurationManager.AppSettings["Vault.User"],
                VaultPassword = ConfigurationManager.AppSettings["Vault.Password"],
                RevisionStartDate = ConfigurationManager.AppSettings["RevisionStartDate"] ?? "2005-01-01",
                RevisionEndDate = ConfigurationManager.AppSettings["RevisionEndDate"] ?? "2030-12-31",
                MappingSaveLocation = ConfigurationManager.AppSettings["MappingSaveLocation"],
                MappingFilePath = ConfigurationManager.AppSettings["CustomMapPath"] ?? "c:\\temp\\mapfile.xml",
                GitCommitMessageTempFile = ConfigurationManager.AppSettings["GitCommitMessageTempFile"] ?? "c:\\temp\\commitmessage.tmp",
                Progress = ShowProgress,
                SkipEmptyCommits = param.SkipEmptyCommits
            };

            if (_buildGraft)
            {
                processor.BuildGrafts();
            }
            else
            {
                processor.Pull
                    (
                        param.Branches
                        , 0 == param.Limit ? 999999999 : param.Limit
               , _ignoreLabels
                    );

                if (!_ignoreLabels)
                    processor.CreateTagsFromLabels();
            }
#if DEBUG
            Console.WriteLine("Press ENTER");
			Console.ReadLine();
#endif
		}

		static bool ShowProgress(long currentVersion, long totalVersion, int ticks)
		{
			var timeSpan = TimeSpan.FromMilliseconds(ticks);
			//if (_useConsole)
			//{

			//	if (Processor.ProgressSpecialVersionInit == currentVersion)
			//		Console.WriteLine("init took {0}", timeSpan);
			//	else if (Processor.ProgressSpecialVersionGc == currentVersion)
			//		Console.WriteLine("gc took {0}", timeSpan);
			//	else if (Processor.ProgressSpecialVersionFinalize == currentVersion)
			//		Console.WriteLine("finalization took {0}", timeSpan);
			//	else if (Processor.ProgressSpecialVersionTags == currentVersion)
			//		Console.WriteLine("tags creation took {0}", timeSpan);
			//	else
			//		Console.WriteLine("processing version {0}/{2} took {1}", currentVersion, timeSpan, totalVersion);
			//}

			return _useCapsLock && Console.CapsLock; //cancel flag
		}
	}
}
