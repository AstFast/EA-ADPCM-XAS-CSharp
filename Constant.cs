using System.Diagnostics;

namespace EA_ADPCM_CSharp
{

	internal class Constant
	{
		internal const int subchunks_in_XAS_chunk = 4;
		internal const int samples_in_XAS_subchunk = 30;
		internal const int samples_in_XAS_header = 2;
		internal const int samples_in_XAS_per_subchunk = samples_in_XAS_subchunk + samples_in_XAS_header;
		internal const int samples_in_EA_XA_R_chunk = 28;
		internal const int sizeof_EA_XA_R1_chunk = 1 + 2 * sizeof(Int16) + samples_in_EA_XA_R_chunk / 2;
		internal const int sizeof_uncompr_EA_XA_R23_block = 1 + (samples_in_EA_XA_R_chunk + 2) * sizeof(Int16);
		internal const int sizeof_compr_EA_XA_R23_block = 1 + samples_in_EA_XA_R_chunk / 2;
		internal const int shift4_rounding = 0x8 - 1;
		internal const int fixed_point_offset = 8;
		internal const int fixp_exponent = 1 << fixed_point_offset;
		internal const int def_rounding = (fixp_exponent >> 1);
		internal static short[][] ea_adpcm_table_v2 = new[]
		{
				new[]{ (short)(0.000000 * 256), (short)(0.000000 * 256) },
				new[]{ (short)(0.937500 * 256), (short)(0.000000 * 256) },
				new[]{ (short)(1.796875 * 256), (short)(-0.812500 * 256) },
				new[]{ (short)(1.531250 * 256), (short)(-0.859375 * 256) },
		};
		internal struct EncodedSample
		{
			public short decoded;
			public sbyte encoded;
		};
		/*
		internal struct SamplesByte
		{
			public int sample1;
			public int sample0;
		};
		*/
		internal struct SamplesByte
		{
			private byte data;

			public int sample1
			{
				get
				{
					if ((data & 0b1000) == 0)
					{
						return data & 0b1111;
					}
					else
					{
						return (data & 0b1111) - 16;
					}
				}
				set
				{
					Debug.Assert(value >= -8 && value < 8);
					if (value < 0)
					{
						data = ((byte)((data & ~0b1111) | (value + 16)));
					}
					else
					{
						data = ((byte)((data & ~0b1111) | value));
					}
				}
			}

			public int sample0
			{
				get
				{
					if (((data >> 4) & 0b1000) == 0)
					{
						return (data >> 4) & 0b1111;
					}
					else
					{
						return ((data >> 4) & 0b1111) - 16;
					}
				}
				set
				{
					Debug.Assert(value >= -8 && value < 8);
					if (value < 0)
					{
						data = ((byte)((data & ~(0b1111 << 4)) | ((value + 16) << 4)));
					}
					else
					{
						data = ((byte)((data & ~(0b1111 << 4)) | (value << 4)));
					}
				}
			}
		}

		internal struct SamplesDWORD
		{
			Int16[] samples;
		};
	}
}
