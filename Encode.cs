using static EA_ADPCM_XAS_CSharp.XASStruct;
namespace EA_ADPCM_XAS_CSharp.Encode
{
	internal class Encode_Data
	{
		EncodedSample encode_XA_sample(Int16 prev_samples,Int16 prev_samples1, Int16[] coef, int sample, int shift)
		{
			int prediction = prev_samples1 * coef[0] + prev_samples * coef[1];

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
		int simple_CalcCoefShift(short[] pSamples,int index, short[] in_prevSamples, int num_samples,ref int out_coef_index,ref byte out_shift) {
			const int num_coefs = 4;
			int min_max_error = int.MaxValue;
			int s_min_max_error = int.MaxValue;
			int best_coef_ind = 0;
			for (int coef_ind = 0; coef_ind<num_coefs; coef_ind++) {
				short[] prevSamples = [ in_prevSamples[0], in_prevSamples[1] ];
				int max_error = 0;
				int s_max_error = 0;
				for (int i = 0; i<num_samples; i++) {
					int prediction = ea_adpcm_table_v2[coef_ind][0] * prevSamples[1] + ea_adpcm_table_v2[coef_ind][1] * prevSamples[0];
					int sample = pSamples[i+index];
					sample <<= fixed_point_offset;
					int s_error = sample - prediction;
					int error = Math.Abs(s_error);
					if (error > max_error) {
						max_error = error;
						s_max_error = s_error;
					}
					prevSamples[0] = prevSamples[1];
					prevSamples[1] = pSamples[i+index];
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
		void encode_XAS_Chunk(ref XAS_Chunk out_chunk, short[] in_PCM)
		{
			out_chunk.headers = new XAS_SubChunkHeader[4];
			out_chunk.XAS_data = new byte[15][];
			for (int i = 0; i < 15; i++)
			{
				out_chunk.XAS_data[i] = new byte[4];
			}
			short[] decoded_PCM = new short[32];
			for (int j = 0; j < 4; j++)
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
				int coef_index = 0;
				byte shift = 0;
				simple_CalcCoefShift(pInSamples, 2, decoded_PCM, 30,ref coef_index,ref shift);
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
						EncodedSample enc = encode_XA_sample(pDecodedSamples[index], pDecodedSamples[index + 1], coef, pInSamples[2 + index], shift);
						pDecodedSamples[2 + index] = enc.decoded;
						data <<= 4;
						data |= (byte)(enc.encoded & 0xF);
						index++;
					}
					out_chunk.XAS_data[i][j] = data;
				}
			}
		}

		void _memset(ref short[] destination,uint index , byte value, uint size)
		{
			for (int i = 0; i < size; i++)
			{
				destination[i + index] = value;
			}
		}
		void Encode_XAS_data(ref XAS_Chunk[] _out_data, short[] in_PCM, uint n_samples_per_channel, uint n_channels)
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
						PCM[sample_ind] = in_PCM[in_PCM_index + t_index+channel_ind];
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
				_memset(ref PCM ,samples_remain_per_channel, 0, (128 - samples_remain_per_channel));
				encode_XAS_Chunk(ref _out_data[index], PCM);
				index++;
			}
		}
		short[] ConvertToShortArray(byte[] byteArray)
		{
			if (byteArray.Length % 2 != 0)
			{
				throw new ArgumentException("Byte array length is not even. Unable to convert to short array.");
			}

			short[] shortArray = new short[byteArray.Length / 2];

			for (int i = 0; i < shortArray.Length; i++)
			{
				shortArray[i] = BitConverter.ToInt16(byteArray, i * 2);
			}

			return shortArray;
		}
		public byte[] EncodeXASData(byte[] rawdata,uint n_samples_per_channel,uint channels)
		{
			uint encode_size = GetXASEncodedSize(n_samples_per_channel, channels);
			XAS_Chunk[] out_data = new XAS_Chunk[encode_size / 76];
			Encode_XAS_data(ref out_data, ConvertToShortArray(rawdata), n_samples_per_channel, channels);
			List<byte> bytes = new List<byte>();
			foreach (var item in out_data)
			{
				foreach (var item1 in item.headers)
				{
					byte[] bytes1 = new byte[4];
					bytes1 = BitConverter.GetBytes(item1.data);
					for (int i = 0; i < bytes1.Length; i++)
					{
						bytes.Add(bytes1[i]);
					}
				}
				foreach (var item1 in item.XAS_data)
				{
					foreach (var item2 in item1)
					{
						bytes.Add(item2);
					}
				}
			}
			return bytes.ToArray();
		}
		public byte[] EncodeXASData(short[] rawdata, uint n_samples_per_channel, uint channels)
		{
			uint encode_size = GetXASEncodedSize(n_samples_per_channel, channels);
			XAS_Chunk[] out_data = new XAS_Chunk[encode_size / 76];
			Encode_XAS_data(ref out_data, rawdata, n_samples_per_channel, channels);
			List<byte> bytes = new List<byte>();
			foreach (var item in out_data)
			{
				foreach (var item1 in item.headers)
				{
					byte[] bytes1 = new byte[4];
					bytes1 = BitConverter.GetBytes(item1.data);
					for (int i = 0; i < bytes1.Length; i++)
					{
						bytes.Add(bytes1[i]);
					}
				}
				foreach (var item1 in item.XAS_data)
				{
					foreach (var item2 in item1)
					{
						bytes.Add(item2);
					}
				}
			}
			return bytes.ToArray();
		}
	}
}
