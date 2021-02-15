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
using Vault2Git.Lib;
//using System.Xml.XPath;
using System.Xml;
using System.Xml.Linq;
using static Vault2Git.Lib.Vault2GitState;
//using System.Globalization;

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

		public Vault2GitState ConversionState = new Vault2GitState();

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
		//private const string _gitLastCommitInfoCmd = "log -1 --all --branches --pretty=format:<c>%n<H>%H</H>%n<P>%P</P>%n<ae>%ae</ae>%n<N><![CDATA[%B]]></N>%n</c>";
		//private const string _gitAllCommitInfoCmd = "log --all --branches --parents";
		//private const string _gitAllCommitInfoCmd = "log --all --branches --parents --pretty=format:\"<c>%n<H>%H</H>%n<P>%P</P>%n<ae>%ae</ae>%n<N><![CDATA[%s%b]]></N>%n</c>\"";
		private const string _gitAllCommitInfoCmd = "log --all --branches --parents --pretty=format:\"<c>%n<H>%H</H>%n<P>%P</P>%n<D>%D</D>%n<ae>%ae</ae>%n<N><![CDATA[%B]]></N>%n</c>\"";
		private const string _gitReplaceGraftCmd = "replace -f --graft {0} {1}";
		private const string _gitReplacementCmd = "replace --format=medium --list {0}";

		private const string _gitCommitCmd = @"commit --allow-empty --all --date=""{2}"" --author=""{0} <{1}>"" -F {3}";
        private const string _gitCheckoutCmd = "checkout --quiet --force {0}";
        private const string _gitCreateBranch = "checkout -b {0} {1}";
        private const string _gitBranchCmd = "branch";
		private const string _gitAddTagCmd = @"tag {0} {1} -a -m ""{2}""";
        private const string _gitInitCmd = "init";
        private const string _gitInitInitalCommitCmd = @"commit --allow-empty --date=""{0}"" --message=""{1} initial commit @master/0/0""";
		private const string _gitFullHashCmd = "rev-parse {0}";

		//private vars
		/// <summary>
		/// Maps Vault TransactionID to Git Commit SHA-1 Hash
		/// </summary>
		//private IDictionary<long, VaultTx2GitTx> _txidMappings;

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
		public bool Pull(List<string> git2vaultRepoPath, long limitCount, bool ignoreLabels)
		{
			var completedStepCount=0;
			var versionProcessingTime = new Stopwatch();
			var overallProcessingTime = new Stopwatch();
			int ticks = 0;

            //load git authors
            Tools.ParseMapFile(AuthorMapPath);

            //create git repo if doesn't exist, otherwise, rebuild vault to git mappings from log
            if (!File.Exists(WorkingFolder + "\\.git\\config"))
            {
                ticks += gitCreateRepo(ConversionState);
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


				VaultTx currentGitVaultVersion = new VaultTx();

				// get current vault version from last git commit
				ticks += gitVaultVersion(ref currentGitVaultVersion);

                
                Console.Write($"Fetching history from vault from {RevisionStartDate} to {RevisionEndDate}... ");

				// populate ConversionState with all vault transactions in the repository paths
				foreach (string rp in vaultRepoPaths)
                {
                    ticks += vaultPopulateInfo(rp);
                }

				// get an ordered list of the Vault transactions that still need to be processed
				SortedList<long,VaultTx> versionsToProcess = ConversionState.GetVaultTransactionsToProcess(currentGitVaultVersion.TxId);

				//var versionsToProcess = gitProgress.Any() ? vaultVersions.Where(p => 
				//	(p.Key.CompareTo(gitProgress.FirstOrDefault().Value.TimeStamp.GetDateTime().ToString("yyyy-MM-ddTHH:mm:ss.fff") + ":"
				//	+ gitProgress.FirstOrDefault().Value.Branch + ':' + gitProgress.FirstOrDefault().Value.TxId.ToString()) > 0 )) : vaultVersions;

                //var keyValuePairs = versionsToProcess.ToList();

                Console.WriteLine($"done! Fetched {versionsToProcess.Count} versions for processing.");

                //report init
                if (null != Progress)
					if (Progress(ProgressSpecialVersionInit, 0L, ticks))
						return true;

				overallProcessingTime.Restart();
				for (int i = 0; i < versionsToProcess.Count; i++)
				{
					versionProcessingTime.Restart();
                    ticks = 0;
					VaultTx version = versionsToProcess.Values[i];

                    ticks = Init(version, ref gitCurrentBranch);

                    //check to see if we are in the correct branch
                    if (!gitCurrentBranch.Equals(version.Branch, StringComparison.OrdinalIgnoreCase))
                        ticks += this.gitCheckoutBranch(version.Branch, out gitCurrentBranch);

                    //get vault version
                    Console.Write($"Starting get version Tx:{version.TxId} - {version.Branch}:{version.Version} from Vault...");

                    ticks += vaultGet(version);

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
					//commit
					Console.Write($"Starting git commit...");
					buildCommitMessage(version);
					ticks += gitCommit(version, GitDomainName);
					Console.WriteLine($" done!");
					if (null != Progress)
						if (Progress(version.Version, versionsToProcess.Count, ticks))
							return true;
					
					//call gc
					if (0 == i + 1 % GitGCInterval)
					{
						ticks = gitGC();
						if (null != Progress)
							if (Progress(ProgressSpecialVersionGc, versionsToProcess.Count, ticks))
								return true;
					}
					//check if limit is reached
					if (i + 1 >= limitCount)
						break;
					completedStepCount++;
					versionProcessingTime.Stop();
					Tools.WriteProgressInfo(string.Empty, versionProcessingTime.Elapsed, completedStepCount, versionsToProcess.Count, overallProcessingTime.Elapsed);

                    //check if escape key is pressed
                    if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
                    {
                        break;
                    }

                }

				BuildGrafts();
				
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

        public bool BuildGrafts()
        {
			//IDictionary<string, IDictionary<string, string>> map = getMappingWithTxIdFromLog();
			List<GitCommit> map = getMappingWithTxIdFromLog();
			
			var origins = map.Where(l => l.Comment.TrimStart().StartsWith("Merge Branches : Origin"));

			// make sure our stored commit mappings all have full hashes
			// This shouldn't be necessary anymore now that short hashes are converted 
			RebuildMapping();
			_txidMappings = Tools.ReadFromXml(MappingSaveLocation)?.ToDictionary(kp => kp.Key, kp => kp.Value) ?? new Dictionary<long, VaultTx2GitTx>();
			
            //Process _txIdMappings in reverse order and keep track of latest version inside of loop

            IDictionary<string, long> branches = new Dictionary<string, long>();
            IEnumerable<KeyValuePair<long, VaultTx2GitTx>> transactions = _txidMappings.Reverse();

			string branch;
			long version;

            foreach(KeyValuePair<long, VaultTx2GitTx> t in transactions)
            {
				
                branch = Tools.GetBranchMapping(t.Value.Branch);
                version = t.Value.TxId;
                if (branches.ContainsKey(branch))
                {
                    branches[branch] = version;
                }
                else
                {
                    branches.Add(branch, version);
                }

				/*
				 *  How do I keep track of replacements so the parents of a merge get the correct commit if it has been replaced?
				 * 
				 */

				var origin = origins.Where(o => o.VaultInfo.Branch == t.Value.Branch && o.VaultInfo.TxId == t.Key);
				foreach (GitCommit o in origin)
				{
                    var sourceBranch = Tools.GetBranchMapping(o.VaultInfo.MergedFrom);
                    if (branches.ContainsKey(sourceBranch))
                    {
						var newParentCommitHash = _txidMappings[branches[sourceBranch]].GitHash;
						if (!o.ParentCommitHash.Contains(newParentCommitHash)) 
						{
							o.ParentCommitHash.Add(newParentCommitHash);

							gitReplaceGraft(o.CommitHash, o.ParentCommitHash);

							GitCommitHash ReplacementCommitHash;
							gitReplacmentCommitHash(o.CommitHash, out ReplacementCommitHash);


							if (o.CommitHash != ReplacementCommitHash)
							{
								_txidMappings[branches[branch]].GitHash.Replace(ReplacementCommitHash);
								Console.WriteLine(string.Format("Replaced commit {0} with commit {1} to add new parent commit {2}",
									o.CommitHash.ToString(),
									ReplacementCommitHash.ToString(),
									newParentCommitHash.ToString())
								); ;
							}
						}
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
			bool recurse = false;
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
					if (trimmedLine.StartsWith("GlobalSection(SourceCodeControl)")
						|| trimmedLine.StartsWith("GlobalSection(VaultVsipSolution"))
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
				recurse = true;
			}
			if (recurse) removeSCCFromSln(filePath);

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

		private int vaultPopulateInfo(string repoPath)
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
				{
					VaultTx info = ConversionState.CreateVaultTransaction(i.TxID, branchName);

					info.Path = f.FullPath;
					info.Version = i.Version;
					info.Comment = i.Comment;
					info.Login = i.UserLogin;
					info.TimeStamp = i.TxDate;
					info.MergedFrom = (i.Comment.TrimStart().StartsWith("Merge Branches : Origin=$")) ? i.Comment.Trim().Split(Environment.NewLine.ToCharArray())[0].Split('/')?.LastOrDefault() : string.Empty;
				}
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

		private int vaultGet(VaultTx info)
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

		private int gitVaultVersion(ref VaultTx currentVersion)
		{
			string[] msgs;
			//get info
			var ticks = gitLog(out msgs);
			//get vault version
			getVaultVersionFromGitLogMessage(msgs.LastOrDefault(), ref currentVersion);
			return ticks;
		}

		private int Init(VaultTx info, ref string gitCurrentBranch)
		{
            //set vault working folder
			int ticks = setVaultWorkingFolder(info.Path);
			bool branchExists;

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

		private int gitReplacmentCommitHash(GitCommitHash CommitHash, out GitCommitHash ReplacementCommitHash )
        {
			int ticks = 0;
			ReplacementCommitHash = CommitHash;

			string ReplacementHash;
			ticks = gitReplacement(CommitHash, out ReplacementHash);

			if (ReplacementHash != string.Empty)
				ReplacementCommitHash = new GitCommitHash(ReplacementHash);

			return ticks;

		}

		private int vaultFinalize(List<string> vaultRepoPaths)
		{
			//unset working folder
            return unSetVaultWorkingFolder(vaultRepoPaths);
		}

		private int gitInitialCommit()
        {
			string[] msgs;

			Dictionary<string, string> env = new Dictionary<string, string>();
			env.Add("GIT_COMMITTER_DATE", DateTime.Parse(RevisionStartDate).ToString("yyyy-MM-ddTHH:mm:ss.fff"));


			int ticks = runGitCommand(string.Format(_gitInitInitalCommitCmd, DateTime.Parse(RevisionStartDate).ToString("yyyy-MM-ddTHH:mm:ss.fff"), VaultTag), string.Empty, out msgs, env);

			if (msgs.Any())
			{
				string gitCommitId = msgs[0].Split(' ')[2];
				gitCommitId = getFullHash(gitCommitId.Substring(0, gitCommitId.Length - 1));
				
				AddMapping(VaultTx.Create(0), gitCommitId);
			}

			return ticks;
		}

        private int gitCommit(VaultTx info, string gitDomainName)
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
				string commitHash = getFullHash(gitCommitId.Substring(0, gitCommitId.Length - 1));
				//ConversionState.
				ConversionState.CreateGitCommit(commitHash);
				AddMapping(info, commitHash);
			}
			return ticks;
		}

        private int gitCreateRepo(Vault2GitState state)
        {
			if (!Directory.Exists(WorkingFolder))
			{
				Directory.CreateDirectory(WorkingFolder);
			}

			int ticks = gitInit();

            //add .gitignore and .gitattributes
            Tools.CopyFile("Resources\\.gitignore", WorkingFolder + "\\.gitignore");
            Tools.CopyFile("Resources\\.gitattributes", WorkingFolder + "\\.gitattributes");

			ticks += gitAddAll();
			ticks += gitInitialCommit();

			return ticks;
        }

        private int gitCreateBranch(VaultTx info, out string currentBranch)
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

            VaultTx2GitTx gitStartPoint = GetMapping(new VaultVersionInfo { Branch = Tools.GetBranchMapping(sourceBranch), TxId = info.TxId});

            string[] msgs;
            int ticks = runGitCommand(string.Format(_gitCreateBranch, info.Branch, gitStartPoint.GitHash), string.Empty, out msgs);
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

		private int gitInit()
		{
			string[] msgs;

			int ticks = runGitCommand(_gitInitCmd, string.Empty, out msgs);
			if (!msgs[0].StartsWith("Initialized empty Git repository"))
			{
				throw new InvalidOperationException("The local git repository doesn't exist and can't be created.");
			}
			return ticks;
		}

		private int gitAddAll()
		{
			string[] msgs;
			int ticks = runGitCommand(_gitAddCmd, string.Empty, out msgs);

			return ticks;
		}

		private int gitReplaceGraft(GitCommitHash gitCommitHash, List<GitCommitHash> gitParentCommitHash)
        {
			int ticks = 0;
			string[] msgs;

			ticks += runGitCommand(string.Format(_gitReplaceGraftCmd, gitCommitHash.ToString(), string.Join(" ", gitParentCommitHash.Select(h => h.ToString()))), string.Empty, out msgs);

			return ticks;
		}

		private int gitReplacement(GitCommitHash gitCommitHash, out string gitReplacementHash)
		{
			int ticks = 0;
			string[] msgs;

			ticks += runGitCommand(string.Format(_gitReplacementCmd, gitCommitHash.ToString()), string.Empty, out msgs);

			if (msgs.Any())
			{
				gitReplacementHash = msgs[0].Split('>')[1].Trim();
			}
			else
				gitReplacementHash = string.Empty;

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

		private bool buildCommitMessage(VaultTx info)
		{
			//parse path repo$RepoPath@branch/version/trx
			var r = new StringBuilder(info.Comment);
			r.AppendLine();
			r.AppendFormat("{4} {0}{1}@{5}/{2}/{3}", this.VaultRepository, info.Path, info.Version, info.TxId, VaultTag, info.Branch);
			r.AppendLine();
			return Tools.SaveFile(GitCommitMessageTempFile, r.ToString());
		}

		private bool getVaultVersionFromGitLogMessage(string stringToParse, ref VaultTx info)
		{
			//search for version tag
			var versionStrings = stringToParse.Split(new string[] { VaultTag }, StringSplitOptions.None);
			if (null == versionStrings || versionStrings.Count() != 2 )
				return false;

            //parse path reporepoPath@branch/TxId/version
            var version = versionStrings[1].Split('@');
			if (null == version || version.Count() != 2)
				return false;

			var ids = version[1].Trim().Split('/');
			if (null == ids || ids.Count() != 3)
				return false;
			
			// remove repository from string
			string path = (version[0].IndexOf('$') > 0) ? version[0].Remove(0, version[0].IndexOf('$')) : string.Empty;

            //populate other fields
            long.TryParse(ids[1], out long ver);
            long.TryParse(ids[2], out long txId);

			info = new VaultTx(txId, ids[0], path, ver, versionStrings[0], string.Empty, string.Empty, VaultDateTime.MinValue);

			return true;
        }

		private int gitLog(out string[] msg)
		{
			return runGitCommand(_gitLastCommitInfoCmd, string.Empty, out msg);
		}

		private int getGitLogs(out string[] msgLines)
		{
			int time = runGitCommand(_gitAllCommitInfoCmd, string.Empty, out msgLines);
			int len = msgLines.Length;
			msgLines[0] = msgLines[0].Insert(0, "<commits>");
			msgLines[len-1] = msgLines[len-1].Insert(msgLines[len-1].Length, "</commits>");

			return time;
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
				RedirectStandardInput = true,
				StandardOutputEncoding = Encoding.UTF8				
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
				//StreamWriter utf8Writer = new StreamWriter(p.StandardInput.BaseStream, new UTF8Encoding(false));

				var ms = new MemoryStream();
				var writer = new StreamWriter(ms, new UTF8Encoding(false));
				writer.Write(stdInput);
				writer.Flush();
				//utf8Writer.Write(stdInput);
				//utf8Writer.Close();
				p.StandardInput.Write(Encoding.UTF8.GetString(ms.ToArray()));
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

		private void AddMapping(VaultTx info, string commitHash)
		{
            
			long key = info.TxId;
            GitCommitHash gitCommitHash = ConversionState.getGitCommitHash(commitHash);

			ConversionState.


   //         if (_txidMappings == null || _txidMappings.Count == 0)
   ////Reload from file
   //{
   //	_txidMappings = Tools.ReadFromXml(MappingSaveLocation)?.ToDictionary(kp => kp.Key, kp => kp.Value) ?? new Dictionary<long, VaultTx2GitTx>();
   //}
   //ConversionState.getGitCommitHash(commitHash)

			if (_txidMappings.ContainsKey(info.TxId))
			{
				if (_txidMappings[key].GitHash.ToString() != gitHash)
                {
					VaultTx2GitTx newValue = new VaultTx2GitTx(gitHash, info);
					Console.WriteLine($"Updated value for existing key {key}:{info.Branch} from {_txidMappings[key].GitHash} to {gitHash}.");
					_txidMappings[key] = newValue;
				}
			}
			else
            {
				VaultTx2GitTx newEntry = new VaultTx2GitTx(gitHash, info);
				_txidMappings.Add(newEntry.TxId, newEntry);
			}
				
			Tools.SaveMapping(_txidMappings.Values.ToList(), MappingSaveLocation);
		}

		private VaultTx2GitTx GetMapping(VaultVersionInfo info)
		{
			long key = info.TxId;
			string branch = info.Branch;

            if (_txidMappings == null || _txidMappings.Count == 0)
			//Reload from file
			{
				_txidMappings = Tools.ReadFromXml(MappingSaveLocation)?.ToDictionary(kp => kp.Key, kp => kp.Value) ?? new Dictionary<long, VaultTx2GitTx>();
			}
			if (!_txidMappings.ContainsKey(key))
			{
				// Rebuild mapping from git
				Console.WriteLine($"Missing an entry for key {key}:{info.Branch}, trying to rebuild mapping from git repository...");
				_txidMappings = RebuildMapping();
				if (!_txidMappings.ContainsKey(key))
				{
                    if (!(info.TxId == 0) | !(_txidMappings.Where(k => k.Value.Branch == info.Branch).Any()))
                    {
                        Console.WriteLine($"Key {key} not found.  Using current version of Master");
                        // can't find it - the branch was probably deleted in vault
                        // default to branch off of current state of master
                        branch = "master";
                    }
                    key = _txidMappings.Where(k => k.Value.Branch == branch).FirstOrDefault().Key;
                }
			}
			return _txidMappings[key];
		}

		private Dictionary<long, VaultTx2GitTx> RebuildMapping()
		{
            List<GitCommit> commits = getMappingWithTxIdFromLog();
			
			var commitInfos = new List<VaultTx2GitTx>();
			for (var i = 0; i < commits.Count(); i++)
			{
				var commitHash = commits[i].CommitHash;
 
				VaultTx tx = new VaultTx(commits[i].VaultInfo.TxId, commits[i].VaultInfo.Branch);
				commitInfos.Add(new VaultTx2GitTx(commitHash, tx));
			}

			Tools.SaveMapping(commitInfos, MappingSaveLocation);

			return commitInfos.ToDictionary(commit => commit.TxId, commit => commit);
		}

		//private IDictionary<string, IDictionary<string, string>> getMappingWithTxIdFromLog()
		private List<GitCommit> getMappingWithTxIdFromLog()
		{
            string[] msgs;
			//long TxId = 0;

            getGitLogs(out msgs);

			List<GitCommit> Commits = GitLogFromXml(msgs);

			var map = Commits.Where(l => l.Comment.Contains(VaultTag)).ToArray();

			for (var i = 0; i < map.Length; i++)
            {
				getVaultVersionFromGitCommit(map[i], ref map[i].VaultInfo);
            }

			return map.ToList();
        }

		private string getFullHash(string hash)
        {
			string FullHash;
			gitFullHash(hash, out FullHash);

			return FullHash;
		}

		private int gitFullHash(string gitHash, out string FullHash)
		{
			int ticks = 0;
			
			string[] msg;

			ticks = runGitCommand(string.Format(_gitFullHashCmd, gitHash),
				string.Empty,
				out msg);

			FullHash = msg[0];

			return ticks;
		}

		private List<GitCommit> GitLogFromXml(string[] msg)
		{
			var reader = new StringReader(string.Join(Environment.NewLine, msg));
			var gitCommits = new List<GitCommit>();
			var xDoc = XDocument.Load(reader).Root;

			var commits = xDoc.Descendants("c");

			foreach (XElement c in commits)
			{
				var x = new GitCommit();

				if (c.Descendants("D").FirstOrDefault().Value.Split(',').Any(d => d.Trim() == "replaced"))
					continue;


				x.CommitHash = new GitCommitHash(c.Descendants("H").FirstOrDefault().Value);
				x.Comment = c.Descendants("N").FirstOrDefault().Value;
				x.ParentCommitHash = c.Descendants("P").FirstOrDefault().Value.Split(' ').Select(l => new GitCommitHash(l)).ToList();

				if (x.Comment.Contains(VaultTag))
				{
					VaultVersionInfo info = new VaultVersionInfo();
					getVaultVersionFromGitLogMessage(x.Comment, ref info);
					if (info != x.VaultInfo)
                    {
						x.VaultInfo = info;
                    }
				}

				gitCommits.Add(x);
			}
			return gitCommits;
		}

		private  bool getVaultVersionFromGitCommit(GitCommit gitCommit, ref VaultVersionInfo info)
        {
			if (gitCommit.Comment.Contains(VaultTag))
            {
				var msg = gitCommit.Comment.Substring(gitCommit.Comment.IndexOf(VaultTag));
				getVaultVersionFromGitLogMessage(msg, ref info);
				return true;
			}
			return false;

        }
    }
}