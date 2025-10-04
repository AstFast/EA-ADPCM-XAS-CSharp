using static EA_ADPCM_XAS_CSharp.XASStruct;

namespace EA_ADPCM_XAS_CSharp
{
	internal unsafe partial class EAAudio
	{
		public partial class XA
		{
			static short decode_XA_sample(Span<short> prev_samples, short[] coef, int int4, byte shift)
			{

				int correction = int4 << shift;
				int prediction = prev_samples[1] * coef[0] + prev_samples[0] * coef[1];
				return Clip_int16((prediction + correction + def_rounding) >> fixed_point_offset);
			}

			static int decode_EA_XA_R2_Chunk(Span<byte> XA_Chunk, Span<short> out_PCM, Span<short> prev_samples)
			{

				Span<byte> p_curr_byte = XA_Chunk;
				Span<short> pSample = out_PCM;
				byte _byte = p_curr_byte[0];
				p_curr_byte = p_curr_byte.Slice(1);
				Span<short> p_prev_samples = prev_samples;
				if (_byte == 0xEE)
				{
					prev_samples[1] = Get_s16be(ref p_curr_byte);
					p_curr_byte = p_curr_byte.Slice(2);
					prev_samples[0] = Get_s16be(ref p_curr_byte);
					p_curr_byte = p_curr_byte.Slice(2);
					for (int i = 0; i < samples_in_EA_XA_R_chunk; i++)
					{
						pSample[0] = Get_s16be(ref p_curr_byte);
						pSample = pSample.Slice(1); 
						p_curr_byte = p_curr_byte.Slice(2);
					}

				}
				else
				{
					int coef_index = _byte >> 4;
					short[] coef = ea_adpcm_table_v2[coef_index];
					byte shift = (byte)(12 + fixed_point_offset - (_byte & 0xF));
					for (int j = 0; j < samples_in_EA_XA_R_chunk / 2; j++)
					{
						byte data = p_curr_byte[0];
						p_curr_byte = p_curr_byte.Slice(1);

						pSample[0] = decode_XA_sample(p_prev_samples, coef, (data >> 4) & 0x0F, shift);
						prev_samples[2] = pSample[0]; // in case of p_prev_samples == prev_samples
						pSample[1] = decode_XA_sample(p_prev_samples.Slice(1), coef, data & 0x0F, shift);

						p_prev_samples = pSample;
						pSample = pSample.Slice(2);
					}
					prev_samples[1] = pSample[-1];
					prev_samples[0] = pSample[-2];
				}

				return XA_Chunk.Length - p_curr_byte.Length;
			}
			public static void decode_EA_XA_R2(Span<byte> data, Span<short> out_PCM, uint n_samples_per_channel, uint n_channels)
			{
				// TODO: multi channel
				Span<byte> _data = data;
				short[] prev_samples = new short[]{ 0 };
				int num_chunks = ((int)n_samples_per_channel + 27) / 28;
				for (int i = 0; i < num_chunks; i++) {
					int data_decoded_size = decode_EA_XA_R2_Chunk(_data, out_PCM, prev_samples);
					_data = _data.Slice(data_decoded_size);
					out_PCM = out_PCM.Slice(samples_in_EA_XA_R_chunk);
				}
			}
			
		}
	}
}