using System;
using System.Runtime.InteropServices;
using static EA_ADPCM_XAS_CSharp.XASStruct;
namespace EA_ADPCM_XAS_CSharp
{
	internal unsafe partial class EAAudio
	{
		public partial class XA
		{

			public static Span<byte> writeSCDlBlock(int nbSamples,in short[] samples,ref EaXaEncoder[] encoders,int channels, bool firstBlock, int revision)
			{
				int nCompleteSubblocks = nbSamples / 28;
				int nSamplesExtraSubblock = nbSamples % 28;
				Span<byte> output = new byte[16 + channels * (184 + 15 + nCompleteSubblocks * 15)];
				Span<byte> dataIte = output;
				//memcpy(&((SCBlockHead*)output)->id, "SCDl", 4);
				//dataIte += sizeof(SCBlockHead);
				//bufferWriteValue<uint32_t>(dataIte, revision == 3 ? BSWAP32(nbSamples) : nbSamples);
				//Span<int> channelsOffsets = MemoryMarshal.Cast<byte, int>(dataIte);
				//dataIte += 4 * input.nChannels;
				Span<byte> channelBlocksStart = dataIte;
				for (int c = 0; c < channels; c++)
				{
					//channelsOffsetsToData[c] = revision == 3 ? BSWAP32(dataIte - channelBlocksStart) : (dataIte - channelBlocksStart);
					Span<short> channelSamples = samples.AsSpan().Slice(c * nbSamples);
					if (revision == 1)
					{
						Span<short> predictionStartSamples = MemoryMarshal.Cast<byte, short>(dataIte);
						
						predictionStartSamples[0] = (short)encoders[c].currentSample;
						predictionStartSamples[1] = (short)encoders[c].previousSample;
						dataIte = dataIte.Slice(4);
						//dataIte += 4;
						encoders[c].clearErrors();
					}

					int i = 0;
					if (firstBlock && revision > 1)
					{
						for (; i < 3 && i < nCompleteSubblocks; i++)
						{
							encoders[c].writeUncompressedSubblock(channelSamples, dataIte, 28, i == 2 && nCompleteSubblocks != 3 ? UncompressedType.FadeToCompressed : UncompressedType.Normal);
							channelSamples = channelSamples.Slice(28);
							dataIte = dataIte.Slice(61);
						}
					}
					for (; i < nCompleteSubblocks; i++)
					{
						encoders[c].encodeSubblock(channelSamples, dataIte, 28);
						channelSamples = channelSamples.Slice(28);
						dataIte = dataIte.Slice(15);
					}
					if (nSamplesExtraSubblock != 0)
					{
						if (revision == 1)
						{
							dataIte.Slice(0,15).Fill(0);
							encoders[c].encodeSubblock(channelSamples, dataIte, nSamplesExtraSubblock);
							dataIte = dataIte.Slice(15);
						}
						else
						{
							encoders[c].writeUncompressedSubblock(channelSamples, dataIte, nSamplesExtraSubblock, nCompleteSubblocks <= 3 ? UncompressedType.Normal : UncompressedType.FadeFromCompressed);
							dataIte = dataIte.Slice(61);
						}
					}
					if (((output.Length - dataIte.Length) & 1)==1)
					{ // Padding
						dataIte[0] = 0;
						dataIte = dataIte.Slice(1);
					}
				}

				int SCDlSize = output.Length - dataIte.Length;
				if ((SCDlSize & 2)!=0)
				{ // Padding
					MemoryMarshal.Cast<byte, short>(dataIte)[0] = 0;
					SCDlSize += 2;
				}
				return output.Slice(0,SCDlSize);
			}

