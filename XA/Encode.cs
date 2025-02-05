using static EA_ADPCM_XAS_CSharp.XASStruct;
namespace EA_ADPCM_XAS_CSharp
{
	public unsafe partial class EAAudio
	{
		public partial class XA
		{
			#region R2
			static int simple_CalcCoefShift(in short[] pSamples, long pSamples_index, in short[] in_prevSamples, int num_samples, out int out_coef_index, out byte out_shift)
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
						int sample = pSamples[i + pSamples_index];
						sample <<= fixed_point_offset;
						int s_error = sample - prediction;
						int error = Math.Abs(s_error);
						if (error > max_error)
						{
							max_error = error;
							s_max_error = s_error;
						}
						prevSamples[0] = prevSamples[1];
						prevSamples[1] = pSamples[i + pSamples_index];
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
				out_shift = (byte)(exp_shift);
				return max_min_error_i16;
			}
			static EncodedSample encode_XA_sample(in short[] prev_samples, in short[] coef, int sample, int shift)
			{
				int prediction = prev_samples[1] * coef[0] + prev_samples[0] * coef[1];

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
				return new EncodedSample { decoded = (short)decoded, encoded = (byte)res };
			}
			static void encode_EA_XA_block(ref byte[] data, long data_index, in short[] PCM, long PCM_index, ref short[] prev, int samples, int PCM_step, short[] coefs, byte shift, int data_step = 1)
			{
				for (int i = 0; i < samples / 2; i++)
				{
					byte _data = 0;
					for (int j = 0; j < 2; j++)
					{
						EncodedSample enc = encode_XA_sample(in prev, coefs, PCM[(i * 2 + j) * PCM_step], shift);
						prev[0] = prev[1];
						prev[1] = enc.decoded;
						_data <<= 4;
						_data |= enc.encoded;
					}
					data[data_index] = _data;
					data_index += data_step;
				}
			}
			static void encode_EA_XA_R2_chunk_nocompr(ref byte[] data, long data_index, in short[] PCM, long PCM_index, ref short[] prev, int nCannels)
			{
				data[data_index] = 0xEE;
				Buffer.BlockCopy(BitConverter.GetBytes(ToBigEndian16(PCM[26 * nCannels])), 0, data, (int)(data_index + 1), 2);
				Buffer.BlockCopy(BitConverter.GetBytes(ToBigEndian16(PCM[27 * nCannels])), 0, data, (int)(data_index + 3), 2);
				prev[0] = PCM[26 * nCannels];
				prev[1] = PCM[27 * nCannels];
				short[] pOutData = new short[28];
				Buffer.BlockCopy(data, (int)data_index + 5, pOutData, 0, 56);
				for (int i = 0; i < 28 * nCannels; i += nCannels)
				{
					pOutData[i] = ToBigEndian16(PCM[i]);
				}
				Buffer.BlockCopy(pOutData, 0, data, (int)data_index + 5, 56);
			}
			static long encode_EA_XA_R2_chunk(ref byte[] data, long data_index, in short[] PCM, long PCM_index, ref short[] prev, int nCannels, short max_error)
			{
				int err = simple_CalcCoefShift(PCM, PCM_index, prev, 28, out int coef_index, out byte shift);
				if (err > max_error)
				{
					encode_EA_XA_R2_chunk_nocompr(ref data, data_index, PCM, PCM_index, ref prev, nCannels);
					return sizeof_uncompr_EA_XA_R23_block;
				}
				else
				{
					data[data_index++] = (byte)(coef_index << 4 | shift);
					shift = (byte)(12 + fixed_point_offset - shift);
					short[] coefs = ea_adpcm_table_v2[coef_index];
					encode_EA_XA_block(ref data, data_index, PCM, PCM_index, ref prev, 28, nCannels, coefs, shift);
					return sizeof_compr_EA_XA_R23_block;
				}
			}
			static long encode_EA_XA_R2_channel(ref byte[] data, long data_index, in short[] PCM, long PCM_index, uint n_samples_per_channel, uint n_channels, short max_error)
			{
				int chunks_per_channel = (int)((n_samples_per_channel + 27) / 28);
				short[] prev = new short[2];
				long curr_data_index = data_index;
				encode_EA_XA_R2_chunk_nocompr(ref data, curr_data_index, PCM, PCM_index, ref prev, (int)n_channels);
				curr_data_index += sizeof_uncompr_EA_XA_R23_block;
				for (int chunk_ind = 1; chunk_ind < chunks_per_channel; chunk_ind++)
				{
					curr_data_index += encode_EA_XA_R2_chunk(ref data, curr_data_index, PCM, PCM_index + 28 * chunk_ind * n_channels, ref prev, (int)n_channels, max_error);
				}
				return curr_data_index - data_index;
			}
			public static long encode_EA_XA_R2(ref byte[] out_data, in short[] PCM, uint n_samples_per_channel, uint n_channels, short max_error)
			{
				long curr_data_index = 0;
				for (int chan_ind = 0; chan_ind < n_channels; chan_ind++)
				{
					curr_data_index += encode_EA_XA_R2_channel(ref out_data, curr_data_index, PCM, chan_ind, n_samples_per_channel, n_channels, max_error);
				}
				return curr_data_index;
			}
			#endregion
			#region R1
			static void encode_EA_XA_R1_chunk(ref byte[] data, long data_index, in short[] PCM, long PCM_index, ref short[] prev, int nCannels)
			{
				Buffer.BlockCopy(BitConverter.GetBytes(ToBigEndian16(prev[0])), 0, data, (int)data_index, 2);
				Buffer.BlockCopy(BitConverter.GetBytes(ToBigEndian16(prev[1])), 0, data, (int)(data_index + 2), 2);
				simple_CalcCoefShift(PCM, PCM_index, prev, 28, out int coef_index, out byte shift);
				data[4] = (byte)(coef_index << 4 | shift);
				short[] _prev = prev;
				encode_EA_XA_block(ref data,data_index+5, PCM,PCM_index,ref _prev, 28, nCannels, ea_adpcm_table_v2[coef_index], (byte)(12 + fixed_point_offset - shift));
			}

			#endregion
		}
	}
}
