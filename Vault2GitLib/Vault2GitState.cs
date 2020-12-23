using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vault2Git.Lib
{
    public partial class Vault2GitState
    {
        /// <summary>
        /// A list of git hashes maintained by the object
        /// </summary>
        private List<GitCommitHash> _gitCommitHash;

        /// <summary>
        /// A list of vault transactions maintained by the object
        /// </summary>
        private List<VaultTx> _vaultTx;

        /// <summary>
        /// An object containing the current mapping from vault transaction to git commit hash
        /// </summary>
        private List<VaultTx2GitTx> _vaultTx2GitTx;

        public Vault2GitState()
        {
            _gitCommitHash = new List<GitCommitHash>();
            _vaultTx2GitTx = new List<VaultTx2GitTx>();
            _vaultTx = new List<VaultTx>();
        }

        public GitCommitHash getGitCommitHash(string commitHash)
        {
            throw new NotImplementedException();
        }
        public GitCommitHash AddCommit(string commitHash)
        {
            throw new NotImplementedException();
        }
    }
}
