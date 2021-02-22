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
using System.Xml;
using System.Xml.Linq;
using static Vault2Git.Lib.Vault2GitState;
using System.Globalization;

namespace Vault2Git.Lib
{
	namespace New
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

			public string MappingFilePath { get; set; }

			public string GitCommitMessageTempFile { get; set; }

			public Vault2GitState ConversionState = new Vault2GitState();

			//callback
			public Func<long, long, int, bool> Progress;

			//flags
			public bool SkipEmptyCommits = false;

			private bool _loginDone = false;

			//git commands
			//private const string _gitVersionCmd = "version";
			private const string _gitGCCmd = "gc --auto";
			private const string _gitFinalizer = "update-server-info";
			private const string _gitAddCmd = "add --all .";
			private const string _gitStatusCmd = "status --porcelain";
			private const string _gitLastCommitInfoCmd = "log -1 --all --branches";
			private const string _gitAllCommitInfoCmd = "log --all --branches --parents --pretty=format:\"<c>%n<H>%H</H>%n<P>%P</P>%n<D>%D</D>%n<ae>%ae</ae>%n<N><![CDATA[%B]]></N>%n</c>\"";
			private const string _gitMergeCmd = "merge --no-ff --no-commit --no-squash -s ours {0}";
			private const string _gitReplaceGraftCmd = "replace -f --graft {0} {1}";
			private const string _gitReplacementCmd = "replace --format=medium --list {0}";

