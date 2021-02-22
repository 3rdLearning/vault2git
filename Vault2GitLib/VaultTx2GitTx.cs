using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace Vault2Git.Lib
{
    public partial class Vault2GitState
    {
        public class VaultTx2GitTx
        {
            private readonly VaultTx _vaultTx;
            private readonly GitCommit _gitCommit;

            public VaultTx2GitTx(GitCommit gitCommit, VaultTx vaultTx)
            {
                _gitCommit = gitCommit;
                _vaultTx = vaultTx;
            }

            public VaultTx VaultTx { get => _vaultTx; }

            public GitCommit GitCommit { get => _gitCommit; }

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
                hashCode = hashCode * -1521134295 + EqualityComparer<GitCommit>.Default.GetHashCode(_gitCommit);
                return hashCode;
            }
        }
    }
}