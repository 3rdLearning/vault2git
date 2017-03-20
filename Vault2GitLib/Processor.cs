using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using VaultClientIntegrationLib;
using VaultClientOperationsLib;
using VaultLib;
using System.Xml.XPath;
using System.Xml;

namespace Vault2Git.Lib
{
	public class Processor
	{
		/// <summary>
		/// path to git.exe
		/// </summary>
		public string GitCmd;

		/// <summary>
		/// path where conversion will take place. If it not already set as value working folder, it will be set automatically
		/// </summary>
		public string WorkingFolder;

		public string VaultServer;
        public bool VaultUseSSL;
		public string VaultUser;
		public string VaultPassword;
		public string VaultRepository;

		public string GitDomainName;

		public int GitGCInterval = 200;

        public string AuthorMapPath { get; set; }

		// Stores whether the login has been already accomplished or not. Prevent an issue with our current Vault version (v5)
		private bool _loginDone = false;

		//callback
		public Func<long, long, int, bool> Progress;

		//flags
		public bool SkipEmptyCommits = false;

		//git commands
		private const string _gitVersionCmd = "version";
		private const string _gitGCCmd = "gc --auto";
		private const string _gitFinalizer = "update-server-info";
		private const string _gitAddCmd = "add --all .";
		private const string _gitStatusCmd = "status --porcelain";
		private const string _gitLastCommitInfoCmd = "log -1 {0}";
		private const string _gitAllCommitInfoCmd = "log {0}";
        private const string _gitCommitCmd = @"commit --allow-empty --all --date=""{2}"" --author=""{0} <{1}>"" -F -";
        //private const string _gitCommitCmd = @"commit --allow-empty --all --date=""{2}"" --author=""{0} <{0}@{1}>"" -F -";
        private const string _gitCheckoutCmd = "checkout --quiet --force {0}";
		private const string _gitBranchCmd = "branch";
		private const string _gitAddTagCmd = @"tag {0} {1} -a -m ""{2}""";

		//private vars
		/// <summary>
		/// Maps Vault TransactionID to Git Commit SHA-1 Hash
		/// </summary>
		private IDictionary<long, String> _txidMappings;

		private string currentGitBranch;

		//constants
		private const string VaultTag = "[git-vault-id]";

		/// <summary>
		/// version number reported to <see cref="Progress"/> when init is complete
		/// </summary>
		public const int ProgressSpecialVersionInit = 0;

		/// <summary>
		/// version number reported to <see cref="Progress"/> when git gc is complete
		/// </summary>
		public const int ProgressSpecialVersionGc = -1;

		/// <summary>
		/// version number reported to <see cref="Progress"/> when finalization finished (e.g. logout, unset wf etc)
		/// </summary>
		public const int ProgressSpecialVersionFinalize = -2;

		/// <summary>
		/// version number reported to <see cref="Progress"/> when git tags creation is completed
		/// </summary>
		public const int ProgressSpecialVersionTags = -3;

		public string RevisionEndDate { get; set; }
		public string RevisionStartDate { get; set; }

		public string MappingSaveLocation { get; set; }

