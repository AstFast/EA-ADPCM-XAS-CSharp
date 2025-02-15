using System;
using static EA_ADPCM_XAS_CSharp.XASStruct;
namespace EA_ADPCM_XAS_CSharp
{
	internal unsafe partial class EAAudio
	{
		public partial class XA
		{
			/*
			public static byte[] encode_EA_XA(in byte[] data,int channels,int SampleRate,int version)
			{
				int data_index = 0;
				List<byte> out_data = new();
				EaXaEncoder[] encoders = new EaXaEncoder[2];
				for (int i = 0; i < 2; i++)
				{
					encoders[i] = new EaXaEncoder();
				}
				byte[] block;
				short[] samples;
				int codedSamples = 0;
				int blockIndex = 1;
				bool lastBlock = false;
				int nSamples = data.Length / 2 / channels;
				int blocksPerSecond = version == 3 ? 5 : 15;
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

					if (blockIndex == 1 && version == 1)
					{
						for (int c = 0; c < channels; c++)
						{
							encoders[c].currentSample = encoders[c].previousSample = samples[c * samplesInBlock];
						}
					}

					int blockSize;
					block.reset(writeSCDlBlock(samplesInBlock,ref samples, blockIndex == 1, blockSize));
                    out.write((char*)block.get(), blockSize);

					blockIndex++;
				}
			}
			*/
		}
	}
}
