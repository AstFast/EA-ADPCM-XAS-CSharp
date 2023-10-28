using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace EA_ADPCM_CSharp
{
	public static class EA_ADPCM_XAS
	{
		public static unsafe void DecodeXAS(void* in_XAS, void* out_PCM, uint n_samples_per_channel, uint n_channels)
		{
			EA_ADPCM_XAS_Work.decode_XAS(in_XAS, (short*)out_PCM, n_samples_per_channel, n_channels);
		}

		public static unsafe void EncodeXAS(byte[]* out_XAS, short* in_PCM, uint n_samples_per_channel, uint n_channels)
		{
			EA_ADPCM_XAS_Work.encode_XAS(out_XAS, in_PCM, n_samples_per_channel, n_channels);
		}

		public static unsafe uint GetXASEncodedSize(uint n_samples_per_channel, uint n_channels)
		{
			return EA_ADPCM_XAS_Work.GetXASEncodedSize(n_samples_per_channel, n_channels);
		}
	}
	class EA_ADPCM_XAS_Work:Constant
	{
		
		private static sbyte Clip_int4(sbyte val)
		{
			if (val >= 7) return 7;
			if (val <= -8) return -8;
			return val;
		}

		private static short Clip_int16(int val)
		{
			return (short)((val >= 0x7FFF) ? 0x7FFF : (val <= -0x8000) ? -0x8000 : val);
		}

		// ~same method as in SX but with fixed point
		private static unsafe int simple_CalcCoefShift(short* pSamples, short* in_prevSamples, int num_samples, int* out_coef_index, byte* out_shift)
		{
			// SX using clip here

			const int num_coefs = 4;

			int min_max_error = int.MaxValue;
			int s_min_max_error = int.MaxValue; // don't need I think
			int best_coef_ind = 0;
			short* prevSamples = stackalloc short[2];

			for (int coef_ind = 0; coef_ind < num_coefs; coef_ind++)
			{
				prevSamples[0] = in_prevSamples[0];
				prevSamples[1] = in_prevSamples[1];

				// fixed point 24.8
				// for coef_ind = 0 max_error = max abs sample
				int max_error = 0;
				int s_max_error = 0;
				for (int i = 0; i < num_samples; i++)
				{
					int prediction = ea_adpcm_table_v2[coef_ind][0] * prevSamples[1] + ea_adpcm_table_v2[coef_ind][1] * prevSamples[0];
					int sample = pSamples[i];
					sample <<= 8;
					int s_error = sample - prediction;
					int error = Math.Abs(s_error);
					if (error > max_error)
					{
						max_error = error;
						s_max_error = s_error;
					}
					prevSamples[0] = prevSamples[1];
					prevSamples[1] = pSamples[i];
				}
				if (max_error < min_max_error)
				{
					min_max_error = max_error;
					best_coef_ind = coef_ind;
					s_min_max_error = s_max_error;
				}
			}
			int max_min_error_i16 = Clip_int16(min_max_error >> 8);

			int mask = 0x4000;
			int exp_shift;
			for (exp_shift = 0; exp_shift < 12; exp_shift++)
			{
				if ((((mask >> 3) + max_min_error_i16) & mask) != 0)
				{
					break;
				}
				mask >>= 1;
			}
			*out_coef_index = best_coef_ind;
			*out_shift = (byte)exp_shift;
			return max_min_error_i16;
		}
		public static unsafe uint GetXASEncodedSize(uint n_samples_per_channel, uint n_channels)
		{
			return GetNumXASTotalChunks(n_samples_per_channel, n_channels) * (uint)sizeof(XAS_Chunk);
		}

		private static uint GetNumXASTotalChunks(uint n_samples_per_channel, uint n_channels)
		{
			return n_channels * _GetNumXASChunks(n_samples_per_channel);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static uint _GetNumXASChunks(uint N_SAMPLES)
		{
			return ((N_SAMPLES + 127) / 128);
		}

		private unsafe struct XAS_Chunk
		{
			public fixed uint headers[4]; // total size 16 bytes, 8 samples
			public fixed byte XAS_data[60]; // data for each 2 samples (1 bytes) interleaved, total size 60 bytes, 120 samples
		}

		private struct XAS_SubChunkHeader
		{
			private ushort data1;

			private ushort data2;

			public uint coef_index
			{
				get
				{
					return (uint)data1 & 0b11;
				}
				set
				{
					Debug.Assert(value < 4);
					data1 = (ushort)(((uint)data1 & ~0b11) | value);
				}
			}

			public uint unused
			{
				get
				{
					return (uint)(data1 >> 2) & 0b11;
				}
				set
				{
					Debug.Assert(value < 4);
					data1 = (ushort)(((uint)data1 & ~(0b11 << 2)) | (value << 2));
				}
			}

			public int sample_0
			{
				get
				{
					if ((data1 & 0b1000000000000000) == 0)
					{
						return (data1 >> 4) & 0b111111111111;
					}
					else
					{
						return ((data1 >> 4) & 0b111111111111) - 4096;
					}
				}
				set
				{
					Debug.Assert(value >= -2048 && value < 2048);
					if (value < 0)
					{
						data1 = ((ushort)((data1 & ~(0b111111111111 << 4)) | ((value + 4096) << 4)));
					}
					else
					{
						data1 = ((ushort)((data1 & ~(0b111111111111 << 4)) | (value << 4)));
					}
				}
			}

			public uint exp_shift
			{
				get
				{
					return (uint)data2 & 0b1111;
				}
				set
				{
					Debug.Assert(value < 16);
					data2 = (ushort)(((uint)data2 & ~0b1111) | value);
				}
			}

			public int sample_1
			{
				get
				{
					if ((data2 & 0b1000000000000000) == 0)
					{
						return (data2 >> 4) & 0b111111111111;
					}
					else
					{
						return ((data2 >> 4) & 0b111111111111) - 4096;
					}
				}
				set
				{
					Debug.Assert(value >= -2048 && value < 2048);
					if (value < 0)
					{
						data2 = ((ushort)((data2 & ~(0b111111111111 << 4)) | ((value + 4096) << 4)));
					}
					else
					{
						data2 = ((ushort)((data2 & ~(0b111111111111 << 4)) | (value << 4)));
					}
				}
			}
		};

		
		private static unsafe short decode_XA_sample(short* prev_samples, short[] coef, sbyte int4, byte shift)
		{
			int correction = (int)int4 << shift;
			int prediction = prev_samples[1] * coef[0] + prev_samples[0] * coef[1];
			return Clip_int16((prediction + correction + 128) >> 8);
		}
		private static unsafe void decode_XAS_Chunk(XAS_Chunk* in_chunk, short* out_PCM)
		{
			for (int j = 0; j < 4; j++)
			{
				short* pSamples = out_PCM + j * 32;

				pSamples[0] = ((short)(((XAS_SubChunkHeader*)in_chunk->headers)[j].sample_0 << 4));
				int coef_index = (int)((XAS_SubChunkHeader*)in_chunk->headers)[j].coef_index;

				pSamples[1] = ((short)(((XAS_SubChunkHeader*)in_chunk->headers)[j].sample_1 << 4));
				byte shift = (byte)(12 + 8 - ((XAS_SubChunkHeader*)in_chunk->headers)[j].exp_shift);

				short[] coef = ea_adpcm_table_v2[coef_index];

				for (int i = 0; i < 15; i++, pSamples += 2)
				{
					SamplesByte data = *(SamplesByte*)&(in_chunk->XAS_data[i * 4 + j]);

					pSamples[2] = decode_XA_sample(pSamples, coef, (sbyte)data.sample0, shift);
					pSamples[3] = decode_XA_sample(pSamples + 1, coef, (sbyte)data.sample1, shift);
				}
			}
		}
		public static unsafe void decode_XAS(void* in_data, short* out_PCM, uint n_samples_per_channel, uint n_channels)
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
					decode_XAS_Chunk(_in_data++, PCM);
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
				decode_XAS_Chunk(_in_data++, PCM);
				for (int sample_ind = 0; sample_ind < samples_remain_per_channel; sample_ind++)
				{
					out_PCM[channel_ind + sample_ind * n_channels] = PCM[sample_ind];
				}
			}
		}
		private static unsafe EncodedSample encode_XA_sample(short* prev_samples, short[] coef, int sample, byte shift)
		{

			int prediction = prev_samples[1] * coef[0] + prev_samples[0] * coef[1];

			int correction = (sample << 8) - prediction;


			int res;
			int rounding = 1 << (shift - 1);
			res = Clip_int4((sbyte)((correction + rounding) >> shift));

			int predecoded = ((res << shift) + prediction + 128) >> 8;
			int decoded = Clip_int16(predecoded);
			int term = 1 << (shift - 8);
			int decoded2;
			decoded2 = Clip_int16(predecoded + term);
			if (res != 7 && Math.Abs(decoded - sample) > Math.Abs(decoded2 - sample))
			{
				res += 1;
				decoded = decoded2;
			}
			else
			{
				decoded2 = Clip_int16(predecoded - term);
				if (res != -8 && Math.Abs(decoded - sample) > Math.Abs(decoded2 - sample))
				{
					res -= 1;
					decoded = decoded2;
				}
			}
			return new EncodedSample { decoded = (short)decoded, encoded = (sbyte)res };
		}
		private static unsafe void encode_XAS_Chunk(XAS_Chunk* out_chunk, short* in_PCM)
		{
			short* decoded_PCM = stackalloc short[32];
			for (int j = 0; j < 4; j++)
			{

				short* pInSamples = in_PCM + j * 32;

				((XAS_SubChunkHeader*)out_chunk->headers)[j].unused = 0;
				((XAS_SubChunkHeader*)out_chunk->headers)[j].sample_0 = (pInSamples[0] + 7) >> 4;
				((XAS_SubChunkHeader*)out_chunk->headers)[j].sample_1 = (pInSamples[1] + 7) >> 4;


				decoded_PCM[0] = (short)(((XAS_SubChunkHeader*)out_chunk->headers)[j].sample_0 << 4);
				decoded_PCM[1] = (short)(((XAS_SubChunkHeader*)out_chunk->headers)[j].sample_1 << 4);

				int coef_index;
				byte shift;
				simple_CalcCoefShift(pInSamples + 2, decoded_PCM, 30, &coef_index, &shift);
				((XAS_SubChunkHeader*)out_chunk->headers)[j].coef_index = (uint)coef_index;
				((XAS_SubChunkHeader*)out_chunk->headers)[j].exp_shift = shift;

				short[] coef = ea_adpcm_table_v2[coef_index];
				shift = (byte)(12 + 8 - shift);

				short* pDecodedSamples = decoded_PCM;

				for (int i = 0; i < 15; i++)
				{
					byte data = 0;

					for (int n = 0; n < 2; n++)
					{
						EncodedSample enc = encode_XA_sample(pDecodedSamples, coef, pInSamples[2], shift);

						pDecodedSamples[2] = enc.decoded; // think as decoder will for better precision
						data <<= 4;
						data |= (byte)(enc.encoded & 0xF);
						pInSamples++;
						pDecodedSamples++;
					}
					out_chunk->XAS_data[i * 4 + j] = data;
				}
			}
		}

		public static unsafe void encode_XAS(byte[]* out_data, short* in_PCM, uint n_samples_per_channel, uint n_channels)
		{
			if (n_samples_per_channel == 0)
			{
				return;
			}
			XAS_Chunk* _out_data = (XAS_Chunk*)out_data;
			uint n_chunks_per_channel = _GetNumXASChunks(n_samples_per_channel);
			short* PCM = stackalloc short[128];
			for (int chunk_ind = 0; chunk_ind < n_chunks_per_channel - 1; chunk_ind++)
			{
				for (int channel_ind = 0; channel_ind < n_channels; channel_ind++)
				{
					short* t = in_PCM + channel_ind;
					for (int sample_ind = 0; sample_ind < 128; sample_ind++, t += n_channels)
					{
						PCM[sample_ind] = *t;
					}
					encode_XAS_Chunk(_out_data++, PCM);

				}
				in_PCM += 128 * n_channels;
			}
			uint samples_remain_per_channel = n_samples_per_channel - (n_chunks_per_channel - 1) * 128;
			for (int channel_ind = 0; channel_ind < n_channels; channel_ind++)
			{
				for (int sample_ind = 0; sample_ind < samples_remain_per_channel; sample_ind++)
				{
					PCM[sample_ind] = in_PCM[channel_ind + sample_ind * n_channels];
				}
				NativeMemory.Clear(PCM + samples_remain_per_channel, (128 - samples_remain_per_channel) * sizeof(short));
				encode_XAS_Chunk(_out_data++, PCM);
			}
		}
	}
}