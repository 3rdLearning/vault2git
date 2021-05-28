using System;
using System.Collections;
using System.Collections.Generic;
using GitLib.Interfaces;

namespace GitLib
{
    public class GitCommitCollection : IGitCommitCollection
    {
        private readonly GitCommitHashCollection _gitCommitHashes;
        private readonly Hashtable _gitCommits;

        public GitCommitCollection()
        {
            _gitCommits = new Hashtable();
            _gitCommitHashes = new GitCommitHashCollection();
        }

        public IGitCommit this[string commitHash] => _gitCommits[commitHash] as IGitCommit;

        public IGitCommit AddCommit(string commitHash)
        {
            return this[commitHash] ?? AddCommitToCollection(commitHash);
        }

        public IGitCommit AddCommit(IGitCommitHash gitCommitHash)
        {
            return AddCommit(gitCommitHash.ToString());
        }

        public IGitCommit AddCommit(byte[] commitHashBytes)
        {
            string commitHash = CommitHashBytesToString(commitHashBytes);
            return this[commitHash] ?? AddCommitToCollection(commitHash);
        }

        private IGitCommit AddCommitToCollection(string commitHash)
        {
            IGitCommitHash gitCommitHash = _gitCommitHashes.AddCommitHash(commitHash);
            _gitCommits[commitHash] = new GitCommit(gitCommitHash, new List<IGitCommitHash>());
            return _gitCommits[commitHash] as IGitCommit;
        }


        public IGitCommitHash ReplaceCommitHash(IGitCommitHash sourceGitCommitHash, IGitCommitHash replacementGitCommitHash)
        {
            return _gitCommitHashes.ReplaceCommitHash(sourceGitCommitHash, replacementGitCommitHash);
        }

        private string CommitHashBytesToString(byte[] commitHashBytes)
        {
            return BitConverter.ToString(commitHashBytes).Replace("-", string.Empty);
        }

        IGitCommit IGitCommitCollection.ReplaceCommitHash(IGitCommitHash sourceGitCommitHash,
            IGitCommitHash replacementGitCommitHash)
        {
            return _gitCommits[ReplaceCommitHash(sourceGitCommitHash, replacementGitCommitHash).ToString()] as IGitCommit;
        }
    }
}

