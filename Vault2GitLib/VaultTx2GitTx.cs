using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Vault2Git.Lib
{
    public partial class Vault2GitState
    {
        private class VaultTx2GitTx
        {
            private readonly VaultTx _vaultTx;
            private readonly GitCommitHash _gitHash;

            public VaultTx2GitTx(string gitHash, VaultTx vaultTx)
            {
                _gitHash = new GitCommit(gitHash);
                _vaultTx = vaultTx;
            }

            public VaultTx2GitTx(GitCommitHash gitHash, VaultTx vaultTx)
            {
                _gitHash = gitHash;
                _vaultTx = vaultTx;
            }

            public VaultTx VaultTx { get => _vaultTx; }

            public GitCommitHash GitHash { get => _gitHash; }

            public string Branch { get => _vaultTx.Branch; }

            public long TxId { get => _vaultTx.TxId; }

            public VaultTx2GitTx()
            {
            }

            public VaultTx2GitTx(GitCommit commit, VaultVersionInfo info)
            {
                _gitHash = new GitCommitHash(gitHash);
                _vaultTx = new VaultTx(info);
            }

            public static VaultTx2GitTx parse(XElement xe)
            {
                return new VaultTx2GitTx(xe.Attribute("GitHash").Value, new VaultTx(long.Parse(xe.Attribute("TxId").Value), xe.Attribute("Branch").Value));
            }

            public override bool Equals(object obj)
            {
                return obj is VaultTx2GitTx tx &&
                       EqualityComparer<VaultTx>.Default.Equals(_vaultTx, tx._vaultTx) &&
                       _gitHash == tx._gitHash;
            }

            public override int GetHashCode()
            {
                int hashCode = 2124377560;
                hashCode = hashCode * -1521134295 + EqualityComparer<VaultTx>.Default.GetHashCode(_vaultTx);
                hashCode = hashCode * -1521134295 + EqualityComparer<GitCommitHash>.Default.GetHashCode(_gitHash);
                return hashCode;
            }
        }
    }
}