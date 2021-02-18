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
            public string Comment { get => _comment; set => _comment = value; }

            public GitCommit(GitCommitHash gitCommitHash, List<GitCommitHash> gitParentCommitHashes)
            {
                _gitCommitHash = gitCommitHash;
                _gitParentCommitHashes = gitParentCommitHashes;
            }

            public bool AddParent(GitCommitHash gitCommitHash)
            {
                if (!_gitParentCommitHashes.Contains(gitCommitHash))
                {
                    _gitParentCommitHashes.Add(gitCommitHash);
                }
                return true;
            }

            public GitCommitHash GetHash()
            {
                return _gitCommitHash;
            }

            public List<GitCommitHash> GetParentHashes()
            {
                return _gitParentCommitHashes;
            }

        }
    }
}