using EA = EA_ADPCM_XAS_CSharp.EAAudio;
using static EA_ADPCM_XAS_CSharp.XASStruct;
using System.Runtime.InteropServices;

namespace EA_ADPCM_XAS_CSharp
{
	public unsafe static class EA_ADPCM
	{
		public static class XA
		{
			public static byte[] decode_Maxis_XA(in byte[] data,int channels)
			{
				short[] bytes =  EA.MaxisXA.decode_Maxis_XA(data,channels);
				return ShortArrayToByteArray(bytes);
			}
			public static byte[] encode_Maxis_XA(in byte[] data, int channels)
			{
				return EA.MaxisXA.encode_Maxis_XA(data, channels);
			}
			public static byte[] encode_EA_XA_R1(in byte[] data, int channels, int SampleRate)
			{
				return EA.XA.encode_EA_XA_R(data, channels, SampleRate, 1);
			}
			public static byte[] encode_EA_XA_R2(in byte[] data, int channels, int SampleRate)
			{
				return EA.XA.encode_EA_XA_R(data, channels, SampleRate, 2);
			}
			public static byte[] encode_EA_XA_R3(in byte[] data, int channels, int SampleRate)
			{
				return EA.XA.encode_EA_XA_R(data, channels, SampleRate, 3);
			}
			public static byte[] encode_EA_XA_R(in byte[] data, int channels, int SampleRate,int revision)
			{
				if (revision < 1 || revision > 3)
				{
					throw new Exception();
				}
				return EA.XA.encode_EA_XA_R(data, channels, SampleRate, revision);
			}
		}
		public class XAS
		{
            public static byte[] decode_EA_XAS_v1(in byte[] raw_data, uint channels)
            {
                uint n_size = ((uint)raw_data.Length / 76) * 128;
				Span<short> out_data = new short[n_size];
				EAAudio.XAS.decode_EA_XAS_v1(raw_data,out_data,n_size / channels,channels);
				return MemoryMarshal.Cast<short,byte>(out_data).ToArray();
            }
			
            public static byte[] decode_EA_XAS_v1(in byte[] raw_data, uint channels,int nSamples)
			{
				return EAAudio.XAS.decode_EA_XAS_v1(raw_data,(int)channels,nSamples).ToArray();
			}

            public static uint GetXASEncodedSize(uint n_samples_per_channel, uint n_channels)
			{
				return XASStruct.GetXASEncodedSize(n_samples_per_channel, n_channels);
			}
			public static byte[] encode_EA_XAS_v1(Span<short> raw_data,uint channels)
			{
				uint n_samples_per_channel = (uint)(raw_data.Length / channels / 2);
				
                uint encode_size = GetXASEncodedSize(n_samples_per_channel,channels);
                Span<byte> de_data = new byte[encode_size];
				EAAudio.XAS.encode_EA_XAS_v1(raw_data, de_data, n_samples_per_channel, channels);
				return de_data.ToArray();
            }
			public static byte[] encode_EA_XAS_v1(in byte[] PCM_data, uint channels)
			{
                return EAAudio.XAS.encode_EA_XAS_v1(PCM_data,channels).ToArray();

            }
		}
		
	}
	internal unsafe static class XASStruct
	{
		public const int fixed_point_offset = 8;
		public const int shift4_rounding = 0x8 - 1;
		public const int fixp_exponent = 1 << fixed_point_offset;
		public const int def_rounding = (fixp_exponent >> 1);
		public static int[] XATable ={
			0, 240,  460,  392,
			0,   0, -208, -220,
			0,   1,    3,    4,
			7,   8,   10,   11,
			0,  -1,   -3,   -4
		};
		public static float[][] ea_adpcm_table_xas_v0 = new float[][]{
			new float[]{ 0.0F,0.0F},
			new float[]{ 0.9375F,0.0F},
			new float[]{ 1.796875F, -0.8125F},
			new float[]{ 1.53125F, -0.859375F},
		};
		public static double[][] XaFiltersOpposite = new double[][]
		{
			new double[]{ 0,         0 },
			new double[]{ -0.9375,  0 },
	        new double[]{ -1.796875, 0.8125 }, 
			new double[]{ -1.53125, 0.859375 }
		};
		public static short[][] ea_adpcm_table_v2 = new short[][]{
			new short[]{ (short)(0.000000 * fixp_exponent), (short)(0.000000 * fixp_exponent) },
			new short[]{ (short)(0.937500 * fixp_exponent), (short)(0.000000 * fixp_exponent) },
			new short[]{ (short)(1.796875 * fixp_exponent), (short)(-0.812500 * fixp_exponent) },
			new short[]{ (short)(1.531250 * fixp_exponent), (short)(-0.859375 * fixp_exponent)},
		};
		public static short[][] ea_adpcm_table_v3 = new short[][]
		{
			new short[] { (short)(0.000000 * fixp_exponent), (short)(0.000000 * fixp_exponent) },
			new short[]{(short)(0.000000 * fixp_exponent), (short)(0.937500 * fixp_exponent)},
			new short[]{(short)(-0.812500 * fixp_exponent), (short)(1.796875 * fixp_exponent)},
			new short[]{(short)(-0.859375 * fixp_exponent), (short)(1.531250 * fixp_exponent)}
		};
		public static int[] ea_adpcm_table_v3_const = new int[] { 0, 15728640, 30211888, 25755428 };
        public static int[] ea_adpcm_table_v4 = new int[] { 0, 240, 460, 392 };
		public static int[] const_shift = new int[] { 16 - fixed_point_offset, 16 - fixed_point_offset, 16 - fixed_point_offset, 16 - fixed_point_offset };
		public static byte[] shuffle = new byte[] { 12, 8, 4, 0, 13, 9, 5, 1, 14, 10, 6, 2, 15, 11, 7, 3 };
		public const int subchunks_in_XAS_chunk = 4;
		public const int samples_in_XAS_subchunk = 30;
		public const int samples_in_XAS_header = 2;
		public const int samples_in_XAS_per_subchunk = samples_in_XAS_subchunk + samples_in_XAS_header;
		public const int samples_in_EA_XA_R_chunk = 28;
		public const int sizeof_EA_XA_R1_chunk = 1 + 2 * sizeof(short) + samples_in_EA_XA_R_chunk / 2;
		public const int sizeof_uncompr_EA_XA_R23_block = 1 + (samples_in_EA_XA_R_chunk + 2) * sizeof(short);
		public const int sizeof_compr_EA_XA_R23_block = 1 + samples_in_EA_XA_R_chunk / 2;
		public static short Clip_int16(int val)
		{
			if (val >= short.MaxValue)
			{
				return short.MaxValue;
			}
			if (val <= short.MinValue)
			{
				return short.MinValue;
			}
			return (short)val;
		}
		public static int Clip_int4(int val)
		{
			if (val >= 7) return 7;
			if (val <= -8) return -8;
			return val;
		}
		public static int sign_extend(int val, int bits)
		{
			int shift = 8 * sizeof(int) - bits;
			return (int)((uint)val << shift) >> shift;
		}
		public static uint _GetNumXASChunks(uint N_SAMPLES)
		{
			return ((N_SAMPLES + 127) / 128);
		}
		public static uint GetNumXASTotalChunks(uint n_samples_per_channel, uint n_channels)
		{
			return n_channels * _GetNumXASChunks(n_samples_per_channel);
		}
		public static uint GetXASEncodedSize(uint n_samples_per_channel, uint n_channels)
		{
			return GetNumXASTotalChunks(n_samples_per_channel, n_channels) * 76;
		}
		public static ref byte[] ShortArrayToByteArray(in short[] shortArray)
		{
			byte[] byteArray = new byte[shortArray.Length * 2];
			Buffer.BlockCopy(shortArray, 0, byteArray, 0, byteArray.Length);
			return ref byteArray;
		}
		/*
		public struct SamplesByte
		{
			public byte data;

			public sbyte sample1
			{
				get { return (sbyte)(data & 0x0F); }
				set { data = (byte)((data & 0xF0) | (value & 0x0F)); }
			}
			public sbyte sample0
			{
				get { return (sbyte)((data >> 4) & 0x0F); }
				set { data = (byte)((data & 0x0F) | ((value & 0x0F) << 4)); }
			}
		}*/
		public struct EncodedSample
		{
			public Int16 decoded;
			public byte encoded;
		}
        public struct XAS_Chunk
		{
			public fixed int headers[4];
			public fixed byte XAS_data[60];//[15][4]
		}
		public struct XAS_SubChunkHeader
		{
			public uint data;
			public uint coef_index
			{
				get { return (data & 0x3u); }
				set { data = (data & 0xFFFFFFFCu) | (value & 0x3u); }
			}

