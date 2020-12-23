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
        private class VaultTx
        {
            private readonly string _branch;
            private readonly long _txId;


            public VaultTx(long txId, string branch = "master")
            {
                _branch = branch;
                _txId = txId;
            }

            public VaultTx(VaultVersionInfo info)
            {
                _branch = info.Branch;
                _txId = info.TxId;
            }

            public string Branch { get => _branch; }
            public long TxId { get => _txId; }

            public static VaultTx Parse(string key)
            {
                return new VaultTx(long.Parse(key.Split(':')[1]), key.Split(':')[0].ToString());
            }

            public override bool Equals(object obj)
            {
                return obj is VaultTx tx &&
                       _branch == tx._branch &&
                       _txId == tx._txId;
            }

            public override int GetHashCode()
            {
                int hashCode = -1854605187;
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(_branch);
                hashCode = hashCode * -1521134295 + _txId.GetHashCode();
                return hashCode;
            }
        }
    }
}