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
        private class VaultTxCollection
        {
            private Dictionary<long, VaultTx> _vaultTx;

            public VaultTxCollection()
            {
                _vaultTx = new Dictionary<long, VaultTx>();
            }
            public VaultTx this[long txId]
            {
                get
                {
                    return _vaultTx[txId];
                }
            }

            internal VaultTx Add(long txId, string branchName = DEFAULT_BRANCH)
            {
                _vaultTx[txId] = VaultTx.Create(txId, branchName);
                return this[txId];
            }

            internal SortedList<long, VaultTx> getVaultTxAfter(long latestTxId)
            {
                VaultTx LatestTx = _vaultTx[latestTxId];

                return LatestTx != null ? (_vaultTx.Where(p => p.Key.CompareTo(LatestTx.TxId) > 0) as SortedList<long, VaultTx>): (_vaultTx.AsEnumerable() as SortedList<long, VaultTx>);

                // (LatestTx. != null) ? vaultVersions.Where(p =>
                //    (p.Key.CompareTo(gitProgress.FirstOrDefault().Value.TimeStamp.GetDateTime().ToString("yyyy-MM-ddTHH:mm:ss.fff") + ":"
                //    + gitProgress.FirstOrDefault().Value.Branch + ':' + gitProgress.FirstOrDefault().Value.TxId.ToString()) > 0)) : vaultVersions;
                //throw new NotImplementedException();
            }
        }
    }
}
