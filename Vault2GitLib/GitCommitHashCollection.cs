﻿using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vault2Git.Lib
{
    public partial class Vault2GitState
    {
        public class GitCommitHashCollection
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
                    return (_gitCommitHashes[commitHash] ?? null) as GitCommitHash;
                }
            }

            public GitCommitHash AddCommitHash(string commitHash)
            {
                return this[commitHash] ?? AddCommitHashToCollection(commitHash);
            }

            public GitCommitHash AddCommitHash(byte[] commitHashBytes)
            {
                string commitHash = CommitHashBytesToString(commitHashBytes);
                return this[commitHash] ?? AddCommitHashToCollection(commitHashBytes);
            }

            public GitCommitHash AddCommitHash(GitCommitHash gitCommitHash)
            {
                return this[gitCommitHash.ToString()] ?? AddCommitHashToCollection(gitCommitHash, gitCommitHash.ToString());
            }

            private GitCommitHash AddCommitHashToCollection(GitCommitHash gitCommitHash, string commitHash)
            {
                _gitCommitHashes[commitHash] = gitCommitHash;
                return this[commitHash];
            }

            private GitCommitHash AddCommitHashToCollection(GitCommitHash gitCommitHash)
            {
                return AddCommitHashToCollection(gitCommitHash, gitCommitHash.ToString());
            }

            private GitCommitHash AddCommitHashToCollection(string commitHash)
            {
                return AddCommitHashToCollection(new GitCommitHash(commitHash), commitHash);
            }

            private GitCommitHash AddCommitHashToCollection(byte[] commitHashBytes)
            {
                return AddCommitHashToCollection(new GitCommitHash(commitHashBytes));
            }

            private string CommitHashBytesToString(byte[] commitHashBytes)
            {
                return BitConverter.ToString(commitHashBytes).Replace("-", string.Empty);
            }

            public int Count()
            {
                return _gitCommitHashes.Count;
            }
        }


    }
}
