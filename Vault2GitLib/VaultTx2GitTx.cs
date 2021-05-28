using GitLib.Interfaces;
using System.Collections.Generic;

namespace Vault2Git.Lib
{
    public partial class Vault2GitState
    {
        public class VaultTx2GitTx
        {
            private readonly VaultTx _vaultTx;
            private readonly IGitCommit _gitCommit;

            public VaultTx2GitTx(IGitCommit gitCommit, VaultTx vaultTx)
            {
                _gitCommit = gitCommit;
                _vaultTx = vaultTx;
            }

            public VaultTx VaultTx { get => _vaultTx; }

            public IGitCommit GitCommit { get => _gitCommit; }

            public string Branch { get => _vaultTx.Branch; }

            public long TxId { get => _vaultTx.TxId; }


            public override bool Equals(object obj)
            {
                return obj is VaultTx2GitTx tx &&
                       EqualityComparer<VaultTx>.Default.Equals(_vaultTx, tx._vaultTx) &&
                       _gitCommit == tx._gitCommit;
            }

            public override int GetHashCode()
            {
                int hashCode = 2124377560;
                hashCode = hashCode * -1521134295 + EqualityComparer<VaultTx>.Default.GetHashCode(_vaultTx);
                hashCode = hashCode * -1521134295 + EqualityComparer<IGitCommit>.Default.GetHashCode(_gitCommit);
                return hashCode;
            }
        }
    }
}