using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        /// An object containing the current mapping from vault transaction to git commit hash
        /// </summary>
        private List<VaultTx2GitTx> _vaultTx2GitTx;

        public Vault2GitState()
        {
            _vaultTx2GitTx = new List<VaultTx2GitTx>();
            _gitCommits = new GitCommitCollection();
            _vaultTxs = new VaultTxCollection();
        }

        public GitCommitHash getGitCommitHash(string commitHash)
        {
            return _gitCommits.AddCommit(commitHash);
            throw new NotImplementedException();
        }
  
        internal GitCommit CreateGitCommit(string commitHash)
        {
            return _gitCommits[commitHash] ?? _gitCommits.AddCommit(commitHash);
        }


        internal VaultTx CreateVaultTransaction(long txId, string branchName = DEFAULT_BRANCH)
        {
            return _vaultTxs[txId] ?? _vaultTxs.Add(txId, branchName);
        }

        internal SortedList<long, VaultTx> GetVaultTransactionsToProcess(long latestTxId)
        {
            return _vaultTxs.getVaultTxAfter(latestTxId);
        }
    }
}
