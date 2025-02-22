using System;
using System.Runtime.InteropServices;
using static EA_ADPCM_XAS_CSharp.XASStruct;
namespace EA_ADPCM_XAS_CSharp
{
	internal unsafe partial class EAAudio
	{
		public partial class MaxisXA
		{
			 class Channel_t {
				public short PrevSample = 0, CurSample = 0;
				public int divisor = 0;
				public int c1 = 0, c2 = 0;
			}
			;
            public static ref short[] decode_Maxis_XA(in byte[] inBuffer, int channels)
            {
				int bytesPerBlock = channels * 15;
				Channel_t[] channelsArray = new Channel_t[channels];
				for (int i = 0; i < channels; i++)
				{
					channelsArray[i] = new Channel_t();
				}
				int blockCount = inBuffer.Length / bytesPerBlock;
				int samplesPerBlock = 14 * channels * 2;
				int requiredOutLength = blockCount * samplesPerBlock;
				short[] outBuffer = new short[requiredOutLength];
				int inIndex = 0;
				int outIndex = 0;
				for (int block = 0; block < blockCount; block++)
				{
					for (int i = 0; i < channels; i++)
					{
						byte b = inBuffer[inIndex++];
						int hi = (b >> 4) & 0x0F;
						int lo = b & 0x0F;

						channelsArray[i].divisor = lo + 8;
						channelsArray[i].c1 = XATable[hi];
						channelsArray[i].c2 = XATable[hi + 4];
					}
					for (int i = 0; i < 14; i++)
					{
						for (int j = 0; j < channels; j++)
						{
							byte b = inBuffer[inIndex++];
							for (int n = 0; n < 2; n++)
							{
								int nibble = (n == 0) ? ((b >> 4) & 0x0F) : (b & 0x0F);
								int newValue = nibble << 28;
								newValue >>= channelsArray[j].divisor;
								newValue += channelsArray[j].CurSample * channelsArray[j].c1 + channelsArray[j].PrevSample * channelsArray[j].c2 + 128;
								newValue >>= 8;
								channelsArray[j].PrevSample = channelsArray[j].CurSample;
								channelsArray[j].CurSample = Clip_int16(newValue);
							}
							outBuffer[outIndex++] = channelsArray[j].PrevSample;
						}
						for (int j = 0; j < channels; j++)
						{
							outBuffer[outIndex++] = channelsArray[j].CurSample;
						}
					}
				}
				return ref outBuffer;
			}
		}
	}
}