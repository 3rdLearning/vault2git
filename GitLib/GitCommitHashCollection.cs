using System;
using System.Collections;
using GitLib.Interfaces;

namespace GitLib
{
    public class GitCommitHashCollection : IGitCommitHashCollection
    {
        /// <summary>
        /// A collection git Commit Hashes
        /// </summary>
        private readonly Hashtable _gitCommitHashes;

        public GitCommitHashCollection()
        {
            _gitCommitHashes = new Hashtable();
        }

        public IGitCommitHash this[string commitHash] => _gitCommitHashes[commitHash] as IGitCommitHash;

        public IGitCommitHash AddCommitHash(string commitHash)
        {
            return this[commitHash] ?? AddCommitHashToCollection(commitHash);
        }

        public IGitCommitHash AddCommitHash(byte[] commitHashBytes)
        {
            string commitHash = CommitHashBytesToString(commitHashBytes);
            return this[commitHash] ?? AddCommitHashToCollection(commitHashBytes);
        }

        public IGitCommitHash AddCommitHash(IGitCommitHash gitCommitHash)
        {
            return this[gitCommitHash.ToString()] ?? AddCommitHashToCollection(gitCommitHash, gitCommitHash.ToString());
        }

        private IGitCommitHash AddCommitHashToCollection(IGitCommitHash gitCommitHash, string commitHash)
        {
            _gitCommitHashes[commitHash] = gitCommitHash;
            return this[commitHash];
        }

        private IGitCommitHash AddCommitHashToCollection(IGitCommitHash gitCommitHash)
        {
            return AddCommitHashToCollection(gitCommitHash, gitCommitHash.ToString());
        }

        private IGitCommitHash AddCommitHashToCollection(string commitHash)
        {
            return AddCommitHashToCollection(new GitCommitHash(commitHash), commitHash);
        }
        public IGitCommitHash ReplaceCommitHash(IGitCommitHash sourceGitCommitHash, string replacementCommitHash)
        {
            return ReplaceCommitHash(sourceGitCommitHash, AddCommitHashToCollection(replacementCommitHash));
        }

        internal IGitCommitHash ReplaceCommitHash(IGitCommitHash sourceGitCommitHash, IGitCommitHash replacementGitCommitHash)
        {
            return AddCommitHash(sourceGitCommitHash).Replace(replacementGitCommitHash);
        }

        private IGitCommitHash AddCommitHashToCollection(byte[] commitHashBytes)
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
