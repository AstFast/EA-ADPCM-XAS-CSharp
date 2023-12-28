using uint32_t = System.UInt32;
using int32_t = System.Int32;
using int16_t = System.Int16;
using table_type = System.Int16;
//using vec128 = EA_ADPCM_XAS_CSharp.Vec128;
using System.Diagnostics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics;
using EA_ADPCM_XAS_CSharp;
namespace EA_ADPCM_XAS_CSharp_NEW
{
	public static class EA_ADPCM_XAS
	{
		public static unsafe void DecodeXAS(void* in_XAS, void* out_PCM, uint n_samples_per_channel, uint n_channels)
		{
			new EA_ADPCM_XAS_new().decode_XAS(in_XAS, (short*)out_PCM, n_samples_per_channel, n_channels);
		}

		public static unsafe uint GetXASEncodedSize(uint n_samples_per_channel, uint n_channels)
		{
			return new EA_ADPCM_XAS_new().GetXASEncodedSize(n_samples_per_channel, n_channels);
		}
	}
	internal unsafe class Header_Pro
	{
		public const int fixed_point_offset = 8;
		public const int fixp_exponent = 1 << fixed_point_offset;
		public const int subchunks_in_XAS_chunk = 4;
		public const int samples_in_XAS_subchunk = 30;
		public const int samples_in_XAS_header = 2;
		public const int samples_in_XAS_per_subchunk = samples_in_XAS_subchunk + samples_in_XAS_header;
		public const int samples_in_EA_XA_R_chunk = 28;
		public const int sizeof_EA_XA_R1_chunk = 1 + 2 * sizeof(int16_t) + samples_in_EA_XA_R_chunk / 2;
		public const int sizeof_uncompr_EA_XA_R23_block = 1 + (samples_in_EA_XA_R_chunk + 2) * sizeof(int16_t);
		public const int sizeof_compr_EA_XA_R23_block = 1 + samples_in_EA_XA_R_chunk / 2;
		public Int16[,] ea_adpcm_table_v2 ={
		{(Int16) (0.000000*fixp_exponent), (Int16) ( 0.000000*fixp_exponent)},
		{(Int16) (0.937500* fixp_exponent), (Int16) ( 0.000000* fixp_exponent)},
		{ (Int16)(1.796875 * fixp_exponent), (Int16)(-0.812500 * fixp_exponent)},
		{ (Int16)(1.531250 * fixp_exponent), (Int16)(-0.859375 * fixp_exponent)},
		};
		internal struct SamplesByte
		{
			private int data;

			public int sample1
			{
				get
				{
					if ((data & 0b1000) == 0)
					{
						return data & 0b1111;
					}
					else
					{
						return (data & 0b1111) - 16;
					}
				}
				set
				{
					Debug.Assert(value >= -8 && value < 8);
					if (value < 0)
					{
						data = (data & ~0b1111) | (value + 16);
					}
					else
					{
						data = ((data & ~0b1111) | value);
					}
				}
			}

			public int sample0
			{
				get
				{
					if (((data >> 4) & 0b1000) == 0)
					{
						return (data >> 4) & 0b1111;
					}
					else
					{
						return ((data >> 4) & 0b1111) - 16;
					}
				}
				set
				{
					Debug.Assert(value >= -8 && value < 8);
					if (value < 0)
					{
						data = ((data & ~(0b1111 << 4)) | ((value + 16) << 4));
					}
					else
					{
						data = ((data & ~(0b1111 << 4)) | (value << 4));
					}
				}
			}
		}
		public struct SamplesDWORD
		{
			fixed int16_t samples[2];
		};

		public struct XAS_SubChunkHeader
		{
			private int data;

			public uint coef_index
			{
				get { return (uint)(data & 0b11); }
				set
				{
					Debug.Assert(value < 4);
					data = (int)((data & ~0b11) | value);
				}
			}

			public uint unused
			{
				get { return (uint)((data >> 2) & 0b11); }
				set
				{
					Debug.Assert(value < 4);
					data = (int)((data & ~(0b11 << 2)) | (value << 2));
				}
			}

			public int sample_0
			{
				get
				{
					if ((data & 0x8000) == 0)
						return data >> 4;
					else
						return ((data >> 4) & 0xFFF) - 4096;
				}
				set
				{
					Debug.Assert(value >= -2048 && value < 2048);
					if (value < 0)
						data = (data & ~(0xFFF << 4)) | ((value + 4096) << 4);
					else
						data = (data & ~(0xFFF << 4)) | (value << 4);
				}
			}

			public uint exp_shift
			{
				get { return (uint)(data & 0b1111_0000_0000_0000_0000); }
				set
				{
					Debug.Assert(value < 16);
					data = (data & ~0b1111_0000_0000_0000_0000) | ((int)value << 12);
				}
			}

			public int sample_1
			{
				get
				{
					if ((data & 0x80000000) == 0)
						return data >> 16;
					else
						return ((data >> 16) & 0xFFF) - 4096;
				}
				set
				{
					Debug.Assert(value >= -2048 && value < 2048);
					if (value < 0)
						data = (data & ~(0xFFF << 16)) | ((value + 4096) << 16);
					else
						data = (data & ~(0xFFF << 16)) | (value << 16);
				}
			}
		}

