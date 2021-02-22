using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

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

            public void Mapping2Xml(XmlWriter writer)
            {
                writer.WriteStartElement("TransactionMap");
                foreach (var version in _vaultTx2GitTx.Values)
                {

                    writer.WriteStartElement("entry");
                    writer.WriteAttributeString("TxId", version.TxId.ToString());
                    writer.WriteAttributeString("Branch", version.Branch.ToString());
                    writer.WriteAttributeString("GitHash", version.GitCommit.GetHash().ToString());
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }

            internal VaultTx2GitTx Add(GitCommit gitCommit, VaultTx vaultTx)
            {
                VaultTx2GitTx entry = new VaultTx2GitTx(gitCommit, vaultTx);
                _vaultTx2GitTx.Add(entry.TxId, entry);
                return entry;
            }

            //internal VaultTx2GitTx GetMapping(VaultTx info)
            //{
            //    if (!_vaultTx2GitTx.Where(a => a.Value.VaultTx.TxId == info.TxId).Any())
            //    {
            //        string branch = "master";
            //        return _vaultTx2GitTx.Where(k => k.Value.Branch == branch).LastOrDefault().Value;
            //    }
            //    else return _vaultTx2GitTx.Where(a => a.Value.VaultTx.TxId == info.TxId).FirstOrDefault().Value;
            //}

            internal SortedDictionary<long, VaultTx2GitTx> GetMappingDictionary()
            {
                return _vaultTx2GitTx;
            }
        }
    }
}