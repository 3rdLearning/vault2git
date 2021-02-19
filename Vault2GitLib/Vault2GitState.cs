using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Vault2Git.Lib
{
    public partial class Vault2GitState
    {

        private const string DEFAULT_BRANCH = "master";
        /// <summary>
        /// An object to manage references to all commits
        /// </summary>
        private GitCommitCollection _gitCommits;

        /// <summary>
        /// An object to manage all transactions
        /// </summary>
        private VaultTxCollection _vaultTxs;

        /// <summary>
        /// An object to manage all transactions
        /// </summary>
        private VaultTx2GitTxCollection _vaultTx2GitTxs;

        /// <summary>
        /// An object containing the current mapping from vault transaction to git commit hash
        /// </summary>
        //private List<VaultTx2GitTx> _vaultTx2GitTx;
        private Dictionary<string, VaultTx2GitTx> _branchMapping;

        private Dictionary<string, string> _renamedbranches;
        private Dictionary<string, string> _authors;

        public Vault2GitState()
        {
            _gitCommits = new GitCommitCollection();
            _vaultTxs = new VaultTxCollection();
            _vaultTx2GitTxs = new VaultTx2GitTxCollection(_gitCommits, _vaultTxs);
            _branchMapping = new Dictionary<string, VaultTx2GitTx>();
            _renamedbranches = new Dictionary<string, string>();
            _authors = new Dictionary<string, string>();
        }

        internal GitCommit CreateGitCommit(string commitHash)
        {
            GitCommit gitGommit = _gitCommits[commitHash] ?? _gitCommits.AddCommit(commitHash);

            return gitGommit;
        }
        internal GitCommit CreateGitCommit(string commitHash, List<string> parentCommitHashes)
        {
            GitCommitHash gitCommitHash = new GitCommitHash(commitHash);
            List<GitCommitHash> parentGitCommitHashes = parentCommitHashes.Where(l => !string.IsNullOrEmpty(l)).Select(l => new GitCommitHash(l)).ToList();

            return CreateGitCommit(gitCommitHash, parentGitCommitHashes);
        }

        private GitCommit CreateGitCommit(GitCommitHash gitCommitHash, List<GitCommitHash> parentCommitHashes)
        {
            GitCommit gitGommit = _gitCommits[gitCommitHash.ToString()] ?? _gitCommits.AddCommit(gitCommitHash.ToString());

            foreach (GitCommitHash parentCommitHash in parentCommitHashes)
            {
                GitCommit parentGitCommit = _gitCommits[parentCommitHash.ToString()] ?? _gitCommits.AddCommit(parentCommitHash.ToString());
                if (!gitGommit.GetParentHashes().Contains(parentGitCommit.GetHash()))
                {
                    gitGommit.AddParent(parentGitCommit.GetHash());
                }
            }
            return gitGommit;
        }

        internal VaultTx2GitTx CreateMapping(GitCommit gitCommit, VaultTx vaultTx)
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
            return _vaultTxs.getVaultTxAfter(latestTxId);
        }

        internal VaultTx2GitTx GetMapping(VaultTx info)
        {
            return _vaultTx2GitTxs.GetMapping(info);
        }

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

        public void SaveMapping(string fileName)
        {
            Dictionary2Xml(_vaultTx2GitTxs.GetMappingDictionary()).Save(fileName);
        }

        public static XElement Dictionary2Xml<TKey, TValue>(IDictionary<TKey, TValue> input)
        {
            return new XElement("dictionary", new XAttribute("keyType", typeof(TKey).FullName),
                new XAttribute("valueType", typeof(TValue).FullName),
                input.Select(kp => new XElement("entry", new XAttribute("key", kp.Key), kp.Value)));
        }

        //internal bool BuildVaultTx2GitCommitFromXML(string saveFileName)
        //{
        //    if (!File.Exists(saveFileName))
        //        throw new FileNotFoundException("File not found");
        //    try
        //    {
        //        _vaultTx2GitTxs = new VaultTx2GitTxCollection(_gitCommits, _vaultTxs);

        //        //var a = XElement2Dictionnary(XDocument.Load(saveFileName).Root).ToDictionary(kp => kp.Key, kp => kp.Value).Values;// ?? new Dictionary<long, VaultTx2GitTx>();
        //        var elements = XDocument.Load(saveFileName).Root.Descendants("entry").ToList();
        //        foreach (XElement xe in elements)
        //        {
        //            VaultTx2GitTx entry = VaultTx2GitTx.parse(xe);
        //            CreateMapping(entry.GitCommit, entry.VaultTx);
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        throw new InvalidDataException("XML data not valid", e);
        //    }
        //    return true;
        //}

        //private static Dictionary<long, VaultTx2GitTx> XElement2Dictionnary(XElement source)
        //{
        //    return source.Descendants("entry").ToDictionary(xe => long.Parse(xe.Attribute("TxId").Value), xe => VaultTx2GitTx.parse(xe));
        //}


        public void BuildVaultTx2GitCommitFromList(List<VaultTx2GitTx> mapping)
        {
            _vaultTx2GitTxs = new VaultTx2GitTxCollection(_gitCommits, _vaultTxs);

            foreach (VaultTx2GitTx entry in mapping.OrderBy(l => l.TxId))
            {
                CreateMapping(entry.GitCommit, entry.VaultTx);
            }
        }

        internal GitCommitHash ReplaceCommitHash(GitCommitHash sourceGitCommitHash, GitCommitHash replacementCommitHash)
        {
            return _gitCommits.ReplaceCommitHash(sourceGitCommitHash, replacementCommitHash);
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
            if (_renamedbranches.TryGetValue(branchName, out string renamedBranchName))
            {
                return renamedBranchName;
            }
            return branchName;
        }
    }
}