		public struct XAS_Chunk
		{
			public fixed int headers[subchunks_in_XAS_chunk];
			public fixed byte data[60];
		}

	}
	internal unsafe class EA_ADPCM_XAS_new : Header_Pro
	{
		private static uint _GetNumXASChunks(uint N_SAMPLES)
		{
			return ((N_SAMPLES + 127) / 128);
		}
		uint32_t GetNumXASTotalChunks(uint32_t n_samples_per_channel, uint32_t n_channels)
		{
			return n_channels * _GetNumXASChunks(n_samples_per_channel);
		}
		public uint32_t GetXASEncodedSize(uint32_t n_samples_per_channel, uint32_t n_channels)
		{
			return GetNumXASTotalChunks(n_samples_per_channel, n_channels) * 76;
		}
		private sbyte Clip_int4(sbyte val)
		{
			if (val >= 7) return 7;
			if (val <= -8) return -8;
			return val;
		}
		private short Clip_int16(int val)
		{
			return (short)((val >= 0x7FFF) ? 0x7FFF : (val <= -0x8000) ? -0x8000 : val);
		}
		unsafe short decode_XA_sample(short* prev_samples, short* coef, sbyte int4, byte shift)
		{
			int correction = (int)int4 << shift;
			int prediction = prev_samples[1] * coef[0] + prev_samples[0] * coef[1];
			return Clip_int16((prediction + correction + 128) >> 8);
		}
		unsafe void decode_XAS_Chunk(XAS_Chunk* in_chunk, int16_t* out_PCM)
		{
			for (int j = 0; j < subchunks_in_XAS_chunk; j++)
			{
				int16_t* pSamples = out_PCM + j * 32;
				pSamples[0] = (short)(((XAS_SubChunkHeader*)(in_chunk->headers[j]))->sample_0 << 4);
				int coef_index = (int)(((XAS_SubChunkHeader*)(in_chunk->headers[j]))->coef_index);
				pSamples[1] = (short)(((XAS_SubChunkHeader*)(in_chunk->headers[j]))->sample_1 << 4);
				byte shift = (byte)(12 + fixed_point_offset - ((XAS_SubChunkHeader*)(in_chunk->headers[j]))->exp_shift);
				table_type* coef = (Int16*)ea_adpcm_table_v2[coef_index, 2];
				for (int i = 0; i < 15; i++, pSamples += 2)
				{
					SamplesByte data = *(SamplesByte*)&(in_chunk->data[i + j * 15]);
					pSamples[2] = decode_XA_sample(pSamples, coef, (sbyte)data.sample0, shift);
					pSamples[3] = decode_XA_sample(pSamples + 1, coef, (sbyte)data.sample1, shift);
				}
			}
		}

		public void decode_XAS(void* in_data, short* out_PCM, uint n_samples_per_channel, uint n_channels)
		{
			if (n_samples_per_channel == 0)
				return;

			XAS_Chunk* _in_data = (XAS_Chunk*)in_data;
			short* PCM = stackalloc short[128];
			uint n_chunks_per_channel = _GetNumXASChunks(n_samples_per_channel);

			for (int chunk_ind = 0; chunk_ind < n_chunks_per_channel - 1; chunk_ind++)
			{
				for (int channel_ind = 0; channel_ind < n_channels; channel_ind++)
				{
					decode_XAS_Chunk_SIMD(_in_data++, PCM);
					for (int sample_ind = 0; sample_ind < 128; sample_ind++)
					{
						out_PCM[channel_ind + sample_ind * n_channels] = PCM[sample_ind];
					}
				}
				out_PCM += 128 * n_channels;
			}

			uint samples_remain_per_channel = n_samples_per_channel - (n_chunks_per_channel - 1) * 128;
			for (int channel_ind = 0; channel_ind < n_channels; channel_ind++)
			{
				decode_XAS_Chunk_SIMD(_in_data++, PCM);
				for (int sample_ind = 0; sample_ind < samples_remain_per_channel; sample_ind++)
				{
					out_PCM[channel_ind + sample_ind * n_channels] = PCM[sample_ind];
				}
			}
		}
		/*
		public static unsafe void SaveWithStep(Int32x4 vect, int* mem, int step)
		{
			mem[0] = *((int*)&vect);
			mem[step] = *((int*)Unsafe.Add(ref *((byte*)&vect), sizeof(int)));
			mem[step * 2] = *((int*)Unsafe.Add(ref *((byte*)&vect), sizeof(int) * 2));
			mem[step * 3] = *((int*)Unsafe.Add(ref *((byte*)&vect), sizeof(int) * 3));
		}
		*/
		public static Int32x4 Mul16Add32(Int16x8 a, Int16x8 b)
		{
			Vector128<int> result = Sse2.MultiplyAddAdjacent(a.I128, b.I128);
			return new Int32x4 { I128 = result };
		}

