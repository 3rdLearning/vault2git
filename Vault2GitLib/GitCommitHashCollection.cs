using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vault2Git.Lib
{
    public partial class Vault2GitState
    {
        private class GitCommitHashCollection
        {
            private Hashtable _gitCommitHashes;

            public GitCommitHashCollection()
            {
                _gitCommitHashes = new Hashtable();
            }

            public GitCommitHash this[string commitHash]
            {
                get
                {
                    return (_gitCommitHashes[commitHash] as GitCommitHash);
                }
            }

            public GitCommitHash AddCommitHash(string commitHash)
            {
                return this[commitHash] ?? AddCommitHashToCollection(commitHash);
            }

            public GitCommitHash AddCommitHash(byte[] commitHashBytes)
            {
                string commitHash = CommitHashBytesToString(commitHashBytes);
                return this[commitHash] ?? AddCommitHashToCollection(commitHash);
            }

            private GitCommitHash AddCommitHashToCollection(string commitHash)
            {
                _gitCommitHashes[commitHash] = GitCommitHash.Create(commitHash);
                return this[commitHash];
            }

            private GitCommitHash AddCommitHashToCollection(byte[] commitHashBytes)
            {
                string commitHash = CommitHashBytesToString(commitHashBytes);
                _gitCommitHashes[commitHash] = GitCommitHash.Create(commitHashBytes);

                return this[commitHash];
            }

            private string CommitHashBytesToString(byte[] commitHashBytes)
            {
                return BitConverter.ToString(commitHashBytes).Replace("-", string.Empty);
            }



        }


    }
}
