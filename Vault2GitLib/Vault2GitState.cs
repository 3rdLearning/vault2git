using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using GitLib;
using GitLib.Interfaces;
using VaultClientIntegrationLib;
using VaultClientOperationsLib;
using VaultLib;

namespace Vault2Git.Lib
{
    public partial class Vault2GitState
    {

        private const string DEFAULT_BRANCH = "master";
        /// <summary>
        /// An object to manage references to all commits
        /// </summary>
        private IGitCommitCollection _gitCommits;

        /// <summary>
        /// An object to manage all transactions
        /// </summary>
        private VaultTxCollection _vaultTxs;

        /// <summary>
        /// An object to manage all transactions
        /// </summary>
        private VaultTx2GitTxCollection _vaultTx2GitTxs;


        private Dictionary<string, VaultTx2GitTx> _branchMapping;

        private Dictionary<string, string> _renamedbranches;
        private Dictionary<string, string> _authors;

        public string RevisionEndDate { get; set; }
        public string RevisionStartDate { get; set; }

        public Vault2GitState()
        {
            _gitCommits = new GitCommitCollection();
            _vaultTxs = new VaultTxCollection();
            _vaultTx2GitTxs = new VaultTx2GitTxCollection();//_gitCommits, _vaultTxs);
            _branchMapping = new Dictionary<string, VaultTx2GitTx>();
            _renamedbranches = new Dictionary<string, string>();
            _authors = new Dictionary<string, string>();
        }

        internal IGitCommit CreateGitCommit(string commitHash, List<string> parentCommitHashes)
        {
            IGitCommitHash gitCommitHash = new GitCommitHash(commitHash);
            List<IGitCommitHash> parentGitCommitHashes = parentCommitHashes.Where(l => !string.IsNullOrEmpty(l)).Select(l => new GitCommitHash(l)).ToList<IGitCommitHash>();

            return CreateGitCommit(gitCommitHash, parentGitCommitHashes);
        }

        private IGitCommit CreateGitCommit(IGitCommitHash gitCommitHash, List<IGitCommitHash> parentCommitHashes)
        {
            IGitCommit gitGommit = _gitCommits[gitCommitHash.ToString()] ?? _gitCommits.AddCommit(gitCommitHash.ToString());

            foreach (IGitCommitHash parentCommitHash in parentCommitHashes)
            {
                IGitCommit parentGitCommit = _gitCommits[parentCommitHash.ToString()] ?? _gitCommits.AddCommit(parentCommitHash.ToString());
                if (!gitGommit.GetParentHashes().Contains(parentGitCommit.GetHash()))
                {
                    gitGommit.AddParent(parentGitCommit.GetHash());
                }
            }
            return gitGommit;
        }

        internal VaultTx2GitTx CreateMapping(IGitCommit gitCommit, VaultTx vaultTx)
        {
            VaultTx2GitTx entry = _vaultTx2GitTxs.Add(gitCommit, vaultTx);

            if (_branchMapping.ContainsKey(entry.Branch))
            {
                _branchMapping[entry.Branch] = entry;
            }
            else
            {
                _branchMapping.Add(vaultTx.Branch, entry);
            }
            return entry;
        }

        public SortedDictionary<long, VaultTx2GitTx> GetMapping()
        {
            return _vaultTx2GitTxs.GetMappingDictionary();
        }

        internal VaultTx CreateVaultTransaction(long txId, string branchName = DEFAULT_BRANCH)
        {
            return _vaultTxs[txId] ?? _vaultTxs.Add(txId, branchName);
        }

        internal SortedDictionary<long, VaultTx> GetVaultTransactionsToProcess(long latestTxId)
        {
            return _vaultTxs.GetVaultTxAfter(latestTxId);
        }

        //internal VaultTx2GitTx GetMapping(VaultTx info)
        //{
        //    return _vaultTx2GitTxs.GetMapping(info);
        //}

        public VaultTx2GitTx GetLastBranchMapping(string branchName)
        {
            string updatedBranchName = GetBranchName(branchName);

            if (_branchMapping.TryGetValue(updatedBranchName, out VaultTx2GitTx entry))
            {
                return entry;
            }
            else
            {
                Console.WriteLine($"Missing an entry for branch {updatedBranchName}, Defaulting to {DEFAULT_BRANCH}");
                return _branchMapping[DEFAULT_BRANCH];
            }
        }

        public void Save(string fileName)
        {
            using (var ms = new MemoryStream())
            {
                using (var writer = new StreamWriter(ms))
                {
                    var xmlSettings = new XmlWriterSettings
                    {
                        Indent = true
                    };

                    using (var xmlWriter = XmlWriter.Create(writer, xmlSettings))
                    {
                        _vaultTx2GitTxs.Mapping2Xml(xmlWriter);
                    }
                    writer.Flush();
                    File.WriteAllBytes(fileName, ms.ToArray());
                }
            }
        }

