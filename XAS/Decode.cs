using static EA_ADPCM_XAS_CSharp.XASStruct;
namespace EA_ADPCM_XAS_CSharp
{
	public unsafe partial class EAAudio
	{
		public partial class XAS
		{
			#region v0
			static void decode_XAS_chunk_v0(void* in_data, short[]* PCM, uint n_channels)
			{
				int samples_done = 0;
				byte[]* _in_data = (byte[]*)in_data;
				uint frame_header = get_u32le(*_in_data);
				float[] coef = ea_adpcm_table_xas_v0[frame_header & 0x0F];
				short[] hist = new short[2];
				hist[0] = (short)((frame_header >> 0) & 0xFFF0);
				hist[1] = (short)((frame_header >> 16) & 0xFFF0);
				byte shift = (byte)((frame_header >> 16) & 0x0F);
				for (int i = 0; i < 2; i++)
				{
					(*PCM)[samples_done * n_channels] = hist[0];
					samples_done += 2;
				}

				for (int i = 0; i < samples_in_XAS_subchunk; i++)
				{
					byte nibbles = (*_in_data)[0x02 + 0x02 + i / 2];
					int sample;
					sample = (i & 1) != 0 ?
							(nibbles >> 0) & 0x0f :
							(nibbles >> 4) & 0x0f;
					sample = (short)(sample << 12) >> shift;
					sample = sample + (int)(hist[1] * coef[0] + hist[0] * coef[1]);
					sample = Clip_int16(sample);
					(*PCM)[samples_done * n_channels] = (short)sample;
					samples_done += 2;
					hist[0] = hist[1];
					hist[1] = (short)sample;
				}
			}
			#endregion
			#region v1
			public static void decode_XAS_v1(in byte[] in_data, ref short[] out_PCM, uint n_samples_per_channel, uint n_channels)
			{
				if (n_samples_per_channel == 0)
				{
					return;
				}
				uint encode_size = GetXASEncodedSize(n_samples_per_channel, n_channels);
				XAS_Chunk[] _in_data = new XAS_Chunk[encode_size / 76];
				int data_index = 0;
				for (int i = 0; i < _in_data.Length; i++)
				{
					_in_data[i].headers = new XAS_SubChunkHeader[subchunks_in_XAS_chunk];
					_in_data[i].XAS_data = new byte[15][];
					for (int j = 0; j < 4; j++)
					{
						_in_data[i].headers[j].data = BitConverter.ToUInt32(in_data, data_index);
						data_index += 4;
					}
					for (int j = 0; j < 15; j++)
					{
						_in_data[i].XAS_data[j] = new byte[subchunks_in_XAS_chunk];
						for (int k = 0; k < subchunks_in_XAS_chunk; k++)
						{
							_in_data[i].XAS_data[j][k] = in_data[data_index];
							data_index++;
						}
					}
				}
				int _in_data_index = 0, out_PCM_Offest = 0;
				short[] PCM = new short[128];
				uint n_chunks_per_channel = _GetNumXASChunks(n_samples_per_channel);
				for (int chunk_ind = 0; chunk_ind < n_chunks_per_channel - 1; chunk_ind++)
				{
					for (int channel_ind = 0; channel_ind < n_channels; channel_ind++)
					{
						VectorSIMD.decode_XAS_Chunk_SIMD(_in_data[_in_data_index], ref PCM);
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
					VectorSIMD.decode_XAS_Chunk_SIMD(_in_data[_in_data_index], ref PCM);
					_in_data_index++;
					for (int sample_ind = 0; sample_ind < samples_remain_per_channel; sample_ind++)
					{
						out_PCM[channel_ind + sample_ind * n_channels] = PCM[sample_ind];
					}
				}
			}
			#endregion
		}
	}
}
