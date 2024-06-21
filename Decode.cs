using static EA_ADPCM_XAS_CSharp.SIMD;
using static EA_ADPCM_XAS_CSharp.XASStruct;
namespace EA_ADPCM_XAS_CSharp.Decode
{
	internal class DecodeFunction
	{
		static short decode_XA_sample(short prev_samples0, short prev_samples1, short[] coef, int int4, byte shift)
		{
			int correction = int4 << shift;
			int prediction = prev_samples1 * coef[0] + prev_samples0 * coef[1];
			return ClipToInt16((prediction + correction + def_rounding) >> fixed_point_offset);
		}

		static short ClipToInt16(int value)
		{
			if (value > short.MaxValue)
			{
				return short.MaxValue;
			}
			else if (value < short.MinValue)
			{
				return short.MinValue;
			}
			else
			{
				return (short)value;
			}
		}
		unsafe void decode_XAS_Chunk(XAS_Chunk in_chunk,ref short[] pSamples)
		{
			for (int j = 0; j < subchunks_in_XAS_chunk; j++)
			{
				int pSamples_index = j*32;
				pSamples[0 + pSamples_index] = (short)(in_chunk.headers[j].sample_0 << 4);
				int coef_index = (int)in_chunk.headers[j].coef_index;
				pSamples[1 + pSamples_index] = (short)(in_chunk.headers[j].sample_1 << 4);
				byte shift = (byte)(12 + fixed_point_offset - in_chunk.headers[j].exp_shift);
				short[] coef = ea_adpcm_table_v2[coef_index];
				for (int i = 0; i < 15; i++,pSamples_index+=2)
				{
					SamplesByte* data = (SamplesByte*)((&in_chunk)->XAS_data[i][j]);
					pSamples[2 + pSamples_index] = decode_XA_sample(pSamples[pSamples_index], pSamples[1 + pSamples_index], coef, data->sample0, shift);
					pSamples[3 + pSamples_index] = decode_XA_sample(pSamples[pSamples_index + 1], pSamples[2 + pSamples_index], coef, data->sample1, shift);
				}
			}
		}

		
		static void decode_XAS(XAS_Chunk[] _in_data,ref short[] out_PCM, uint n_samples_per_channel, uint n_channels) {
			if (n_samples_per_channel == 0)
			{
				return;
			}
			int _in_data_index = 0;
			int out_PCM_Offest = 0;
			short[] PCM = new short[128];
			uint n_chunks_per_channel = _GetNumXASChunks(n_samples_per_channel);
			for (int chunk_ind = 0; chunk_ind < n_chunks_per_channel - 1; chunk_ind++)
			{
				for (int channel_ind = 0; channel_ind < n_channels; channel_ind++)
				{
					decode_XAS_Chunk_SIMD(_in_data[_in_data_index],ref PCM);
					_in_data_index++;
					for (int sample_ind = 0; sample_ind < 128; sample_ind++)
					{
						out_PCM[out_PCM_Offest + channel_ind + sample_ind * n_channels] = PCM[sample_ind];
					}
				}
				out_PCM_Offest += (int)(128 * n_channels);
			}
			uint samples_remain_per_channel = n_samples_per_channel - (n_chunks_per_channel - 1) * 128;
			for (int channel_ind = 0; channel_ind < n_channels; channel_ind++)
			{
				decode_XAS_Chunk_SIMD(_in_data[_in_data_index],ref PCM);
				_in_data_index++;
				for (int sample_ind = 0; sample_ind < samples_remain_per_channel; sample_ind++)
				{
					out_PCM[channel_ind + sample_ind * n_channels] = PCM[sample_ind];
				}
			}
		}

		public static void decode_EA_XA_R2(ref byte[] data,ref short[] out_PCM, uint n_samples_per_channel, uint n_channels) {
		    short[] prev_samples = new short[]{ 0 };
			long data_index = 0;
			int out_PCM_index = 0;
		    int num_chunks = (int)((n_samples_per_channel + 27) / 28);
	        for (int i = 0; i<num_chunks; i++) {
		         long data_decoded_size = decode_EA_XA_R2_Chunk(ref data,ref data_index, out_PCM,out_PCM_index, prev_samples);
	             data_index += data_decoded_size;
		         out_PCM_index += samples_in_EA_XA_R_chunk;
	        }
        }
		static long decode_EA_XA_R2_Chunk(ref byte[] XA_Chunk,ref long p_curr_byte_index, short[] out_PCM/*[28]*/,long out_PCM_index, short[] prev_samples/*[3]*/)
		{
			byte[] p_curr_byte = XA_Chunk;
			
			short[] pSample = out_PCM;
			int pSample_index = (int)out_PCM_index;
			byte _byte = p_curr_byte[p_curr_byte_index];
			p_curr_byte_index++;
			short[] p_prev_samples = prev_samples;
			if (_byte == 0xEE)
			{

				prev_samples[1] = Get_s16be(p_curr_byte);
				p_curr_byte_index += 2;
				prev_samples[0] = Get_s16be(p_curr_byte);
				p_curr_byte_index += 2;
				for (int i = 0; i < samples_in_EA_XA_R_chunk; i++)
				{
					pSample[pSample_index++] = Get_s16be(p_curr_byte);
					p_curr_byte_index += 2;
				}
			}
			else
			{
				int coef_index = _byte >> 4;
				short[] coef = ea_adpcm_table_v2[coef_index];
				byte shift = (byte)(12 + fixed_point_offset - (_byte & 0xF));
				for (int j = 0; j < samples_in_EA_XA_R_chunk / 2; j++)
				{
					SamplesByte data = (sbyte)(p_curr_byte[p_curr_byte_index]);
					p_curr_byte_index++;
					pSample[0] = decode_XA_sample(p_prev_samples[0], p_prev_samples[1], coef, data.sample0, shift);
					prev_samples[2] = pSample[0];
					pSample[1] = decode_XA_sample(p_prev_samples[1], p_prev_samples[2], coef, data.sample1, shift);
					p_prev_samples = pSample/*pSample*/;
					pSample_index += 2;
				}
				prev_samples[1] = pSample[-1];
				prev_samples[0] = pSample[-2];
			}

			return p_curr_byte_index;
		}
		
