using System.Runtime.InteropServices;
using static EA_ADPCM_XAS_CSharp.XASStruct;
namespace EA_ADPCM_XAS_CSharp
{
	internal unsafe partial class EAAudio
	{
		public partial class XAS
		{
			#region v1
			static int simple_CalcCoefShift(in short[] pSamples, long index,in short[] in_prevSamples, int num_samples, out int out_coef_index, out byte out_shift)
			{
				const int num_coefs = 4;

				int min_max_error = int.MaxValue;
				int s_min_max_error = int.MaxValue;
				int best_coef_ind = 0;
				for (int coef_ind = 0; coef_ind < num_coefs; coef_ind++)
				{
					short[] prevSamples = new short[] { in_prevSamples[0], in_prevSamples[1] };
					int max_error = 0;
					int s_max_error = 0;
					for (int i = 0; i < num_samples; i++)
					{
						int prediction = ea_adpcm_table_v2[coef_ind][0] * prevSamples[1] + ea_adpcm_table_v2[coef_ind][1] * prevSamples[0];
						int sample = pSamples[i + index];
						sample <<= fixed_point_offset;
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
				int max_min_error_i16 = Clip_int16(min_max_error >> fixed_point_offset);

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
				out_coef_index = best_coef_ind;
				out_shift = (byte)exp_shift;
				return max_min_error_i16;
			}
			static EncodedSample encode_XA_sample(in short[] prev_samples, int index, short[] coef, int sample, int shift)
			{
				int prediction = prev_samples[1 + index] * coef[0] + prev_samples[index] * coef[1];

				int correction = (sample << fixed_point_offset) - prediction;
				int res;
				int rounding = 1 << (shift - 1);
				res = Clip_int4((correction + rounding) >> shift);

				int predecoded = ((res << shift) + prediction + def_rounding) >> fixed_point_offset;
				int decoded = Clip_int16(predecoded);
				int term = 1 << (shift - fixed_point_offset);
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
					if (res != 8 && Math.Abs(decoded - sample) > Math.Abs(decoded2 - sample))
					{
						res -= 1;
						decoded = decoded2;
					}
				}
				return new EncodedSample { decoded = (Int16)decoded, encoded = (byte)res };
			}
			static void encode_XAS_Chunk(ref XAS_Chunk out_chunk,in short[] in_PCM)
			{
				out_chunk.headers = new XAS_SubChunkHeader[4];
				out_chunk.XAS_data = new byte[15][];
				for (int i = 0; i < 15; i++)
				{
					out_chunk.XAS_data[i] = new byte[4];
				}
				short[] decoded_PCM = new short[32];
				for (int j = 0; j < subchunks_in_XAS_chunk; j++)
				{
					short[] pInSamples = new short[32];
					for (int i = 0; i < 32; i++)
					{
						pInSamples[i] = in_PCM[j * 32 + i];
					}
					out_chunk.headers[j].unused = 0;
					out_chunk.headers[j].sample_0 = (pInSamples[0] + shift4_rounding) >> 4;
					out_chunk.headers[j].sample_1 = (pInSamples[1] + shift4_rounding) >> 4;

					decoded_PCM[0] = (short)(out_chunk.headers[j].sample_0 << 4);
					decoded_PCM[1] = (short)(out_chunk.headers[j].sample_1 << 4);
					simple_CalcCoefShift(pInSamples, 2, decoded_PCM, 30, out int coef_index, out byte shift);
					out_chunk.headers[j].coef_index = (uint)coef_index;
					out_chunk.headers[j].exp_shift = shift;
					short[] coef = ea_adpcm_table_v2[coef_index];
					short[] pDecodedSamples = decoded_PCM;
					int index = 0;
					shift = (byte)(12 + fixed_point_offset - shift);
					for (int i = 0; i < 15; i++)
					{
						byte data = 0;
						for (int n = 0; n < 2; n++)
						{
							EncodedSample enc = encode_XA_sample(pDecodedSamples, index, coef, pInSamples[2 + index], shift);
							pDecodedSamples[2 + index] = enc.decoded;
							data <<= 4;
							data |= (byte)(enc.encoded & 0xF);
							index++;
						}
						out_chunk.XAS_data[i][j] = data;
					}
				}
			}
			public static byte[] encode_XAS_v1(in short[] in_PCM,uint encode_size, uint n_samples_per_channel, uint n_channels)
			{
				uint n_chunks_per_channel = _GetNumXASChunks(n_samples_per_channel);
				XAS_Chunk[] _out_data = new XAS_Chunk[encode_size / 76];
				short[] PCM = new short[128];
				int index = 0;
				int in_PCM_index = 0;
				for (int chunk_ind = 0; chunk_ind < n_chunks_per_channel - 1; chunk_ind++)
				{
					for (int channel_ind = 0; channel_ind < n_channels; channel_ind++)
					{
						int t_index = 0;
						for (int sample_ind = 0; sample_ind < 128; sample_ind++, t_index += (int)n_channels)
						{
							PCM[sample_ind] = in_PCM[in_PCM_index + t_index + channel_ind];
						}
						encode_XAS_Chunk(ref _out_data[index], PCM);
						index++;
					}
					in_PCM_index += (int)(128 * n_channels);
				}
				uint samples_remain_per_channel = n_samples_per_channel - (n_chunks_per_channel - 1) * 128;
				for (int channel_ind = 0; channel_ind < n_channels; channel_ind++)
				{
					for (int sample_ind = 0; sample_ind < samples_remain_per_channel; sample_ind++)
					{
						PCM[sample_ind] = in_PCM[in_PCM_index + channel_ind + sample_ind * n_channels];
					}
					Array.Fill<short>(PCM,0,(int)samples_remain_per_channel, (int)(128 - samples_remain_per_channel));
					encode_XAS_Chunk(ref _out_data[index], PCM);
					index++;
				}
				byte[] out_data = new byte[_out_data.LongLength * 76];
				{
					int _index = 0;
					for (int i = 0; i < _out_data.Length; i++)
					{
						uint[] uints = new uint[4];
						for (int j = 0; j < 4; j++)
						{
							uints[j] = _out_data[i].headers[j].data;
						}
						Buffer.BlockCopy(uints,0,out_data,_index,16);
						_index += 16;
						for (int j = 0; j < _out_data[i].XAS_data.Length; j++)
						{
							for (int k = 0; k < _out_data[i].XAS_data[j].Length; k++)
							{
								out_data[_index] = _out_data[i].XAS_data[j][k];
								_index++;
							}
						}
					}
				}
				return out_data;
			}
			#endregion
		}
	}
}
