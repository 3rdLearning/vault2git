using System;
using System.Collections.Generic;
using System.Linq;

namespace Vault2Git.Lib
{
    public partial class Vault2GitState
    {
        public class VaultTx2GitTxCollection
        {
            private GitCommitCollection _gitCommits;
            private VaultTxCollection _vaultTxs;
            private SortedDictionary<long, VaultTx2GitTx> _vaultTx2GitTx;

            internal VaultTx2GitTxCollection(GitCommitCollection gitCommits, VaultTxCollection vaultTxs)
            {
                _gitCommits = gitCommits;
                _vaultTxs = vaultTxs;
                _vaultTx2GitTx = new SortedDictionary<long, VaultTx2GitTx>();
            }

            internal VaultTx2GitTx Add(GitCommit gitCommit, VaultTx vaultTx)
            {
                VaultTx2GitTx entry = new VaultTx2GitTx(gitCommit, vaultTx);
                _vaultTx2GitTx.Add(entry.TxId, entry);
                return entry;
            }

            internal VaultTx2GitTx GetMapping(VaultTx info)
            {
                if (!_vaultTx2GitTx.Where(a => a.Value.VaultTx.TxId == info.TxId).Any())
                {
                    string branch = "master";
                    return _vaultTx2GitTx.Where(k => k.Value.Branch == branch).LastOrDefault().Value;
                }
                else return _vaultTx2GitTx.Where(a => a.Value.VaultTx.TxId == info.TxId).FirstOrDefault().Value;
            }

            internal SortedDictionary<long, VaultTx2GitTx> GetMappingDictionary()
            {
                return _vaultTx2GitTx;
            }
        }
    }
}