using System;
using System.Runtime.InteropServices;
using static EA_ADPCM_XAS_CSharp.XASStruct;

namespace EA_ADPCM_XAS_CSharp
{
    internal unsafe partial class EAAudio
    {
        public partial class XAS
        {
            static short clipInt16(int a)
            {
                if (((a + 0x8000U) & ~0xFFFF) != 0)
                {
                    return (short)((a >> 31) ^ 0x7FFF);
                }
                else return (short)a;
            }
            static void encodeXasBlock(Span<short> samples,Span<byte> output, int nSamples,uint nChannels,ref EaXaEncoder[] encoders)
            {
                for (int c = 0; c < nChannels; c++)
                {
                    Span<short> inputSamples = samples.Slice(c * nSamples);
                    Span<short> inputSamplesPadded = new short[128];
                    if (nSamples < 128)
                    {
                        inputSamplesPadded.Fill(0);
                        for (int i = 0; i < nSamples; i++)
                        {
                            inputSamplesPadded[i] = inputSamples[i];
                        }
                        inputSamples = inputSamplesPadded;
                    }
                    short[][] startSamples = new short[4][];
                    for (int i = 0; i < 4; i++) startSamples[i] = new short[2];
                    byte[][] encoded = new byte[4][];
                    for (int i = 0; i < 4; i++) encoded[i] = new byte[16];

                    for (int i = 0; i < 4; i++)
                    { // 128 samples split in 4 groups of 32
                        startSamples[i][0] = (short)(clipInt16(inputSamples[0] + 8) & 0xFFF0);
                        startSamples[i][1] = (short)(clipInt16(inputSamples[1] + 8) & 0xFFF0);
                        encoders[c].previousSample = startSamples[i][0];
                        encoders[c].currentSample = startSamples[i][1];
                        encoders[c].clearErrors();
                        encoders[c].encodeSubblock(inputSamples.Slice(2), encoded[i], 30);
                        inputSamples = inputSamples.Slice(32);
                    }
                    Span<short> startSamplesWithInfos = MemoryMarshal.Cast<byte,short>(output);
                    for (int i = 0; i < 4; i++)
                    {
                        byte infoByte = encoded[i][0];
                        startSamplesWithInfos[0] = (short)(startSamples[i][0] | (infoByte >> 4)); // Coef info
                        startSamplesWithInfos[1] = (short)(startSamples[i][1] | (infoByte & 0x0F)); // Shift info
                        startSamplesWithInfos = startSamplesWithInfos.Slice(2);
                    }
                    output = MemoryMarshal.Cast<short,byte>(startSamplesWithInfos);
                    for (int j = 1; j <= 15; j++)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            output[0] = encoded[i][j];
                            output = output.Slice(1);
                        }
                    }
                }
            }

            public static byte[] encode_EA_XAS_v1(in byte[] bytes,uint nChannels)
            {
                int nSamples = (int)(bytes.Length / 2 / nChannels);
                List<byte> _out = new List<byte>();
                EaXaEncoder[] encoders = new EaXaEncoder[nChannels];
                for (int i = 0; i < nChannels; i++)
                {
                    encoders[i] = new EaXaEncoder();
                }
                int codedSamples = 0;
                bool lastBlock = false;

                Span<byte> block = new byte[76 * nChannels];
                short[] samples = new short[128 * nChannels];
                int dataIndex = 0;
                while (!lastBlock)
                {
                    int samplesInBlock = 128;
                    codedSamples += samplesInBlock;
                    if (codedSamples >= nSamples)
                    {
                        int toRemove = codedSamples - nSamples;
                        samplesInBlock -= toRemove;
                        codedSamples = nSamples;
                        lastBlock = true;
                    }

                    if (!ReadSamples(in bytes,ref dataIndex,ref samples,(int)nChannels, samplesInBlock))
                    {
                        throw new IndexOutOfRangeException();
                    }
                    encodeXasBlock(samples,block, samplesInBlock,nChannels,ref encoders);
                    _out.AddRange(block);
                }

                return _out.ToArray();
            }
        }
    }
}