		public static Int16x8 ClipInt16(Int32x4 a)
		{
			Vector128<short> result = Sse2.PackSignedSaturate(a.I128, a.I128);
			return new Int16x8 { I128 = result };
		}
		public static Int16x8 Clip_int16(Int32x4 a)
		{
			return new Int16x8 {I128 = Sse2.PackSignedSaturate(a.I128, a.I128) };
		}
		public Int32x4 LoadByIndex(Int32x4 indexes, int* mem)
		{
			Vector128<int> tmp = Vector128.Create(
				mem[indexes.GetElement(0)],
				mem[indexes.GetElement(1)],
				mem[indexes.GetElement(2)],
				mem[indexes.GetElement(3)]
			);
			return new Int32x4 { I128 = tmp };
		}
		public void SaveWithStep(Int32x4 vect, int* mem, int step)
		{
			mem[0] = vect.GetElement(0);
			mem[step] = vect.GetElement(1);
			mem[step * 2] = vect.GetElement(2);
			mem[step * 3] = vect.GetElement(3);
		}

		public void SaveWithStepLow4(Int16x8 vect, short* mem, int step)
		{
			mem[0] = vect.GetElement(0); mem += step;
			mem[0] = vect.GetElement(1); mem += step;
			mem[0] = vect.GetElement(2); mem += step;
			mem[0] = vect.GetElement(3);
		}

		public static Vector128<int> PermuteByIndex(Vector128<int> vect, Vector128<int> index)
		{
			return Ssse3.Shuffle(vect.AsByte(), index.AsByte()).AsInt32();
		}
		unsafe void decode_XAS_Chunk_SIMD(XAS_Chunk* in_chunk, short* out_PCM)
		{
			//Vector128<int> head = Sse2.LoadVector128(address: in_chunk->headers);
			Vec128 head = Sse2.LoadVector128(in_chunk->headers);
			table_type[][] ea_adpcm_table_v3 = new table_type[][]
			{
				new table_type[] { (table_type)(0.000000 * fixp_exponent), (table_type)(0.000000 * fixp_exponent) },
				new table_type[] { (table_type)(0.000000 * fixp_exponent), (table_type)(0.937500 * fixp_exponent) },
				new table_type[] { (table_type)(-0.812500 * fixp_exponent), (table_type)(1.796875 * fixp_exponent) },
				new table_type[] { (table_type)(-0.859375 * fixp_exponent), (table_type)(1.531250 * fixp_exponent) }
			};
			int32_t[] const_shift = { 16 - fixed_point_offset, 16 - fixed_point_offset, 16 - fixed_point_offset, 16 - fixed_point_offset };
			byte[] shuffle = { 12, 8, 4, 0, 13, 9, 5, 1, 14, 10, 6, 2, 15, 11, 7, 3 };

			UInt32x4 rounding = (UInt32x4)Vec128Extensions.GetOnes128();
			UInt32x4 coef_mask = rounding >> 30;
			Int32x4 nibble_mask = (Int32x4)rounding << 28;
			rounding = ((Int32x4)(rounding >> 31) << (fixed_point_offset - 1)).SIMD_reinterpret_cast<UInt32x4>();

			Int16x8 samples = head.SIMD_reinterpret_cast<Int16x8>();
			samples = samples >> 4 << 4;
			Int32x4 shift = head;
			shift = *(Int32x4*)&const_shift + (Int32x4)((shift << 12).SIMD_reinterpret_cast<UInt32x4>() >> 28);
			Int32x4 coef_index = (Int32x4)(head.I128 & coef_mask);
			Int16x8 coefs = LoadByIndex(coef_index, (int*)&ea_adpcm_table_v3).SIMD_reinterpret_cast<Int16x8>();

			SaveWithStep(samples.SIMD_reinterpret_cast<Int32x4>(), (int*)out_PCM, 16);
			Vec128 _shuffle = *(Vec128*)&shuffle;

			for (int i = 0; i < 4; i++)
			{
				Int32x4 data = Vec128Extensions.LoadUnaligned((int*)&in_chunk->data[i * 16]).SIMD_reinterpret_cast<Int32x4>();
				var data_1 = (Vec128)(PermuteByIndex((Vec128)data, _shuffle));
				data = data_1.SIMD_reinterpret_cast<Int32x4>();

				int itrs = 4 - ((i + 1) >> 2);

				for (int j = 0; j < itrs; j++)
				{
					for (int k = 0; k < 2; k++)
					{
						Int32x4 prediction = Mul16Add32(samples, coefs);
						Int32x4 correction = (data & nibble_mask).SIMD_reinterpret_cast<Int32x4>() >> shift;
						Int32x4 predecode = (prediction + correction + rounding) >> fixed_point_offset;
						Int16x8 decoded = Clip_int16(predecode);
						samples = ClipInt16((Vec128)(samples.SIMD_reinterpret_cast<UInt32x4>() >> 16) | (Vec128)(((Int32x4)decoded.SIMD_reinterpret_cast<UInt16x8>()) << 16));
						data = data << 4;
					}
					SaveWithStep(samples.SIMD_reinterpret_cast<Int32x4>(), (int*)(out_PCM + i * 8 + j * 2 + 2), 16);
				}
			}
		}
	}
}