		/// <summary>
		/// Pulls versions
		/// </summary>
		/// <param name="git2vaultRepoPath">Key=git, Value=vault</param>
		/// <param name="limitCount"></param>
		/// <returns></returns>
		public bool Pull(IEnumerable<KeyValuePair<string, string>> git2vaultRepoPath, long limitCount)
		{
			var completedStepCount=0;
			var versionProcessingTime = new Stopwatch();
			var overallProcessingTime = new Stopwatch();
			int ticks = 0;
			//get git current branch
			string gitCurrentBranch;
			ticks += this.gitCurrentBranch(out gitCurrentBranch);

            //load git authors
            Tools.ParseAuthorsFile(AuthorMapPath);

			//reorder target branches to start from current (to avoid checkouts)
			var targetList =
				git2vaultRepoPath.OrderByDescending(p => p.Key.Equals(gitCurrentBranch, StringComparison.CurrentCultureIgnoreCase));

			ticks += vaultLogin();
			try
			{
				foreach (var pair in targetList)
				{

					currentGitBranch = pair.Key;
					var vaultRepoPath = pair.Value;

					long currentGitVaultVersion = 0;

					//reset ticks
					ticks = 0;

					RebuildMapping(currentGitBranch);
					//get current version
					ticks += gitVaultVersion(currentGitBranch, ref currentGitVaultVersion);

					//get vaultVersions
					IDictionary<long, VaultVersionInfo> vaultVersions = new SortedList<long, VaultVersionInfo>();

					ticks += vaultPopulateInfo(vaultRepoPath, vaultVersions);

					var versionsToProcess = vaultVersions.Where(p => p.Key > currentGitVaultVersion);
					var keyValuePairs = versionsToProcess.ToList();

					//do init only if there is something to work on
					if (keyValuePairs.Any())
						ticks += Init(vaultRepoPath, currentGitBranch);

					//report init
					if (null != Progress)
						if (Progress(ProgressSpecialVersionInit, 0L, ticks))
							return true;

					var counter = 0;
					overallProcessingTime.Restart();
					foreach (var version in keyValuePairs)
					{
						versionProcessingTime.Restart();
						//get vault version
						Console.Write($"Starting get version {version.Key} from Vault...");
						ticks = vaultGet(vaultRepoPath, version.Key, version.Value.TrxId);
						Console.WriteLine($" done!");
						//change all sln files
						Directory.GetFiles(
							WorkingFolder,
							"*.sln",
							SearchOption.AllDirectories)
							//remove temp files created by vault
							.Where(f => !f.Contains("~"))
							.ToList()
							.ForEach(f => ticks += removeSCCFromSln(f));
						//change all csproj files
						Directory.GetFiles(
							WorkingFolder,
							"*.csproj",
							SearchOption.AllDirectories)
							//remove temp files created by vault
							.Where(f => !f.Contains("~"))
							.ToList()
							.ForEach(f => ticks += removeSCCFromCSProj(f));
						//change all vdproj files
						Directory.GetFiles(
							WorkingFolder,
							"*.vdproj",
							SearchOption.AllDirectories)
							//remove temp files created by vault
							.Where(f => !f.Contains("~"))
							.ToList()
							.ForEach(f => ticks += removeSCCFromVDProj(f));
						//get vault version info
						var info = vaultVersions[version.Key];
						//commit
						Console.Write($"Starting git commit...");
						ticks += gitCommit(info.Login, info.TrxId, version.Key, GitDomainName, buildCommitMessage(vaultRepoPath, version.Key, info), info.TimeStamp);
						Console.WriteLine($" done!");
						if (null != Progress)
							if (Progress(version.Key, keyValuePairs.Count, ticks))
								return true;
						counter++;
						//call gc
						if (0 == counter % GitGCInterval)
						{
							ticks = gitGC();
							if (null != Progress)
								if (Progress(ProgressSpecialVersionGc, keyValuePairs.Count, ticks))
									return true;
						}
						//check if limit is reached
						if (counter >= limitCount)
							break;
						completedStepCount++;
						versionProcessingTime.Stop();
						Tools.WriteProgressInfo(string.Empty, versionProcessingTime.Elapsed, completedStepCount, keyValuePairs.Count, overallProcessingTime.Elapsed);
					}
					ticks = vaultFinalize(vaultRepoPath);
				}
			}
			finally
			{
				//complete
				//ticks += vaultLogout(); // Drops log-out as it kills the Native allocations
				//finalize git (update server info for dumb clients)
				ticks += gitFinalize();
				if (null != Progress)
					Progress(ProgressSpecialVersionFinalize, 0L, ticks);
			}
			return false;
		}


		/// <summary>
		/// removes Source control refs from sln files
		/// </summary>
		/// <param name="filePath">path to sln file</param>
		/// <returns></returns>
		private static int removeSCCFromSln(string filePath)
		{
			var ticks = Environment.TickCount;
			var lines = File.ReadAllLines(filePath).ToList();
			//scan lines 
			var searchingForStart = true;
			var beginingLine = 0;
			var endingLine = 0;
			var currentLine = 0;
			foreach (var line in lines)
			{
				var trimmedLine = line.Trim();
				if (searchingForStart)
				{
					if (trimmedLine.StartsWith("GlobalSection(SourceCodeControl)"))
					{
						beginingLine = currentLine;
						searchingForStart = false;
					}
				}
				else
				{
					if (trimmedLine.StartsWith("EndGlobalSection"))
					{
						endingLine = currentLine;
						break;
					}
				}
				currentLine++;
			}
			//removing lines
			if (beginingLine > 0 & endingLine > 0)
			{
				lines.RemoveRange(beginingLine, endingLine - beginingLine + 1);
				File.WriteAllLines(filePath, lines.ToArray(), Encoding.UTF8);
			}
			return Environment.TickCount - ticks;
		}

