using static EA_ADPCM_XAS_CSharp.XASStruct;
namespace EA_ADPCM_XAS_CSharp.Encode
{
	internal class EncodeFunction
	{
		private short[] ArrayOffest(short[] scr,int offest)
		{
			short[] bytes = new short[scr.Length-offest];
			for (int i = 0; i < scr.Length-offest; i++)
			{
				bytes[i] = scr[offest+i];
			}
			return bytes;
		}
		public long encode_EA_XA_R2(ref byte[] data, short[] PCM, uint n_samples_per_channel, uint n_channels, short max_error)
		{
			long data_index = 0;
            for (int chan_ind = 0; chan_ind<n_channels; chan_ind++)
			{
				data_index += encode_EA_XA_R2_channel(ref data, ArrayOffest(PCM ,chan_ind), n_samples_per_channel, n_channels, max_error);
	        }
            return data_index;
        }
        long encode_EA_XA_R2_channel(ref byte[] data, short[] PCM, uint n_samples_per_channel, uint n_channels, short max_error)
		{
            int chunks_per_channel = (int)((n_samples_per_channel + 27) / 28);
		    short[] prev = new short[2];
			long data_index = 0;
			int PCM_index = 0;
		    encode_EA_XA_R2_chunk_nocompr(ref data, PCM, prev, (int) n_channels);
		    data_index += sizeof_uncompr_EA_XA_R23_block;
            for (int chunk_ind = 1; chunk_ind<chunks_per_channel; chunk_ind++)
			{
				PCM_index += 28 * chunk_ind * (int)n_channels;
                data_index += encode_EA_XA_R2_chunk(ref data,(int)data_index, ArrayOffest(PCM,PCM_index), prev, (int)n_channels, max_error);
	        }
            return data_index;
        }
        void encode_EA_XA_R1_chunk(byte[/*sizeof_EA_XA_R1_chunk*/] data, short[/*28*/] PCM, short[] prev,  int nCannels) 
		{
			byte[] temp = BitConverter.GetBytes(ToBigEndian16((ushort)prev[0]));
			data[0] = temp[0];
			data[1] = temp[1];
			byte[] temp1 = BitConverter.GetBytes(ToBigEndian16((ushort)prev[1]));
			data[2] = temp1[0];
			data[3] = temp1[1];
		    int coef_index = 0;
		    byte shift = 0;
		    simple_CalcCoefShift(PCM,0, prev, 28,ref coef_index,ref shift);
		    data[4] = (byte)(coef_index << 4 | shift);
			short[] _prev = new short[2]/*[2]*/;
			Buffer.BlockCopy(_prev,0, prev,0, 4);
		    encode_EA_XA_block(ref data,5, PCM, _prev, 28, nCannels, ea_adpcm_table_v2[coef_index], (byte)(12 + fixed_point_offset - shift));
	    }
	    long encode_EA_XA_R2_chunk(ref byte[/*sizeof_uncompr_EA_XA_R23_block*/] data,int index, short[] PCM, short[] prev, int nCannels, short max_error) 
		{
			int data_index = index;
            int coef_index = 0;
		    byte shift = 0;
		    int err = simple_CalcCoefShift(PCM,0, prev, 28, ref coef_index,ref shift);
            if (err > max_error)
			{
                encode_EA_XA_R2_chunk_nocompr(ref data, PCM, prev, nCannels);
                return sizeof_uncompr_EA_XA_R23_block;
            }
            else 
			{
				data[data_index] = (byte)(coef_index << 4 | shift);
                shift = (byte)(12 + fixed_point_offset - shift);
                short[] coefs = ea_adpcm_table_v2[coef_index];
	            encode_EA_XA_block(ref data,0, PCM, prev, 28, nCannels, coefs, shift);
                return sizeof_compr_EA_XA_R23_block;
            }
        }
		void encode_EA_XA_block(ref byte[] data,int data_index, short[] PCM, short[] prev, int samples, int PCM_step, short[] coefs, byte shift, int data_step = 1)
		{
			int index = data_index;
			for (int i = 0; i < samples / 2; i++) 
			{
				byte _data = 0;
				for (int j = 0; j < 2; j++)
				{
					EncodedSample enc = encode_XA_sample(prev[0], prev[1], coefs, PCM[(i * 2 + j) * PCM_step], shift);
					prev[0] = prev[1];
					prev[1] = enc.decoded;
					_data <<= 4;
					_data |= enc.encoded;
				}
				data[index] = _data;
				index += data_step;
			}
        }
		void encode_EA_XA_R2_chunk_nocompr(ref byte[] data, short[] PCM, short[] prev, int nChannels)
		{
			data[0] = 0xEE;
			int data_index = 0;
			byte[] temp = BitConverter.GetBytes(ToBigEndian16((ushort)PCM[26 * nChannels]));
			data[data_index+1] =  temp[0];
			data[data_index + 2] = temp[1];
			byte[] temp1 = BitConverter.GetBytes(ToBigEndian16((ushort)PCM[27 * nChannels])); 
			data[data_index + 3] = temp1[0];
			data[data_index + 4] = temp1[1];
			prev[0] = PCM[26 * nChannels];
			prev[1] = PCM[27 * nChannels];
			short[] pOutData = new short[(data.Length-5)/2];
			Buffer.BlockCopy(data,5,pOutData,0,data.Length-5);
			for (int i = 0; i < 28 * nChannels; i += nChannels)
			{
				pOutData[i] = (short)ToBigEndian16((ushort)PCM[i]);
			}
			Buffer.BlockCopy(pOutData, 0, data, 5, data.Length - 5);
		}
	    static EncodedSample encode_XA_sample(Int16 prev_samples,Int16 prev_samples1, Int16[] coef, int sample, int shift)
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
		static int simple_CalcCoefShift(short[] pSamples,int index, short[] in_prevSamples, int num_samples,ref int out_coef_index,ref byte out_shift) {
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
		static void encode_XAS_Chunk(ref XAS_Chunk out_chunk, short[] in_PCM)
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

		static void _memset(ref short[] destination,uint index , byte value, uint size)
		{
			for (int i = 0; i < size; i++)
			{
				destination[i + index] = value;
			}
		}
		static void Encode_XAS_data(ref XAS_Chunk[] _out_data, short[] in_PCM, uint n_samples_per_channel, uint n_channels)
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
		static short[] ConvertToShortArray(byte[] byteArray)
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
		public static byte[] EAXASEncode(byte[] rawdata,uint n_samples_per_channel,uint channels)
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
		public static byte[] EAXASEncode(short[] rawdata, uint n_samples_per_channel, uint channels)
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
