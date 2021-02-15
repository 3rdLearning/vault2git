using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vault2Git.Lib
{
    public partial class Vault2GitState
    {
        public class GitCommit
        {
            private GitCommitHash _gitCommitHash;
            private List<GitCommitHash> _gitParentCommitHashes;
            private string _comment;

            // TODO: Implement Correctly - only here to resolve error
            public string Comment { get => _comment; }

            public GitCommit(GitCommitHash gitCommitHash, List<GitCommitHash> gitParentCommitHashes)
            {
                _gitCommitHash = gitCommitHash;
                _gitParentCommitHashes = gitParentCommitHashes;
            }


            public bool AddParent(GitCommitHash gitCommitHash)
            {
                throw new NotImplementedException();
            }
        }
    }
}