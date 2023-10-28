using System;
using size_t = System.UIntPtr;
using System.Text;

namespace EA_ADPCM_CSharp
{
	public class EA_ADPCM_XA
	{
	}
	class EA_ADPCM_XA_Work : Constant
	{
		unsafe Int16 Get_s16be(void* ptr) {
			return (short)1;
		}
		Int16 Clip_int16(int val)
		{
			return (short)((val >= 0x7FFF) ? 0x7FFF : (val <= -0x8000) ? -0x8000 : val);
		}
		unsafe Int16 decode_XA_sample(Int16* prev_samples, Int16* coef, sbyte int4, byte shift) {
			int correction = (int)int4 << shift;
			int prediction = prev_samples[1] * coef[0] + prev_samples[0] * coef[1];
			return Clip_int16((prediction + correction + def_rounding) >> fixed_point_offset);
		}
		unsafe size_t decode_EA_XA_R2_Chunk(byte* XA_Chunk, Int16* out_PCM, Int16[] prev_samples) {
			byte* p_curr_byte = XA_Chunk;
			Int16* pSample = (short*)&out_PCM;
			byte _byte = *(p_curr_byte++);
			Int16* p_prev_samples = (short*)&prev_samples;
			if (_byte == 0xEE) {
				//prev_samples[1] = Get_s16be(p_curr_byte), p_curr_byte += 2;
				//prev_samples[0] = Get_s16be(p_curr_byte), p_curr_byte += 2;
				for (int i = 0; i < samples_in_EA_XA_R_chunk; i++)
				{
					//*(pSample++) = Get_s16be(p_curr_byte), p_curr_byte += 2;
				}
			}
			else {
				int coef_index = _byte >> 4;
				Int16[] coef1 = new Int16[] { ea_adpcm_table_v2[coef_index][0], ea_adpcm_table_v2[coef_index][1] };
				Int16* coef = (short*)&coef1;
				byte shift = (byte)(12 + fixed_point_offset - (_byte & 0xF));
				for (int j = 0; j < samples_in_EA_XA_R_chunk / 2; j++) {
					SamplesByte data = *(SamplesByte*)(p_curr_byte++);

					pSample[0] = decode_XA_sample(p_prev_samples, coef, (sbyte)data.sample0, shift);
					prev_samples[2] = pSample[0]; // in case of p_prev_samples == prev_samples
					pSample[1] = decode_XA_sample(p_prev_samples + 1, coef, (sbyte)data.sample1, shift);
					p_prev_samples = pSample;
					pSample += 2;
				}
				prev_samples[1] = pSample[-1];
				prev_samples[0] = pSample[-2];
			}
			return (nuint)(p_curr_byte - XA_Chunk);
		}
		unsafe void decode_EA_XA_R2(void* data, Int16* out_PCM, UInt32 n_samples_per_channel, UInt32 n_channels) {
			byte* _data = (byte*)data;
			Int16[] prev_samples = new Int16[3] { 0, 0, 0 };
			int num_chunks = (int)((n_samples_per_channel + 27) / 28);
			for (int i = 0; i < num_chunks; i++) {
				size_t data_decoded_size = decode_EA_XA_R2_Chunk(_data, out_PCM, prev_samples);
				_data += data_decoded_size;
				out_PCM += samples_in_EA_XA_R_chunk;
			}
		}
		void encode_EA_XA_R2_chunk_nocompr(byte[] data, Int16[] PCM, Int16[] prev, int nCannels)
		{
		}
		unsafe void encode_EA_XA_block(byte[] data, Int16[] PCM, Int16[] prev, int samples, int PCM_step, Int16* coefs, byte shift, int data_step = 1) {
			for (int i = 0; i < samples / 2; i++)
			{
				byte _data = 0;
				for (int j = 0; j < 2; j++)
				{
					//EncodedSample enc = encode_XA_sample(prev, coefs, PCM[(i * 2 + j) * PCM_step], shift);
					prev[0] = prev[1];
					//prev[1] = enc.decoded;
					_data <<= 4;
					//_data |= (byte)enc.encoded;
				}

				//data = _data;
				//data += (byte)data_step;
			}
		}

		unsafe size_t encode_EA_XA_R2_chunk(byte[] data, Int16[] PCM, Int16[] prev, int nCannels, Int16 max_error)
		{
			int coef_index;
			byte shift;
			int err = 0;
			// int err = simple_CalcCoefShift(PCM, prev, 28, &coef_index, &shift);
			if (err > max_error)
			{
				encode_EA_XA_R2_chunk_nocompr(data, PCM, prev, nCannels);
				return sizeof_uncompr_EA_XA_R23_block;
			}
			else
			{
				//data++ = coef_index << 4 | shift;
				//shift = (byte)(12 + fixed_point_offset - shift);
				//Int16[] coef1 = new Int16[] { ea_adpcm_table_v2[coef_index][0], ea_adpcm_table_v2[coef_index][1] };
				//Int16* coefs = (short*)&coef1;
				//encode_EA_XA_block(data, PCM, prev, 28, nCannels, coefs, shift);
				return sizeof_compr_EA_XA_R23_block;
			}
		}
	}
}
