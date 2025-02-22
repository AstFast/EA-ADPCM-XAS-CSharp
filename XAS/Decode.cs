using static EA_ADPCM_XAS_CSharp.XASStruct;
namespace EA_ADPCM_XAS_CSharp
{
	internal unsafe partial class EAAudio
	{
		public partial class XAS
		{
			#region v0
			
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