		/// <summary>
		/// removes Source control refs from csProj files
		/// </summary>
		/// <param name="filePath">path to sln file</param>
		/// <returns></returns>
		public static int removeSCCFromCSProj(string filePath)
		{
			var ticks = Environment.TickCount;
			var doc = new XmlDocument();
			try
			{
				doc.Load(filePath);
				while (true)
				{
					var nav = doc.CreateNavigator().SelectSingleNode("//*[starts-with(name(), 'Scc')]");
					if (null == nav)
						break;
					nav.DeleteSelf();
				}
				doc.Save(filePath);
			}
			catch
			{
				Console.WriteLine("Failed for {0}", filePath);
				throw;
			}
			return Environment.TickCount - ticks;
		}

		/// <summary>
		/// removes Source control refs from vdProj files
		/// </summary>
		/// <param name="filePath">path to sln file</param>
		/// <returns></returns>
		private static int removeSCCFromVDProj(string filePath)
		{
			var ticks = Environment.TickCount;
			var lines = File.ReadAllLines(filePath).ToList();
			File.WriteAllLines(filePath, lines.Where(l => !l.Trim().StartsWith(@"""Scc")).ToArray(), Encoding.UTF8);
			return Environment.TickCount - ticks;
		}

		private int vaultPopulateInfo(string repoPath, IDictionary<long, VaultVersionInfo> info)
		{
			var ticks = Environment.TickCount;
			Console.Write($"Fetching history from vault from {RevisionStartDate} to {RevisionEndDate}... ");
			var historyItems = ServerOperations.ProcessCommandVersionHistory(repoPath,
				0,
				VaultDateTime.Parse(RevisionStartDate),
				VaultDateTime.Parse(RevisionEndDate),
				0);

			foreach (var i in historyItems)
				info.Add(i.Version, new VaultVersionInfo()
				{
					TrxId = i.TxID,
					Comment = i.Comment,
					Login = i.UserLogin,
					TimeStamp = i.TxDate.GetDateTime()
				});
			Console.WriteLine($"done! Fetched {historyItems.Length} versions.");
			return Environment.TickCount - ticks;
		}

		/// <summary>
		/// Creates Git tags from Vault labels
		/// </summary>
		/// <returns></returns>
		public bool CreateTagsFromLabels()
		{
			vaultLogin();

			// Search for all labels recursively
			string repositoryFolderPath = "$";

			long objId = RepositoryUtil.FindVaultTreeObjectAtReposOrLocalPath(repositoryFolderPath).ID;
			string qryToken;
			long rowsRetMain;
			long rowsRetRecur;

			VaultLabelItemX[] labelItems;

			ServerOperations.client.ClientInstance.BeginLabelQuery(repositoryFolderPath, objId, false, false, true, true, 0,
				out rowsRetMain,
				out rowsRetRecur,
				out qryToken);

			//ServerOperations.client.ClientInstance.BeginLabelQuery(repositoryFolderPath,
			//														   objId,
			//														   true, // get recursive
			//														   true, // get inherited
			//														   true, // get file items
			//														   true, // get folder items
			//														   0,    // no limit on results
			//														   out rowsRetMain,
			//														   out rowsRetRecur,
			//out qryToken);


			ServerOperations.client.ClientInstance.GetLabelQueryItems_Recursive(qryToken,
				0,
				(int)rowsRetRecur,
				out labelItems);

            if (labelItems == null)
                labelItems = new VaultLabelItemX[0];
            try
			{
				int ticks = 0;

				foreach (VaultLabelItemX currItem in labelItems)
				{
					Console.WriteLine($"Processing label {currItem.LabelID} for version {currItem.Version} with comemnt {currItem.Comment}");
					var gitCommitId = GetMapping(currItem.Version);

					if (!(gitCommitId?.Length > 0)) continue;

					var gitLabelName = Regex.Replace(currItem.Label, "[\\W]", "_");
					ticks += gitAddTag($"{currItem.Version}_{gitLabelName}", gitCommitId, currItem.Comment);
				}

				//add ticks for git tags
				Progress?.Invoke(ProgressSpecialVersionTags, 0L, ticks);
			}
			finally
			{
				//complete
				ServerOperations.client.ClientInstance.EndLabelQuery(qryToken);
				vaultLogout();
				gitFinalize();
			}
			return true;
		}

		private int vaultGet(string repoPath, long version, long txId)
		{
			var ticks = Environment.TickCount;
			//apply version to the repo folder
			GetOperations.ProcessCommandGetVersion(
				repoPath,
				Convert.ToInt32(version),
				new GetOptions()
				{
					MakeWritable = MakeWritableType.MakeAllFilesWritable,
					Merge = MergeType.OverwriteWorkingCopy,
					OverrideEOL = VaultEOL.None,
					//remove working copy does not work -- bug http://support.sourcegear.com/viewtopic.php?f=5&t=11145
					PerformDeletions = PerformDeletionsType.RemoveWorkingCopy,
					SetFileTime = SetFileTimeType.Modification,
					Recursive = true
				});

			//now process deletions, moves, and renames (due to vault bug)
			var allowedRequests = new int[]
			{
				9, //delete
				12, //move
				15 //rename
			};
			foreach (var item in ServerOperations.ProcessCommandTxDetail(txId).items
				.Where(i => allowedRequests.Contains(i.RequestType)))

				//delete file
				//check if it is within current branch
				if (item.ItemPath1.StartsWith(repoPath, StringComparison.CurrentCultureIgnoreCase))
				{
					var pathToDelete = Path.Combine(this.WorkingFolder, item.ItemPath1.Substring(repoPath.Length + 1));
					//Console.WriteLine("delete {0} => {1}", item.ItemPath1, pathToDelete);
					if (File.Exists(pathToDelete))
						File.Delete(pathToDelete);
					if (Directory.Exists(pathToDelete))
						Directory.Delete(pathToDelete, true);
				}
			return Environment.TickCount - ticks;
		}

		struct VaultVersionInfo
		{
			public long TrxId;
			public string Comment;
			public string Login;
			public DateTime TimeStamp;
		}

		private int gitVaultVersion(string gitBranch, ref long currentVersion)
		{
			string[] msgs;
			//get info
			var ticks = gitLog(gitBranch, out msgs);
			//get vault version
			currentVersion = getVaultVersionFromGitLogMessage(msgs);
			return ticks;
		}

		private int Init(string vaultRepoPath, string gitBranch)
		{
			//set working folder
			var ticks = setVaultWorkingFolder(vaultRepoPath);
			//checkout branch
			string[] msgs;
			for (int tries = 0; ; tries++)
			{
				ticks += runGitCommand(string.Format(_gitCheckoutCmd, gitBranch), string.Empty, out msgs);
				//confirm current branch (sometimes checkout failed)
				string currentBranch;
				ticks += this.gitCurrentBranch(out currentBranch);
				if (gitBranch.Equals(currentBranch, StringComparison.OrdinalIgnoreCase))
					break;
				if (tries > 5)
					throw new Exception("cannot switch");
			}
			return ticks;
		}

		private int vaultFinalize(string vaultRepoPath)
		{
			//unset working folder
			return unSetVaultWorkingFolder(vaultRepoPath);
		}

		private int gitCommit(string vaultLogin, long vaultTrxid, long vaultVersion, string gitDomainName, string vaultCommitMessage,
			DateTime commitTimeStamp)
		{
			string gitCurrentBranch;
            string gitName;
            string gitEmail;
            string gitAuthor;

            this.gitCurrentBranch(out gitCurrentBranch);

			string[] msgs;
			var ticks = runGitCommand(_gitAddCmd, string.Empty, out msgs);
			if (SkipEmptyCommits)
			{
				//checking status
				ticks += runGitCommand(
					_gitStatusCmd,
					string.Empty,
					out msgs
					);
				if (!msgs.Any())
					return ticks;
			}

            gitAuthor = Tools.GetGitAuthor(vaultLogin);
            if (gitAuthor != null)
            {
                gitName = gitAuthor.Split(':')[0];
                gitEmail = gitAuthor.Split(':')[1];
            }
            else
            {
                gitName = vaultLogin;
                gitEmail = vaultLogin + '@' + gitDomainName;
            }

            Dictionary<string, string> env = new Dictionary<string, string>();
            env.Add("GIT_COMMITTER_DATE", commitTimeStamp.ToString("yyyy-MM-ddTHH:mm:ss"));
            env.Add("GIT_COMMITTER_NAME", gitName);
            env.Add("GIT_COMMITTER_EMAIL", gitEmail);

            ticks += runGitCommand(
				string.Format(_gitCommitCmd, gitName, gitEmail, string.Format("{0:s}", commitTimeStamp)),
				vaultCommitMessage,
				out msgs, 
                env
				);

			// Mapping Vault Transaction ID to Git Commit SHA-1 Hash
			if (msgs[0].StartsWith("[" + gitCurrentBranch))
			{
				string gitCommitId = msgs[0].Split(' ')[1];
				gitCommitId = gitCommitId.Substring(0, gitCommitId.Length - 1);
				AddMapping(vaultVersion, gitCommitId);
			}
			return ticks;
		}

		private int gitCurrentBranch(out string currentBranch)
		{
			string[] msgs;
			var ticks = runGitCommand(_gitBranchCmd, string.Empty, out msgs);
			if (msgs.Any())
				currentBranch = msgs.Where(s => s.StartsWith("*")).First().Substring(1).Trim();
			else
			{
				currentBranch = string.Empty;
				throw new InvalidOperationException("The local git repository doesn't contain any branches. Please create at least one.");
			}
			return ticks;
		}

		private string buildCommitMessage(string repoPath, long version, VaultVersionInfo info)
		{
			//parse path repo$RepoPath@version/trx
			var r = new StringBuilder(info.Comment);
			r.AppendLine();
			r.AppendFormat("{4} {0}{1}@{2}/{3}", this.VaultRepository, repoPath, version, info.TrxId, VaultTag);
			r.AppendLine();
			return r.ToString();
		}

		private long getVaultVersionFromGitLogMessage(string[] msg)
		{
			//get last string
			var stringToParse = msg.Last();
			//search for version tag
			var versionString = stringToParse.Split(new string[] { VaultTag }, StringSplitOptions.None).LastOrDefault();
			if (null == versionString)
				return 0;
			//parse path reporepoPath@version/trx
			//get version/trx part
			var versionTrxTag = versionString.Split('@').LastOrDefault();
			if (null == versionTrxTag)
				return 0;

			//get version
			long version = 0;
			long.TryParse(versionTrxTag.Split('/').First(), out version);
			return version;
		}

		private int gitLog(string gitBranch, out string[] msg)
		{
			return runGitCommand(string.Format(_gitLastCommitInfoCmd, gitBranch), string.Empty, out msg);
		}

		private int getGitLogs(string gitBranch, out string[] msgLines)
		{
			return runGitCommand(string.Format(_gitAllCommitInfoCmd, gitBranch), string.Empty, out msgLines);
		}

		private int gitAddTag(string gitTagName, string gitCommitId, string gitTagComment)
		{
			string[] msg;
			return runGitCommand(string.Format(_gitAddTagCmd, gitTagName, gitCommitId, gitTagComment),
				string.Empty,
				out msg);
		}

		private int gitGC()
		{
			string[] msg;
			return runGitCommand(_gitGCCmd, string.Empty, out msg);
		}

		private int gitFinalize()
		{
			string[] msg;
			return runGitCommand(_gitFinalizer, string.Empty, out msg);
		}

		private int setVaultWorkingFolder(string repoPath)
		{
			var ticks = Environment.TickCount;
			ServerOperations.SetWorkingFolder(repoPath, this.WorkingFolder, true);
			return Environment.TickCount - ticks;
		}

		private int unSetVaultWorkingFolder(string repoPath)
		{
			var ticks = Environment.TickCount;
			//remove any assignment first
			//it is case sensitive, so we have to find how it is recorded first
			var exPath = ServerOperations.GetWorkingFolderAssignments()
				.Cast<DictionaryEntry>()
				.Select(e => e.Key.ToString())
				.Where(e => repoPath.Equals(e, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
			if (null != exPath)
				ServerOperations.RemoveWorkingFolder(exPath);
			return Environment.TickCount - ticks;
		}

		private int runGitCommand(string cmd, string stdInput, out string[] stdOutput)
		{
			return runGitCommand(cmd, stdInput, out stdOutput, null);
		}

		private int runGitCommand(string cmd, string stdInput, out string[] stdOutput, IDictionary<string, string> env)
		{
			var ticks = Environment.TickCount;

			var pi = new ProcessStartInfo(GitCmd, cmd)
			{
				WorkingDirectory = WorkingFolder,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardInput = true
			};
			//set env vars
			if (null != env)
				foreach (var e in env)
					pi.EnvironmentVariables.Add(e.Key, e.Value);
			using (var p = new Process()
			{
				StartInfo = pi
			})
			{
				p.Start();
				p.StandardInput.Write(stdInput);
				p.StandardInput.Close();
				var msgs = new List<string>();
				while (!p.StandardOutput.EndOfStream)
					msgs.Add(p.StandardOutput.ReadLine());
				stdOutput = msgs.ToArray();
				p.WaitForExit();
			}
			return Environment.TickCount - ticks;
		}

		private int vaultLogin()
		{
			Console.Write($"Starting Vault login to {VaultServer} for repository {VaultRepository}... ");
			var ticks = Environment.TickCount;
            if (ServerOperations.client.ClientInstance == null)
            {
                ServerOperations.client.CreateClientInstance(false);
            }
			if (ServerOperations.client.ClientInstance.ConnectionStateType == ConnectionStateType.Unconnected)
			{
				ServerOperations.client.ClientInstance.WorkingFolderOptions.StoreDataInWorkingFolders = false;
				ServerOperations.client.ClientInstance.Connection.SetTimeouts(Convert.ToInt32(TimeSpan.FromMinutes(10).TotalSeconds)
					, Convert.ToInt32(TimeSpan.FromMinutes(10).TotalSeconds));
                if (VaultUseSSL == true)
                    ServerOperations.client.LoginOptions.URL = string.Format("https://{0}/VaultService", this.VaultServer);
                else
                    ServerOperations.client.LoginOptions.URL = string.Format("http://{0}/VaultService", this.VaultServer);
                ServerOperations.client.LoginOptions.User = this.VaultUser;
				ServerOperations.client.LoginOptions.Password = this.VaultPassword;
				ServerOperations.client.LoginOptions.Repository = this.VaultRepository;
				ServerOperations.Login();
				ServerOperations.client.MakeBackups = false;
				ServerOperations.client.AutoCommit = false;
				ServerOperations.client.Verbose = true;
				_loginDone = true;
			}
			Console.WriteLine($"done!");
			return Environment.TickCount - ticks;
		}

		private int vaultLogout()
		{
			var ticks = Environment.TickCount;
			ServerOperations.Logout();
			return Environment.TickCount - ticks;
		}

		private void AddMapping(long vault, string git)
		{
			if (_txidMappings == null || _txidMappings.Count == 0)
			//Reload from file
			{
				_txidMappings = Tools.ReadFromXml(MappingSaveLocation)?.ToDictionary(kp => long.Parse(kp.Key), kp => kp.Value) ?? new Dictionary<long, string>();
			}
			if (_txidMappings.ContainsKey(vault))
			{
				var formerValue = _txidMappings[vault];
				_txidMappings[vault] = git;
				Console.WriteLine($"Updated value for existing key {vault} from {formerValue} to {git}.");
			}

			_txidMappings.Add(new KeyValuePair<long, string>(vault, git));
			Tools.SaveMapping(_txidMappings, MappingSaveLocation);
		}

		private string GetMapping(long key)
		{
			if (_txidMappings == null || _txidMappings.Count == 0)
			//Reload from file
			{
				_txidMappings = Tools.ReadFromXml(MappingSaveLocation)?.ToDictionary(kp => long.Parse(kp.Key), kp => kp.Value) ?? new Dictionary<long, string>();
			}
			if (!_txidMappings.ContainsKey(key))
			{
				// Rebuild mapping from git
				Console.WriteLine($"Missing an entry for key {key}, trying to rebuild mapping from git repository...");
				_txidMappings = RebuildMapping(currentGitBranch);
				if (!_txidMappings.ContainsKey(key))
				{
					return null;
				}
				return _txidMappings[key];
			}
			return _txidMappings[key];
		}

		private IDictionary<long, string> RebuildMapping(string gitBranch)
		{
			string[] msgs;

			getGitLogs(gitBranch, out msgs);
			var filtered = msgs.Where(l => l.Contains(VaultTag) || l.StartsWith("commit ")).ToArray();
			var commitInfos = new Dictionary<long, string>();
			for (var i = 0; i < filtered.Length - 1; i += 2)
			{
				var comitId = filtered[i].Replace("commit", string.Empty).Trim();
				var split = filtered[i + 1].Replace(VaultTag, string.Empty).Trim().Split('/');
				if (split.Length != 3)
					continue;
				commitInfos.Add(long.Parse(split[1].Split('@')[1]), comitId);
			}
			Tools.SaveMapping(commitInfos, MappingSaveLocation);

			return commitInfos;
		}
	}

}