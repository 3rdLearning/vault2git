using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vault2Git.Lib
{
    public partial class Vault2GitState
    {
        private class GitCommitHashCollection
        {

            private const int ArrayBlockSize = 100;

            private GitCommitHash[] _gitCommitHashes;
            private int _hashCount;
            private GitCommitHash _gitCommitHashInitReference = null;

            public GitCommitHashCollection()
            {
                _gitCommitHashes = new GitCommitHash[ArrayBlockSize];
                _hashCount = 0;
            }

            public ref GitCommitHash this[string commitHash]
            {
                get
                {
                    ref GitCommitHash NewCommitHash = ref _gitCommitHashInitReference;
                    if (TryFindCommit(commitHash, ref NewCommitHash))
                        throw new IndexOutOfRangeException(@"Index Key {commitHash} not found");
                    return ref NewCommitHash;
                }
            }


            private bool TryFindCommit(string commitHash, ref GitCommitHash gitCommitHash)
            {
                for (int i=0; i < _hashCount; i++ )
                {
                    if (_gitCommitHashes[i].ToString(false) == commitHash.ToString())
                    {
                        gitCommitHash = _gitCommitHashes[i];
                        return true;
                    }
                }

                return false;
            }

            //private ref GitCommitHash FindCommit(string commitHash)
            //{
            //    ref GitCommitHash NewCommitHash = ref _gitCommitHashInitReference;

            //    if (TryFindCommit(commitHash, ref NewCommitHash))
            //        return ref NewCommitHash;
                
            //    return ref NewCommitHash;
            //}

            //private ref GitCommitHash FindCommit(byte[] commitHashBytes)
            //{
            //    string commitHash = BitConverter.ToString(commitHashBytes).Replace("-", string.Empty);
            //    return ref FindCommit(commitHash);
            //}

            public ref GitCommitHash AddCommitHash(string commitHash)
            {
                ref GitCommitHash NewCommitHash = ref _gitCommitHashInitReference;

                // check for existing and return that
                if (!TryFindCommit(commitHash, ref NewCommitHash))
                    NewCommitHash = ref AddCommitHashToCollection(commitHash);

                return ref NewCommitHash;

            }

            private ref GitCommitHash AddCommitHashToCollection(string commitHash)
            {
                if (_hashCount % ArrayBlockSize == 0)
                {
                    _gitCommitHashes = new GitCommitHash[_gitCommitHashes.Length + ArrayBlockSize];

                }
                _gitCommitHashes[_hashCount] = new GitCommitHash(commitHash);

                return ref _gitCommitHashes[_hashCount++];
            }

            public GitCommitHash AddCommitHash(byte[] commitHashBytes)
            {
                return new GitCommitHash(commitHashBytes);
            }
        }


    }
}
