using System;
using System.Runtime.InteropServices;
using static EA_ADPCM_XAS_CSharp.XASStruct;

namespace EA_ADPCM_XAS_CSharp
{
    internal unsafe partial class EAAudio
    {
        public partial class XAS
        {

            public static Span<byte> decode_EA_XAS_v1(in byte[] TatolChunk,int channels,int nSamples)
            {
                byte[] buffer = new byte[0x4c];
                short[] numArray = new short[0x400];
                int[] numArray2 = new int[0x20];
                int[] numArray3 = new int[] { 0, 240, 460, 0x188 };
                int[] numArray5 = new int[4];
                numArray5[2] = -208;
                numArray5[3] = -220;
                int[] numArray4 = numArray5;
                int index = 0;
                int num15 = TatolChunk.Length / 0x4c / channels;
                int num19 = 0;
                int data_index = 0;
                int out_data_size = 0;
                List<short> data = new();
                while (num19 < num15)
                {
                    int num20 = 0;
                    while (true)
                    {
                        if (num20 >= channels)
                        {
                            int num14 = (nSamples < 0x80) ? nSamples : 0x80;
                            int num24 = 0;
                            nSamples -= 0x80;
                            out_data_size += num14;
                            while (true)
                            {
                                if (num24 >= (num14 * channels))
                                {
                                    num19++;
                                    break;
                                }
                                data.Add(numArray[num24]);
                                num24++;
                            }
                            break;
                        }
                        Buffer.BlockCopy(TatolChunk, data_index, buffer, 0, 0x4c);
                        data_index += 0x4c;
                        int num21 = 0;
                        while (true)
                        {
                            if (num21 >= 4)
                            {
                                num20++;
                                break;
                            }
                            numArray2[0] = (short)((buffer[num21 * 4] & 240) | (buffer[(num21 * 4) + 1] << 8));
                            numArray2[1] = (short)((buffer[(num21 * 4) + 2] & 240) | (buffer[(num21 * 4) + 3] << 8));
                            index = buffer[num21 * 4] & 15;
                            int num2 = buffer[(num21 * 4) + 2] & 15;
                            int num22 = 2;
                            while (true)
                            {
                                if (num22 >= 0x20)
                                {
                                    int num23 = 0;
                                    while (true)
                                    {
                                        if (num23 >= 0x20)
                                        {
                                            num21++;
                                            break;
                                        }
                                        numArray[(((num21 * 0x20) + num23) * channels) + num20] = (short)numArray2[num23];
                                        num23++;
                                    }
                                    break;
                                }
                                int num4 = (buffer[(12 + num21) + (num22 * 2)] & 240) >> 4;
                                if (num4 > 7)
                                {
                                    num4 -= 0x10;
                                }
                                numArray2[num22] = ((((numArray2[num22 - 1] * numArray3[index]) + (numArray2[num22 - 2] * numArray4[index])) + (num4 << ((20 - num2) & 0x1f))) + 0x80) >> 8;
                                if (numArray2[num22] > 0x7fff)
                                {
                                    numArray2[num22] = 0x7fff;
                                }
                                else if (numArray2[num22] < -32768)
                                {
                                    numArray2[num22] = -32768;
                                }
                                num4 = buffer[(12 + num21) + (num22 * 2)] & 15;
                                if (num4 > 7)
                                {
                                    num4 -= 0x10;
                                }
                                int num3 = (numArray2[num22] * numArray3[index]) + (numArray2[num22 - 1] * numArray4[index]);
                                numArray2[num22 + 1] = ((num3 + (num4 << ((20 - num2) & 0x1f))) + 0x80) >> 8;
                                if (numArray2[num22 + 1] > 0x7fff)
                                {
                                    numArray2[num22 + 1] = 0x7fff;
                                }
                                else if (numArray2[num22 + 1] < -32768)
                                {
                                    numArray2[num22 + 1] = -32768;
                                }
                                num22 += 2;
                            }
                        }
                    }
                }
                data =  data.Slice(0, out_data_size);
                Span<short> _out = data.ToArray();
                return MemoryMarshal.Cast<short,byte>(_out);
            }
        }
    }
}
