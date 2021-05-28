using GitLib.Interfaces;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;

namespace Vault2Git.Lib
{
    public partial class Vault2GitState
    {
        public class VaultTx2GitTxCollection
        {
            private SortedDictionary<long, VaultTx2GitTx> _vaultTx2GitTx;

            internal VaultTx2GitTxCollection()
            {
                _vaultTx2GitTx = new SortedDictionary<long, VaultTx2GitTx>();
            }

            public void Mapping2Xml(XmlWriter writer)
            {
                writer.WriteStartElement("TransactionMap");
                foreach (var version in _vaultTx2GitTx.Values)
                {

                    writer.WriteStartElement("entry");
                    writer.WriteAttributeString("TxId", version.TxId.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("Branch", version.Branch.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("GitHash", version.GitCommit.GetHash().ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("Version", version.VaultTx.Version.ToString(CultureInfo.InvariantCulture));
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }

            internal VaultTx2GitTx Add(IGitCommit gitCommit, VaultTx vaultTx)
            {
                return _vaultTx2GitTx.ContainsKey(vaultTx.TxId) ? _vaultTx2GitTx[vaultTx.TxId] : CreateVaultTx2GitTxEntry(gitCommit, vaultTx);
            }

            private VaultTx2GitTx CreateVaultTx2GitTxEntry(IGitCommit gitCommit, VaultTx vaultTx)
            {
                VaultTx2GitTx entry = new VaultTx2GitTx(gitCommit, vaultTx);
                _vaultTx2GitTx.Add(entry.TxId, entry);
                return _vaultTx2GitTx[vaultTx.TxId];
            }

            internal SortedDictionary<long, VaultTx2GitTx> GetMappingDictionary()
            {
                return _vaultTx2GitTx;
            }
            internal VaultTx GetLastVaultTxProcessed()
            {
                return _vaultTx2GitTx.LastOrDefault().Value.VaultTx;
            }
        }
    }
}