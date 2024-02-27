using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using static EA_ADPCM_XAS_CSharp.XASStruct;

namespace EA_ADPCM_XAS_CSharp
{

	internal  unsafe static class SIMD
	{
		
		static Vector128<uint> GetOnes128()
		{
			Vector128<uint> undef = Vector128<uint>.Zero;
			return Sse2.CompareEqual(undef, undef);
		}
		static Vector128<int> LoadUnaligned(int* ptr){
			return Sse2.LoadVector128(ptr);
		}
		static Vector128<int> LoadByIndex(Vector128<int> indexes, int[] mem) 
		{
			Vector128<int> tmp;
			tmp = Sse2.ConvertScalarToVector128Int32(mem[Sse41.Extract(indexes, 0)]);
			tmp = Sse41.Insert(tmp, mem[Sse41.Extract(indexes, 1)], 1);
			tmp = Sse41.Insert(tmp, mem[Sse41.Extract(indexes, 2)], 2);
			tmp = Sse41.Insert(tmp, mem[Sse41.Extract(indexes, 3)], 3);
			return tmp;
		}
		static Vector128<short> Clip_int16(Vector128<int> a)
		{
			return Sse41.PackSignedSaturate(a, a);
		}
		static int[] MIOS(short[] shorts,int start_index)
		{
			short[] shorts1 = new short[shorts.Length-start_index];
			Array.Copy(shorts,start_index,shorts1,0,shorts1.Length);
			return ShortArrayToIntArray(shorts1);
		}
		static void RA_SHIFT_ELEMENT(ref Vector128<int> RES, Vector128<int> VAL, Vector128<int> SHIFT,byte NUM_EL)
		{
			RES = Sse41.Insert(RES, Sse41.Extract(VAL, NUM_EL) >> Sse41.Extract(SHIFT, NUM_EL), NUM_EL);
		}
		static Vector128<int> RIGHT(Vector128<int> _val, Vector128<int> _sh)
		{
			Vector128<int> res = Sse2.ConvertScalarToVector128Int32(Sse2.ConvertToInt32(_val) >> Sse2.ConvertToInt32(_sh));

			RA_SHIFT_ELEMENT(ref res, _val, _sh, 1);
			RA_SHIFT_ELEMENT(ref res, _val, _sh, 2);
			RA_SHIFT_ELEMENT(ref res, _val, _sh, 3);

			return res;
		}
		static int[] SaveWithStep(Vector128<int> vect, int[] mem, int step)
		{
			mem[0] = Sse41.Extract(vect, 0);
			mem[step] = Sse41.Extract(vect, 1);
			mem[step * 2] = Sse41.Extract(vect, 2);
			mem[step * 3] = Sse41.Extract(vect, 3);
			return mem;
		}
		static int[] ShortArrayToIntArray(short[] shorts)
		{
			int[] ints = new int[shorts.Length/2];
			int index = 0;
            for (int i = 0; i < shorts.Length; i+=2,index++)
            {
				ints[index] = shorts[i] << 16 + shorts[i];
            }
			return ints;
        }
		static short[] IntArrayToShortArray(int[] ints)
		{
			short[] shorts = new short[ints.Length*2];
			int index = 0;
            for (int i = 0; i < ints.Length; i++)
            {
				byte[] temp = BitConverter.GetBytes(ints[i]);
				shorts[index] = (short)(temp[0] << 8 + temp[1]);
				index++;
				shorts[index] = (short)(temp[2] << 8 + temp[3]);
				index++;
			}
			return shorts;
        }
		public static void decode_XAS_Chunk_SIMD(XAS_Chunk in_chunk,ref short[] out_PCM)
		{
			int[] const_shift = [16 - fixed_point_offset, 16 - fixed_point_offset, 16 - fixed_point_offset, 16 - fixed_point_offset];
			byte[] shuffle = [12, 8, 4, 0, 13, 9, 5, 1, 14, 10, 6, 2, 15, 11, 7, 3];
			int[] int_data = new int[4];
            for (int i = 0; i < 4; i++)
            {
				int_data[i] = (int)in_chunk.headers[i].data;
            }
			Vector128<int> head = Vector128.Create(int_data);
			Vector128<uint> rounding = GetOnes128();
			Vector128<uint> coef_mask = rounding >> 30;
			Vector128<int> nibble_mask = rounding.AsInt32() << 28;
			rounding = (rounding >> 31 << (fixed_point_offset - 1));
			Vector128<short> samples = head.AsInt16() >> 4 << 4;
			Vector128<int> shift = Vector128.Create(const_shift) + ((head << 12) >> 28);
			Vector128<int> coef_index = head & coef_mask.AsInt32();
			int[] ints = new int[4];
            for (int i = 0; i <ea_adpcm_table_v3.Length; i++)
            {
				ints[i] = ShortArrayToIntArray(ea_adpcm_table_v3[i])[0];
			}
            Vector128<short> coefs = LoadByIndex(coef_index, ints).AsInt16();
			out_PCM = IntArrayToShortArray(SaveWithStep(samples.AsInt32(), ShortArrayToIntArray(out_PCM), 16));


            for (int i = 0; i < 4; i++)
            {
				
				Vector128<int> data = Vector128.Load((int*)((&in_chunk)->XAS_data[0][i * 16]));
				data = Ssse3.Shuffle(data.AsByte(), Vector128.Create(shuffle)).AsInt32();
				int itrs = 4 - ((i + 1) >> 2);
                for (int j = 0; j < itrs; j++)
                {
                    for (int k = 0; k < 2; k++)
                    {
						Vector128<int> prediction = Sse2.MultiplyAddAdjacent(samples, coefs);
						Vector128<int> correction = RIGHT((data & nibble_mask).AsInt32(),shift);
						Vector128<int> predecode = (prediction + correction + rounding.AsInt32()) >> fixed_point_offset;
						Vector128<short> decoded = Clip_int16(predecode);
						samples = ((samples.AsInt32() >> 16) | (decoded.AsInt32() << 16)).AsInt16();
						data <<= 4;
					}
					SaveWithStep(samples.AsInt32(), MIOS(out_PCM,i * 8 + j * 2 + 2), 16);
				}
            }
        }
	}
}
