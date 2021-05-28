using GitLib.Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using VaultClientIntegrationLib;
using VaultClientOperationsLib;
using VaultLib;
using static Vault2Git.Lib.Vault2GitState;

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
            public bool VaultUseSsl;
            public string VaultUser;
            public string VaultPassword;
            public string VaultRepository;

            public string GitDomainName;

            public int GitGcInterval = 200;

            public string MappingFilePath { get; set; }

            public string GitCommitMessageTempFile { get; set; }

            public Vault2GitState ConversionState = new Vault2GitState();

            //callback
            public Func<long, long, int, bool> Progress;

            //flags
            public bool SkipEmptyCommits = false;


            //git commands
            //private const string _gitVersionCmd = "version";
            private const string GitGcCmd = "gc --auto";
            private const string GitFinalizer = "update-server-info";
            private const string GitAddCmd = "add --all .";
            private const string GitStatusCmd = "status --porcelain";
            private const string GitAllCommitInfoCmd = "log --all --branches --parents --pretty=format:\"<c>%n<H>%H</H>%n<P>%P</P>%n<D>%D</D>%n<ae>%ae</ae>%n<N><![CDATA[%B]]></N>%n</c>\"";
            private const string GitMergeCmd = "merge --no-ff --no-commit --no-squash -s ours {0}";

            private const string GitCommitCmd = @"commit --allow-empty --all --date=""{2}"" --author=""{0} <{1}>"" --allow-empty-message -F {3}";
            private const string GitCheckoutCmd = "checkout --quiet --force {0}";
            private const string GitCreateBranchCmd = "checkout -b {0} {1}";
            private const string GitBranchCmd = "branch";
            private const string GitVerifyBranchExistsCmd = "rev-parse --verify --quite --heads {0}";
            private const string GitAddTagCmd = @"tag {0} {1} -a -m ""{2}""";
            private const string GitInitCmd = "init";
            private const string GitInitInitalCommitCmd = @"commit --allow-empty --date=""{0}"" --message=""{1} initial commit @master/0/0""";
            private const string GitFullHashCmd = "rev-parse {0}";
            private const string GitParentHashesCmd = "rev-parse {0}^@";

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
            public const int _progressSpecialVersionInit = 0;

            /// <summary>
            /// version number reported to <see cref="Progress"/> when git gc is complete
            /// </summary>
            public const int _progressSpecialVersionGc = -1;

            /// <summary>
            /// version number reported to <see cref="Progress"/> when finalization finished (e.g. logout, unset wf etc)
            /// </summary>
            public const int _progressSpecialVersionFinalize = -2;

            /// <summary>
            /// version number reported to <see cref="Progress"/> when git tags creation is completed
            /// </summary>
            public const int _progressSpecialVersionTags = -3;

            public string RevisionEndDate { get; set; }
            public string RevisionStartDate { get; set; }

            public string MappingSaveLocation { get; set; }

            /// <summary>
            /// Pulls versions
            /// </summary>
            /// <param name="git2VaultRepoPath">Key=git, Value=vault</param>
            /// <param name="limitCount">Limit the number of transactions to process</param>
            /// <param name="ignoreLabels"></param>
            /// <returns></returns>
            public bool Pull(List<string> git2VaultRepoPath, long limitCount, bool ignoreLabels)
            {
                var completedStepCount = 0;
                var versionProcessingTime = new Stopwatch();
                var overallProcessingTime = new Stopwatch();
                int ticks = 0;


                //create git repo if doesn't exist, otherwise, rebuild vault to git mappings from log
                if (!File.Exists(WorkingFolder + "\\.git\\config"))
                {
                    ticks += GitCreateRepo();
                }

                //get git current branch
                ticks += this.GitCurrentBranch(out string gitCurrentBranch);

                ticks += VaultLogin();
                try
                {
                    //reset ticks
                    ticks = 0;

                    List<string> vaultRepoPaths = git2VaultRepoPath;

                    ConversionState.RevisionStartDate = RevisionStartDate;
                    ConversionState.RevisionEndDate = RevisionEndDate;
                    

                    Console.Write($"Fetching history from vault from {RevisionStartDate} to {RevisionEndDate}... ");

                    ConversionState.LoadState(MappingFilePath, MappingSaveLocation, vaultRepoPaths, GetGitLogs());


                    // get vault version to use as starting point
                    VaultTx currentGitVaultVersion = ConversionState.GetVaultLastTransactionProcessed();

                    // get an ordered list of the Vault transactions that still need to be processed
                    SortedDictionary<long, VaultTx> versionsToProcess = ConversionState.GetVaultTransactionsToProcess(currentGitVaultVersion.TxId);


                    Console.WriteLine($"done! Fetched {versionsToProcess.Count} versions for processing.");

                    //report init
                    if (null != Progress)
                        if (Progress(_progressSpecialVersionInit, 0L, ticks))
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
                            ticks += this.GitCheckoutBranch(version.Branch, out gitCurrentBranch);


                        //check to see if we should prepare a merge commit
                        ticks += GitMergeCommit(version);

                        //get vault version
                        Console.Write($"Starting get version Tx:{version.TxId} - {version.Branch}:{version.Version} from Vault...");

                        ticks += VaultGet(version);

                        Console.WriteLine($" done!");
                        //change all sln files
                        Directory.GetFiles(
                            WorkingFolder,
                            "*.sln",
                            SearchOption.AllDirectories)
                            //remove temp files created by vault
                            .Where(f => !f.Contains("~"))
                            .ToList()
                            .ForEach(f => ticks += RemoveSccFromSln(f));
                        //change all csproj files
                        Directory.GetFiles(
                            WorkingFolder,
                            "*.csproj",
                            SearchOption.AllDirectories)
                            //remove temp files created by vault
                            .Where(f => !f.Contains("~"))
                            .ToList()
                            .ForEach(f => ticks += RemoveSccFromCsProj(f));
                        //change all vdproj files
                        Directory.GetFiles(
                            WorkingFolder,
                            "*.vdproj",
                            SearchOption.AllDirectories)
                            //remove temp files created by vault
                            .Where(f => !f.Contains("~"))
                            .ToList()
                            .ForEach(f => ticks += RemoveSccFromVdProj(f));
                        //commit
                        Console.Write($"Starting git commit...");
                        BuildCommitMessage(version);
                        ticks += GitCommit(version, GitDomainName);
                        Console.WriteLine($" done!");
                        if (null != Progress)
                            if (Progress(version.Version, versionsToProcess.Count, ticks))
                                return true;

                        //call gc
                        if (0 == i + 1 % GitGcInterval)
                        {
                            ticks = GitGc();
                            if (null != Progress)
                                if (Progress(_progressSpecialVersionGc, versionsToProcess.Count, ticks))
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

                    ticks = VaultFinalize(vaultRepoPaths);

                }
                finally
                {
                    //complete
                    //ticks += vaultLogout(); // Drops log-out as it kills the Native allocations
                    //finalize git (update server info for dumb clients)
                    ticks += GitFinalize();
                    Progress?.Invoke(_progressSpecialVersionFinalize, 0L, ticks);
                }
                return false;
            }


            /// <summary>
            /// removes Source control refs from sln files
            /// </summary>
            /// <param name="filePath">path to sln file</param>
            /// <returns></returns>
            private static int RemoveSccFromSln(string filePath)
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
                if (recurse) RemoveSccFromSln(filePath);

                return Environment.TickCount - ticks;
            }

            /// <summary>
            /// removes Source control refs from csProj files
            /// </summary>
            /// <param name="filePath">path to sln file</param>
            /// <returns></returns>
            public static int RemoveSccFromCsProj(string filePath)
            {
                var ticks = Environment.TickCount;
                var doc = new XmlDocument();
                try
                {
                    doc.Load(filePath);
                    while (true)
                    {
                        var nav = doc.CreateNavigator()?.SelectSingleNode("//*[starts-with(name(), 'Scc')]");
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
            private static int RemoveSccFromVdProj(string filePath)
            {
                var ticks = Environment.TickCount;
                var lines = File.ReadAllLines(filePath).ToList();
                File.WriteAllLines(filePath, lines.Where(l => !l.Trim().StartsWith(@"""Scc")).ToArray(), Encoding.UTF8);
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
                    out _,
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

                        //TODO: not sure how to handle this across multiple branch folders. adjusting it just so it compiles and I'll address it later
                        var gitCommitId = string.Empty; //GetMapping(currItem.Version.ToString());

                        if (!(gitCommitId.Length > 0)) continue;

                        var gitLabelName = Regex.Replace(currItem.Label, "[\\W]", "_");
                        ticks += GitAddTag($"{currItem.Version}_{gitLabelName}", gitCommitId, currItem.Comment);
                    }

                    //add ticks for git tags
                    Progress?.Invoke(_progressSpecialVersionTags, 0L, ticks);
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

            private int VaultGet(VaultTx info)
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
                var allowedRequests = new[]
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


            private int Init(VaultTx info, ref string gitCurrentBranch)
            {
                //set vault working folder
                int ticks = SetVaultWorkingFolder(info.Path);

                //verify git branch exists - create if needed
                ticks += GitVerifyBranchExists(info.Branch, out bool branchExists);

                if (branchExists)
                {
                    if (!gitCurrentBranch.Equals(info.Branch, StringComparison.OrdinalIgnoreCase))
                        ticks += this.GitCheckoutBranch(info.Branch, out gitCurrentBranch);
                }
                else
                {
                    ticks += GitCreateBranch(info, out gitCurrentBranch);
                }

                return ticks;
            }


            private int VaultFinalize(List<string> vaultRepoPaths)
            {
                //unset working folder
                return UnSetVaultWorkingFolder(vaultRepoPaths);
            }

            private int GitInitialCommit()
            {

                Dictionary<string, string> env = new Dictionary<string, string>
                {
                    { "GIT_COMMITTER_DATE", DateTime.Parse(RevisionStartDate).ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture) }
                };


                int ticks = RunGitCommand(string.Format(CultureInfo.InvariantCulture, GitInitInitalCommitCmd, DateTime.Parse(RevisionStartDate, CultureInfo.InvariantCulture).ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture), VaultTag), string.Empty, out string[] msgs, env);

                if (msgs.Any())
                {
                    string commitHash = msgs[0].Split(' ')[2];
                    commitHash = GetFullHash(commitHash.Substring(0, commitHash.Length - 1));
                    IGitCommit gitCommit = ConversionState.CreateGitCommit(commitHash, new List<string>());

                    AddMapping(VaultTx.Create(0), gitCommit);
                }

                return ticks;
            }

            private int GitMergeCommit(VaultTx info)
            {
                var ticks = 0;
                if (!string.IsNullOrEmpty(info.MergedFrom))
                {
                    if (ConversionState.GetBranchName(info.MergedFrom) != ConversionState.GetBranchName(info.Branch))
                    {
                        ticks = GitMerge(ConversionState.GetBranchName(info.MergedFrom));
                    }
                }

                return ticks;
            }

            private int GitCommit(VaultTx info, string gitDomainName)
            {
                string gitName;
                string gitEmail;
                string gitAuthor;
                string commitTimeStamp;


                this.GitCurrentBranch(out string gitCurrentBranch);

                string[] msgs;
                var ticks = RunGitCommand(GitAddCmd, string.Empty, out _);
                if (SkipEmptyCommits)
                {
                    //checking status
                    ticks += RunGitCommand(
                        GitStatusCmd,
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

                Dictionary<string, string> env = new Dictionary<string, string>
                {
                    { "GIT_COMMITTER_DATE", commitTimeStamp },
                    { "GIT_COMMITTER_NAME", gitName },
                    { "GIT_COMMITTER_EMAIL", gitEmail }
                };

                ticks += RunGitCommand(
                    string.Format(CultureInfo.InvariantCulture, GitCommitCmd, gitName, gitEmail, $"{info.TimeStamp.GetDateTime():s}", GitCommitMessageTempFile),
                    string.Empty,
                    out msgs,
                    env
                    );

                // Mapping Vault Transaction ID to Git Commit SHA-1 Hash
                if (msgs[0].StartsWith("[" + gitCurrentBranch))
                {
                    string gitCommitId = msgs[0].Split(' ')[1];
                    string commitShortHash = gitCommitId.Substring(0, gitCommitId.Length - 1);
                    string commitHash = GetFullHash(commitShortHash);
                    List<string> parentCommitHashes = GetParentHashes(commitShortHash);
                    IGitCommit gitCommit = ConversionState.CreateGitCommit(commitHash, parentCommitHashes);
                    AddMapping(info, gitCommit);
                }

                return ticks;
            }

            private int GitCreateRepo()
            {
                if (!Directory.Exists(WorkingFolder))
                {
                    Directory.CreateDirectory(WorkingFolder);
                }

                int ticks = GitInit();

                //add .gitignore and .gitattributes
                Tools.CopyFile("Resources\\.gitignore", WorkingFolder + "\\.gitignore");
                Tools.CopyFile("Resources\\.gitattributes", WorkingFolder + "\\.gitattributes");

                ticks += GitAddAll();
                ticks += GitInitialCommit();

                return ticks;
            }

            private int GitCreateBranch(VaultTx info, out string currentBranch)
            {
                /*  get vault items (itempath1/itempath2)
				 *  filter by itempath2 = branchpath
				 *  get source branch from end of itempath1
				 *  get hash of latest commit in the identified branch
				 *  use the hash as the source of the branch
				*/

                TxInfo txDetail = ServerOperations.ProcessCommandTxDetail(info.TxId);

                string sourceBranch;

                var items = txDetail.items.Where(i => (i.ItemPath2 == info.Path || info.Path.StartsWith(i.ItemPath2 + "/"))).ToList();
                
                if (items.Any())
                    sourceBranch = items.First().ItemPath1.Split('/').LastOrDefault();
                else
                    sourceBranch = "master:";

                VaultTx2GitTx gitStartPoint = ConversionState.GetLastBranchMapping(ConversionState.GetBranchName(sourceBranch));

                int ticks = RunGitCommand(string.Format(CultureInfo.InvariantCulture, GitCreateBranchCmd, info.Branch, gitStartPoint.GitCommit.GetHash().ToString()), string.Empty, out _);
                currentBranch = info.Branch;

                return ticks;

            }

            private int GitMerge(string sourceBranch)
            {
                int ticks = RunGitCommand(string.Format(CultureInfo.InvariantCulture, GitMergeCmd, sourceBranch), string.Empty, out string[] msgs, out string[] errorMsgs);

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

            private int GitCurrentBranch(out string currentBranch)
            {
                int ticks = RunGitCommand(GitBranchCmd, string.Empty, out string[] msgs);
                if (msgs.Any())
                    currentBranch = msgs.First(s => s.StartsWith("*")).Substring(1).Trim();
                else
                {
                    currentBranch = string.Empty;
                    throw new InvalidOperationException("The local git repository doesn't contain any branches. Please create at least one.");
                }
                return ticks;
            }

            private int GitVerifyBranchExists(string branchName, out bool exists)
            {
                exists = false;
                int ticks = RunGitCommand(string.Format(CultureInfo.InvariantCulture, GitVerifyBranchExistsCmd, branchName), string.Empty, out string[] msgs);
                if (msgs.Any())
                {
                    if (msgs[0].StartsWith("fatal:"))
                        throw new InvalidOperationException("The local git repository doesn't exist.");
                    exists = true;
                }

                return ticks;
            }

            private int GitInit()
            {
                int ticks = RunGitCommand(GitInitCmd, string.Empty, out string[] msgs);
                if (!msgs[0].StartsWith("Initialized empty Git repository"))
                {
                    throw new InvalidOperationException("The local git repository doesn't exist and can't be created.");
                }
                return ticks;
            }

            private int GitAddAll()
            {
                int ticks = RunGitCommand(GitAddCmd, string.Empty, out _);

                return ticks;
            }

            private int GitCheckoutBranch(string gitBranch, out string currentBranch)
            {
                //checkout branch
                int ticks = 0;
                ticks += RunGitCommand(string.Format(CultureInfo.InvariantCulture, GitCheckoutCmd, gitBranch), string.Empty, out _);


                for (int tries = 0; ; tries++)
                {
                    ticks += RunGitCommand(string.Format(CultureInfo.InvariantCulture, GitCheckoutCmd, gitBranch), string.Empty, out _);
                    //confirm current branch (sometimes checkout failed)

                    ticks += this.GitCurrentBranch(out currentBranch);
                    if (gitBranch.Equals(currentBranch, StringComparison.OrdinalIgnoreCase))
                        break;
                    if (tries > 5)
                        throw new InvalidOperationException($"git cannot switch to branch {gitBranch}");
                }
                return ticks;
            }

            private void BuildCommitMessage(VaultTx info)
            {
                //parse path repo$RepoPath@branch/version/trx
                var r = new StringBuilder(info.Comment);
                Tools.SaveFile(GitCommitMessageTempFile, r.ToString());
            }

            private string[] GetGitLogs()
            {
                RunGitCommand(GitAllCommitInfoCmd, string.Empty, out var msgLines);
                int len = msgLines.Length;
                msgLines[0] = msgLines[0].Insert(0, "<commits>");
                msgLines[len - 1] = msgLines[len - 1].Insert(msgLines[len - 1].Length, "</commits>");

                return msgLines;
            }

            private int GitAddTag(string gitTagName, string gitCommitId, string gitTagComment)
            {
                return RunGitCommand(string.Format(CultureInfo.InvariantCulture, GitAddTagCmd, gitTagName, gitCommitId, gitTagComment),
                    string.Empty,
                    out _);
            }

            private int GitGc()
            {
                return RunGitCommand(GitGcCmd, string.Empty, out _);
            }

            private int GitFinalize()
            {
                return RunGitCommand(GitFinalizer, string.Empty, out _);
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
                //IEnumerable<DictionaryEntry> wf = ServerOperations.GetWorkingFolderAssignments().Cast<DictionaryEntry>();
                var wf = ServerOperations.GetWorkingFolderAssignments().Cast<DictionaryEntry>().ToList();

                foreach (string folder in repoPath)
                {
                    string exPath = wf
                        .Select(e => e.Key.ToString()).FirstOrDefault(e => folder.Equals(e, StringComparison.OrdinalIgnoreCase));

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
                    p.EnableRaisingEvents = true;
                    p.OutputDataReceived += (sender, e) => { if (!string.IsNullOrEmpty(e.Data)) msgs.Add(e.Data); };
                    p.ErrorDataReceived += (sender, e) => { if (!string.IsNullOrEmpty(e.Data)) errorMsgs.Add(e.Data); };
                    
                    p.Start();

                    var ms = new MemoryStream();
                    using (var writer = new StreamWriter(ms, new UTF8Encoding(false)))
                    {
                        writer.Write(stdInput);
                        writer.Flush();
                    }
                    p.StandardInput.Write(Encoding.UTF8.GetString(ms.ToArray()));
                    
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    p.StandardInput.Close();

                    bool result = p.WaitForExit(10000); //there is a bug that can cause stdout to not flush when calling this with an explicit timeout - https://github.com/dotnet/runtime/issues/27128
                    if (result)
                    {
                        p.WaitForExit();
                    }
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
                    if (VaultUseSsl)
                        ServerOperations.client.LoginOptions.URL = string.Format(CultureInfo.InvariantCulture, "https://{0}/VaultService", this.VaultServer);
                    else
                        ServerOperations.client.LoginOptions.URL = string.Format(CultureInfo.InvariantCulture, "http://{0}/VaultService", this.VaultServer);
                    ServerOperations.client.LoginOptions.User = this.VaultUser;
                    ServerOperations.client.LoginOptions.Password = this.VaultPassword;
                    ServerOperations.client.LoginOptions.Repository = this.VaultRepository;
                    ServerOperations.Login();
                    ServerOperations.client.MakeBackups = false;
                    ServerOperations.client.AutoCommit = false;
                    ServerOperations.client.Verbose = true;
                    //_loginDone = true;
                }
                Console.WriteLine($"done!");
                return Environment.TickCount - ticks;
            }

            private void VaultLogout()
            {
                ServerOperations.Logout();
            }

            private void AddMapping(VaultTx info, IGitCommit gitCommit)
            {
                ConversionState.CreateMapping(gitCommit, info);
                ConversionState.Save(MappingSaveLocation);
            }


            private string GetFullHash(string hash)
            {
                GitFullHash(hash, out string fullHash);

                return fullHash;
            }

            private List<string> GetParentHashes(string hash)
            {
                GitParentHashes(hash, out List<string> parentHashes);

                return parentHashes;
            }

            private void GitFullHash(string gitHash, out string fullHash)
            {

                RunGitCommand(string.Format(CultureInfo.InvariantCulture, GitFullHashCmd, gitHash),
                    string.Empty,
                    out string[] msg);

                fullHash = msg[0];
            }

            private void GitParentHashes(string gitHash, out List<string> fullHashes)
            {

                RunGitCommand(string.Format(CultureInfo.InvariantCulture, GitParentHashesCmd, gitHash),
                    string.Empty,
                    out string[] msg);
                fullHashes = new List<string>(msg);
            }

        }
    }
}