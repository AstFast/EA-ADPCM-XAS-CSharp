using EA = EA_ADPCM_XAS_CSharp.EAAudio;
using static EA_ADPCM_XAS_CSharp.XASStruct;
using System.Runtime.InteropServices;

namespace EA_ADPCM_XAS_CSharp
{
	public unsafe static class EA_ADPCM
	{
		public class XA
		{
			public static byte[] decode_XA_v1(byte[] in_data, uint n_samples_per_channel, uint channels)
			{
				uint frame_size = (uint)((channels > 1) ? 0x0f * 2 : 0x0f);
				int num_chunks = (int)((n_samples_per_channel + (frame_size - 1)) / frame_size);
				IntPtr optr = Marshal.AllocHGlobal(num_chunks * samples_in_EA_XA_R_chunk);
				GCHandle handle = GCHandle.Alloc(in_data, GCHandleType.Pinned);
				IntPtr ptr = handle.AddrOfPinnedObject();
				EA.XA.adpcm_history1_32 = 0;
				EA.XA.adpcm_history2_32 = 0;
				EA.XA.decode_EA_XA_v1(ptr.ToPointer(), (short[]*)optr.ToPointer(),(int)channels,num_chunks);
				handle.Free();
				byte[] Out_data = *(byte[]*)optr.ToPointer();
				Marshal.FreeHGlobal(optr);
				return Out_data;
			}
			public static byte[] decode_XA_v2(byte[] in_data,uint n_samples_per_channel, uint channels)
			{
				uint num_chunks = (n_samples_per_channel + (samples_in_EA_XA_R_chunk - 1)) / samples_in_EA_XA_R_chunk;
				IntPtr optr = Marshal.AllocHGlobal((int)num_chunks * sizeof_uncompr_EA_XA_R23_block);
				GCHandle handle = GCHandle.Alloc(in_data, GCHandleType.Pinned);
				IntPtr ptr = handle.AddrOfPinnedObject();
				EA.XA.decode_EA_XA_R2(ptr.ToPointer(), (short[]*)optr.ToPointer(), n_samples_per_channel, channels);
				handle.Free();
				byte[] Out_data = *(byte[]*)optr.ToPointer();
				Marshal.FreeHGlobal(optr);
				return Out_data;
			}
			public static byte[] decode_maxis_xa(byte[] in_data, uint channels)
			{
				EA.XA.adpcm_history1_32 = 0;
				EA.XA.adpcm_history2_32 = 0;
				IntPtr optr = Marshal.AllocHGlobal((in_data.Length / 15) *samples_in_EA_XA_R_chunk);
				GCHandle handle = GCHandle.Alloc(in_data, GCHandleType.Pinned);
				IntPtr ptr = handle.AddrOfPinnedObject();
				EA.XA.deocode_maxis_xa((byte[]*)optr.ToPointer(), (short[]*)ptr.ToPointer(),(int)channels);
				handle.Free();
				byte[] Out_data = *(byte[]*)optr.ToPointer();
				Marshal.FreeHGlobal(optr);
				return Out_data;
			}
			public static byte[] encode_XA_v1(byte[] data, uint n_samples_per_channel, uint channels)
			{
				IntPtr optr = Marshal.AllocHGlobal((data.Length / sizeof_EA_XA_R1_chunk) * samples_in_EA_XA_R_chunk);
				GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
				IntPtr ptr = handle.AddrOfPinnedObject();
				EA.XA.encode_EA_XA_R1(optr.ToPointer(), (short[]*)ptr.ToPointer(), (int)channels);
				byte[] Out_data = *(byte[]*)optr.ToPointer();
				handle.Free();
				Marshal.FreeHGlobal(optr);
				return Out_data;
			}
			public static byte[] encode_XA_v2(byte[] data, uint n_samples_per_channel, uint channels)
			{
				IntPtr optr = Marshal.AllocHGlobal(2);
				short[] in_PCM = new short[data.Length / 2];
				Buffer.BlockCopy(data, 0,in_PCM , 0, data.Length);
				EA.XA.encode_EA_XA_R2(optr.ToPointer(),in_PCM,n_samples_per_channel,channels);
				byte[] Out_data = *(byte[]*)optr.ToPointer();
				Marshal.FreeHGlobal(optr);
				return Out_data;
			}

		}
		public class XAS
		{
			public static byte[] decode_XAS_v0(byte[] in_data, uint channels)
			{
				uint n_total_samples = (uint)((in_data.Length / sizeof_EA_XA_R1_chunk) * samples_in_XAS_per_subchunk);
				short[] PCM_data = new short[sizeof(short) * n_total_samples];
				GCHandle handle = GCHandle.Alloc(in_data, GCHandleType.Pinned);
				IntPtr ptr = handle.AddrOfPinnedObject();
				EA.XAS.decode_XAS_v0(ptr.ToPointer(), ref PCM_data, n_total_samples / channels, channels);
				handle.Free();
				byte[] out_data = new byte[sizeof(int) * n_total_samples];
				Buffer.BlockCopy(in_data, 0, out_data, 0, out_data.Length);
				return out_data;
			}
			public static byte[] decode_XAS_v1(byte[] in_data, uint channels)
			{
				uint n_total_samples = (uint)((in_data.Length / 76) * 128);
				short[] PCM_data = new short[sizeof(short) * n_total_samples];
				GCHandle handle = GCHandle.Alloc(in_data, GCHandleType.Pinned);
				IntPtr ptr = handle.AddrOfPinnedObject();
				EA.XAS.decode_XAS_v1(ptr.ToPointer(), ref PCM_data, n_total_samples / channels, channels);
				handle.Free();
				byte[] out_data = new byte[sizeof(int) * n_total_samples];
				Buffer.BlockCopy(in_data, 0, out_data, 0, out_data.Length);
				return out_data;
			}
			public static byte[] decode_XAS_v1_s2(byte[] bytes, uint channels)
			{
				List<byte> data = new List<byte>();
				byte[] array = new byte[76];
				short[] array2 = new short[1024];
				int[] array3 = new int[32];
				int array_index = 0;
				int[] array4 = new int[]
				{
				0,
				240,
				460,
				392
				};
				int[] array5 = new int[]
				{
				0,
				0,
				-208,
				-220
				};
				long count = (bytes.Length / 76) / channels;
				int num9 = (bytes.Length / 76) * 128;
				for (int i = 0; i < count; i++)
				{
					for (int j = 0; j < channels; j++)
					{
						Array.Copy(bytes, array_index, array, 0, 76);
						array_index += 76;
						for (int k = 0; k < 4; k++)
						{
							array3[0] = (short)((array[k * 4] & 240) | (array[k * 4 + 1] << 8));
							array3[1] = (short)((array[k * 4 + 2] & 240) | array[k * 4 + 3] << 8);
							int num14 = array[k * 4] & 15;
							int num15 = array[k * 4 + 2] & 15;
							for (int l = 2; l < 32; l += 2)
							{
								int num16 = (array[12 + k + l * 2] & 240) >> 4;
								if (num16 > 7)
								{
									num16 -= 16;
								}
								int num17 = array3[l - 1] * array4[num14] + array3[l - 2] * array5[num14];
								array3[l] = Clip_int16(num17 + (num16 << 20 - num15) + 128 >> 8);
								num16 = (int)(array[12 + k + l * 2] & 15);
								if (num16 > 7)
								{
									num16 -= 16;
								}
								num17 = array3[l] * array4[num14] + array3[l - 1] * array5[num14];
								array3[l + 1] = Clip_int16(num17 + (num16 << 20 - num15) + 128 >> 8);
							}
							for (int m = 0; m < 32; m++)
							{
								array2[(k * 32 + m) * channels + j] = (short)array3[m];
							}
						}
					}
					int num18;
					if (num9 >= 128U)
					{
						num18 = 128;
					}
					else
					{
						num18 = (int)num9;
					}
					num9 -= 128;
					for (int n = 0; n < num18 * channels; n++)
					{
						data.AddRange(BitConverter.GetBytes(array2[n]));
					}

				}
				return data.ToArray();
			}
			public static uint GetXASEncodedSize(uint n_samples_per_channel, uint n_channels)
			{
				return XASStruct.GetXASEncodedSize(n_samples_per_channel, n_channels);
			}
			public static byte[] encode_XAS_v1(byte[] rawdata, uint n_samples_per_channel, uint channels)
			{
				uint encode_size = GetXASEncodedSize(n_samples_per_channel, channels);
				XAS_Chunk[] out_data = new XAS_Chunk[encode_size / 76];
				short[] shorts = new short[rawdata.Length / 2];
				Buffer.BlockCopy(rawdata, 0, shorts, 0, rawdata.Length);
				EA.XAS.encode_XAS_v1(ref out_data, shorts, n_samples_per_channel, channels);
				List<byte> bytes = new List<byte>();
				foreach (var item in out_data)
				{
					foreach (var item1 in item.headers)
					{
						byte[] bytes1 = new byte[4];
						bytes1 = BitConverter.GetBytes(item1.data);
						for (int i = 0; i < bytes1.Length; i++)
						{
							bytes.Add(bytes1[i]);
						}
					}
					foreach (var item1 in item.XAS_data)
					{
						foreach (var item2 in item1)
						{
							bytes.Add(item2);
						}
					}
				}
				return bytes.ToArray();
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
	}
}
