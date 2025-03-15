using static EA_ADPCM_XAS_CSharp.XASStruct;
using System.Runtime.InteropServices;
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
	}
}