			public static byte[] encode_EA_XA_R(in byte[] data, int channels,int SampleRate,int revision)
			{
				List<byte> outdata = new();
				int nSamples = data.Length / 2 / channels;
				EaXaEncoder[] encoders = new EaXaEncoder[channels];
				for (int i = 0; i < channels; i++)
				{
					encoders[i] = new EaXaEncoder();
				}
				Span<byte> block;
				short[] samples;
				int codedSamples = 0;
				int blockIndex = 1;
				bool lastBlock = false;
				int blocksPerSecond = revision == 3 ? 5 : 15;
				int data_index = 0;
				while (!lastBlock)
				{
					int samplesInBlock = ((blockIndex * SampleRate) / blocksPerSecond) - codedSamples;
					int modulo28 = samplesInBlock % 28;
					if (modulo28 != 0)
					{
						samplesInBlock = samplesInBlock + 28 - modulo28;
					}
					codedSamples += samplesInBlock;
					if (codedSamples >= nSamples)
					{
						int toRemove = codedSamples - nSamples;
						samplesInBlock -= toRemove;
						codedSamples = nSamples;
						lastBlock = true;
					}

					samples = new short[samplesInBlock * channels];
					if (!ReadSamples(data,ref data_index,ref samples,channels, samplesInBlock))
					{
						throw new IndexOutOfRangeException();
					}

					if (blockIndex == 1 && revision == 1)
					{
						for (int c = 0; c < channels; c++)
						{
							encoders[c].currentSample = encoders[c].previousSample = samples[c * samplesInBlock];
						}
					}
#if NET7_0
					outdata.AddRange(writeSCDlBlock(samplesInBlock, samples, ref encoders, channels, blockIndex == 1, revision).ToArray());
#else
					outdata.AddRange(writeSCDlBlock(samplesInBlock, samples, ref encoders, channels, blockIndex == 1, revision));
#endif
					blockIndex++;
				}

				//writeNumberOfBlocksInHeader(out, blockIndex - 1);

                //out.write("SCEl\x08\x00\x00\x00", 8); // EOF block

				//return true;
				return outdata.ToArray();
			}
		}
        static EncodedSample encode_XA_sample(Span<short> prev_samples, short[] coef, int sample, byte shift)
        {

            int prediction = prev_samples[1] * coef[0] + prev_samples[0] * coef[1];

            int correction = (sample << fixed_point_offset) - prediction;

            int res;
            int rounding = 1 << (shift - 1);
            res = Clip_int4((correction + rounding) >> shift);

            int predecoded = ((res << shift) + prediction + def_rounding) >> fixed_point_offset;
            int decoded = Clip_int16(predecoded);

            // ---- for better precision on clipping or near-clipping, this can be removed
            int term = 1 << (shift - fixed_point_offset); // it's like +-1 to res until >> fixed_point_offset
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
            return new EncodedSample { decoded = (short)decoded, encoded = (byte)res };
        }
        static void encode_EA_XA_block(Span<byte> data, Span<short> PCM, Span<short> prev, int samples, int PCM_step, short[] coefs, byte shift, int data_step = 1)
		{
			for (int i = 0; i < samples / 2; i++)
			{
				byte _data = 0;
				for (int j = 0; j < 2; j++)
				{
					EncodedSample enc = encode_XA_sample(prev, coefs, PCM[(i * 2 + j) * PCM_step], shift);
					prev[0] = prev[1];
					prev[1] = enc.decoded;
					_data <<= 4;
					_data |= enc.encoded;
				}
				data[0] = _data;
				data = data.Slice(data_step);
			}
		}
		static void encode_EA_XA_R2_chunk_nocompr(Span<byte> data, Span<short> PCM, Span<short> prev, int nCannels)
		{
			data[0] = 0xEE;
			MemoryMarshal.Cast<byte,short>(data.Slice(1))[0] = ToBigEndian16(PCM[26 * nCannels]);
            MemoryMarshal.Cast<byte, short>(data.Slice(3))[0] = ToBigEndian16(PCM[27 * nCannels]);
			prev[0] = PCM[26 * nCannels];
			prev[1] = PCM[27 * nCannels];
			Span<short> pOutData = MemoryMarshal.Cast<byte, short>(data.Slice(5));
			for (int i = 0; i < 28 * nCannels; i += nCannels)
			{
				pOutData[i] = ToBigEndian16(PCM[i]);
			}
		}
        static int simple_CalcCoefShift(Span<short> pSamples, Span<short> in_prevSamples, int num_samples, out int out_coef_index, out byte out_shift)
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
                    int sample = pSamples[i];
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
        static int encode_EA_XA_R2_chunk(Span<byte> data, Span<short> PCM, Span<short> prev, int nCannels, short max_error)
		{
			//sizeof_uncompr_EA_XA_R23_block
			
			int err = simple_CalcCoefShift(PCM, prev, 28,out int coef_index,out byte shift);
			if (err > max_error)
			{
				encode_EA_XA_R2_chunk_nocompr(data, PCM, prev, nCannels);
				return sizeof_uncompr_EA_XA_R23_block;
			}
			else
			{
				data[0] = (byte)(coef_index << 4 | shift);
				data = data.Slice(1);
                shift = (byte)(12 + fixed_point_offset - shift);
				short[] coefs = ea_adpcm_table_v2[coef_index];
				encode_EA_XA_block(data, PCM, prev, 28, nCannels, coefs, shift);
				return sizeof_compr_EA_XA_R23_block;
			}
		}
		static int encode_EA_XA_R2_channel(Span<byte> data, Span<short> PCM, uint n_samples_per_channel, uint n_channels, short max_error) 
		{
			int chunks_per_channel = ((int)n_samples_per_channel + 27) / 28;
			short[] prev = new short[2];
			Span<byte> curr_data = data;
			encode_EA_XA_R2_chunk_nocompr(curr_data, PCM, prev, (int)n_channels);
			curr_data = curr_data.Slice(sizeof_uncompr_EA_XA_R23_block);
			for (int chunk_ind = 1; chunk_ind < chunks_per_channel; chunk_ind++) {
				curr_data = curr_data.Slice(encode_EA_XA_R2_chunk(curr_data, PCM.Slice(28 * chunk_ind * (int)n_channels), prev, (int)n_channels, max_error));
			}
			return data.Length - curr_data.Length;
		}

		public static int encode_EA_XA_R2(Span<byte> data, Span<short> PCM, uint n_samples_per_channel, uint n_channels, short max_error)
		{
			Span<byte> curr_data = data;
			for (int chan_ind = 0; chan_ind < n_channels; chan_ind++)
			{
				curr_data = curr_data.Slice(encode_EA_XA_R2_channel(curr_data, PCM.Slice(chan_ind), n_samples_per_channel, n_channels, max_error));
			}
			return data.Length - curr_data.Length;
		}
	}
}
