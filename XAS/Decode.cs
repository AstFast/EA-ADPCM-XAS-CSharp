using System;
using System.Runtime.InteropServices;
using static EA_ADPCM_XAS_CSharp.XASStruct;

namespace EA_ADPCM_XAS_CSharp
{
    internal unsafe partial class EAAudio
    {
        public partial class XAS
        {
            public static void decode_EA_XAS_v1(Span<byte> in_data, Span<short> out_PCM, uint n_samples_per_channel, uint n_channels)
            {
                if (n_samples_per_channel == 0)
                    return;
                Span<XAS_Chunk> _in_data = MemoryMarshal.Cast<byte, XAS_Chunk>(in_data);
                Span<short> PCM = new short[128];
                int _in_data_index = 0;
                uint n_chunks_per_channel = _GetNumXASChunks(n_samples_per_channel);
                for (int chunk_ind = 0; chunk_ind < n_chunks_per_channel - 1; chunk_ind++)
                {
                    for (int channel_ind = 0; channel_ind < n_channels; channel_ind++)
                    {
                        VectorSIMD.decode_EA_XAS_Chunk_SIMD(_in_data[_in_data_index], PCM);
                        _in_data_index++;
                        for (int sample_ind = 0; sample_ind < 128; sample_ind++)
                        {
                            out_PCM[channel_ind + sample_ind * (int)n_channels] = PCM[sample_ind];
                        }
                    }
                    out_PCM = out_PCM.Slice(128 * (int)n_channels);
                }
                uint samples_remain_per_channel = n_samples_per_channel - (n_chunks_per_channel - 1) * 128;
                for (int channel_ind = 0; channel_ind < n_channels; channel_ind++)
                {
                    VectorSIMD.decode_EA_XAS_Chunk_SIMD(_in_data[_in_data_index], PCM);
                    _in_data_index++;
                    for (int sample_ind = 0; sample_ind < samples_remain_per_channel; sample_ind++)
                    {
                        out_PCM[channel_ind + sample_ind * (int)n_channels] = PCM[sample_ind];
                    }
                }
            }
            static void decode_EA_XAS_v0_chunk(Span<byte> stream, Span<short> outbuf, int channelspacing, int first_sample, int samples_to_do)
            {
                //byte[] frame = new byte[0x13];
                int frame_offset;
                int frames_in, samples_done = 0, sample_count = 0;
                int bytes_per_frame, samples_per_frame;


                /* external interleave (fixed size), mono */
                bytes_per_frame = 0x02 + 0x02 + 0x0f;//0x13;19
                samples_per_frame = 1 + 1 + 0x0f * 2;//32
                frames_in = first_sample / samples_per_frame;
                first_sample = first_sample % samples_per_frame;

                frame_offset = bytes_per_frame * frames_in;
                //read_streamfile(frame, frame_offset, bytes_per_frame); /* ignore EOF errors */
                Span<byte> frame = stream.Slice(frame_offset, frame_offset + bytes_per_frame);
                //TODO make expand function and fuse with above

                /* process frame */
                {
                    float coef1, coef2;
                    short hist1, hist2;
                    byte shift;
                    uint frame_header = get_u32le(frame); /* always LE */

                    coef1 = xa_coefs[frame_header & 0x0F, 0];
                    coef2 = xa_coefs[frame_header & 0x0F, 1];
                    hist2 = (short)((frame_header >> 0) & 0xFFF0);
                    hist1 = (short)((frame_header >> 16) & 0xFFF0);
                    shift = (byte)((frame_header >> 16) & 0x0F);

                    /* write header samples (needed) */
                    if (sample_count >= first_sample && samples_done < samples_to_do)
                    {
                        outbuf[samples_done * channelspacing] = hist2;
                        samples_done++;
                    }
                    sample_count++;
                    if (sample_count >= first_sample && samples_done < samples_to_do)
                    {
                        outbuf[samples_done * channelspacing] = hist1;
                        samples_done++;
                    }
                    sample_count++;

                    /* process nibbles */
                    for (int i = 0; i < 0x0f * 2; i++)
                    {
                        byte nibbles = frame[0x02 + 0x02 + i / 2];
                        int sample;

                        sample = ((i & 1) == 1) ? /* high nibble first */
                                (nibbles >> 0) & 0x0f :
                                (nibbles >> 4) & 0x0f;
                        sample = (short)(sample << 12) >> shift; /* 16b sign extend + scale */
                        sample = (int)(sample + hist1 * coef1 + hist2 * coef2);
                        sample = Clip_int16(sample);

                        if (sample_count >= first_sample && samples_done < samples_to_do)
                        {
                            outbuf[samples_done * channelspacing] = (short)sample;
                            samples_done++;
                        }
                        sample_count++;

                        hist2 = hist1;
                        hist1 = (short)sample;
                    }
                }
            }
            public static byte[] decode_EA_XAS_v0(in byte[] raw_data,int channels)
            {
                Span<byte> buffer = new byte[33];
                bool lastBlock = false;
                int data_index = 0;
                int bytes_per_frame = 0x02 + 0x02 + 0x0f;//0x13;19
                int samples_per_frame = 1 + 1 + 0x0f * 2;//32
                Span<short> samples = new short[samples_per_frame];
                int nSamples = (int)(raw_data.Length / 2 / channels);
                int codedSamples = 0;
                List<byte> _out = new List<byte>();
                while (!lastBlock)
                {
                    codedSamples += samples_per_frame;
                    if (codedSamples >= nSamples)
                    {
                        int toRemove = codedSamples - nSamples;
                        samples_per_frame -= toRemove;
                        codedSamples = nSamples;
                        lastBlock = true;
                    }
                    if (!ReadSamples(raw_data, ref data_index,samples, channels, samples_per_frame))
                    {
                        throw new IndexOutOfRangeException();
                    }
                    for (int ch = 0; ch < channels; ch++)
                    {
                        decode_EA_XAS_v0_chunk(MemoryMarshal.Cast<short,byte>(samples.Slice(ch * samples_per_frame / 2)), MemoryMarshal.Cast<byte, short>(buffer.Slice(ch)),
                                channels, 0, 33);
                    }
                    _out.AddRange(buffer);
                }
                return _out.ToArray();
            }
        }
    }
}
