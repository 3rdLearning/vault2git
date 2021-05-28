using System.Collections.Generic;
using System.Linq;
using GitLib.Interfaces;

namespace GitLib
{
    public class GitCommit : IGitCommit
    {
        private readonly IGitCommitHash _gitCommitHash;
        private readonly List<IGitCommitHash> _gitParentCommitHashes;

        public string Comment { get; set; }

        public GitCommit(IGitCommitHash gitCommitHash, List<IGitCommitHash> gitParentCommitHashes)
        {
            _gitCommitHash = gitCommitHash;
            _gitParentCommitHashes = gitParentCommitHashes;
        }

        public void AddParent(IGitCommitHash gitCommitHash)
        {
            if (!_gitParentCommitHashes.Contains(gitCommitHash as GitCommitHash))
            {
                _gitParentCommitHashes.Add(gitCommitHash as GitCommitHash);
            }
        }

        public IGitCommitHash GetHash()
        {
            return _gitCommitHash;
        }

        public List<IGitCommitHash> GetParentHashes()
        {
            return _gitParentCommitHashes.ToList();
        }
    }
}