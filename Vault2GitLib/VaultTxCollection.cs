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

            internal VaultTx Add(long txId, string branchName = DEFAULT_BRANCH)
            {
                _vaultTx[txId] = VaultTx.Create(txId, branchName);
                return this[txId];
            }

            internal SortedDictionary<long, VaultTx> getVaultTxAfter(long latestTxId)
            {
                return new SortedDictionary<long, VaultTx>(_vaultTx.Where(p => p.Value.TxId > latestTxId).ToDictionary(p => p.Key, p => p.Value));
            }
        }
    }
}
