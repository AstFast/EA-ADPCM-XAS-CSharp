using System;
using System.Runtime.InteropServices;
using static EA_ADPCM_XAS_CSharp.XASStruct;

namespace EA_ADPCM_XAS_CSharp
{
    internal unsafe partial class EAAudio
    {
        public partial class XAS
        {
            static EncodedSample encode_XA_sample(Span<short> prev_samples, short[] coef, int sample, byte shift) {

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
                if (res != 7 && Math.Abs(decoded - sample) > Math.Abs(decoded2 - sample)) {
                    res += 1;
                    decoded = decoded2;
                }
                else {
                    decoded2 = Clip_int16(predecoded - term);
                    if (res != -8 && Math.Abs(decoded - sample) > Math.Abs(decoded2 - sample)) {
                        res -= 1;
                        decoded = decoded2;
                    }
                }
                return new EncodedSample{decoded = (short)decoded,encoded = (byte)res};
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
            static void encode_XAS_Chunk(ref XAS_Chunk out_chunk, Span<short> in_PCM /*, size_t nSamples = 128*/)
            {
                //assert(nSamples <= 128);
                for (int j = 0; j < subchunks_in_XAS_chunk; j++)
                {

                    Span<short> pInSamples = in_PCM.Slice(j * 32);
                    XAS_SubChunkHeader header = new XAS_SubChunkHeader();
                    header.data = (uint)(out_chunk.headers[j]);
                    header.unused = 0;
                    header.sample_0 = (pInSamples[0] + shift4_rounding) >> 4;
                    header.sample_1 = (pInSamples[1] + shift4_rounding) >> 4;

                    Span<short> decoded_PCM = new short[32];
                    decoded_PCM[0] = (short)(header.sample_0 << 4);
                    decoded_PCM[1] = (short)(header.sample_1 << 4);
                    simple_CalcCoefShift(pInSamples.Slice(2), decoded_PCM, 30,out int coef_index,out byte shift);
                    header.coef_index = (uint)coef_index;
                    header.exp_shift = shift;
                    out_chunk.headers[j] = (int)header.data;
                    short[] coef = ea_adpcm_table_v2[coef_index];
                    shift = (byte)(12 + fixed_point_offset - shift);

                    Span<short> pDecodedSamples = decoded_PCM;

                    for (int i = 0; i < 15; i++)
                    {
                        byte data = 0;

                        for (int n = 0; n < 2; n++)
                        {
                            EncodedSample enc = encode_XA_sample(pDecodedSamples, coef, pInSamples[2], shift);
                            pDecodedSamples[2] = enc.decoded; // think as decoder will for better precision
                            data <<= 4;
                            data |= (byte)(enc.encoded & 0xF);
                            pInSamples = pInSamples.Slice(1);
                            pDecodedSamples = pDecodedSamples.Slice(1);
                        }
                        out_chunk.XAS_data[i * 4 + j] = data;
                    }
                }
            }
            public static void encode_EA_XAS_v1(Span<short> in_PCM,Span<byte> out_data, uint n_samples_per_channel, uint n_channels) 
            {
                if (n_samples_per_channel == 0)
                    return;
                Span<XAS_Chunk> _out_data = MemoryMarshal.Cast<byte,XAS_Chunk>(out_data);
                uint n_chunks_per_channel = _GetNumXASChunks(n_samples_per_channel);
                Span<short> PCM = new short[128];
                int index = 0;
                for (int chunk_ind = 0; chunk_ind < n_chunks_per_channel - 1; chunk_ind++) {
                    for (int channel_ind = 0; channel_ind < n_channels; channel_ind++) {
                        Span<short> t = in_PCM.Slice(channel_ind);
                        for (int sample_ind = 0; sample_ind < 128; sample_ind++) {
                            PCM[sample_ind] = t[0];
                            t = t.Slice((int)n_channels);
                        }
                        encode_XAS_Chunk(ref _out_data[index], PCM);
                        index++;

                    }
                    in_PCM = in_PCM.Slice(128 * (int)n_channels);
                }
                uint samples_remain_per_channel = n_samples_per_channel - (n_chunks_per_channel - 1) * 128;
                for (int channel_ind = 0; channel_ind < n_channels; channel_ind++)
                {
                    for (int sample_ind = 0; sample_ind < samples_remain_per_channel; sample_ind++)
                    {
                        PCM[sample_ind] = in_PCM[channel_ind + sample_ind * (int)n_channels];
                    }
                    PCM.Slice((int)samples_remain_per_channel).Fill(0);
                    encode_XAS_Chunk(ref _out_data[index], PCM);
                    index++;
                }
            }
        }
        

    }
}