        public int LoadState(string mappingFilePath, string mappingSaveLocation, List<string> vaultRepoPaths, string[] gitLogXml)
        {
            int ticks = 0;
            //load git authors and renamed branches from configuration
            var (branches, authors) = Tools.ParseMapFile(mappingFilePath);
            AddAuthors(authors);
            AddRenamedBranches(branches);

            // load vault commits from Vault Repo
            foreach (string rp in vaultRepoPaths)
            {
               ticks += VaultPopulateInfo(rp);
            }

            // load git commits from git repo
            GitPopulateInfo(gitLogXml);

            
            // Restore data from xml file
            Dictionary<long, string> entries = Tools.ParseVault2GitFile(mappingSaveLocation);

            foreach (var entry in entries)
            {
                CreateMapping(_gitCommits.AddCommit(entry.Value), _vaultTxs.Add(entry.Key));
            }
            
            //Save(mappingSaveLocation);
            return ticks;
        }


        public static XElement Dictionary2Xml<TKey, TValue>(IDictionary<TKey, TValue> input)
        {
            return new XElement("dictionary", new XAttribute("keyType", typeof(TKey).FullName ?? string.Empty),
                new XAttribute("valueType", typeof(TValue).FullName ?? string.Empty),
                input.Select(kp => new XElement("entry", new XAttribute("key", kp.Key), kp.Value)));
        }

        public void BuildVaultTx2GitCommitFromList(List<VaultTx2GitTx> mapping)
        {
            _vaultTx2GitTxs = new VaultTx2GitTxCollection();//_gitCommits, _vaultTxs);

            foreach (VaultTx2GitTx entry in mapping.OrderBy(l => l.TxId))
            {
                CreateMapping(entry.GitCommit, entry.VaultTx);
            }
        }

        internal IGitCommitHash ReplaceCommitHash(IGitCommitHash sourceGitCommitHash, IGitCommitHash replacementCommitHash)
        {
            return _gitCommits.ReplaceCommitHash(sourceGitCommitHash, replacementCommitHash).GetHash();
        }

        public void AddAuthors(Dictionary<string, string> authors)
        {
            _authors = _authors.Union(authors).ToDictionary(d => d.Key, d => d.Value);
        }

        public void AddRenamedBranches(Dictionary<string, string> branches)
        {
            _renamedbranches = _renamedbranches.Union(branches).ToDictionary(d => d.Key, d => d.Value);
        }

        public string GetBranchName(string branchName)
        {
            branchName = branchName.ToLower().Replace(" ", string.Empty);
            return _renamedbranches.TryGetValue(branchName, out string renamedBranchName) ? renamedBranchName : branchName;
        }

        public string GetGitAuthor(string vaultUser)
        {
            vaultUser = vaultUser.ToLower();
            return _authors.ContainsKey(vaultUser) ? _authors[vaultUser] : null;
        }

        private int VaultPopulateInfo(string repoPath)
        {
            var ticks = Environment.TickCount;

            string[] pathToBranch = repoPath.Split('~');
            string path = pathToBranch[0];
            string branch = pathToBranch[1];

            VaultTxHistoryItem[] historyItems;
            VaultClientFolderColl repoFolders;

            if (repoPath.EndsWith("*"))
            {
                repoFolders = ServerOperations.ProcessCommandListFolder(path, false).Folders;

            }
            else
            {
                repoFolders = new VaultClientFolderColl
                {
                    ServerOperations.ProcessCommandListFolder(path, false)
                };
            }

            foreach (VaultClientFolder f in repoFolders)
            {

                string branchName;

                if (branch == "*")
                    branchName = GetBranchName(f.Name);
                else
                    branchName = GetBranchName(branch);

                historyItems = ServerOperations.ProcessCommandVersionHistory(f.FullPath,
                    0,
                    VaultDateTime.Parse(RevisionStartDate),
                    VaultDateTime.Parse(RevisionEndDate),
                    0);

                foreach (VaultTxHistoryItem i in historyItems)
                {
                    VaultTx info = CreateVaultTransaction(i.TxID, branchName);

                    info.Path = f.FullPath;
                    info.Version = i.Version;
                    info.Comment = i.Comment;
                    info.Login = i.UserLogin;
                    info.TimeStamp = i.TxDate;
                    if (i.Comment != null)
                    {
                        info.MergedFrom = (i.Comment.TrimStart().StartsWith("Merge Branches : Origin=$")) ? i.Comment.Trim().Split(Environment.NewLine.ToCharArray())[0].Split('/').LastOrDefault() : string.Empty;
                    }
                }
            }

            return Environment.TickCount - ticks;
        }

        private void GitPopulateInfo(string[] gitLogXml)
        {
            var reader = new StringReader(string.Join(Environment.NewLine, gitLogXml));
            
            var xDoc = XDocument.Load(reader).Root;

            var elements = xDoc?.Descendants("c");

            if (elements != null)
            {
                foreach (XElement e in elements)
                {
                    {
                        if ((e.Descendants(XName.Get("D")).FirstOrDefault()?.Value ?? "").Split(',').Any(d => d.Trim() == "replaced"))
                            continue;

                        string commitHash = e.Descendants("H").FirstOrDefault()?.Value;
                        var comment = e.Descendants("N").FirstOrDefault()?.Value;
                        List<string> parentCommitHashes = e.Descendants("P").FirstOrDefault()?.Value.Split(' ')
                            .Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
                        var commit = CreateGitCommit(commitHash, parentCommitHashes);
                        commit.Comment = comment;
                    }
                }
            }
        }

        public VaultTx GetVaultLastTransactionProcessed()
        {
            return _vaultTx2GitTxs.GetLastVaultTxProcessed();
        }
    }
}