//! \file       CryptAlgorithms.cs
//! \date       Thu Feb 04 12:08:40 2016
//! \brief      KiriKiri engine encryption algorithms.
//
// Copyright (C) 2016 by morkt
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to
// deal in the Software without restriction, including without limitation the
// rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
// sell copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
// IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.Linq;
using GameRes.Utility;

namespace GameRes.Formats.KiriKiri
{
    [Serializable]
    public abstract class ICrypt
    {
        /// <summary>
        /// whether Adler32 checksum should be calculated after contents have been encrypted.
        /// </summary>
        public virtual bool HashAfterCrypt { get { return false; } }

        /// <summary>
        /// sometimes startup.tjs file is not encrypted.
        /// </summary>
        public bool StratupTjsNotEncrypted { get; set; }

        /// <summary>
        /// whether XP3 index is obfuscated:
        ///  - duplicate entries
        ///  - entries have additional dummy segments
        /// </summary>
        public bool ObfuscatedIndex { get; set; }

        public virtual byte Decrypt (Xp3Entry entry, long offset, byte value)
        {
            byte[] buffer = new byte[1] { value };
            Decrypt (entry, offset, buffer, 0, 1);
            return buffer[0];
        }

        public abstract void Decrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count);

        public virtual void Encrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            throw new NotImplementedException (Strings.arcStrings.MsgEncNotImplemented);
        }

        /// <summary>
        /// Perform necessary initialization specific to an archive being opened.
        /// </summary>
        public virtual void Init (ArcFile arc)
        {
        }
    }

    [Serializable]
    public class NoCrypt : ICrypt
    {
        public override byte Decrypt (Xp3Entry entry, long offset, byte value)
        {
            return value;
        }
        public override void Decrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            return;
        }
        public override void Encrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            return;
        }
    }

    [Serializable]
    public class FateCrypt : ICrypt
    {
        public override bool HashAfterCrypt { get { return true; } }

        public override byte Decrypt (Xp3Entry entry, long offset, byte value)
        {
            byte result = (byte)(value ^ 0x36);
            if (0x13 == offset)
                result ^= 1;
            else if (0x2ea29 == offset)
                result ^= 3;
            return result;
        }

        public override void Decrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            for (int i = 0; i < count; ++i)
            {
                values[pos+i] ^= 0x36;
            }
            if (offset > 0x2ea29)
                return;
            if (offset + count > 0x2ea29)
                values[pos+0x2ea29-offset] ^= 3;
            if (offset > 0x13)
                return;
            if (offset + count > 0x13)
                values[pos+0x13-offset] ^= 1;
        }

        public override void Encrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            Decrypt (entry, offset, values, pos, count);
        }
    }

    [Serializable]
    public class MizukakeCrypt : ICrypt
    {
        public override bool HashAfterCrypt { get { return true; } }

        public override void Decrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            if (offset <= 0x103 && offset + count > 0x103)
                values[pos+0x103-offset]--;
            for (int i = 0; i < count; ++i)
            {
                values[pos+i] ^= 0xB6;
            }
            if (offset > 0x3F82)
                return;
            if (offset + count > 0x3F82)
                values[pos+0x3F82-offset] ^= 1;
            if (offset > 0x83)
                return;
            if (offset + count > 0x83)
                values[pos+0x83-offset] ^= 3;
        }

        public override void Encrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            for (int i = 0; i < count; ++i)
            {
                values[pos+i] ^= 0xB6;
            }
            if (offset <= 0x3F82 && offset + count > 0x3F82)
                values[pos+0x3F82-offset] ^= 1;
            if (offset <= 0x83 && offset + count > 0x83)
                values[pos+0x83-offset] ^= 3;
            if (offset <= 0x103 && offset + count > 0x103)
                values[pos+0x103-offset]++;
        }
    }

    [Serializable]
    public class HashCrypt : ICrypt
    {
        public override byte Decrypt (Xp3Entry entry, long offset, byte value)
        {
            return (byte)(value ^ entry.Hash);
        }

        public override void Decrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            byte key = (byte)entry.Hash;
            for (int i = 0; i < count; ++i)
            {
                values[pos+i] ^= key;
            }
        }

        public override void Encrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            Decrypt (entry, offset, values, pos, count);
        }
    }

    [Serializable]
    public class XorCrypt : ICrypt
    {
        private byte m_key;

        public byte Key
        {
            get { return m_key; }
            set { m_key = value; }
        }

        public XorCrypt (uint key)
        {
            m_key = (byte)key;
        }

        public override byte Decrypt (Xp3Entry entry, long offset, byte value)
        {
            return (byte)(value ^ m_key);
        }

        public override void Decrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            for (int i = 0; i < count; ++i)
            {
                values[pos+i] ^= m_key;
            }
        }

        public override void Encrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            Decrypt (entry, offset, values, pos, count);
        }
    }

    [Serializable]
    public class FlyingShineCrypt : ICrypt
    {
        static private byte Adjust (uint hash, out int shift)
        {
            shift = (int)(hash & 0xff);
            if (0 == shift) shift = 0x0f;
            byte key = (byte)(hash >> 8);
            if (0 == key) key = 0xf0;
            return key;
        }

        public override byte Decrypt (Xp3Entry entry, long offset, byte value)
        {
            int shift;
            byte xor = Adjust (entry.Hash, out shift);
            return Binary.RotByteR ((byte)(value ^ xor), shift);
        }

        public override void Decrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            int shift;
            byte xor = Adjust (entry.Hash, out shift);
            for (int i = 0; i < count; ++i)
            {
                byte data = (byte)(values[pos+i] ^ xor);
                values[pos+i] = Binary.RotByteR (data, shift);
            }
        }

        public override void Encrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            int shift;
            byte xor = Adjust (entry.Hash, out shift);
            for (int i = 0; i < count; ++i)
            {
                byte data = Binary.RotByteL (values[pos+i], shift);
                values[pos+i] = (byte)(data ^ xor);
            }
        }
    }

    [Serializable]
    public class SeitenCrypt : ICrypt
    {
        public override byte Decrypt (Xp3Entry entry, long offset, byte value)
        {
            uint key = entry.Hash ^ (uint)offset;
            if (0 != (key & 2))
            {
                int ecx = (int)key & 0x18;
                value ^= (byte)((key >> ecx) | (key >> (ecx & 8)));
            }
            if (0 != (key & 4))
            {
                value += (byte)key;
            }
            if (0 != (key & 8))
            {
                value -= (byte)(key >> (int)(key & 0x10));
            }
            return value;
        }

        public override void Decrypt (Xp3Entry entry, long offset, byte[] buffer, int pos, int count)
        {
            for (int i = 0; i < count; ++i)
            {
                int shift;
                uint key = entry.Hash ^ (uint)offset;
                byte v = buffer[pos+i];
                if (0 != (key & 2))
                {
                    shift = (int)key & 0x18;
                    uint ebx = key >> shift;
                    shift &= 8;
                    v ^= (byte)(ebx | (key >> shift));
                }
                if (0 != (key & 4))
                {
                    v += (byte)key;
                }
                if (0 != (key & 8))
                {
                    shift = (int)key & 0x10;
                    v -= (byte)(key >> shift);
                }
                buffer[pos+i] = v;
                ++offset;
            }
        }

        public override void Encrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            for (int i = 0; i < count; ++i)
            {
                uint key = entry.Hash ^ (uint)offset;
                if (0 != (key & 8))
                {
                    values[pos+i] += (byte)(key >> (int)(key & 0x10));
                }
                if (0 != (key & 4))
                {
                    values[pos+i] -= (byte)key;
                }
                if (0 != (key & 2))
                {
                    int ecx = (int)key & 0x18;
                    values[pos+i] ^= (byte)((key >> ecx) | (key >> (ecx & 8)));
                }
            }
        }
    }

    [Serializable]
    public class OkibaCrypt : ICrypt
    {
        public override byte Decrypt (Xp3Entry entry, long offset, byte value)
        {
            if (offset < 0x65)
                return (byte)(value ^ (byte)(entry.Hash >> 4));
            uint key = entry.Hash;
            // 0,1,2,3 -> 1,0,3,2
            key = ((key & 0xff0000) << 8) | ((key & 0xff000000) >> 8)
                | ((key & 0xff00) >> 8)   | ((key & 0xff) << 8);
            key >>= 8 * ((int)(offset - 0x65) & 3);
            return (byte)(value ^ (byte)key);
        }

        public override void Decrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            int i = 0;
            if (offset < 0x65)
            {
                uint key = entry.Hash >> 4;
                int limit = Math.Min (count, (int)(0x65 - offset));
                for (; i < limit; ++i)
                {
                    values[pos+i] ^= (byte)key;
                    ++offset;
                }
            }
            if (i < count)
            {
                offset -= 0x65;
                uint key = entry.Hash;
                key = ((key & 0xff0000) << 8) | ((key & 0xff000000) >> 8)
                    | ((key & 0xff00) >> 8)   | ((key & 0xff) << 8);
                do
                {
                    values[pos+i] ^= (byte)(key >> (8 * ((int)offset & 3)));
                    ++offset;
                }
                while (++i < count);
            }
        }

        public override void Encrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            Decrypt (entry, offset, values, pos, count);
        }
    }

    [Serializable]
    public class DieselmineCrypt : ICrypt
    {
        public override byte Decrypt (Xp3Entry entry, long offset, byte value)
        {
            byte key = (byte)entry.Hash;
            if (offset < 123)
                value ^= (byte)(21 * key);
            else if (offset < 246)
                value += (byte)(-32 * key);
            else if (offset < 369)
                value ^= (byte)(43 * key);
            else if (offset <= 0xffffffffL)
                value += (byte)(-54 * key);
            return value;
        }

        public override void Decrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            byte key = (byte)entry.Hash;
            for (int i = 0; i < count && offset <= 0xffffffffL; ++i, ++offset)
            {
                if (offset < 123)
                    values[pos+i] ^= (byte)(21 * key);
                else if (offset < 246)
                    values[pos+i] += (byte)(-32 * key);
                else if (offset < 369)
                    values[pos+i] ^= (byte)(43 * key);
                else
                    values[pos+i] += (byte)(-54 * key);
            }
        }

        public override void Encrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            byte key = (byte)entry.Hash;
            for (int i = 0; i < count && offset <= 0xffffffffL; ++i, ++offset)
            {
                if (offset < 123)
                    values[pos+i] ^= (byte)(21 * key);
                else if (offset < 246)
                    values[pos+i] -= (byte)(-32 * key);
                else if (offset < 369)
                    values[pos+i] ^= (byte)(43 * key);
                else
                    values[pos+i] -= (byte)(-54 * key);
            }
        }
    }

    [Serializable]
    public class DameganeCrypt : ICrypt
    {
        public override byte Decrypt (Xp3Entry entry, long offset, byte value)
        {
            if (0 != (offset & 1))
                return (byte)(value ^ entry.Hash);
            else
                return (byte)(value ^ offset);
        }

        public override void Decrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            for (int i = 0; i < count; ++i, ++offset)
            {
                if (0 != (offset & 1))
                    values[pos+i] ^= (byte)entry.Hash;
                else
                    values[pos+i] ^= (byte)offset;
            }
        }

        public override void Encrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            Decrypt (entry, offset, values, pos, count);
        }
    }

    [Serializable]
    public class GakuenButouCrypt : ICrypt
    {
        public override byte Decrypt (Xp3Entry entry, long offset, byte value)
        {
            if (0 != (offset & 1))
                return (byte)(value ^ offset);
            else
                return (byte)(value ^ entry.Hash);
        }

        public override void Decrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            for (int i = 0; i < count; ++i, ++offset)
            {
                if (0 != (offset & 1))
                    values[pos+i] ^= (byte)offset;
                else
                    values[pos+i] ^= (byte)entry.Hash;
            }
        }

        public override void Encrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            Decrypt (entry, offset, values, pos, count);
        }
    }

    [Serializable]
    public class AlteredPinkCrypt : ICrypt
    {
        static readonly byte[] KeyTable = {
            0x43, 0xF8, 0xAD, 0x08, 0xDF, 0xB7, 0x26, 0x44, 0xF0, 0xD9, 0xE9, 0x24, 0x1A, 0xC1, 0xEE, 0xB4,
            0x11, 0x4B, 0xE4, 0xAF, 0x01, 0x5B, 0xF0, 0xAB, 0x6A, 0x70, 0x78, 0x84, 0xB0, 0x78, 0x4F, 0xED,
            0x39, 0x52, 0x69, 0xAF, 0xC4, 0x92, 0x2A, 0x21, 0xDE, 0xDC, 0x6E, 0x63, 0x9D, 0x9B, 0x63, 0xE1,
            0xB1, 0x94, 0x40, 0x6E, 0x3A, 0x52, 0x5A, 0x28, 0x08, 0x4D, 0xFB, 0x22, 0x18, 0xEB, 0xBA, 0x98,
            0x49, 0x77, 0xBF, 0xAA, 0x43, 0x75, 0xF5, 0xD3, 0x83, 0x71, 0x58, 0xA4, 0xAF, 0x1B, 0x53, 0x99,
            0x8A, 0x27, 0x5B, 0xC2, 0x7F, 0x7A, 0xCD, 0x8D, 0x33, 0x59, 0xEB, 0xA6, 0xFA, 0x7C, 0x00, 0x19,
            0xC4, 0xAA, 0x24, 0xF8, 0x84, 0xCD, 0xF7, 0x20, 0x4B, 0xAB, 0xF1, 0xD5, 0x01, 0x6F, 0x7C, 0x91,
            0x08, 0x7D, 0x8D, 0x89, 0x7C, 0x71, 0x65, 0x99, 0x9B, 0x6F, 0x3A, 0x1C, 0x49, 0xE3, 0xAF, 0x1F,
            0xC6, 0xA5, 0x79, 0xFE, 0xAE, 0xA1, 0xCA, 0x59, 0x3C, 0xEE, 0xC1, 0x02, 0xBD, 0x2B, 0x8E, 0xC5,
            0x7D, 0x38, 0x80, 0x8F, 0x72, 0xF3, 0x86, 0x5D, 0xF4, 0x20, 0x0A, 0x5B, 0xA0, 0xE3, 0x85, 0xB5,
            0x67, 0x43, 0x96, 0xBB, 0x75, 0x86, 0x8D, 0x7E, 0x7E, 0xE6, 0xAA, 0x18, 0x57, 0xC4, 0xAA, 0x87,
            0xDC, 0x74, 0x05, 0xAA, 0xBD, 0x5E, 0x4F, 0xA9, 0xB5, 0x5E, 0xC5, 0xE8, 0x11, 0x6D, 0x68, 0x89,
            0x17, 0x7C, 0x10, 0x05, 0xA2, 0xBA, 0x43, 0x01, 0xD6, 0xFD, 0x26, 0x19, 0x57, 0xFA, 0x4D, 0x01,
            0xB0, 0xED, 0x3A, 0x55, 0xEB, 0x65, 0x8E, 0xD1, 0x58, 0x27, 0xAD, 0xA1, 0x5E, 0x57, 0x3F, 0xA0,
            0xEF, 0x59, 0x3E, 0xA4, 0xEB, 0x12, 0x15, 0x60, 0xBE, 0x95, 0x61, 0x0B, 0x98, 0xF5, 0xF4, 0x12,
            0x1C, 0xD8, 0x62, 0x3F, 0xFD, 0xCF, 0x01, 0x3A, 0xE7, 0xC2, 0x19, 0x38, 0x6C, 0xC3, 0x90, 0x3E,
        };

        public override byte Decrypt (Xp3Entry entry, long offset, byte value)
        {
            return (byte)(value ^ KeyTable[offset & 0xFF]);
        }

        public override void Decrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            for (int i = 0; i < count; ++i)
            {
                values[pos+i] ^= KeyTable[(offset+i) & 0xFF];
            }
        }

        public override void Encrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            Decrypt (entry, offset, values, pos, count);
        }
    }

    [Serializable]
    public class NatsupochiCrypt : ICrypt
    {
        public override byte Decrypt (Xp3Entry entry, long offset, byte value)
        {
            return (byte)(value ^ (entry.Hash >> 3));
        }

        public override void Decrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            byte key = (byte)(entry.Hash >> 3);
            for (int i = 0; i < count; ++i)
            {
                values[pos+i] ^= key;
            }
        }

        public override void Encrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            Decrypt (entry, offset, values, pos, count);
        }
    }

    [Serializable]
    public class PoringSoftCrypt : ICrypt
    {
        public override byte Decrypt (Xp3Entry entry, long offset, byte value)
        {
            return (byte)~(value ^ (entry.Hash + 1));
        }

        public override void Decrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            byte key = (byte)~(entry.Hash + 1);
            for (int i = 0; i < count; ++i)
            {
                values[pos+i] ^= key;
            }
        }

        public override void Encrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            Decrypt (entry, offset, values, pos, count);
        }
    }

    [Serializable]
    public class AppliqueCrypt : ICrypt
    {
        public override byte Decrypt (Xp3Entry entry, long offset, byte value)
        {
            return offset < 5 ? value : (byte)(value ^ (entry.Hash >> 12));
        }

        public override void Decrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            if (offset < 5)
            {
                int skip = Math.Min (5 - (int)offset, count);
                offset += skip;
                pos += skip;
                count -= skip;
            }
            byte key = (byte)(entry.Hash >> 12);
            for (int i = 0; i < count; ++i)
            {
                values[pos+i] ^= key;
            }
        }

        public override void Encrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            Decrypt (entry, offset, values, pos, count);
        }
    }

    [Serializable]
    public class TokidokiCrypt : ICrypt
    {
        public override bool HashAfterCrypt { get { return true; } }

        public override void Decrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            uint key;
            uint limit = GetParameters (entry, out key);
            for (int i = 0; i < count && offset < limit; ++i, ++offset)
            {
                values[pos+i] ^= (byte)(key >> (((int)offset & 3) << 3));
            }
        }

        public override void Encrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            Decrypt (entry, offset, values, pos, count);
        }

        uint GetParameters (Xp3Entry entry, out uint key)
        {
            var ext = System.IO.Path.GetExtension (entry.Name);
            if (!string.IsNullOrEmpty (ext))
            {
                ext = ext.ToLowerInvariant();
                var ext_bin = new byte[16];
                Encodings.cp932.GetBytes (ext, 0, Math.Min (4, ext.Length), ext_bin, 0);
                key = ~LittleEndian.ToUInt32 (ext_bin, 0);
                if (".asd\0.ks\0.tjs\0".Contains (ext+'\0'))
                    return entry.Size;
            }
            else
                key = uint.MaxValue;
            return Math.Min (entry.Size, 0x100u);
        }
    }

    [Serializable]
    public class SourireCrypt : ICrypt
    {
        public override byte Decrypt (Xp3Entry entry, long offset, byte value)
        {
            return (byte)(value ^ entry.Hash ^ 0xCD);
        }

        public override void Decrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            byte key = (byte)(entry.Hash ^ 0xCD);
            for (int i = 0; i < count; ++i)
            {
                values[pos+i] ^= key;
            }
        }

        public override void Encrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            Decrypt (entry, offset, values, pos, count);
        }
    }

    [Serializable]
    public class HibikiCrypt : ICrypt
    {
        public override byte Decrypt (Xp3Entry entry, long offset, byte value)
        {
            if (0 != (offset & 4) || offset <= 0x64)
                return (byte)(value ^ (entry.Hash >> 5));
            else
                return (byte)(value ^ (entry.Hash >> 8));
        }

        public override void Decrypt (Xp3Entry entry, long offset, byte[] buffer, int pos, int count)
        {
            byte key1 = (byte)(entry.Hash >> 5);
            byte key2 = (byte)(entry.Hash >> 8);
            for (int i = 0; i < count; ++i, ++offset)
            {
                if (0 != (offset & 4) || offset <= 0x64)
                    buffer[pos+i] ^= key1;
                else
                    buffer[pos+i] ^= key2;
            }
        }

        public override void Encrypt (Xp3Entry entry, long offset, byte[] buffer, int pos, int count)
        {
            Decrypt (entry, offset, buffer, pos, count);
        }
    }

    [Serializable]
    public class AkabeiCrypt : ICrypt
    {
        private readonly uint m_seed;

        public AkabeiCrypt (uint seed)
        {
            m_seed = seed;
        }

        public override byte Decrypt (Xp3Entry entry, long offset, byte value)
        {
            int key_pos = (int)offset & 0x1F;
            var key = GetKey (entry.Hash).ElementAt (key_pos);
            return (byte)(value ^ key);
        }

        public override void Decrypt (Xp3Entry entry, long offset, byte[] buffer, int pos, int count)
        {
            var key = GetKey (entry.Hash).ToArray();
            int key_pos = (int)offset;
            for (int i = 0; i < count; ++i)
            {
                buffer[pos+i] ^= key[key_pos++ & 0x1F];
            }
        }

        public override void Encrypt (Xp3Entry entry, long offset, byte[] buffer, int pos, int count)
        {
            Decrypt (entry, offset, buffer, pos, count);
        }

        IEnumerable<byte> GetKey (uint hash)
        {
            hash = (hash ^ m_seed) & 0x7FFFFFFF;
            hash = hash << 31 | hash;
            for (int i = 0; i < 0x20; ++i)
            {
                yield return (byte)hash;
                hash = (hash & 0xFFFFFFFE) << 23 | hash >> 8;
            }
        }
    }

    [Serializable]
    public class HaikuoCrypt : ICrypt
    {
        public override byte Decrypt (Xp3Entry entry, long offset, byte value)
        {
            return (byte)(value ^ entry.Hash ^ (entry.Hash >> 8));
        }

        public override void Decrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            byte key = (byte)(entry.Hash ^ (entry.Hash >> 8));
            for (int i = 0; i < count; ++i)
            {
                values[pos+i] ^= key;
            }
        }

        public override void Encrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            Decrypt (entry, offset, values, pos, count);
        }
    }

    [Serializable]
    public class StripeCrypt : ICrypt
    {
        readonly byte   m_key;

        public StripeCrypt (byte key)
        {
            m_key = key;
        }

        public override byte Decrypt (Xp3Entry entry, long offset, byte value)
        {
            return (byte)((value ^ m_key) + 1);
        }

        public override void Decrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            for (int i = 0; i < count; ++i)
            {
                values[pos+i] ^= m_key;
                values[pos+i] ++;
            }
        }

        public override void Encrypt (Xp3Entry entry, long offset, byte[] values, int pos, int count)
        {
            for (int i = 0; i < count; ++i)
            {
                values[pos+i] --;
                values[pos+i] ^= m_key;
            }
        }
    }
}
