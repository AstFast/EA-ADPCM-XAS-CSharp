﻿using EA = EA_ADPCM_XAS_CSharp.EAAudio;
using static EA_ADPCM_XAS_CSharp.XASStruct;
using System.Runtime.InteropServices;
using System;

namespace EA_ADPCM_XAS_CSharp
{
	public unsafe static class EA_ADPCM
	{
		public class XA
		{
			public static byte[] decode_XA_v2(in byte[] in_data,uint n_samples_per_channel, uint channels)
			{
				long n_chunks = (n_samples_per_channel + 27) / 28;
				long encoded_size = n_chunks * 61;
				IntPtr out_data = Marshal.AllocHGlobal((int)encoded_size);
				GCHandle handle = GCHandle.Alloc(in_data);
				IntPtr _in_data = handle.AddrOfPinnedObject();
				EA.XA.decode_EA_XA_R2((byte*)_in_data,(short*)out_data, n_samples_per_channel, channels);
				handle.Free();
				byte[] bytes = new byte[encoded_size];
				Marshal.Copy(out_data,bytes,0,bytes.Length);
				Marshal.FreeHGlobal(out_data);
				return bytes;
			}
			public static byte[] encode_XA_v2(in short[] in_data, uint n_samples_per_channel, uint channels)
			{
				int n_chunks = (int)((n_samples_per_channel + 27) / 28);
				long encoded_size = n_chunks * 61;
				byte[] out_data = new byte[encoded_size];
				_ = EA.XA.encode_EA_XA_R2(ref out_data,in in_data,n_samples_per_channel,channels,10);
				return out_data;
			}

		}
		public class XAS
		{
			
			public static byte[] decode_XAS_v1(in byte[] in_data, uint channels)
			{
				uint n_total_samples = (uint)((in_data.Length / 76) * 128);
				short[] PCM_data = new short[sizeof(short) * n_total_samples];
				EA.XAS.decode_XAS_v1(in_data, ref PCM_data, n_total_samples / channels, channels);
				return ShortArrayToByteArray(PCM_data);
			}
			
			public static uint GetXASEncodedSize(uint n_samples_per_channel, uint n_channels)
			{
				return XASStruct.GetXASEncodedSize(n_samples_per_channel, n_channels);
			}
			public static byte[] encode_XAS_v1(in byte[] rawdata, uint n_samples_per_channel, uint channels)
			{
				uint encode_size = GetXASEncodedSize(n_samples_per_channel, channels);
				short[] shorts = new short[rawdata.Length / 2];
				Buffer.BlockCopy(rawdata, 0, shorts, 0, rawdata.Length);
				return EA.XAS.encode_XAS_v1(shorts, encode_size, n_samples_per_channel, channels);
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
		public static byte[] ShortArrayToByteArray(short[] shortArray)
		{
			byte[] byteArray = new byte[shortArray.Length * 2];
			Buffer.BlockCopy(shortArray, 0, byteArray, 0, byteArray.Length);
			return byteArray;
		}
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
		}
		public struct EncodedSample
		{
			public Int16 decoded;
			public byte encoded;
		}
		public struct XAS_Chunk
		{
			public XAS_SubChunkHeader[] headers;
			public byte[][] XAS_data;
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

		
		public static int ea_xa_bytes_to_samples(int bytes, int channels)
		{
			if (channels <= 0) 
			{ return 0;};
			return bytes / channels / 0x0f * 28;
		}
		public static int bytestream2_get_bytes(ref byte[] ptr)
		{
			int val = ptr[0];
			Array.Resize(ref ptr, ptr.Length - 1);
			return val;
		}
		public static short bytestream2_get_le16s(ref byte[] ptr)
		{
			short val = BitConverter.ToInt16(ptr, 0);
			Array.Resize(ref ptr, ptr.Length - 2);
			return val;
		}
		public static int low_sNibble(int _byte)
		{
			return (int)((sbyte)((byte)_byte << 4) >> 4);
		}

		public static short Get_s16be(void* ptr)
		{
			return ToBigEndian16(*(short*)ptr);
		}
		public static short Get_s16be(byte[] ptr, long index)
		{
			return BitConverter.ToInt16(new byte[] { ptr[1 + index], ptr[index] },0);
		}
		public static uint get_u32le(byte[] p)
		{
			return (uint)get_s32le(p);
		}
		public static int get_s32le(byte[] p)
		{
			return (p[0] | (p[1] << 8) | (p[2] << 16) | (p[3] << 24));
		}
		public static short ToBigEndian16(short val)
		{
			return (short)(((val & 0xFF00) >> 8) | ((val & 0x00FF) << 8));
		}
		/*
		public static short decode_XA_sample(in short[] prev_samples,int index, short[] coef, int int4, byte shift) 
		{
			int correction = int4 << shift;
		    int prediction = prev_samples[1 + index] * coef[0] + prev_samples[index] * coef[1];
	        return (short)Clip_int16((prediction + correction + def_rounding) >> fixed_point_offset);
        }
		*/
    }
}