			private const string _gitCommitCmd = @"commit --allow-empty --all --date=""{2}"" --author=""{0} <{1}>"" -F {3}";
			private const string _gitCheckoutCmd = "checkout --quiet --force {0}";
			private const string _gitCreateBranch = "checkout -b {0} {1}";
			private const string _gitBranchCmd = "branch";
			private const string _gitVerifyBranchExistsCmd = "rev-parse --verify --quite --heads {0}";
			private const string _gitAddTagCmd = @"tag {0} {1} -a -m ""{2}""";
			private const string _gitInitCmd = "init";
			private const string _gitInitInitalCommitCmd = @"commit --allow-empty --date=""{0}"" --message=""{1} initial commit @master/0/0""";
			private const string _gitFullHashCmd = "rev-parse {0}";
			private const string _gitParentHashesCmd = "rev-parse {0}^@";

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
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "git")]
            public bool Pull(List<string> git2vaultRepoPath, long limitCount, bool ignoreLabels)
			{
				var completedStepCount = 0;
				var versionProcessingTime = new Stopwatch();
				var overallProcessingTime = new Stopwatch();
				int ticks = 0;

				//load git authors and renamed branches from configuration
				var (branches, authors) = Tools.ParseMapFile(MappingFilePath);
				ConversionState.AddAuthors(authors);
				ConversionState.AddRenamedBranches(branches);

				//create git repo if doesn't exist, otherwise, rebuild vault to git mappings from log
				if (!File.Exists(WorkingFolder + "\\.git\\config"))
				{
					ticks += gitCreateRepo();
				}
				else
				{
					RebuildMapping();
				}

				//get git current branch
				ticks += this.gitCurrentBranch(out string gitCurrentBranch);


				ticks += VaultLogin();
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
					SortedDictionary<long, VaultTx> versionsToProcess = ConversionState.GetVaultTransactionsToProcess(currentGitVaultVersion.TxId);


					Console.WriteLine($"done! Fetched {versionsToProcess.Count} versions for processing.");

					//report init
					if (null != Progress)
						if (Progress(ProgressSpecialVersionInit, 0L, ticks))
							return true;

					overallProcessingTime.Restart();

					int i = 0;
					foreach (var kp in versionsToProcess)
					//for (int i = 0; i < versionsToProcess.Count; i++)
					{

						versionProcessingTime.Restart();
						ticks = 0;
						VaultTx version = kp.Value;

						ticks = Init(version, ref gitCurrentBranch);

						//check to see if we are in the correct branch
						if (!gitCurrentBranch.Equals(version.Branch, StringComparison.OrdinalIgnoreCase))
							ticks += this.gitCheckoutBranch(version.Branch, out gitCurrentBranch);


						//check to see if we should prepare a merge commit
						ticks += gitMergeCommit(version);

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
							ticks = GitGC();
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
						i++;

					}

					//BuildGrafts();

					ticks = vaultFinalize(vaultRepoPaths);

				}
				finally
				{
					//complete
					//ticks += vaultLogout(); // Drops log-out as it kills the Native allocations
					//finalize git (update server info for dumb clients)
					ticks += GitFinalize();
                    Progress?.Invoke(ProgressSpecialVersionFinalize, 0L, ticks);
                }
				return false;
			}

   
            public bool BuildGrafts()
			{
				//IDictionary<string, IDictionary<string, string>> map = getMappingWithTxIdFromLog();
				List<VaultTx2GitTx> map = getMappingWithTxIdFromLog();

				var origins = map.Where(l => l.GitCommit.Comment.TrimStart().StartsWith("Merge Branches : Origin"));
				map.ForEach(o => o.VaultTx.MergedFrom = (o.GitCommit.Comment.TrimStart().StartsWith("Merge Branches : Origin=$")) ? o.GitCommit.Comment.Trim().Split(Environment.NewLine.ToCharArray())[0].Split('/')?.LastOrDefault() : string.Empty);

				IDictionary<string, long> branches = new Dictionary<string, long>();
				SortedDictionary<long, VaultTx2GitTx> mapping = ConversionState.GetMapping();

				string branch;
				long version;

				foreach (KeyValuePair<long, VaultTx2GitTx> t in mapping)
				{
					branch = ConversionState.GetBranchName(t.Value.Branch); // Tools.GetBranchMapping(t.Value.Branch);
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

					var origin = origins.Where(o => o.VaultTx.Branch == t.Value.Branch && o.VaultTx.TxId == t.Key);
					foreach (VaultTx2GitTx o in origin)
					{
						var sourceBranch = ConversionState.GetBranchName(o.VaultTx.MergedFrom);
						if (branches.ContainsKey(sourceBranch))
						{

							var newParentCommitHash = mapping[branches[sourceBranch]].GitCommit.GetHash();
							if (!o.GitCommit.GetParentHashes().Contains(newParentCommitHash))
							{
								o.GitCommit.AddParent(newParentCommitHash);

								gitReplaceGraft(o.GitCommit.GetHash(), o.GitCommit.GetParentHashes());

								gitReplacmentCommitHash(o.GitCommit.GetHash(), out GitCommitHash ReplacementCommitHash);


								if (o.GitCommit.GetHash() != ReplacementCommitHash)
								{
									mapping[branches[branch]].GitCommit.GetHash().Replace(ReplacementCommitHash);
									Console.WriteLine(string.Format("Replaced commit {0} with commit {1} to add new parent commit {2}",
										o.GitCommit.GetHash().ToString(false),
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
						branchName = ConversionState.GetBranchName(f.Name);
					else
						branchName = ConversionState.GetBranchName(branch);

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
						if (i.Comment != null)
						{
							info.MergedFrom = (i.Comment.TrimStart().StartsWith("Merge Branches : Origin=$")) ? i.Comment.Trim().Split(Environment.NewLine.ToCharArray())[0].Split('/')?.LastOrDefault() : string.Empty;
						}
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
				VaultLogin();

				// Search for all labels recursively
				string repositoryFolderPath = "$";

				long objId = RepositoryUtil.FindVaultTreeObjectAtReposOrLocalPath(repositoryFolderPath).ID;


				ServerOperations.client.ClientInstance.BeginLabelQuery(repositoryFolderPath, objId, false, false, true, true, 0,
					out long rowsRetMain,
					out long rowsRetRecur,
					out string qryToken);

	
				ServerOperations.client.ClientInstance.GetLabelQueryItems_Recursive(qryToken,
					0,
					(int)rowsRetRecur,
					out VaultLabelItemX[] labelItems);

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
						ticks += GitAddTag($"{currItem.Version}_{gitLabelName}", gitCommitId, currItem.Comment);
					}

					//add ticks for git tags
					Progress?.Invoke(ProgressSpecialVersionTags, 0L, ticks);
				}
				finally
				{
					//complete
					ServerOperations.client.ClientInstance.EndLabelQuery(qryToken);
					VaultLogout();
					GitFinalize();
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
				var ticks = GitLog(out msgs);
				//get vault version
				GetVaultVersionFromGitLogMessage(msgs.LastOrDefault(), ref currentVersion);
				return ticks;
			}

			private int Init(VaultTx info, ref string gitCurrentBranch)
			{
				//set vault working folder
				int ticks = SetVaultWorkingFolder(info.Path);

				//verify git branch exists - create if needed
				ticks += gitVerifyBranchExists(info.Branch, out bool branchExists);

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

			private int gitReplacmentCommitHash(GitCommitHash CommitHash, out GitCommitHash ReplacementCommitHash)
			{
				ReplacementCommitHash = CommitHash;

				string ReplacementHash;
				int ticks = gitReplacement(CommitHash, out ReplacementHash);

				if (ReplacementHash != string.Empty)
					ReplacementCommitHash = new GitCommitHash(ReplacementHash);

				return ticks;
			}

			private int vaultFinalize(List<string> vaultRepoPaths)
			{
				//unset working folder
				return UnSetVaultWorkingFolder(vaultRepoPaths);
			}

			private int gitInitialCommit()
			{

                Dictionary<string, string> env = new Dictionary<string, string>
                {
                    { "GIT_COMMITTER_DATE", DateTime.Parse(RevisionStartDate).ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture) }
                };


                int ticks = RunGitCommand(string.Format(CultureInfo.InvariantCulture, _gitInitInitalCommitCmd, DateTime.Parse(RevisionStartDate).ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture), VaultTag), string.Empty, out string[] msgs, env);

				if (msgs.Any())
				{
					string commitHash = msgs[0].Split(' ')[2];
					commitHash = getFullHash(commitHash.Substring(0, commitHash.Length - 1));
                    GitCommit gitCommit = ConversionState.CreateGitCommit(commitHash, new List<string>());

					AddMapping(VaultTx.Create(0), gitCommit);
				}

				return ticks;
			}

			private int gitMergeCommit(VaultTx info)
			{
				var ticks = 0;
				if (!string.IsNullOrEmpty(info.MergedFrom))
				{
					if (ConversionState.GetBranchName(info.MergedFrom) != ConversionState.GetBranchName(info.Branch))
					{
						ticks = gitMerge(ConversionState.GetBranchName(info.MergedFrom));
					}
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
				var ticks = RunGitCommand(_gitAddCmd, string.Empty, out _);
				if (SkipEmptyCommits)
				{
					//checking status
					ticks += RunGitCommand(
						_gitStatusCmd,
						string.Empty,
						out msgs
						);
					if (!msgs.Any())
						return ticks;
				}


				gitAuthor = ConversionState.GetGitAuthor(info.Login);
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

				commitTimeStamp = info.TimeStamp.GetDateTime().ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

				Dictionary<string, string> env = new Dictionary<string, string>();
				env.Add("GIT_COMMITTER_DATE", commitTimeStamp);
				env.Add("GIT_COMMITTER_NAME", gitName);
				env.Add("GIT_COMMITTER_EMAIL", gitEmail);

				ticks += RunGitCommand(
					string.Format(CultureInfo.InvariantCulture, _gitCommitCmd, gitName, gitEmail, string.Format("{0:s}", commitTimeStamp), GitCommitMessageTempFile),
					string.Empty,
					out msgs,
					env
					);

				// Mapping Vault Transaction ID to Git Commit SHA-1 Hash
				if (msgs[0].StartsWith("[" + gitCurrentBranch))
				{
					string gitCommitId = msgs[0].Split(' ')[1];
					string commitShortHash = gitCommitId.Substring(0, gitCommitId.Length - 1);
					string commitHash = getFullHash(commitShortHash);
					List<string> parentCommitHashes = getParentHashes(commitShortHash);
					GitCommit gitCommit = ConversionState.CreateGitCommit(commitHash, parentCommitHashes);
					AddMapping(info, gitCommit);
				}

				return ticks;
			}

			private int gitCreateRepo()
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

				VaultTx2GitTx gitStartPoint = ConversionState.GetLastBranchMapping(ConversionState.GetBranchName(sourceBranch));

				string[] msgs;
				int ticks = RunGitCommand(string.Format(CultureInfo.InvariantCulture, _gitCreateBranch, info.Branch, gitStartPoint.GitCommit.GetHash().ToString()), string.Empty, out msgs);
				currentBranch = info.Branch;

				return ticks;

			}

			private int gitMerge(string sourceBranch)
			{
                int ticks = RunGitCommand(string.Format(CultureInfo.InvariantCulture, _gitMergeCmd, sourceBranch), string.Empty, out string[] msgs, out string[] errorMsgs);

                /* `git merge` command outputs success on Standard Error Output
				 * 
				 * The "Already up to date" message occurs if git doesn't see any changes. In this case, the merge 
				 * isn't created, but we still want to treat it as a success and to proceed. The source commit might
				 * be empty, or it might contain a change.  If it has a change, it was not the result of a merge.  
				 * It was probably a pre-commit change after a merge failed to create make the desired change.  This 
				 * would happen if the source repository already had the change marked as handled.
				*/
                if (!((errorMsgs.Any() && errorMsgs[0].StartsWith("Automatic merge went well")) || (msgs.Any() && msgs[0].StartsWith("Already up to date"))))
				{
					throw new InvalidOperationException($"Merge failed with message: {msgs[0]}");
				}
				return ticks;
			}

			private int gitCurrentBranch(out string currentBranch)
			{
                int ticks = RunGitCommand(_gitBranchCmd, string.Empty, out string[] msgs);
                if (msgs.Any())
					currentBranch = msgs.Where(s => s.StartsWith("*")).First().Substring(1).Trim();
				else
				{
					currentBranch = string.Empty;
					throw new InvalidOperationException("The local git repository doesn't contain any branches. Please create at least one.");
				}
				return ticks;
			}

			private int gitVerifyBranchExists(string branchName, out bool exists)
			{
                exists = false;
                int ticks = RunGitCommand(string.Format(CultureInfo.InvariantCulture, _gitVerifyBranchExistsCmd, branchName), string.Empty, out string[] msgs);
				if (msgs.Any())
				{
					if (msgs[0].StartsWith("fatal:"))
						throw new InvalidOperationException("The local git repository doesn't exist.");
					exists = true;
				}

				return ticks;
			}

			private int gitInit()
			{
                int ticks = RunGitCommand(_gitInitCmd, string.Empty, out string[] msgs);
                if (!msgs[0].StartsWith("Initialized empty Git repository"))
				{
					throw new InvalidOperationException("The local git repository doesn't exist and can't be created.");
				}
				return ticks;
			}

			private int gitAddAll()
			{
				int ticks = RunGitCommand(_gitAddCmd, string.Empty, out _);

				return ticks;
			}

			private int gitReplaceGraft(GitCommitHash gitCommitHash, List<GitCommitHash> gitParentCommitHash)
			{
				int ticks = 0;

                ticks += RunGitCommand(string.Format(CultureInfo.InvariantCulture, _gitReplaceGraftCmd, gitCommitHash.ToString(), string.Join(" ", gitParentCommitHash.Select(h => h.ToString()))), string.Empty, out string[] msgs);

                return ticks;
			}

			private int gitReplacement(GitCommitHash gitCommitHash, out string gitReplacementHash)
			{
				int ticks = 0;

                ticks += RunGitCommand(string.Format(CultureInfo.InvariantCulture, _gitReplacementCmd, gitCommitHash.ToString()), string.Empty, out string[] msgs);

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
				ticks += RunGitCommand(string.Format(CultureInfo.InvariantCulture, _gitCheckoutCmd, gitBranch), string.Empty, out _);


				for (int tries = 0; ; tries++)
				{
					ticks += RunGitCommand(string.Format(CultureInfo.InvariantCulture, _gitCheckoutCmd, gitBranch), string.Empty, out _);
					//confirm current branch (sometimes checkout failed)

					ticks += this.gitCurrentBranch(out currentBranch);
					if (gitBranch.Equals(currentBranch, StringComparison.OrdinalIgnoreCase))
						break;
					if (tries > 5)
						throw new InvalidOperationException($"git cannot switch to branch {gitBranch}");
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

			private bool GetVaultVersionFromGitLogMessage(string stringToParse, ref VaultTx info)
			{
				//search for version tag
				var versionStrings = stringToParse.Split(new string[] { VaultTag }, StringSplitOptions.None);
				if (null == versionStrings || versionStrings.Count() != 2)
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

			private int GitLog(out string[] msg)
			{
				return RunGitCommand(_gitLastCommitInfoCmd, string.Empty, out msg);
			}

			private int GetGitLogs(out string[] msgLines)
			{
				int time = RunGitCommand(_gitAllCommitInfoCmd, string.Empty, out msgLines);
				int len = msgLines.Length;
				msgLines[0] = msgLines[0].Insert(0, "<commits>");
				msgLines[len - 1] = msgLines[len - 1].Insert(msgLines[len - 1].Length, "</commits>");

				return time;
			}

			private int GitAddTag(string gitTagName, string gitCommitId, string gitTagComment)
			{
				return RunGitCommand(string.Format(CultureInfo.InvariantCulture, _gitAddTagCmd, gitTagName, gitCommitId, gitTagComment),
					string.Empty,
					out _);
			}

			private int GitGC()
			{
				return RunGitCommand(_gitGCCmd, string.Empty, out _);
			}

			private int GitFinalize()
			{
				return RunGitCommand(_gitFinalizer, string.Empty, out _);
			}

			private int SetVaultWorkingFolder(string repoPath)
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

			private int UnSetVaultWorkingFolder(List<string> repoPath)
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

			private int RunGitCommand(string cmd, string stdInput, out string[] stdOutput)
			{
				return RunGitCommand(cmd, stdInput, out stdOutput, out _, null);
			}

			private int RunGitCommand(string cmd, string stdInput, out string[] stdOutput, out string[] stdError)
			{
				return RunGitCommand(cmd, stdInput, out stdOutput, out stdError, null);
			}

			private int RunGitCommand(string cmd, string stdInput, out string[] stdOutput, IDictionary<string, string> env)
            {
				return RunGitCommand(cmd, stdInput, out stdOutput, out _, env);
			}

			private int RunGitCommand(string cmd, string stdInput, out string[] stdOutput, out string[] stdError, IDictionary<string, string> env)
			{
				var ticks = Environment.TickCount;

				var pi = new ProcessStartInfo(GitCmd, cmd)
				{
					WorkingDirectory = WorkingFolder,
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardInput = true,
					StandardOutputEncoding = Encoding.UTF8,
					RedirectStandardError = true,
					StandardErrorEncoding = Encoding.UTF8
				};

				var msgs = new List<string>();
				var errorMsgs = new List<string>();

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

					var ms = new MemoryStream();
					using (var writer = new StreamWriter(ms, new UTF8Encoding(false)))
					{
						writer.Write(stdInput);
						writer.Flush();
					}
					p.StandardInput.Write(Encoding.UTF8.GetString(ms.ToArray()));
					p.StandardInput.Close();
					p.OutputDataReceived += (sender, e) => { if(!string.IsNullOrEmpty(e.Data)) msgs.Add(e.Data); };
					p.ErrorDataReceived += (sender, e) => { if (!string.IsNullOrEmpty(e.Data)) errorMsgs.Add(e.Data); };
					p.BeginOutputReadLine();
					p.BeginErrorReadLine();
					p.WaitForExit(10000);
				}

				stdOutput = msgs.ToArray();
				stdError = errorMsgs.ToArray();
				return Environment.TickCount - ticks;
			}

			private int VaultLogin()
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

			private int VaultLogout()
			{
				var ticks = Environment.TickCount;
				ServerOperations.Logout();
				return Environment.TickCount - ticks;
			}

			private void AddMapping(VaultTx info, GitCommit gitCommit)
			{

				ConversionState.CreateMapping(gitCommit, info);
				ConversionState.Save(MappingSaveLocation);
			}

			private bool RebuildMapping()
			{
				ConversionState.BuildVaultTx2GitCommitFromList(getMappingWithTxIdFromLog());
				ConversionState.Save(MappingSaveLocation);
				return true;
			}

			private List<VaultTx2GitTx> getMappingWithTxIdFromLog()
			{
				string[] msgs;

				GetGitLogs(out msgs);

				List<VaultTx2GitTx> mapping = GitLogFromXml(msgs);

				return mapping;
			}

			private string getFullHash(string hash)
			{
				string FullHash;
				gitFullHash(hash, out FullHash);

				return FullHash;
			}

			private List<string> getParentHashes(string hash)
			{
				List<string> ParentHashes;
				gitParentHashes(hash, out ParentHashes);

				return ParentHashes;
			}

			private int gitFullHash(string gitHash, out string FullHash)
			{
				string[] msg;

				int ticks = RunGitCommand(string.Format(CultureInfo.InvariantCulture, _gitFullHashCmd, gitHash),
					string.Empty,
					out msg);

				FullHash = msg[0];

				return ticks;
			}

			private int	gitParentHashes(string gitHash, out List<string> fullHashes)
            {
				string[] msg;

				int ticks = RunGitCommand(string.Format(CultureInfo.InvariantCulture, _gitParentHashesCmd, gitHash),
					string.Empty,
					out msg);
				fullHashes = new List<string>(msg);

				return ticks;
			}

			private List<VaultTx2GitTx> GitLogFromXml(string[] msg)
			{
				var reader = new StringReader(string.Join(Environment.NewLine, msg));
				var mappings = new List<VaultTx2GitTx>();
				var xDoc = XDocument.Load(reader).Root;

				var elements = xDoc.Descendants("c");

				foreach (XElement e in elements)
				{

					if (e.Descendants("D").FirstOrDefault().Value.Split(',').Any(d => d.Trim() == "replaced"))
						continue;

					string commitHash = e.Descendants("H").FirstOrDefault().Value;
					var comment = e.Descendants("N").FirstOrDefault().Value;
					List<string> parentCommitHashes = e.Descendants("P").FirstOrDefault().Value.Split(' ').Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
					var commit = ConversionState.CreateGitCommit(commitHash, parentCommitHashes);
					commit.Comment = comment;

					if (comment.Contains(VaultTag))
					{
						VaultTx info = new VaultTx();
						GetVaultVersionFromGitLogMessage(comment, ref info);
						var x = new VaultTx2GitTx(commit, info);
						mappings.Add(x);
					}
				}
				return mappings;
			}
		}
	}
}