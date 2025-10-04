using System.Buffers.Binary;
using System.Runtime.Intrinsics.X86;
using System.Threading.Channels;
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
				short[] prev_samples = new short[] { 0 };
				int num_chunks = ((int)n_samples_per_channel + 27) / 28;
				for (int i = 0; i < num_chunks; i++)
				{
					int data_decoded_size = decode_EA_XA_R2_Chunk(_data, out_PCM, prev_samples);
					_data = _data.Slice(data_decoded_size);
					out_PCM = out_PCM.Slice(samples_in_EA_XA_R_chunk);
				}
			}
			public static int decode_EA_XA_R(Span<byte> data, Span<short> out_PCM, uint n_channels, int codec_id,int nb_samples)
			{
				//Code Error No Run
				Span<byte> org_data = data;
                bool big_endian = (codec_id == 3);
                int count = 0;
                int[] offsets = new int[6];
				int current_sample, previous_sample, next_sample = 0;
				byte shift;
				int coeff1, coeff2;
				short[][] out_data = new short[n_channels][];
                EaXaEncoder[] encoders = new EaXaEncoder[n_channels];
				List<short> _temp;
                for (int channel = 0; channel < n_channels; channel++)
				{
					encoders[channel] = new EaXaEncoder();
					offsets[channel] = (big_endian ? BinaryPrimitives.ReadInt32BigEndian(data) :
													 BinaryPrimitives.ReadInt32LittleEndian(data)) +
									   ((int)n_channels + 1) * 4;
					data = data.Slice(4);
                }
                for (int channel = 0; channel < n_channels; channel++)
                {
                    int count1;
					_temp = new List<short>();
					data = org_data.Slice(offsets[channel]);
                    //bytestream2_seek(&gb, offsets[channel], SEEK_SET);
                    //samplesC = samples_p[channel];

                    if (codec_id == 1)
                    {
                        current_sample = sign_extend(BinaryPrimitives.ReadInt16LittleEndian(data), 16);
						data = data.Slice(2);
                        previous_sample = sign_extend(BinaryPrimitives.ReadInt16LittleEndian(data), 16);
						data = data.Slice(2);
                    }
                    else
                    {
                        current_sample = encoders[channel].currentSample;
                        previous_sample = encoders[channel].previousSample;
                    }

                    for (count1 = 0; count1 < nb_samples / 28; count1++)
                    {
						int _byte = data[0];
						data = data.Slice(1);
                        if (_byte == 0xEE)
                        {  /* only seen in R2 and R3 */
                            current_sample = sign_extend(BinaryPrimitives.ReadInt16BigEndian(data), 16);
                            previous_sample = sign_extend(BinaryPrimitives.ReadInt16BigEndian(data), 16);

                            for (int count2 = 0; count2 < 28; count2++)
                                _temp.Add((short)sign_extend(BinaryPrimitives.ReadInt16BigEndian(data), 16));
                        }
                        else
                        {
                            coeff1 = XATable[_byte >> 4];
                            coeff2 = XATable[(_byte >> 4) + 4];
                            shift = (byte)(20 - (_byte & 0x0F));

                            for (int count2 = 0; count2 < 28; count2++)
                            {
								if ((count2 & 1) == 1)
								{
									next_sample = sign_extend(_byte, 4) << shift;
								}
								else
								{
									_byte = data[0];
									data = data.Slice(1);
									next_sample = sign_extend(_byte >> 4, 4) << shift;
								}

                                next_sample += (current_sample * coeff1) +
                                               (previous_sample * coeff2);
                                next_sample = Clip_int16(next_sample >> 8);

                                previous_sample = current_sample;
                                current_sample = next_sample;
                                _temp.Add((short)current_sample);
                            }
                        }
                    }
                    if (!(count != 0))
                    {
                        count = count1;
                    }
                    else if (count != count1)
                    {
                        count = Math.Max(count, count1);
                    }

                    if (codec_id != 1)
                    {
                        encoders[channel].currentSample = current_sample;
						encoders[channel].previousSample = previous_sample;
                    }
					out_data[channel] = _temp.ToArray();
                }
				
				int _count = 0;
				if (n_channels == 1)
				{
					_count = out_data[0].Length;
					out_PCM = out_data[0].AsSpan();
				}
				else {
					_count = Math.Min(out_data[0].Length, out_data[1].Length);
					short[] temp = new short[_count];
                    for (int i = 0; i < _count; i++)
                    {
                        for (int j = 0; j < n_channels; j++)
                        {
							temp[i * 2 +j] = out_data[j][i];
                        }
                    }
					out_PCM = temp.AsSpan();
                }
                
				return count;
            }
		}
	}
}