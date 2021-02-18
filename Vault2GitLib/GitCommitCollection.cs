using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vault2Git.Lib
{
    public partial class Vault2GitState
    {
        internal class GitCommitCollection
        {
            private GitCommitHashCollection _gitCommitHashes;
            private Hashtable _gitCommits;

            public GitCommitCollection()
            {
                _gitCommits = new Hashtable();
                _gitCommitHashes = new GitCommitHashCollection();
            }

            public GitCommit this[string commitHash]
            {
                get
                {
                    return (_gitCommits[commitHash] ?? null) as GitCommit;
                }
            }

            public GitCommit AddCommit(string commitHash)
            {
                return this[commitHash] ?? AddCommitToCollection(commitHash);
            }

            private GitCommit AddCommit(GitCommitHash gitCommitHash)
            {
                return AddCommit(gitCommitHash.ToString());
            }

            public GitCommit AddCommit(byte[] commitHashBytes)
            {
                string commitHash = CommitHashBytesToString(commitHashBytes);
                return this[commitHash] ?? AddCommitToCollection(commitHash);
            }

            private GitCommit AddCommitToCollection(string commitHash)
            {
                GitCommitHash gitCommitHash = _gitCommitHashes.AddCommitHash(commitHash);
                _gitCommits[commitHash] = new GitCommit(gitCommitHash, new List<GitCommitHash>());
                return _gitCommits[commitHash] as GitCommit;
            }

            internal GitCommit Add(string commitHash)
            {
                throw new NotImplementedException();
            }

            private GitCommit AddCommitToCollection(byte[] commitHashBytes)
            {
                string commitHash = CommitHashBytesToString(commitHashBytes);
                _gitCommits[commitHash] = new GitCommitHash(commitHashBytes);

                return this[commitHash];
            }

            private string CommitHashBytesToString(byte[] commitHashBytes)
            {
                return BitConverter.ToString(commitHashBytes).Replace("-", string.Empty);
            }
        }

    }
}

