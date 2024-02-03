using static EA_ADPCM_XAS_CSharp.XASStruct;
namespace EA_ADPCM_XAS_CSharp.Decode
{
	internal class Decode_Data
	{
		short decode_XA_sample(short[] prev_samples, short[] coef, sbyte int4, byte shift) 
		{
			int correction = int4 << shift;
			int prediction = prev_samples[1] * coef[0] + prev_samples[0] * coef[1];
			return (short)Clip_int16((prediction + correction + def_rounding) >> fixed_point_offset);
		}
		void decode_XAS_Chunk(XAS_Chunk in_chunk,ref short[] pSamples) 
		{
			for (int j = 0; j<4; j++) {
				int Array_index = j*32;
				pSamples[0 + Array_index] = ((short)(in_chunk.headers[j].sample_0 << 4));
				int coef_index = (int)in_chunk.headers[j].coef_index;
				pSamples[1 + Array_index] = ((short)(in_chunk.headers[j].sample_1 << 4));
				byte shift = (byte)(20 - in_chunk.headers[j].exp_shift);
				short[] coef = ea_adpcm_table_v2[coef_index];
				for (int i = 0; i< 15; i++,Array_index+=2)
				{
					SamplesByte data = (sbyte)in_chunk.XAS_data[i][j];
					pSamples[2 + Array_index] = decode_XA_sample([pSamples[Array_index], pSamples[Array_index + 1]], coef, data.sample0, shift);
					pSamples[3 + Array_index] = decode_XA_sample([pSamples[Array_index + 1],pSamples[Array_index + 2]], coef, data.sample1, shift);
				}
			}
		}
		void decode_XAS(XAS_Chunk[] _in_data,ref short[] out_PCM, uint n_samples_per_channel, uint n_channels) {
			if (n_samples_per_channel == 0)
			{
				return;
			}
			int index = 0;
			int out_PCM_index = 0;
			short[] PCM = new short[128];
			uint n_chunks_per_channel = _GetNumXASChunks(n_samples_per_channel);
			for (int chunk_ind = 0; chunk_ind<n_chunks_per_channel - 1; chunk_ind++) {
				for (int channel_ind = 0; channel_ind<n_channels; channel_ind++) {
					decode_XAS_Chunk(_in_data[index],ref PCM);
					index++;
					for (int sample_ind = 0; sample_ind< 128; sample_ind++) 
					{
						out_PCM[channel_ind + sample_ind * n_channels + out_PCM_index] = PCM[sample_ind];
					}
				}
				out_PCM_index += (int)(128 * n_channels);
			}
			uint samples_remain_per_channel = n_samples_per_channel - (n_chunks_per_channel - 1) * 128;
			for (int channel_ind = 0; channel_ind < n_channels; channel_ind++)
			{
				decode_XAS_Chunk(_in_data[index],ref PCM);
				index++;

				for (int sample_ind = 0; sample_ind < samples_remain_per_channel; sample_ind++)
				{
					out_PCM[channel_ind + sample_ind * n_channels + out_PCM_index] = PCM[sample_ind];
				}
			}
		}
		byte[] ShortArrayToByteArray(short[] shortArray)
		{
			byte[] byteArray = new byte[shortArray.Length * 2];
			Buffer.BlockCopy(shortArray, 0, byteArray, 0, byteArray.Length);
			return byteArray;
		}

		public short[] Decode(byte[] in_data,uint channels)
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
}
