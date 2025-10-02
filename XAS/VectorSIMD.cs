
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using static EA_ADPCM_XAS_CSharp.XASStruct;

namespace EA_ADPCM_XAS_CSharp
{
    internal class VectorSIMD
    {
        static Vector128<int> GetOnes128()
        {
            Vector128<int> zeroVector = Vector128<int>.Zero;
            return Sse2.CompareEqual(zeroVector, zeroVector);
        }
        static Vector128<int> ShiftRightArithmetic(Vector128<int> value, Vector128<int> count)
        {
            if (Avx2.IsSupported)
            {
                return Avx2.ShiftRightArithmetic(value, count);
            }
            int val0 = value.GetElement(0);
            int sh0 = count.GetElement(0);
            Vector128<int> result = Vector128.Create(val0 >> sh0);
            result = result.WithElement(1, value.GetElement(1) >> count.GetElement(1));
            result = result.WithElement(2, value.GetElement(2) >> count.GetElement(2));
            result = result.WithElement(3, value.GetElement(3) >> count.GetElement(3));
            return result;
        }
        static Vector128<int> LoadByIndex(Vector128<int> indexes, int[] mem)
        {
            if (Avx.IsSupported)
            {
                return Avx.PermuteVar(Vector128.Create(mem).AsSingle(),indexes).AsInt32();
            }
            Vector128<int> tmp;
            tmp = Sse2.ConvertScalarToVector128Int32(mem[Sse41.Extract(indexes, 0)]);
            tmp = Sse41.Insert(tmp, mem[Sse41.Extract(indexes, 1)], 1);
            tmp = Sse41.Insert(tmp, mem[Sse41.Extract(indexes, 2)], 2);
            tmp = Sse41.Insert(tmp, mem[Sse41.Extract(indexes, 3)], 3);
            return tmp;
        }
        static void SaveWithStep(Vector128<int> vect,ref int[] mem, int step)
        {
            mem[0] = Sse41.Extract(vect, 0);
            mem[step] = Sse41.Extract(vect, 1);
            mem[step * 2] = Sse41.Extract(vect, 2);
            mem[step * 3] = Sse41.Extract(vect, 3);
        }
        static void SaveWithStep(Vector128<int> vect,Span<int> mem, int step)
        {
            mem[0] = Sse41.Extract(vect, 0);
            mem[step] = Sse41.Extract(vect, 1);
            mem[step * 2] = Sse41.Extract(vect, 2);
            mem[step * 3] = Sse41.Extract(vect, 3);
        }
        static Vector128<byte> PermuteByIndex(Vector128<byte> vect, Vector128<byte> index)
        {
            return Ssse3.Shuffle(vect, index);
        }
        public static unsafe void decode_EA_XAS_Chunk_SIMD(XAS_Chunk in_chunk, Span<short> out_PCM)
        {

            //Vector128<int> head = LoadUnaligned((int*)&in_chunk.headers);
            Vector128<int> head = Vector128.Load(in_chunk.headers);
            Vector128<uint> rounding = GetOnes128().AsUInt32();

            Vector128<uint> coef_mask = rounding >> 30;
            Vector128<int> nibble_mask = (rounding << 28).AsInt32();

            rounding = (rounding >> 31 << (fixed_point_offset - 1));

            Vector128<short> samples = head.AsInt16();
            samples = samples >> 4 << 4;

            Vector128<int> shift = head;
            shift = Vector128.Create(const_shift) + ((shift << 12) >> 28);
            Vector128<int> coef_index = head & coef_mask.AsInt32();
            Vector128<short> coefs = LoadByIndex(coef_index, ea_adpcm_table_v3_const).AsInt16();

            SaveWithStep(samples.AsInt32(),MemoryMarshal.Cast<short,int>(out_PCM), 16);

            Vector128<int> _shuffle = Vector128.Create(XASStruct.shuffle).AsInt32();

            for (int i = 0; i < 4; i++)
            {
                Vector128<int> data = Vector128.Load(in_chunk.XAS_data + i*16).AsInt32();

                data = PermuteByIndex(data.AsByte(), _shuffle.AsByte()).AsInt32();

                int itrs = 4 - ((i + 1) >> 2); // i != 3 ? 4 : 3;

                for (int j = 0; j < itrs; j++)
                {
                    for (int k = 0; k < 2; k++)
                    {
                        Vector128<int> prediction = Sse2.MultiplyAddAdjacent(samples, coefs);
                        Vector128<int> correction = ShiftRightArithmetic((data & nibble_mask) , shift);
                        Vector128<int> predecode = (prediction + correction + rounding.AsInt32()) >> fixed_point_offset;
                        Vector128<short> decoded = Sse2.PackSignedSaturate(predecode,predecode);
                        samples = (samples >> 16) | (decoded << 16);
                        data = data << 4;
                    }
                    SaveWithStep(samples.AsInt32(), MemoryMarshal.Cast<short, int>(out_PCM.Slice(i * 8 + j * 2 + 2)), 16);
                }
            }

        }

    }
}