			public uint unused
			{
				get { return (data >> 2) & 0x3u; }
				set { data = (data & 0xFFFFFFF3u) | ((value & 0x3u) << 2); }
			}

			public int sample_0
			{
				get { return (int)(data >> 4) & 0xFFF; }
				set { data = (data & 0xFFFFF000u) | ((uint)value & 0xFFFu) << 4; }
			}

			public uint exp_shift
			{
				get { return ((data >> 16) & 0xFu); }
				set { data = (data & 0xFFF0FFFFu) | ((value & 0xFu) << 16); }
			}

			public int sample_1
			{
				get { return (int)(data >> 20) & 0xFFF; }
				set { data = (data & 0x000FFFFFu) | ((uint)value & 0xFFFu) << 20; }
			}
		}
		public static short ToBigEndian16(int val)
		{
			return (short)(((val & 0xFF00) >> 8) | ((val & 0x00FF) << 8));
		}
		public static short Get_s16be(ref Span<byte> ptr) 
		{
			return ToBigEndian16(BitConverter.ToInt16(ptr));
		}
		public static void bufferWrite16BEUnalign(ref Span<byte> data, int ToSample)
		{
#if NET7_0
            short _temp_ = ToBigEndian16(ToSample);
			MemoryMarshal.Write(data.Slice(0, 2),ref _temp_);
#else
			MemoryMarshal.Write(data.Slice(0, 2), ToBigEndian16(ToSample));
#endif
			data = data.Slice(2);
		}

		public static bool ReadSamples(in byte[] data, ref int data_index, ref short[] output, int channels, int nSamples)
		{
			try
			{
				if (channels == 1)
				{
					Buffer.BlockCopy(data, data_index, output, 0, 2 * nSamples);
					data_index += 2 * nSamples;
				}
				else
				{
					int samplesToRead = nSamples * channels;
					short[] rawSamples = new short[samplesToRead];
					Buffer.BlockCopy(data, data_index, rawSamples, 0, 2 * samplesToRead);
					data_index += 2 * samplesToRead;
					for (int c = 0; c < channels; c++)
					{
						int rawSamplesIte = c;
						int outputIte_index = c * nSamples;
						for (int i = 0; i < nSamples; i++)
						{
							output[outputIte_index++] = rawSamples[rawSamplesIte];
							rawSamplesIte += channels;
						}
					}
				}
				return true;
			}
			catch
			{
				return false;
			}
		}
	}
}
