using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GitLib.Interfaces;

namespace GitLib
{
    public class GitCommitHash : IEquatable<GitCommitHash>, IGitCommitHash
    {
        /// <summary>
        /// The reference to a replacement hash.  Initialized to be a self reference and change using the Replace method
        /// </summary>
        private GitCommitHash _replacement;

        /// <summary>
        /// The binary representation of an SHA-1 hash
        /// </summary>
        private readonly byte[] _commitHash;

        /// <summary>
        ///  Constructor to create a new GitCommitHash
        /// </summary>
        /// <param name="commitHash">A string of 40 hex digits representing the SHA-1 hash</param>
        public GitCommitHash(string commitHash)
        {
            if (commitHash.Length != 40)
            {
                throw new ArgumentException("invalid length - input must be exactly 40 characters", nameof(commitHash));
            }
            _commitHash = StringToByteArray(commitHash);
            _replacement = this;
        }

        /// <summary>
        /// Constructor to create a new GitCommitHash using an existing 20 byte array
        /// </summary>
        /// <param name="commitHashBytes">The binary representation of an SHA-1 hash</param>
        public GitCommitHash(byte[] commitHashBytes)
        {
            if (commitHashBytes.Length != 20)
            {
                throw new ArgumentException("invalid length - array must contain exactly 20 bytes", nameof(commitHashBytes));
            }
            _commitHash = commitHashBytes;
            _replacement = this;
        }

        /// <summary>
        /// Replaces the current hash with a new hash.
        /// </summary>
        /// <remarks>
        /// Any operation on a reference to the original will be passed to the replacement.
        /// </remarks>
        /// <param name="replacementGitCommitHash">The new GitCommitHash</param>
        /// <returns>A reference to the original GitCommitHash</returns>
        public IGitCommitHash Replace(IGitCommitHash replacementGitCommitHash)
        {
            _replacement = replacementGitCommitHash as GitCommitHash;
            return this;
        }

        /// <summary>
        /// Converts a string of 40 hex digits representing the SHA-1 hash to a byte array
        /// </summary>
        /// <param name="sha1">The string of 40 hex digits representing the SHA-1 hash</param>
        /// <returns>A byte array of the SHA-1 hash</returns>
        private static byte[] StringToByteArray(string sha1)
        {
            if (sha1.Length != 40)
            {
                throw new ArgumentException("invalid length - input must be exactly 40 characters", nameof(sha1));
            }

            string hex = sha1.ToLower();

            if (hex.All(c => "0123456789abcdef".Contains(c)))
            {
                int numberChars = hex.Length;
                byte[] bytes = new byte[numberChars / 2];
                for (int i = 0; i < numberChars; i += 2)
                    bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
                return bytes;
            }
            else
            {

                throw new ArgumentException("Value can only contain hexadecimal characters and must contain an even number of characters", nameof(sha1));
            }
        }

        /// <summary>
        /// Converts a 20 element byte array to a string of 40 hex digits representing the SHA-1 hash
        /// </summary>
        /// <param name="bytes">A byte array of the SHA-1 hash</param>
        /// <returns>A string of 40 hex digits representing the SHA-1 hash</returns>
        private static string ByteArrayToString(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", string.Empty);
        }


        /// <summary>
        /// Returns the string of 40 hex digits representing the SHA-1 hash
        /// </summary>
        /// <remarks>If a replacement GitCommitHash has been added to the GitCommitHash, this call will be passed to the replacement GitCommitHash</remarks>
        /// <returns>A string of 40 hex digits representing the SHA-1 hash</returns>
        public override string ToString()
        {
            return this.ToString(true, CultureInfo.InvariantCulture);
        }

        public string ToString(CultureInfo cultureInfo)
        {
            return this.ToString(true, cultureInfo);
        }

        /// <summary>
        /// Returns the string of 40 hex digits representing the SHA-1 hash
        /// </summary>
        /// <remarks>If a replacement GitCommitHash has been added to the GitCommitHash, this method can retrieve the value from the replacement using the <paramref name="followReplacement"/> parameter</remarks>
        /// <param name="followReplacement">follow replacement references</param>
        /// <returns></returns>
        public string ToString(bool followReplacement)
        {
            return ToString(followReplacement, CultureInfo.InvariantCulture);
        }

        public string ToString(bool followReplacement, CultureInfo cultureInfo)
        {
            return (!Object.ReferenceEquals(_replacement, this) && followReplacement) ? _replacement.ToString(true, cultureInfo) : ByteArrayToString(_commitHash).ToLower(cultureInfo);
        }



        /// <summary>
        /// Compares 1 GitCommitHash to another.
        /// </summary>
        /// <remarks>
        /// Equality check compares the output of <see cref="GitCommitHash.ToString()" /> on both objects.  The check will traverse to comparision objects.
        /// </remarks>
        /// <param name="other"></param>
        /// <returns>Boolean equality result</returns>
        public bool Equals(GitCommitHash other)
        {
            if (other is null)
                return false;

            return (this.ToString() == other.ToString());
        }


        /// <summary>
        /// Equality check on object.  Overridden to use <see cref="GitCommitHash.Equals(GitCommitHash)" />.
        /// </summary>
        /// <param name="obj">The object</param>
        /// <returns>Boolean equality result</returns>
        public override bool Equals(object obj)
        {
            return this.Equals(obj as GitCommitHash);
        }

        /// <summary>
        /// Overload of == operator to use <see cref="GitCommitHash.Equals(GitCommitHash)" />
        /// </summary>
        /// <param name="lhs">The left hand side of the comparison</param>
        /// <param name="rhs">The left hand side of the comparison</param>
        /// <returns>Boolean equality result</returns>
        public static bool operator ==(GitCommitHash lhs, GitCommitHash rhs)
        {
            if (lhs is null)
            {
                if (rhs is null)
                {
                    return true;
                }

                return false;

            }

            return lhs.Equals(rhs);
        }

        /// <summary>
        /// Overload of != operator to use <see cref="GitCommitHash.Equals(GitCommitHash)" />
        /// </summary>
        /// <param name="lhs">The left hand side of the comparison</param>
        /// <param name="rhs">The left hand side of the comparison</param>
        /// <returns>Boolean inequality result</returns>
        public static bool operator !=(GitCommitHash lhs, GitCommitHash rhs)
        {
            return !(lhs == rhs);
        }

        /// <summary>
        /// Override of the object hash code calculation
        /// </summary>
        /// <remarks>This uses the string value of the SHA-1 hash of the current node, or if a replacement 
        /// exists, the replacement node. This allows two objects that have different roots that resolve to 
        /// the same values in a replacement node be treated as potentially equal.</remarks>
        /// <returns>The calculated HashCode</returns>
        public override int GetHashCode()
        {
            int hashCode = 1079742553;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(this.ToString());
            return hashCode;
        }




    }
}
