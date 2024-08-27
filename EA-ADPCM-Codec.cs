using System.Runtime.InteropServices;
using static EA_ADPCM_XAS_CSharp.XASStruct;
namespace EA_ADPCM_XAS_CSharp
{
	internal unsafe class EAAudio
	{
		public class XA
		{
			#region XA v1
			#region decode
			public static int adpcm_history1_32 = 0,adpcm_history2_32 = 0;
			static void decode_XA_channel_v1(byte[]* data, short[]* PCM,int channelspacing, int channel,int num_chunk,bool is_stereo)
			{
				byte frame_info;
				int shift;
				int[] coef = new int[2];
				bool hn = (channel == 0);
				if (is_stereo)
				{
					frame_info = (*data)[0];
					coef[0] = XATable[(hn ? frame_info >> 4 : frame_info & 0x0F) + 0];
					coef[1] = XATable[(hn ? frame_info >> 4 : frame_info & 0x0F) + 4];

					frame_info = (*(data+0x01))[0];
					shift = (hn ? frame_info >> 4 : frame_info & 0x0F) + 8;
				}
				else
				{
					frame_info = (*data)[0];
					coef[0] = XATable[(frame_info >> 4) + 0];
					coef[1] = XATable[(frame_info >> 4) + 4];
					shift = (frame_info & 0x0F) + 8;
				}
				for (int i = 0, sample_count = 0; i < num_chunk; i++, sample_count += channelspacing)
				{
					long byte_offset = is_stereo ? (0x02 + i) : (0x01 + i / 2);
					int nibble_shift = is_stereo ? (hn ? 4 : 0) : ((!(i % 2 == 1)) ? 4 : 0);
					byte sample_byte = (*(data+byte_offset))[0];
					byte sample_nibble = (byte)((sample_byte >> nibble_shift) & 0x0F);
					int new_sample = (sample_nibble << 28) >> shift;
					new_sample = (new_sample + coef[0] * adpcm_history1_32 + coef[1] * adpcm_history2_32 + 128) >> 8;
					new_sample = Clip_int16(new_sample);
					(*PCM)[sample_count] = (short)new_sample;
					adpcm_history2_32 = adpcm_history1_32;
					adpcm_history1_32 = new_sample;
				}
			}
			public static void decode_EA_XA_v1(void* data, short[]* PCM,int channels,int sample_to_do)
			{
				bool is_stereo = (channels > 1);
				byte[]* _data = (byte[]*)data;
				int frame_size = is_stereo ? 0x0f * 2 : 0x0f;
				for (int ch = 0; ch < channels; ch++)
				{
					decode_XA_channel_v1(_data + ch, PCM + ch,channels, ch,sample_to_do, is_stereo);
					_data += frame_size;
				}
			}
			#endregion
			#region encode
			static void encode_EA_XA_R1_chunk(byte[]* data/*sizeof_EA_XA_R1_chunk*/,short[]* PCM/*28*/,ref short[] prev,  int nCannels)
			{
                *(short*)data = ToBigEndian16(prev[0]);
                *(short*)(data + 2) = ToBigEndian16(prev[1]);
			    int coef_index;
			    byte shift;
			    simple_CalcCoefShift(PCM,ref prev, 28, &coef_index, &shift);
			    (*data)[4] = (byte)(coef_index << 4 | shift);
                short[] _prev =  prev;
			    encode_EA_XA_block(data + 5, PCM,ref _prev, 28, nCannels, ea_adpcm_table_v2[coef_index], (byte)(12 + fixed_point_offset - shift));
		    }
			public static long encode_EA_XA_R1(void* data, short[]* PCM, int nCannels)
			{
				byte[]* _data = (byte[]*)data;
				short[] prev_samples = { 0, 0, 0 };
				long num_chunks = _data->Length / nCannels / sizeof_EA_XA_R1_chunk;
				for (int i = 0; i < num_chunks; i++)
				{
                    for (int j = 0; j < nCannels; j++)
                    {
						encode_EA_XA_R1_chunk(_data, PCM, ref prev_samples, nCannels);
						_data += samples_in_EA_XA_R_chunk;
						PCM += sizeof_EA_XA_R1_chunk;
					}
				}
				return _data - (byte[]*)data;
			}
			#endregion
			#endregion
			#region XA v2
			#region decode
			static short decode_XA_sample(short[] prev_samples, short[] coef, int int4, byte shift)
			{
				int correction = (int)int4 << shift;
				int prediction = prev_samples[1] * coef[0] + prev_samples[0] * coef[1];
				return (short)Clip_int16((prediction + correction + def_rounding) >> fixed_point_offset);
			}
			static long decode_EA_XA_R2_Chunk(void* XA_Chunk, short[]* out_PCM,short[]* prev_samples)
			{
				byte[]* p_curr_byte = (byte[]*)XA_Chunk;
				short[]* pSample = out_PCM;
				byte _byte = (*p_curr_byte)[0];
				p_curr_byte++;
				short[]* p_prev_samples = prev_samples;
				if (_byte == 0xEE)
				{
					(*prev_samples)[1] = Get_s16be(p_curr_byte);
					p_curr_byte += 2;
					(*prev_samples)[0] = Get_s16be(p_curr_byte);
					p_curr_byte += 2;
					for (int i = 0; i < samples_in_EA_XA_R_chunk; i++)
					{
						(*pSample)[0] = Get_s16be(p_curr_byte);
						pSample++;
						p_curr_byte += 2;
					}
				}
				else
				{
					int coef_index = _byte >> 4;
					short[] coef = ea_adpcm_table_v2[coef_index];
					byte shift = (byte)(12 + fixed_point_offset - (_byte & 0xF));
					for (int j = 0; j < samples_in_EA_XA_R_chunk / 2; j++)
					{
						SamplesByte data = *(SamplesByte*)(p_curr_byte++);
						(*pSample)[0] = decode_XA_sample(*p_prev_samples, coef, data.sample0, shift);
						(*prev_samples)[2] = (*pSample)[0];
						(*pSample)[1] = decode_XA_sample(*(p_prev_samples + 1), coef, data.sample1, shift);
						p_prev_samples = pSample;
						pSample += 2;
					}
					(*prev_samples)[1] = (*pSample)[-1];
					(*prev_samples)[0] = (*pSample)[-2];
				}

				return p_curr_byte - (byte[]*)XA_Chunk;
			}
			public static void decode_EA_XA_R2(void* data, short[]* out_PCM, uint n_samples_per_channel, uint n_channels)
			{
				byte[]* _data = (byte[]*)data;
				short[] prev_samples = { 0, 0, 0 };
				uint num_chunks = (n_samples_per_channel + (samples_in_EA_XA_R_chunk - 1)) / samples_in_EA_XA_R_chunk;
				for (int i = 0; i < num_chunks; i++)
				{
					long data_decoded_size = decode_EA_XA_R2_Chunk(_data, out_PCM,&prev_samples);
					_data += data_decoded_size;
					out_PCM += samples_in_EA_XA_R_chunk;
				}
			}
			#endregion
			#region encode
			static int simple_CalcCoefShift(short[]* pSamples,ref short[] in_prevSamples, int num_samples, int* out_coef_index, byte* out_shift)
			{
				const int num_coefs = 4;

				int min_max_error = int.MaxValue;
				int s_min_max_error = int.MaxValue;
				int best_coef_ind = 0;
				for (int coef_ind = 0; coef_ind < num_coefs; coef_ind++)
				{
					short[] prevSamples = new short[] { in_prevSamples[0], in_prevSamples[1] };
					int max_error = 0;
					int s_max_error = 0;
					for (int i = 0; i < num_samples; i++)
					{
						int prediction = ea_adpcm_table_v2[coef_ind][0] * prevSamples[1] + ea_adpcm_table_v2[coef_ind][1] * prevSamples[0];
						int sample = (*pSamples)[i];
						sample <<= fixed_point_offset;
						int s_error = sample - prediction;
						int error = Math.Abs(s_error);
						if (error > max_error)
						{
							max_error = error;
							s_max_error = s_error;
						}
						prevSamples[0] = prevSamples[1];
						prevSamples[1] = (*pSamples)[i];
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
				*out_coef_index = best_coef_ind;
				*out_shift = (byte)exp_shift;
				return max_min_error_i16;
			}
			static EncodedSample encode_XA_sample(ref short[] prev_samples, short[] coef, int sample, byte shift)
			{
				int prediction = prev_samples[1] * coef[0] + prev_samples[0] * coef[1];

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
					if (res != -8 && Math.Abs(decoded - sample) > Math.Abs(decoded2 - sample))
					{
						res -= 1;
						decoded = decoded2;
					}
				}
				return new EncodedSample { decoded = (Int16)decoded, encoded = (byte)res };
			}
			static void encode_EA_XA_block(byte[]* data,short[]* PCM,ref short[] prev, int samples, int PCM_step, short[] coefs, byte shift, int data_step = 1)
			{
                for (int i = 0; i<samples/2; i++)
				{
                    byte _data = 0;
                    for (int j = 0; j< 2; j++)
					{
                        EncodedSample enc = encode_XA_sample(ref prev, coefs, (*PCM)[(i * 2 + j) * PCM_step], shift);
			            prev[0] = prev[1];
                        prev[1] = enc.decoded;
                        _data <<= 4;
                        _data |= enc.encoded;
                    }
					(*data)[0] = _data;
                    data += data_step;
                 }
            }
            static void encode_EA_XA_R2_chunk_nocompr(byte[]* data/*sizeof_uncompr_EA_XA_R23_block*/, short[]* PCM/*samples_in_EA_XA_R_chunk*/,ref short[] prev/*2*/, int nCannels) 
			{
				(*data)[0] = 0xEE;
                *(short*)(data + 1) = ToBigEndian16((*PCM)[26 * nCannels]);
                *(short*) (data + 3) = ToBigEndian16((*PCM)[27 * nCannels]);
			    prev[0] = (*PCM)[26 * nCannels];
                prev[1] = (*PCM)[27 * nCannels];
	            short[]* pOutData = (short[]*)(data + 5);
	            for (int i = 0; i< samples_in_EA_XA_R_chunk * nCannels; i+=nCannels) 
				{
		            (*pOutData)[i] = ToBigEndian16((*PCM)[i]);
		        }
	        }
			static long encode_EA_XA_R2_chunk(byte[]* data/*sizeof_uncompr_EA_XA_R23_block*/, short[]* PCM/*samples_in_EA_XA_R_chunk*/,ref short[] prev/*2*/, int nCannels, short max_error) 
			{
                int coef_index;
			    byte shift;
			    int err = simple_CalcCoefShift(PCM,ref prev, 28, &coef_index, &shift);
                if (err > max_error)
				{
                    encode_EA_XA_R2_chunk_nocompr(data, PCM,ref prev, nCannels);
                    return sizeof_uncompr_EA_XA_R23_block;
                }
                else 
				{
					(*data)[0] = (byte)(coef_index << 4 | shift);
					data++;
                    shift = (byte)(12 + fixed_point_offset - shift);
                    short[] coefs = ea_adpcm_table_v2[coef_index];
		            encode_EA_XA_block(data, PCM,ref prev, 28, nCannels, coefs, shift);
                    return sizeof_compr_EA_XA_R23_block;
                }
            }
            static long encode_EA_XA_R2_channel(void* data, short[]* PCM, uint n_samples_per_channel, uint n_channels, short max_error)
			{
                uint chunks_per_channel = (n_samples_per_channel + (samples_in_EA_XA_R_chunk-1)) / samples_in_EA_XA_R_chunk;
			    short[] prev = new short[2];
			    byte[]* curr_data = (byte[]*)data;
			    encode_EA_XA_R2_chunk_nocompr(curr_data, PCM,ref prev, (int) n_channels);
			    curr_data += sizeof_uncompr_EA_XA_R23_block;
                for (int chunk_ind = 1; chunk_ind<chunks_per_channel; chunk_ind++)
				{
                    curr_data += encode_EA_XA_R2_chunk(curr_data, PCM + 28*chunk_ind* n_channels,ref prev, (int)n_channels, max_error);
		        }
                return curr_data - (byte[]*) data;
	        }
	        public static long encode_EA_XA_R2(void* data,short[] PCM, uint n_samples_per_channel, uint n_channels, short max_error = 10) 
			{
                byte[]* curr_data = (byte[]*)data;
				GCHandle handle = GCHandle.Alloc(PCM, GCHandleType.Pinned);
				IntPtr ptr = handle.AddrOfPinnedObject();
				for (int chan_ind = 0; chan_ind<n_channels; chan_ind++)
				{
                    curr_data += encode_EA_XA_R2_channel(curr_data, (short[]*)IntPtr.Add(ptr,chan_ind).ToPointer(), n_samples_per_channel, n_channels, max_error);
		        }
				handle.Free();
                return curr_data - (byte[]*) data;
	        }
			#endregion
			#endregion
			#region Maxis XA
			
			static void deocode_maxis_xa_channel(byte[]* data, short[]* PCM,int channelspacing,int channel,int sample_to_do)
			{
				int frame_samples = 28;
				byte frame_info = (*(data + channel))[0];
				int[] coef = { XATable[(frame_info >> 4) + 0], XATable[(frame_info >> 4) + 4]};
				byte shift = (byte)((frame_info & 0x0F) + 8);
				for (int i = 0, sample_count = 0; i < sample_to_do; i++,sample_count+= channel)
				{
					long byte_offset = (0x01 * channelspacing + (channelspacing == 2 ? i / 2 + channel + (i / 2) * 0x01 : i / 2));
					int nibble_shift = ((i & 1)==0) ? 4 : 0;
					byte sample_byte = (*(data + byte_offset))[0];
					byte sample_nibble = (byte)((sample_byte >> nibble_shift) & 0x0F);
					int new_sample = (sample_nibble << 28) >> shift;
					new_sample = (new_sample + coef[0] * adpcm_history1_32 + coef[1] * adpcm_history2_32 + 128) >> 8;
					new_sample = Clip_int16(new_sample);

					(*PCM)[sample_count] = (byte)new_sample;
					adpcm_history2_32 = adpcm_history1_32;
					adpcm_history1_32 = new_sample;
				}
			}
			public static void deocode_maxis_xa(byte[]* data, short[]* PCM, int channels)
			{
                for (int i = 0; i < channels; i++)
                {
					deocode_maxis_xa_channel(data,PCM,channels,i, ea_xa_bytes_to_samples(data->Length,channels));
					data += 0x0f * channels;
				}
            }
			#endregion
		}
		public class XAS
		{
			#region XAS v0
			static void decode_XAS_chunk_v0(void* in_data,short[]* PCM,uint n_channels)
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
					samples_done+=2;
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
					samples_done+=2;
					hist[0] = hist[1];
					hist[1] = (short)sample;
				}
			}
			public static long decode_XAS_v0(void* in_data,ref short[] out_PCM,uint n_samples_per_channel, uint n_channels)
			{
				byte[]* _in_data = (byte[]*)in_data;
				long out_PCM_index = 0;
				IntPtr ptr = Marshal.AllocHGlobal(samples_in_XAS_per_subchunk * sizeof(short));//seem XA is 28
				long n_chunks_per_channel = (n_samples_per_channel + (sizeof_EA_XA_R1_chunk - 1)) / sizeof_EA_XA_R1_chunk;
				for (int r = 0;r < n_chunks_per_channel-1;r++)
                {
					for (int j = 0; j < n_channels; j++)
					{
						decode_XAS_chunk_v0(_in_data, (short[]*)ptr.ToPointer(),n_channels);
						_in_data += sizeof_EA_XA_R1_chunk;
                        for (int i = 0; i < sizeof_EA_XA_R1_chunk; i++)
						{
							out_PCM[out_PCM_index + j + i * n_channels] = Marshal.ReadInt16(ptr,i * sizeof(short));
						}
                    }
					out_PCM_index += sizeof_EA_XA_R1_chunk * n_channels;
				}
				long samples_remain_per_channel = n_samples_per_channel - (n_chunks_per_channel - 1) * sizeof_EA_XA_R1_chunk;
				for (int i = 0; i < n_channels; i++)
                {
					decode_XAS_chunk_v0(_in_data, (short[]*)ptr.ToPointer(), n_channels);
                    for (int j = 0; j < samples_remain_per_channel; j++)
                    {
						out_PCM[out_PCM_index + i + j * n_channels] = Marshal.ReadInt16(ptr, i * sizeof(short));
					}
                }
				Marshal.FreeHGlobal(ptr);
				return _in_data - (byte[]*)in_data;
			}
			#endregion
			#region XAS v1
			static void _memset(ref short[] destination, uint index, byte value, uint size)
			{
				for (int i = 0; i < size; i++)
				{
					destination[i + index] = value;
				}
			}
			static int simple_CalcCoefShift(short[] pSamples, long index, short[] in_prevSamples, int num_samples, out int out_coef_index, out byte out_shift)
			{
				const int num_coefs = 4;

				int min_max_error = int.MaxValue;
				int s_min_max_error = int.MaxValue;
				int best_coef_ind = 0;
				for (int coef_ind = 0; coef_ind < num_coefs; coef_ind++)
				{
					short[] prevSamples = new short[] { in_prevSamples[0], in_prevSamples[1] };
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
						prevSamples[1] = pSamples[i];
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
			static EncodedSample encode_XA_sample(short[] prev_samples, int index, short[] coef, int sample, int shift)
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
							EncodedSample enc = encode_XA_sample(pDecodedSamples, index, coef, pInSamples[2 + index], shift);
							pDecodedSamples[2 + index] = enc.decoded;
							data <<= 4;
							data |= (byte)(enc.encoded & 0xF);
							index++;
						}
						out_chunk.XAS_data[i][j] = data;
					}
				}
			}
			public static void encode_XAS_v1(ref XAS_Chunk[] _out_data, short[] in_PCM, uint n_samples_per_channel, uint n_channels)
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
			public static void decode_XAS_v1(void* in_data, ref short[] out_PCM, uint n_samples_per_channel, uint n_channels)
			{
				if (n_samples_per_channel == 0)
				{
					return;
				}
				byte[]* _data = (byte[]*)in_data;
				uint encode_size = GetXASEncodedSize(n_samples_per_channel, n_channels);
				XAS_Chunk[] _in_data = new XAS_Chunk[encode_size / 76];
                for (int i = 0; i < _in_data.Length; i++)
                {
					_in_data[i].headers = new XAS_SubChunkHeader[subchunks_in_XAS_chunk];
					_in_data[i].XAS_data = new byte[15][];
                    for (int j = 0; j < 4; j++)
                    {
						_in_data[i].headers[j].data = *(uint*)_data;
						_data += 4;
					}
                    for (int j = 0; j < 15; j++)
                    {
						_in_data[i].XAS_data[j] = new byte[subchunks_in_XAS_chunk];
                        for (int k = 0; k < subchunks_in_XAS_chunk; k++)
                        {
							_in_data[i].XAS_data[j][k] = (*_data)[0];
							_data++;
						}
                    }
                }
                int _in_data_index = 0,out_PCM_Offest = 0;
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
