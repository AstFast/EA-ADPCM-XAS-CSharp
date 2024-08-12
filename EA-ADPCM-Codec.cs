/*reference:
 * EA-ADPCM-XAS:https://github.com/CrabJournal/EA-ADPCM-Codec
 * EA-ADPCM(Maxis XA):https://simstek.fandom.com/wiki/XA
 * 
 * Other:
 * https://ffmpeg.org/doxygen/trunk/adpcm_8c.html
 * https://github.com/vgmstream/vgmstream/blob/master/src/coding/ea_xa_decoder.c
 */
using System;
using static EA_ADPCM_XAS_CSharp.XASStruct;
namespace EA_ADPCM_XAS_CSharp
{
	internal class EAAudio
	{
		public class XA
		{
			private class Channel
			{
				public short PrevSample { get; set; }
				public short CurSample { get; set; }
				public int Divisor { get; set; }
				public int C1 { get; set; }
				public int C2 { get; set; }
			}
			public static void decode_Maxis_XA(byte[] inBuffer, int blockCount, short[] outBuffer, int channels)
			{
				var channel = new Channel[channels];
				for (int i = 0; i < channels; i++)
				{
					channel[i] = new Channel();
				}

				int inIndex = 0;
				int outIndex = 0;

				while (blockCount-- > 0)
				{
					for (int i = 0; i < channels; i++)
					{
						byte currentByte = inBuffer[inIndex++];
						channel[i].Divisor = (currentByte & 0x0F) + 8;
						channel[i].C1 = XASStruct.XATable[currentByte>>4];
						channel[i].C2 = XASStruct.XATable[(currentByte >> 4) + 4];
					}

					for (int i = 0; i < 14; i++)
					{
						for (int j = 0; j < channels; j++)
						{
							byte currentByte = inBuffer[inIndex++];
							for (int n = 0; n < 2; n++)
							{
								int newValue = (n == 0) ? (currentByte>>4) : (currentByte & 0x0F);
								newValue = (newValue << 28) >> channel[j].Divisor;
								newValue = (newValue + channel[j].CurSample * channel[j].C1 + channel[j].PrevSample * channel[j].C2 + 128) >> 8;
								channel[j].PrevSample = channel[j].CurSample;
								channel[j].CurSample = (short)Clip_int16(newValue);
							}
							outBuffer[outIndex++] = channel[j].PrevSample;
						}
						for (int j = 0; j < channels; j++)
						{
							outBuffer[outIndex++] = channel[j].CurSample;
						}
					}
				}
			}
		}
		public class XAS
		{
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
			public static void decode_XAS_v1(XAS_Chunk[] _in_data, ref short[] out_PCM, uint n_samples_per_channel, uint n_channels)
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
			public static void decode_XAS_v0(Stream stream, short[] outbuf, int channelspacing, int firstSample, int samplesToDo)
			{
				byte[] frame = new byte[0x13];
				int framesIn, samplesDone = 0, sampleCount = 0;
				int bytesPerFrame = 0x02 + 0x02 + 0x0f;
				int samplesPerFrame = 1 + 1 + 0x0f * 2;
				framesIn = firstSample / samplesPerFrame;
				firstSample = firstSample % samplesPerFrame;
				long frameOffset = stream.Position + bytesPerFrame * framesIn;
				stream.Seek(frameOffset, SeekOrigin.Begin);
				stream.Read(frame, 0, bytesPerFrame);
				{
					short[] hists = new short[2];
					uint frameHeader = BitConverter.ToUInt32(frame, 0);
					float[] coef = XASStruct.ea_adpcm_table_xas_v0[frameHeader & 0x0F];
					hists[1] = (short)((frameHeader >> 0) & 0xFFF0);
					hists[0] = (short)((frameHeader >> 16) & 0xFFF0);
					byte shift = (byte)((frameHeader >> 16) & 0x0F);
					if (sampleCount >= firstSample && samplesDone < samplesToDo)
					{
						outbuf[samplesDone * channelspacing] = hists[1];
						samplesDone++;
					}
					sampleCount++;
					if (sampleCount >= firstSample && samplesDone < samplesToDo)
					{
						outbuf[samplesDone * channelspacing] = hists[0];
						samplesDone++;
					}
					sampleCount++;
					for (int i = 0; i < 0x0f * 2; i++)
					{
						byte nibbles = frame[0x02 + 0x02 + i / 2];
						int sample = (i & 1) != 0?(nibbles >> 0) & 0x0F :(nibbles >> 4) & 0x0F;
						sample = (short)(sample << 12) >> shift;
						sample = (int)(sample + hists[0] * coef[0] + hists[1] * coef[1]);
						sample = XASStruct.Clip_int16(sample);
						if (sampleCount >= firstSample && samplesDone < samplesToDo)
						{
							outbuf[samplesDone * channelspacing] = (short)sample;
							samplesDone++;
						}
						sampleCount++;
						hists[1] = hists[0];
						hists[0] = (short)sample;
					}
				}
			}

		}

	}
}