        public static short[] EAXASDecode(byte[] in_data,uint channels)
		{
			XAS_Chunk[] xas = new XAS_Chunk[in_data.Length/76];
			int index = 0;
            for (int i = 0; i < in_data.Length/76; i++)
            {
				xas[i].headers = new XAS_SubChunkHeader[4];
                for (int j = 0; j < 4; j++)
				{
					xas[i].headers[j].data = (uint)BitConverter.ToInt32(in_data, index);
					index += 4;
                }
				xas[i].XAS_data = new byte[15][];
				for (int k = 0; k < 15; k++)
				{
					xas[i].XAS_data[k] = new byte[4];
				}
                for (int j = 0; j < 15; j++)
                {
                    for (int k = 0; k < 4; k++)
                    {
						xas[i].XAS_data[j][k] = in_data[index];
						index++;
                    }
                }
            }
			uint n_total_samples = (uint)((in_data.Length / 76) * 128);
			short[] PCM_data = new short[2 * n_total_samples];
			decode_XAS(xas,ref PCM_data,n_total_samples / channels, channels);
			return PCM_data;
		}
	}
	internal static class DecodeCode
	{
		
		public static byte[] Decode(byte[] bytes, uint channels)
		{
			List<byte> data = new List<byte>();
			byte[] array = new byte[76];
			short[] array2 = new short[1024];
			int[] array3 = new int[32];
			int array_index = 0;
			int[] array4 =
			[
				0,
				240,
				460,
				392
			];
			int[] array5 =
			[
				0,
				0,
				-208,
				-220
			];
			int count = (int)((bytes.Length / 76) / channels);
			int num9 = (bytes.Length / 76) * 128;
			for (int i = 0; i < count; i++)
			{
				for (int j = 0; j < channels; j++)
				{
					Array.Copy(bytes, array_index, array, 0, 76);
					array_index += 76;
					for (int k = 0; k < 4; k++)
					{
						array3[0] = (int)((short)((int)(array[k * 4] & 240) | (int)array[k * 4 + 1] << 8));
						array3[1] = (int)((short)((int)(array[k * 4 + 2] & 240) | (int)array[k * 4 + 3] << 8));
						int num14 = (int)(array[k * 4] & 15);
						int num15 = (int)(array[k * 4 + 2] & 15);
						for (int l = 2; l < 32; l += 2)
						{
							int num16 = (array[12 + k + l * 2] & 240) >> 4;
							if (num16 > 7)
							{
								num16 -= 16;
							}
							int num17 = array3[l - 1] * array4[num14] + array3[l - 2] * array5[num14];
							array3[l] = num17 + (num16 << 20 - num15) + 128 >> 8;
							if (array3[l] > 32767)
							{
								array3[l] = 32767;
							}
							else if (array3[l] < -32768)
							{
								array3[l] = -32768;
							}
							num16 = (int)(array[12 + k + l * 2] & 15);
							if (num16 > 7)
							{
								num16 -= 16;
							}
							num17 = array3[l] * array4[num14] + array3[l - 1] * array5[num14];
							array3[l + 1] = num17 + (num16 << 20 - num15) + 128 >> 8;
							if (array3[l + 1] > 32767)
							{
								array3[l + 1] = 32767;
							}
							else if (array3[l + 1] < -32768)
							{
								array3[l + 1] = -32768;
							}
						}
						for (int m = 0; m < 32; m++)
						{
							array2[(k * 32 + m) * channels + j] = (short)array3[m];
						}
					}
				}
				int num18;
				if (num9 >= 128U)
				{
					num18 = 128;
				}
				else
				{
					num18 = (int)num9;
				}
				num9 -= 128;
				for (int n = 0; n < num18 * channels; n++)
				{
					data.AddRange(BitConverter.GetBytes(array2[n]));
				}

			}
			return data.ToArray();
		}
	}
}
