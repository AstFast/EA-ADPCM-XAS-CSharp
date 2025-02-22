using static EA_ADPCM_XAS_CSharp.XASStruct;
namespace EA_ADPCM_XAS_CSharp
{
	internal unsafe partial class EAAudio
	{
		public partial class MaxisXA
		{
			
			static void EncodeAndInterleave(in short[] samples,ref byte[] output,ref EaXaEncoder[] encoders, int channels, int nSamples)
			{
				int nBytes = 1 + (nSamples + 1) / 2;
				short[] temp = Array.Empty<short>();
				for (int c = 0; c < channels; c++)
				{
					int inputSamples_index = c * nSamples;
					byte[] encoded = new byte[15];
					encoders[c].encodeSubblock(samples, inputSamples_index, ref encoded,0, nSamples,false,ref temp);
					for (int b = 0; b < nBytes; b++)
					{
						output[c + b * channels] = encoded[b];
					}
				}
			}
			public static byte[] encode_Maxis_XA(in byte[] data,int channels)
			{
				EaXaEncoder[] encoders = new EaXaEncoder[channels];
				for (int i = 0; i < channels; i++)
				{
					encoders[i] = new EaXaEncoder();
				}
				int nSamples = data.Length / 2 / channels;
				int codedSamples = 0;
				bool lastBlock = false;
				int blockSize = 15 * channels;
				byte[] block = new byte[blockSize];
				short[] samples = new short[28 * channels];
				int data_index = 0;
				List<byte> out_data = new();
				while (!lastBlock)
				{
					int samplesInBlock = 28;
					codedSamples += samplesInBlock;
					if (codedSamples >= nSamples)
					{
						int toRemove = codedSamples - nSamples;
						samplesInBlock -= toRemove;
						codedSamples = nSamples;
						lastBlock = true;
					}
					if (!ReadSamples(data,ref data_index,ref samples,channels, samplesInBlock))
					{
						throw new IndexOutOfRangeException();
					}
					if (samplesInBlock < 28)
					{
						Array.Fill<byte>(block, 0,0, blockSize);
					}
					EncodeAndInterleave(samples,ref block,ref encoders,channels, samplesInBlock);
					out_data.AddRange(block);
				}
				return out_data.ToArray();
			}
		}
	}
}