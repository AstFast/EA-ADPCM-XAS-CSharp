using System.Runtime.InteropServices;
using static EA_ADPCM_XAS_CSharp.XASStruct;
namespace EA_ADPCM_XAS_CSharp
{
	internal unsafe partial class EAAudio
	{
		public partial class XA
		{
			static short decode_XA_sample(short* prev_samples, short[] coef, int int4, byte shift)
			{
				int correction = int4 << shift;
				int prediction = prev_samples[1] * coef[0] + prev_samples[0] * coef[1];
				return Clip_int16((prediction + correction + def_rounding) >> fixed_point_offset);
			}
			static long decode_EA_XA_R2_Chunk(byte* XA_Chunk, short* out_PCM,short* prev_samples)
			{
				byte* p_curr_byte = XA_Chunk;
				short* pSample = out_PCM;
				byte _byte = *(p_curr_byte++);
				short* p_prev_samples = prev_samples;
				if (_byte == 0xEE) {
					prev_samples[1] = Get_s16be(p_curr_byte);
					p_curr_byte += 2;
					prev_samples[0] = Get_s16be(p_curr_byte);
					p_curr_byte += 2;
					for (int i = 0; i < samples_in_EA_XA_R_chunk; i++)
					{
						*(pSample++) = Get_s16be(p_curr_byte);
						p_curr_byte += 2;
					}
				}
				else {
					int coef_index = _byte >> 4;
					short[] coef = ea_adpcm_table_v2[coef_index];
					byte shift = (byte)(12 + fixed_point_offset - (_byte & 0xF));
					for (int j = 0; j < samples_in_EA_XA_R_chunk / 2; j++) {
						SamplesByte data = *(SamplesByte*)(p_curr_byte++);

						pSample[0] = decode_XA_sample(p_prev_samples, coef, data.sample0, shift);
						prev_samples[2] = pSample[0]; // in case of p_prev_samples == prev_samples
						pSample[1] = decode_XA_sample(p_prev_samples + 1, coef, data.sample1, shift);


						p_prev_samples = pSample;
						pSample += 2;
					}
					prev_samples[1] = pSample[-1];
					prev_samples[0] = pSample[-2];
				}

				return p_curr_byte - XA_Chunk;
			}
			public static void decode_EA_XA_R2(void* data, short* out_PCM, uint n_samples_per_channel, uint n_channels)
			{
				byte* _data = (byte*)data;
				short* prev_samples = (short*)Marshal.AllocHGlobal(6);//short[] prev_samples = { 0,0,0 };
				int num_chunks = (int)((n_samples_per_channel + 27) / 28);
				for (int i = 0; i < num_chunks; i++) {
					long data_decoded_size = decode_EA_XA_R2_Chunk(_data, out_PCM,prev_samples);
					_data += data_decoded_size;
					out_PCM += samples_in_EA_XA_R_chunk;
				}
				Marshal.FreeHGlobal((IntPtr)prev_samples);
			} 
		}
	}
}
