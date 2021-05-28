using System.Collections.Generic;
using System.Linq;

namespace Vault2Git.Lib
{
    public partial class Vault2GitState
    {
        internal class VaultTxCollection
        {
            private SortedDictionary<long, VaultTx> _vaultTx;

            public VaultTxCollection()
            {
                _vaultTx = new SortedDictionary<long, VaultTx>();
            }
            public VaultTx this[long txId]
            {
                get
                {
                    return _vaultTx.ContainsKey(txId) ? _vaultTx[txId] : null;
                }
            }

            private VaultTx AddCommitToCollection(VaultTx vaultTx)
            {
                _vaultTx[vaultTx.TxId] = vaultTx;
                return _vaultTx[vaultTx.TxId];
            }

            internal VaultTx Add(long txId, string branchName = DEFAULT_BRANCH)
            {
                return this[txId] ?? AddCommitToCollection(VaultTx.Create(txId, branchName));
            }

            internal SortedDictionary<long, VaultTx> GetVaultTxAfter(long latestTxId)
            {
                return new SortedDictionary<long, VaultTx>(_vaultTx.Where(p => p.Value.TxId > latestTxId).ToDictionary(p => p.Key, p => p.Value));
            }
        }
    }
}
