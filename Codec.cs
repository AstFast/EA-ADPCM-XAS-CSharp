using EA_ADPCM_XAS_CSharp.Decode;
using EA_ADPCM_XAS_CSharp.Encode;
namespace EA_ADPCM_XAS_CSharp
{
	public static class DecodeXAS
	{
		public static short[] Decode(byte[] in_data, uint channels)
		{
			return new Decode_Data().Decode(in_data,channels);
		}
	}
	public static class EncodeXAS
	{
		public static uint GetXASEncodedSize(uint n_samples_per_channel, uint n_channels)
		{
			return XASStruct.GetXASEncodedSize(n_samples_per_channel,n_channels);
		}
		public static byte[] Encode(byte[] rawdata, uint n_samples_per_channel, uint channels)
		{
			return new Encode_Data().EncodeXASData(rawdata, n_samples_per_channel, channels);
		}
		public static byte[] Encode(short[] rawdata, uint n_samples_per_channel, uint channels)
		{
			return new Encode_Data().EncodeXASData(rawdata, n_samples_per_channel, channels);
		}
	}
	internal static class XASStruct
	{
		public const int fixed_point_offset = 8;
		public const int shift4_rounding = 0x8 - 1;
		public const int fixp_exponent = 1 << fixed_point_offset;
		public const int def_rounding = (fixp_exponent >> 1);
		public static short[][] ea_adpcm_table_v2 = [
				[(short)(0.000000 * fixp_exponent), (short)(0.000000 * fixp_exponent)],
			[(short)(0.937500 * fixp_exponent), (short)(0.000000 * fixp_exponent)],
			[(short)(1.796875 * fixp_exponent), (short)(-0.812500 * fixp_exponent)],
			[(short)(1.531250 * fixp_exponent), (short)(-0.859375 * fixp_exponent)],
		];
		public static int Clip_int16(int val)
		{
			return (val >= 0x7FFF) ? 0x7FFF : (val <= -0x8000) ? -0x8000 : val;
		}
		public static int Clip_int4(int val)
		{
			if (val >= 7) return 7;
			if (val <= -8) return -8;
			return val;
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
		public struct SamplesByte
		{
			private sbyte data;

			public sbyte sample1
			{
				get { return (sbyte)(data & 0x0F); }
				set { data = (sbyte)((data & 0xF0) | (value & 0x0F)); }
			}
			public sbyte sample0
			{
				get { return (sbyte)((data >> 4) & 0x0F); }
				set { data = (sbyte)((data & 0x0F) | ((value & 0x0F) << 4)); }
			}
			public static implicit operator sbyte(SamplesByte sample)
			{
				return sample.data;
			}
			public static implicit operator SamplesByte(sbyte value)
			{
				SamplesByte sb = new SamplesByte();
				sb.data = value;
				return sb;
			}
		}
		public struct EncodedSample
		{
			public Int16 decoded;
			public byte encoded;
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

		public struct XAS_Chunk
		{
			public XAS_SubChunkHeader[] headers;
			public byte[][] XAS_data;
		}
	}
}
