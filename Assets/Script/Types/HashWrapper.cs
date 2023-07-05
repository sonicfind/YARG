using YARG.Serialization;
using YARG.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace YARG.Hashes
{
    public class Hash128Wrapper : IComparable<Hash128Wrapper>
    {
        protected readonly Hash128 hash;
        private readonly int hashCode;

        public Hash128Wrapper(Hash128 hash)
        {
            this.hash = hash;
            hashCode = hash.GetHashCode();
        }

        public Hash128Wrapper(BinaryFileReader reader)
        {
            ulong ul_1 = reader.ReadUInt64();
            ulong ul_2 = reader.ReadUInt64();
            hash = new(ul_1, ul_2);
        }

        public void Write(BinaryWriter writer)
        {
            unsafe
            {
                fixed (Hash128* addr = &hash)
                {
                    byte* ptr = (byte*)addr;
                    writer.Write(new ReadOnlySpan<byte>(ptr, 16));
                }
            }
        }

        public bool Equals(Hash128Wrapper other)
        {
            return hash == other.hash;
        }

        public int CompareTo(Hash128Wrapper other)
        {
            return hash.CompareTo(other.hash);
        }

        public static bool operator==(Hash128Wrapper lhs, Hash128Wrapper rhs)
        {
            return lhs.Equals(rhs);
        }

        public static bool operator!=(Hash128Wrapper lhs, Hash128Wrapper rhs)
        {
            return !lhs.Equals(rhs);
        }

        public override int GetHashCode()
        {
            return hashCode;
        }
    }
}
