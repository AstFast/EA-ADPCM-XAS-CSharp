using int32x4 = System.Runtime.Intrinsics.Vector128<int>;
using uint32x4 = System.Runtime.Intrinsics.Vector128<uint>;
using int16x8 = System.Runtime.Intrinsics.Vector128<short>;
using static EA_ADPCM_XAS_CSharp.XASStruct;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace EA_ADPCM_XAS_CSharp
{
	internal unsafe class VectorSIMD
	{
		static Vector128<uint> GetOnes128()
		{
			Vector128<uint> undef = Vector128<uint>.Zero;
			return Sse2.CompareEqual(undef, undef);
		}
		static int32x4 LoadByIndex(int32x4 indexes, int[] mem)
		{
			Vector128<int> tmp;
			tmp = Sse2.ConvertScalarToVector128Int32(mem[Sse41.Extract(indexes, 0)]);
			tmp = Sse41.Insert(tmp, mem[Sse41.Extract(indexes, 1)], 1);
			tmp = Sse41.Insert(tmp, mem[Sse41.Extract(indexes, 2)], 2);
			tmp = Sse41.Insert(tmp, mem[Sse41.Extract(indexes, 3)], 3);
			return tmp;
		}
		static void SaveWithStep(int32x4 vect, ref short[] mem, int step)
		{

			short* temp;
			temp = (short*)Sse41.Extract(vect, 0);
			mem[0] = temp[0];
			mem[1] = temp[1];
			temp = (short*)Sse41.Extract(vect, 1);
			mem[step * 2] = temp[0];
			mem[step * 2 + 1] = temp[1];
			temp = (short*)Sse41.Extract(vect, 2);
			mem[step * 2 * 2] = temp[0];
			mem[step * 2 * 2 + 1] = temp[1];
			temp = (short*)Sse41.Extract(vect, 3);
			mem[step * 2 * 3] = temp[0];
			mem[step * 2 * 3 + 1] = temp[0];
		}
		static void SaveWithStep(int32x4 vect, ref short[] mem, int index, int step)
		{
			short* temp;
			temp = (short*)Sse41.Extract(vect, 0);
			mem[0 + index] = temp[0];
			mem[1 + index] = temp[1];
			temp = (short*)Sse41.Extract(vect, 1);
			mem[step * 2 + index] = temp[0];
			mem[step * 2 + 1 + index] = temp[1];
			temp = (short*)Sse41.Extract(vect, 2);
			mem[step * 2 * 2 + index] = temp[0];
			mem[step * 2 * 2 + 1 + index] = temp[1];
			temp = (short*)Sse41.Extract(vect, 3);
			mem[step * 2 * 3 + index] = temp[0];
			mem[step * 2 * 3 + 1 + index] = temp[0];
		}
		static void SaveWithStep(int32x4 vect, ref int[] mem, int step)
		{
			mem[0] = Sse41.Extract(vect, 0);
			mem[step] = Sse41.Extract(vect, 1);
			mem[step * 2] = Sse41.Extract(vect, 2);
			mem[step * 3] = Sse41.Extract(vect, 3);
		}
		static Vector128<int> PermuteByIndex(Vector128<int> vect, Vector128<int> index)
		{
			return Ssse3.Shuffle(vect.AsByte(), index.AsByte()).AsInt32();
		}
		static int16x8 Clip_int16(int32x4 a)
		{
			return Sse2.PackSignedSaturate(a, a);
		}
		static int32x4 mul16_add32(int16x8 a, int16x8 b)
		{
			return Sse2.MultiplyAddAdjacent(a, b);
		}
		static Vector128<int> RightShiftElements(Vector128<int> left, Vector128<int> right)
		{
			Vector128<int> result = Vector128<int>.Zero;
			for (int i = 0; i < Vector128<int>.Count; i++)
			{
				int shiftedValue = left.GetElement(i) >> right.GetElement(i);
				result = result.WithElement(i, shiftedValue);
			}
			return result;
		}
#if NET7_0
		public static void decode_XAS_Chunk_SIMD(XAS_Chunk in_chunk, ref short[] out_PCM)
		{
			uint[] __temp = new uint[in_chunk.headers.Length];
			for (int i = 0; i < __temp.Length; i++)
			{
				__temp[i] = in_chunk.headers[i].data;
			}
			Vector128<int> head = Vector128.Create(__temp).AsInt32();
			uint32x4 rounding = Vector128<uint>.AllBitsSet;
			uint32x4 coef_mask = Vector128.ShiftRightLogical(rounding,30);
			int32x4 nibble_mask = Vector128.ShiftLeft(rounding.AsInt32(),28);
			rounding = Vector128.ShiftLeft(Vector128.ShiftRightLogical(rounding,31),(fixed_point_offset - 1));
			int16x8 samples = Vector128.ShiftLeft(Vector128.ShiftRightArithmetic(head.AsInt16(),4),4);
			int32x4 shift = Vector128.ShiftRightArithmetic(Vector128.Create<int>(const_shift) + Vector128.ShiftLeft(head,12),28);
			int32x4 coef_index = head & coef_mask.AsInt32();
			int16x8 coefs = LoadByIndex(coef_index, ea_adpcm_table_v4).AsInt16();
			SaveWithStep(samples.AsInt32(), ref out_PCM, 16);
			Vector128<int> _shuffle = Vector128.Create<byte>(shuffle).AsInt32();
            for (int i = 0; i < 4; i++)
            {
				int[] bytes = new int[4];
				for (int j = 0; j < 4; j++)
				{
					bytes[j] = BitConverter.ToInt32(in_chunk.XAS_data[i * 4 + j], 0);
				}
				int32x4 data = Vector128.Create(bytes);
				data = PermuteByIndex(data, _shuffle).AsInt32();
				int itrs = 4 - ((i + 1) >> 2);
                for (int j = 0; j < itrs; j++)
                {
                    for (int k = 0; k < 2; k++)
                    {
						int32x4 prediction = mul16_add32(samples, coefs);
						int32x4 correction = RightShiftElements((data & nibble_mask), shift);
						int32x4 predecode = Vector128.ShiftRightArithmetic((prediction + correction + rounding.AsInt32()),fixed_point_offset);
						int16x8 decoded = Clip_int16(predecode);
						samples = Vector128.ShiftRightArithmetic(samples,16) | Vector128.ShiftLeft(decoded,16);
						data = Vector128.ShiftLeft(data , 4);
					}
					SaveWithStep(samples.AsInt32(), ref out_PCM, i * 8 + j * 2 + 2, 16);
				}
				
			}
        }
#else
		public static void decode_XAS_Chunk_SIMD(XAS_Chunk in_chunk, ref short[] out_PCM)
		{
			Vector128<int> head = Vector128.LoadUnsafe<XAS_SubChunkHeader[]>(ref in_chunk.headers).AsInt32();
			uint32x4 rounding = Vector128<uint>.AllBitsSet;
			uint32x4 coef_mask = rounding >> 30;
			int32x4 nibble_mask = rounding.AsInt32() << 28;
			rounding = rounding >> 31 << (fixed_point_offset - 1);
			int16x8 samples = head.AsInt16() >> 4 << 4;
			int32x4 shift = Vector128.Create<int>(const_shift) + ((head << 12) >> 28);
			int32x4 coef_index = head & coef_mask.AsInt32();
			int16x8 coefs = LoadByIndex(coef_index, ea_adpcm_table_v4).AsInt16();
			SaveWithStep(samples.AsInt32(), ref out_PCM, 16);
			Vector128<int> _shuffle = Vector128.Create<byte>(shuffle).AsInt32();
            for (int i = 0; i < 4; i++)
            {
				int[] bytes = new int[4];
				for (int j = 0; j < 4; j++)
				{
					bytes[j] = BitConverter.ToInt32(in_chunk.XAS_data[i * 4 + j], 0);
				}
				int32x4 data = Vector128.Create(bytes);
				data = PermuteByIndex(data, _shuffle).AsInt32();
				int itrs = 4 - ((i + 1) >> 2);
                for (int j = 0; j < itrs; j++)
                {
                    for (int k = 0; k < 2; k++)
                    {
						int32x4 prediction = mul16_add32(samples, coefs);
						int32x4 correction = RightShiftElements((data & nibble_mask), shift);
						int32x4 predecode = (prediction + correction + rounding.AsInt32()) >> fixed_point_offset;
						int16x8 decoded = Clip_int16(predecode);
						samples = (samples >> 16) | (decoded << 16);
						data = data << 4;
					}
					SaveWithStep(samples.AsInt32(), ref out_PCM, i * 8 + j * 2 + 2, 16);
				}
				
			}
        }
		#endif
	}
}
