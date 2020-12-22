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

		public string GitCommitMessageTempFile { get; set; }

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
		private const string _gitLastCommitInfoCmd = "log -1 --all --branches";
		private const string _gitAllCommitInfoCmd = "log --all --branches --parents";
		private const string _gitCommitCmd = @"commit --allow-empty --all --date=""{2}"" --author=""{0} <{1}>"" -F {3}";
        private const string _gitCheckoutCmd = "checkout --quiet --force {0}";
        private const string _gitCreateBranch = "checkout -b {0} {1}";
        private const string _gitBranchCmd = "branch";
		private const string _gitAddTagCmd = @"tag {0} {1} -a -m ""{2}""";
        private const string _gitInitCmd = "init";
        private const string _gitInitInitalCommitCmd = @"commit --allow-empty --date=""{0}"" --message=""{1} initial commit @master/0/0""";

        //private vars
        /// <summary>
        /// Maps Vault TransactionID to Git Commit SHA-1 Hash
        /// </summary>
        private IDictionary<String, String> _txidMappings;

		//private string currentGitBranch;

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
		public bool Pull(List<string> git2vaultRepoPath, long limitCount)
		{
			var completedStepCount=0;
			var versionProcessingTime = new Stopwatch();
			var overallProcessingTime = new Stopwatch();
			int ticks = 0;

            //load git authors
            Tools.ParseMapFile(AuthorMapPath);

            //create git repo if doesn't exist, otherwise, rebuild vault to git mappings from log
            if (!File.Exists(WorkingFolder + ".git/config"))
            {
                ticks += gitCreateRepo();
            }
            else
            {
                RebuildMapping();
            }

            //get git current branch
            string gitCurrentBranch;
			ticks += this.gitCurrentBranch(out gitCurrentBranch);


			ticks += vaultLogin();
			try
			{
				//reset ticks
				ticks = 0;

                List<string> vaultRepoPaths = git2vaultRepoPath;

                VaultVersionInfo currentGitVaultVersion = new VaultVersionInfo
                {
                    Branch = string.Empty,
                    Comment = string.Empty,
                    Login = string.Empty,
                    Path = string.Empty,
                    TimeStamp = VaultLib.VaultDateTime.MinValue,
                    TxId = 0,
                    Version = 0
                };

				//get current version
				ticks += gitVaultVersion(ref currentGitVaultVersion);

                //get vaultVersions
                Console.Write($"Fetching history from vault from {RevisionStartDate} to {RevisionEndDate}... ");

                IDictionary<string, VaultVersionInfo> vaultVersions = new SortedList<string, VaultVersionInfo>();
                foreach (string rp in vaultRepoPaths)
                {
                    ticks += vaultPopulateInfo(rp, vaultVersions);
                }
                
                var gitProgress = vaultVersions.Where(p => (p.Value.Branch == currentGitVaultVersion.Branch && p.Value.TxId == currentGitVaultVersion.TxId));

                var versionsToProcess = gitProgress.Any() ? vaultVersions.Where(p => (p.Key.CompareTo(gitProgress.FirstOrDefault().Value.TimeStamp.GetDateTime().ToString("yyyy-MM-ddTHH:mm:ss.fff") + ":" + gitProgress.FirstOrDefault().Value.Branch) > 0 )) : vaultVersions;

                var keyValuePairs = versionsToProcess.ToList();

                Console.WriteLine($"done! Fetched {keyValuePairs.Count} versions for processing.");

                //report init
                if (null != Progress)
					if (Progress(ProgressSpecialVersionInit, 0L, ticks))
						return true;

				var counter = 0;
				overallProcessingTime.Restart();
				foreach (var version in keyValuePairs)
				{
					versionProcessingTime.Restart();
                    ticks = 0;

                    ticks = Init(version.Value, ref gitCurrentBranch);

                    //check to see if we are in the correct branch
                    if (!gitCurrentBranch.Equals(version.Value.Branch, StringComparison.OrdinalIgnoreCase))
                        ticks += this.gitCheckoutBranch(version.Value.Branch, out gitCurrentBranch);

                    //get vault version
                    Console.Write($"Starting get version {version.Key} from Vault...");

                    ticks += vaultGet(version.Value);

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
					VaultVersionInfo info = vaultVersions[version.Key];
					//commit
					Console.Write($"Starting git commit...");
					buildCommitMessage(info);
					ticks += gitCommit(info, GitDomainName);
					Console.WriteLine($" done!");
					if (null != Progress)
						if (Progress(info.Version, keyValuePairs.Count, ticks))
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
				ticks = vaultFinalize(vaultRepoPaths);

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

        public bool buildGrafts()
        {
            IDictionary<string, IDictionary<string, string>> map = getMappingWithTxIdFromLog();

            var origins = map.Where(l => l.Value.Keys.Contains("origin"));

            if (_txidMappings == null || _txidMappings.Count == 0)
            //Reload from file
            {
                _txidMappings = Tools.ReadFromXml(MappingSaveLocation)?.ToDictionary(kp => kp.Key.ToLower(), kp => kp.Value) ?? new Dictionary<string, string>();
            }

            //Process _txIdMappings in reverse order and keep track of latest version inside of loop

            IDictionary<string, string> branches = new Dictionary<string, string>();
            IEnumerable<KeyValuePair<string, string>> transactions = _txidMappings.Reverse();

            string branch, version;
            List<Tuple<string, string, string>> graft = new List<Tuple<string, string, string>>();
            foreach(KeyValuePair<string, string> t in transactions)
            {
                branch = Tools.GetBranchMapping(t.Key.Split(':').First());
                version = t.Key.Split(':').Last();
                if (branches.ContainsKey(branch))
                {
                    branches[branch] = version;
                }
                else
                {
                    branches.Add(branch, version);
                }

                if (origins.Any(o => o.Key == t.Key))
                {
                    var origin = origins.Where(o => o.Key == t.Key).FirstOrDefault();
                    var sourceBranch = Tools.GetBranchMapping(origin.Value["origin"].Split('/').Last());
                    if (branches.ContainsKey(sourceBranch))
                    {
                        var a = string.Format("{0}:{1}", sourceBranch, branches[sourceBranch]);
                        //var v = branches[branch];
                        //var a = transactions.Where(x => x.Key.Equals(string.Format("{0}:{1}", branch, branches[branch]))).FirstOrDefault().Value;
                        graft.Add(Tuple.Create<string, string, string>(origin.Value["commit"], origin.Value["parent"], _txidMappings[a]));
                    }
                    
                }
            }

            if (graft.Any())
            {
                using (StreamWriter writetext = new StreamWriter(WorkingFolder + ".git\\info\\grafts"))
                {
                    foreach (Tuple<string, string, string> g in graft)
                    {
                        writetext.WriteLine(string.Format("{0} {1} {2}", g.Item1, g.Item2, g.Item3));
                    }
                }
            }
            return true;
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

		private int vaultPopulateInfo(string repoPath, IDictionary<string, VaultVersionInfo> info)
		{
			var ticks = Environment.TickCount;

            string[] PathToBranch = repoPath.Split('~');
            string path = PathToBranch[0];
            string branch = PathToBranch[1];

            VaultTxHistoryItem[] historyItems;
            VaultClientFolderColl repoFolders;

            if (repoPath.EndsWith("*"))
            {
                repoFolders = ServerOperations.ProcessCommandListFolder(path, false).Folders;
                
            }
            else
            {
                repoFolders = new VaultClientFolderColl();

                repoFolders.Add(ServerOperations.ProcessCommandListFolder(path, false));
            }

            foreach (VaultClientFolder f in repoFolders)
            {

                string branchName;

                if (branch == "*")
                    branchName = Tools.GetBranchMapping(f.Name);
                else
                    branchName = Tools.GetBranchMapping(branch);

                string EndDate = (f.Name == "SAS") ? "2016-01-23" : RevisionEndDate;

                historyItems = ServerOperations.ProcessCommandVersionHistory(f.FullPath,
                    0,
                    VaultDateTime.Parse(RevisionStartDate),
                    VaultDateTime.Parse(EndDate),
                    0);

                foreach (VaultTxHistoryItem i in historyItems)
                    info.Add(i.TxDate.GetDateTime().ToString("yyyy-MM-ddTHH:mm:ss.fff")+ ':' + branchName, new VaultVersionInfo()
                    {
                        Branch = branchName,
                        Path = f.FullPath,
                        Version = i.Version,
                        TxId = i.TxID,
                        Comment = i.Comment,
                        Login = i.UserLogin,
                        TimeStamp = i.TxDate
                    });
            }
            
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

                    ///TODO: not sure how to handle this across multiple branch folders. adjusting it just so it compiles and I'll address it later
                    var gitCommitId = string.Empty; //GetMapping(currItem.Version.ToString());

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

		private int vaultGet(VaultVersionInfo info)
		{
			var ticks = Environment.TickCount;
			//apply version to the repo folder
			 GetOperations.ProcessCommandGetVersion(
				info.Path,
				Convert.ToInt32(info.Version),
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
            foreach (var item in ServerOperations.ProcessCommandTxDetail(info.TxId).items
                .Where(i => allowedRequests.Contains(i.RequestType)))

                //delete file
                //check if it is within current branch, but ignore if it equals the branch
                if (item.ItemPath1.StartsWith(info.Path, StringComparison.CurrentCultureIgnoreCase) && !item.ItemPath1.Equals(info.Path, StringComparison.CurrentCultureIgnoreCase))
				{
					var pathToDelete = Path.Combine(this.WorkingFolder, item.ItemPath1.Substring(info.Path.Length + 1));
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
            public string Branch;
            public string Path;
            public long Version;
			public long TxId;
			public string Comment;
			public string Login;
			public VaultLib.VaultDateTime TimeStamp;
		}

		private int gitVaultVersion(ref VaultVersionInfo currentVersion)
		{
			string[] msgs;
			//get info
			var ticks = gitLog(out msgs);
			//get vault version
			getVaultVersionFromGitLogMessage(msgs, ref currentVersion);
			return ticks;
		}

		private int Init(VaultVersionInfo info, ref string gitCurrentBranch)
		{
            //set vault working folder
			int ticks = setVaultWorkingFolder(info.Path);
            bool branchExists = false;

            //verify git branch exists - create if needed
            ticks += gitVerifyBranchExists(info.Branch, out branchExists);

            if (branchExists)
            {
                if (!gitCurrentBranch.Equals(info.Branch, StringComparison.OrdinalIgnoreCase))
                    ticks += this.gitCheckoutBranch(info.Branch, out gitCurrentBranch);
            }
            else
            {
                ticks += gitCreateBranch(info, out gitCurrentBranch);
            }

            return ticks;
		}

		private int vaultFinalize(List<string> vaultRepoPaths)
		{
			//unset working folder
            return unSetVaultWorkingFolder(vaultRepoPaths);
		}


        private int gitCommit(VaultVersionInfo info, string gitDomainName)
        {
            string gitCurrentBranch;
            string gitName;
            string gitEmail;
            string gitAuthor;
            string commitTimeStamp;

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

            gitAuthor = Tools.GetGitAuthor(info.Login);
            if (gitAuthor != null)
            {
                gitName = gitAuthor.Split(':')[0];
                gitEmail = gitAuthor.Split(':')[1];
            }
            else
            {
                gitName = info.Login;
                gitEmail = info.Login + '@' + gitDomainName;
            }

            commitTimeStamp = info.TimeStamp.GetDateTime().ToString("yyyy-MM-ddTHH:mm:ss");

            Dictionary<string, string> env = new Dictionary<string, string>();
            env.Add("GIT_COMMITTER_DATE", commitTimeStamp);
            env.Add("GIT_COMMITTER_NAME", gitName);
            env.Add("GIT_COMMITTER_EMAIL", gitEmail);

            ticks += runGitCommand(
				string.Format(_gitCommitCmd, gitName, gitEmail, string.Format("{0:s}", commitTimeStamp), GitCommitMessageTempFile),
				string.Empty,
				out msgs, 
                env
				);

			// Mapping Vault Transaction ID to Git Commit SHA-1 Hash
			if (msgs[0].StartsWith("[" + gitCurrentBranch))
			{
				string gitCommitId = msgs[0].Split(' ')[1];
				gitCommitId = gitCommitId.Substring(0, gitCommitId.Length - 1);
				AddMapping(info, gitCommitId);
			}
			return ticks;
		}

        private int gitCreateRepo()
        {
            string[] msgs;
            int ticks = runGitCommand(_gitInitCmd, string.Empty, out msgs);
            if (!msgs[0].StartsWith("Initialized empty Git repository"))
            {
                throw new InvalidOperationException("The local git repository doesn't exist and can't be created.");
            }

            //add .gitignore and .gitattributes
            
            Tools.CopyFile("Resources\\.gitignore", WorkingFolder + ".gitignore");
            Tools.CopyFile("Resources\\.gitattributes", WorkingFolder + ".gitattributes");
            ticks += runGitCommand(_gitAddCmd, string.Empty, out msgs);

            Dictionary<string, string> env = new Dictionary<string, string>();
            env.Add("GIT_COMMITTER_DATE", DateTime.Parse(RevisionStartDate).ToString("yyyy-MM-ddTHH:mm:ss.fff"));


            ticks += runGitCommand(string.Format(_gitInitInitalCommitCmd, DateTime.Parse(RevisionStartDate).ToString("yyyy-MM-ddTHH:mm:ss.fff"), VaultTag), string.Empty, out msgs, env);

            if (msgs.Any())
            {
                string gitCommitId = msgs[0].Split(' ')[2];
                gitCommitId = gitCommitId.Substring(0, gitCommitId.Length - 1);
                VaultVersionInfo info = new VaultVersionInfo { Branch = "Master", TxId = 0 };
                AddMapping(info, gitCommitId);
            }

            


            return ticks;
        }

        private int gitCreateBranch(VaultVersionInfo info, out string currentBranch)
        {
            /*  get vault items (itempath1/itempath2)
             *  filter by itempath2 = branchpath
             *  get source branch from end of itempath1
             *  get hash of latest commit in the identified branch
             *  use the hash as the source of the branch
            */

            TxInfo txDetail = ServerOperations.ProcessCommandTxDetail(info.TxId);

            string sourceBranch;

            var items = txDetail.items.Where(i => (i.ItemPath2 == info.Path || info.Path.StartsWith(i.ItemPath2 + "/")));
            if (items.Count() > 0)
                sourceBranch = items.FirstOrDefault().ItemPath1.Split('/').LastOrDefault();
            else
                sourceBranch = "master:";

            string gitStartPoint = GetMapping(new VaultVersionInfo { Branch = Tools.GetBranchMapping(sourceBranch), TxId = 0  });

            string[] msgs;
            int ticks = runGitCommand(string.Format(_gitCreateBranch, info.Branch, gitStartPoint), string.Empty, out msgs);
            currentBranch = info.Branch;

            return ticks;

        }

        private int gitCurrentBranch(out string currentBranch)
		{
			string[] msgs;
			int ticks = runGitCommand(_gitBranchCmd, string.Empty, out msgs);
			if (msgs.Any())
				currentBranch = msgs.Where(s => s.StartsWith("*")).First().Substring(1).Trim();
			else
			{
				currentBranch = string.Empty;
				throw new InvalidOperationException("The local git repository doesn't contain any branches. Please create at least one.");
			}
			return ticks;
		}

        private int gitVerifyBranchExists(string BranchName, out bool exists)
        {
            string[] msgs;
            exists = false;
            int ticks = runGitCommand(_gitBranchCmd, string.Empty, out msgs);
            if (msgs.Where(s => s.Replace("* ", string.Empty).Trim().Equals(BranchName)).Any())
                exists = true;

            return ticks;
        }

        private int gitCheckoutBranch(string gitBranch, out string currentBranch)
        {
            //checkout branch
            int ticks = 0;
            string[] msgs;



            ticks += runGitCommand(string.Format(_gitCheckoutCmd, gitBranch), string.Empty, out msgs);


            for (int tries = 0; ; tries++)
            {
                ticks += runGitCommand(string.Format(_gitCheckoutCmd, gitBranch), string.Empty, out msgs);
                //confirm current branch (sometimes checkout failed)
                
                ticks += this.gitCurrentBranch(out currentBranch);
                if (gitBranch.Equals(currentBranch, StringComparison.OrdinalIgnoreCase))
                    break;
                if (tries > 5)
                    throw new Exception("cannot switch");
            }
            return ticks;
        }

		private bool buildCommitMessage(VaultVersionInfo info)
		{
			//parse path repo$RepoPath@branch/version/trx
			var r = new StringBuilder(info.Comment);
			r.AppendLine();
			r.AppendFormat("{4} {0}{1}@{5}/{2}/{3}", this.VaultRepository, info.Path, info.Version, info.TxId, VaultTag, info.Branch);
			r.AppendLine();
			return r.ToString();
		}

        private VaultVersionInfo getVaultVersionFromGitLogMessage(string[] msg, ref VaultVersionInfo info)
		{

            //get last string
            var stringToParse = msg.Last();
			
            //search for version tag
			var versionString = stringToParse.Split(new string[] { VaultTag }, StringSplitOptions.None).LastOrDefault();
			if (null == versionString)
				return info;

            //parse path reporepoPath@branch/TxId/version
            var version = versionString.Split('@');
            var versionTrxTag = version.LastOrDefault();

			if (null == versionTrxTag)
				return info;

			//get version
			string[] tag = versionTrxTag.Split('/');

            if (tag.Count() > 2)
            {
                info.Branch = tag[0];
                long.TryParse(tag[1], out info.Version);
                long.TryParse(tag[2], out info.TxId);
            }

            return info;
		}

		private int gitLog(out string[] msg)
		{
			return runGitCommand(_gitLastCommitInfoCmd, string.Empty, out msg);
		}

		private int getGitLogs(out string[] msgLines)
		{
			return runGitCommand(_gitAllCommitInfoCmd, string.Empty, out msgLines);
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

            //check for existing assignment and remove if found
            var workingFolders = ServerOperations.GetWorkingFolderAssignments();
            if (workingFolders.ContainsValue(this.WorkingFolder))
            {
                ServerOperations.RemoveWorkingFolder(workingFolders.GetKey(workingFolders.IndexOfValue(this.WorkingFolder)).ToString());
            }

			ServerOperations.SetWorkingFolder(repoPath, this.WorkingFolder, true);
			return Environment.TickCount - ticks;
		}

		private int unSetVaultWorkingFolder(List<string> repoPath)
		{
			var ticks = Environment.TickCount;
            
            //remove any assignment first
            //it is case sensitive, so we have to find how it is recorded first
            IEnumerable<DictionaryEntry> wf = ServerOperations.GetWorkingFolderAssignments().Cast<DictionaryEntry>();

            foreach (string folder in repoPath)
            {

                string exPath = wf.Select(e => e.Key.ToString())
                    .Where(e => folder.Equals(e, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

                if (null != exPath)
                    ServerOperations.RemoveWorkingFolder(exPath);
            }
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

		private void AddMapping(VaultVersionInfo info, string git)
		{
            string key = info.Branch + ":" + info.TxId;
            if (_txidMappings == null || _txidMappings.Count == 0)
			//Reload from file
			{
				_txidMappings = Tools.ReadFromXml(MappingSaveLocation)?.ToDictionary(kp => kp.Key, kp => kp.Value) ?? new Dictionary<string, string>();
			}
			if (_txidMappings.ContainsKey(key))
			{
				var formerValue = _txidMappings[key];
				_txidMappings[key] = git;
				Console.WriteLine($"Updated value for existing key {key} from {formerValue} to {git}.");
			}

			_txidMappings.Add(new KeyValuePair<string, string>(key, git));
			Tools.SaveMapping(_txidMappings, MappingSaveLocation);
		}

		private string GetMapping(VaultVersionInfo info)
		{
            string key = info.Branch + ":";

            if (info.TxId != 0)
            {
                key += info.TxId;
            }

            if (_txidMappings == null || _txidMappings.Count == 0)
			//Reload from file
			{
				_txidMappings = Tools.ReadFromXml(MappingSaveLocation)?.ToDictionary(kp => kp.Key, kp => kp.Value) ?? new Dictionary<string, string>();
			}
			if (!_txidMappings.ContainsKey(key))
			{
				// Rebuild mapping from git
				Console.WriteLine($"Missing an entry for key {key}, trying to rebuild mapping from git repository...");
				_txidMappings = RebuildMapping();
				if (!_txidMappings.ContainsKey(key))
				{
                    if (!(info.TxId == 0) | !(_txidMappings.Where(k => k.Key.StartsWith(key)).Any()))
                    {
                        // can't find it - the branch was probably deleted in vault
                        // default to branch off of current state of master
                        key = "master:";
                    }
                    key = _txidMappings.Where(k => k.Key.StartsWith(key)).FirstOrDefault().Key;
                }
			}
			return _txidMappings[key];
		}

		private IDictionary<string, string> RebuildMapping()
		{
			string[] msgs;

            getGitLogs(out msgs);
			var filtered = msgs.Where(l => l.Contains(VaultTag) || l.StartsWith("commit ")).ToArray();
			var commitInfos = new Dictionary<string, string>();
			for (var i = 0; i < filtered.Length - 1; i += 2)
			{
                //var comitId = filtered[i].Replace("commit", string.Empty).Trim();
                var comitId = filtered[i].Split(' ')[1];
                var split = filtered[i + 1].Replace(VaultTag, string.Empty).Trim().Split('@').LastOrDefault().Split('/');
				if (split.Length != 3)
					continue;
				commitInfos.Add(split[0] + ":" + split[2], comitId);
			}
			Tools.SaveMapping(commitInfos, MappingSaveLocation);

			return commitInfos;
		}

        private IDictionary<string, IDictionary<string, string>> getMappingWithTxIdFromLog()
        {
            string[] msgs;
            

            getGitLogs(out msgs);
            var filtered = msgs.Where(l => l.Contains(VaultTag) || l.StartsWith("commit ") || l.Contains("Merge Branches : Origin=$")).ToArray();

            var commitInfos = new Dictionary<string, IDictionary<string, string>>();

            for (var i = 0; i < filtered.Length - 1; i++)
            {
                var data = new Dictionary<string, string>();

                data.Add("commit", filtered[i].Replace("commit", string.Empty).Trim().Split(' ').FirstOrDefault());
                data.Add("parent", filtered[i].Replace("commit", string.Empty).Trim().Split(' ').ElementAtOrDefault(1));
                while (i < filtered.Length & !filtered[i+1].Contains(VaultTag)){
                    i++;
                    if (!data.ContainsKey("origin"))
                        data.Add("origin",filtered[i].Substring(filtered[i].IndexOf('$')));
                }
                i++;
                var split = filtered[i].Replace(VaultTag, string.Empty).Trim().Split('@').LastOrDefault().Split('/');
                if (split.Length != 3)
                    continue;
                data.Add("txid", split[1]);
                commitInfos.Add(split[0] + ":" + split[2], data);
            }

            return commitInfos;
        }
	}
}