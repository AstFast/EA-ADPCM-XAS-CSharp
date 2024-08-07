﻿
using static EA_ADPCM_XAS_CSharp.XASStruct;
namespace EA_ADPCM_XAS_CSharp
{
	internal class Decode
	{
		
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
		
		static short decode_XA_sample(short[] prev_samples, short[] coef, int int4, byte shift)
		{
			int correction = int4 << shift;
			int prediction = prev_samples[1] * coef[0] + prev_samples[0] * coef[1];
			return ClipToInt16((prediction + correction + def_rounding) >> fixed_point_offset);
		}
		static long decode_EA_XA_R2_Chunk(ref byte[] p_curr_byte, ref long p_curr_byte_index, ref short[] pSample, long out_PCM_index, short[] prev_samples)
		{
			int pSample_index = (int)out_PCM_index;
			byte _byte = p_curr_byte[p_curr_byte_index++];
			short[] p_prev_samples = prev_samples;
			if (_byte == 0xEE)
			{

				prev_samples[1] = Get_s16be(p_curr_byte,(int)p_curr_byte_index);
				p_curr_byte_index += 2;
				prev_samples[0] = Get_s16be(p_curr_byte,(int)p_curr_byte_index);
				p_curr_byte_index += 2;
				for (int i = 0; i < samples_in_EA_XA_R_chunk; i++)
				{
					pSample[pSample_index++] = Get_s16be(p_curr_byte,(int)p_curr_byte_index);
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
					pSample[0] = decode_XA_sample(p_prev_samples, coef, data.sample0, shift);
					prev_samples[2] = pSample[0];
					pSample[1] = decode_XA_sample(p_prev_samples, coef, data.sample1, shift);
					p_prev_samples = pSample;
					pSample_index += 2;
				}
				prev_samples[1] = pSample[-1];
				prev_samples[0] = pSample[-2];
			}
			return p_curr_byte_index;
		}
		public static void decode_EA_XA_R2(ref byte[] data, ref short[] out_PCM, uint n_samples_per_channel, uint n_channels)
		{
			short[] prev_samples = new short[3] { 0, 0, 0 };
			long data_index = 0;
			int out_PCM_index = 0;
			int num_chunks = (int)((n_samples_per_channel + 27) / 28);
			for (int i = 0; i < num_chunks; i++)
			{
				long data_decoded_size = decode_EA_XA_R2_Chunk(ref data, ref data_index, ref out_PCM, out_PCM_index, prev_samples);
				data_index += data_decoded_size;
				out_PCM_index += samples_in_EA_XA_R_chunk;
			}
		}
		public static void decode_XAS(XAS_Chunk[] _in_data, ref short[] out_PCM, uint n_samples_per_channel, uint n_channels)
		{
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
	}
    internal class Encode
    {
		static void _memset(ref short[] destination, uint index, byte value, uint size)
		{
			for (int i = 0; i < size; i++)
			{
				destination[i + index] = value;
			}
		}
		static int simple_CalcCoefShift(short[] pSamples, int index, short[] in_prevSamples, int num_samples, out int out_coef_index, out byte out_shift)
		{
			const int num_coefs = 4;
			int min_max_error = int.MaxValue;
			int s_min_max_error = int.MaxValue;
			int best_coef_ind = 0;
			for (int coef_ind = 0; coef_ind < num_coefs; coef_ind++)
			{
				short[] prevSamples = [in_prevSamples[0], in_prevSamples[1]];
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
					prevSamples[1] = pSamples[i + index];
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
		static EncodedSample encode_XA_sample(short[] prev_samples,int index, short[] coef, int sample, int shift)
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
		static void encode_EA_XA_block(ref byte[] data, int data_index, short[] PCM,int PCM_index, short[] prev, int samples, int PCM_step, short[] coefs, byte shift, int data_step = 1)
		{
			int index = data_index;
			for (int i = 0; i < samples / 2; i++)
			{
				byte _data = 0;
				for (int j = 0; j < 2; j++)
				{
					EncodedSample enc = encode_XA_sample(prev,0, coefs, PCM[(i * 2 + j) * PCM_step + PCM_index], shift);
					prev[0] = prev[1];
					prev[1] = enc.decoded;
					_data <<= 4;
					_data |= enc.encoded;
				}
				data[index] = _data;
				index += data_step;
			}
		}

		public static void encode_EA_XA_R1_chunk(ref byte[/*sizeof_EA_XA_R1_chunk*/] data, short[/*28*/] PCM, short[] prev, int nCannels)
		{
			byte[] temp = BitConverter.GetBytes(ToBigEndian16(prev[0]));
			data[0] = temp[0];
			data[1] = temp[1];
			temp = BitConverter.GetBytes(ToBigEndian16(prev[1]));
			data[2] = temp[0];
			data[3] = temp[1];
			simple_CalcCoefShift(PCM, 0, prev, 28, out int coef_index, out byte shift);
			data[4] = (byte)(coef_index << 4 | shift);
			short[] _prev = new short[2];
			Buffer.BlockCopy(prev, 0, _prev, 0, 4);
			encode_EA_XA_block(ref data, 5, PCM,0, _prev, 28, nCannels, ea_adpcm_table_v2[coef_index], (byte)(12 + fixed_point_offset - shift));
		}
		static void encode_EA_XA_R2_chunk_nocompr(ref byte[] data, short[] PCM,int PCM_index,ref short[] prev, int nChannels)
		{
			data[0] = 0xEE;
			int data_index = 0;
			byte[] temp = BitConverter.GetBytes(ToBigEndian16(PCM[26 * nChannels + PCM_index]));
			data[data_index + 1] = temp[0];
			data[data_index + 2] = temp[1];
			temp = BitConverter.GetBytes(ToBigEndian16(PCM[27 * nChannels + PCM_index]));
			data[data_index + 3] = temp[0];
			data[data_index + 4] = temp[1];
			prev[0] = PCM[26 * nChannels + PCM_index];
			prev[1] = PCM[27 * nChannels + PCM_index];
			short[] pOutData = new short[(data.Length - 4) / 2];
			Buffer.BlockCopy(data, 5, pOutData, 0, data.Length - 4);
			for (int i = 0; i < 28 * nChannels; i += nChannels)
			{
				pOutData[i] = ToBigEndian16(PCM[i + PCM_index]);
			}
			Buffer.BlockCopy(pOutData, 0, data, 5, data.Length - 4);
		}
		static long encode_EA_XA_R2_chunk(ref byte[/*sizeof_uncompr_EA_XA_R23_block*/] data, int data_index, short[] PCM,int PCM_index,short[] prev, int nCannels, short max_error)
		{
			int err = simple_CalcCoefShift(PCM, data_index, prev, 28, out int coef_index, out byte shift);
			if (err > max_error)
			{
				encode_EA_XA_R2_chunk_nocompr(ref data, PCM , PCM_index,ref prev, nCannels);
				return sizeof_uncompr_EA_XA_R23_block;
			}
			else
			{
				data[data_index] = (byte)(coef_index << 4 | shift);
				shift = (byte)(12 + fixed_point_offset - shift);
				short[] coefs = ea_adpcm_table_v2[coef_index];
				encode_EA_XA_block(ref data, 0, PCM, PCM_index, prev, 28, nCannels, coefs, shift);
				return sizeof_compr_EA_XA_R23_block;
			}
		}
		static long encode_EA_XA_R2_channel(ref byte[] data, short[] PCM,int PCM_index, uint n_samples_per_channel, uint n_channels, short max_error)
		{
			int chunks_per_channel = (int)((n_samples_per_channel + 27) / 28);
			short[] prev = new short[2];
			long data_index = 0;
			encode_EA_XA_R2_chunk_nocompr(ref data, PCM ,PCM_index ,ref prev, (int)n_channels);
			data_index += sizeof_uncompr_EA_XA_R23_block;
			for (int chunk_ind = 1; chunk_ind < chunks_per_channel; chunk_ind++)
			{
				PCM_index += 28 * chunk_ind * (int)n_channels;
				data_index += encode_EA_XA_R2_chunk(ref data, (int)data_index,PCM, PCM_index, prev, (int)n_channels, max_error);
			}
			return data_index;
		}
		public static long encode_EA_XA_R2(ref byte[] data, short[] PCM, uint n_samples_per_channel, uint n_channels, short max_error)
		{
			long data_index = 0;
			for (int chan_ind = 0; chan_ind < n_channels; chan_ind++)
			{
				data_index += encode_EA_XA_R2_channel(ref data, PCM, chan_ind, n_samples_per_channel, n_channels, max_error);
			}
			return data_index;
		}
		static void encode_XAS_Chunk(ref XAS_Chunk out_chunk, short[] in_PCM)
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
						EncodedSample enc = encode_XA_sample(pDecodedSamples,index, coef, pInSamples[2 + index], shift);
						pDecodedSamples[2 + index] = enc.decoded;
						data <<= 4;
						data |= (byte)(enc.encoded & 0xF);
						index++;
					}
					out_chunk.XAS_data[i][j] = data;
				}
			}
		}
		public static void encode_XAS(ref XAS_Chunk[] _out_data, short[] in_PCM, uint n_samples_per_channel, uint n_channels)
		{
			uint n_chunks_per_channel = _GetNumXASChunks(n_samples_per_channel);
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
				_memset(ref PCM, samples_remain_per_channel, 0, (128 - samples_remain_per_channel));
				encode_XAS_Chunk(ref _out_data[index], PCM);
				index++;
			}
		}
	}
}
