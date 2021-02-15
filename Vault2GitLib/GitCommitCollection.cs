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
        private class GitCommitCollection
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
                    return (_gitCommits[commitHash] as GitCommit);
                }
            }

            public GitCommit AddCommit(string commitHash)
            {
                return this[commitHash] ?? AddCommitToCollection(commitHash);
            }

            public GitCommit AddCommitHash(byte[] commitHashBytes)
            {
                string commitHash = CommitHashBytesToString(commitHashBytes);
                return this[commitHash] ?? AddCommitToCollection(commitHash);
            }

            private GitCommit AddCommitToCollection(string commitHash)
            {
                _gitCommits[commitHash] = GitCommitHash.Create(commitHash);
                return this[commitHash];
            }

            internal GitCommit Add(string commitHash)
            {
                throw new NotImplementedException();
            }

            private GitCommit AddCommitToCollection(byte[] commitHashBytes)
            {
                string commitHash = CommitHashBytesToString(commitHashBytes);
                _gitCommits[commitHash] = GitCommitHash.Create(commitHashBytes);

                return this[commitHash];
            }

            private string CommitHashBytesToString(byte[] commitHashBytes)
            {
                return BitConverter.ToString(commitHashBytes).Replace("-", string.Empty);
            }
        }

    }
}

