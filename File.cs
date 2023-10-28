using System;
using System.Threading.Channels;
using size_t = System.UIntPtr;

namespace EA_ADPCM_CSharp
{
	public class File
	{
		public unsafe struct WAVHEADER
		{
			internal char[] chunkId;                // 0x0		"RIFF"
			internal UInt32 chunkSize;             // 0x4		sizeof file - 8
			internal char[] format;                 // 0x8		"WAVE"
			internal char[] subchunk1Id;            // 0xC		"fmt "
			internal UInt32 subchunk1Size;         // 0x10		16
			internal UInt16 audioFormat;           // 0x14		1 = PCM
			internal UInt16 numChannels;           // 0x16
			internal UInt32 sampleRate;            // 0x18
			internal UInt32 byteRate;              // 0x1C
			internal UInt16 blockAlign;            // 0x20
			internal UInt16 bitsPerSample;         // 0x22
			internal char[] subchunk2Id;            // 0x24		"data" or "smpl"
			internal UInt32 subchunk2Size;         // 0x28
			public static implicit operator WAVHEADER(byte[] bytes)
			{
				WAVHEADER wavHeader = new();
				BinaryReader reader = new BinaryReader(new MemoryStream(bytes));
				wavHeader.chunkId = new char[4];
				wavHeader.chunkId = reader.ReadChars(4);
				wavHeader.chunkSize = reader.ReadUInt32();
				wavHeader.format = new char[4];
				wavHeader.format = reader.ReadChars(4);
				wavHeader.subchunk1Id = new char[4];
				wavHeader.subchunk1Id = reader.ReadChars(4);
				wavHeader.subchunk1Size = reader.ReadUInt32();
				wavHeader.audioFormat = reader.ReadUInt16();
				wavHeader.numChannels = reader.ReadUInt16();
				wavHeader.sampleRate = reader.ReadUInt32();
				wavHeader.byteRate = reader.ReadUInt32();
				wavHeader.blockAlign = reader.ReadUInt16();
				wavHeader.bitsPerSample = reader.ReadUInt16();
				wavHeader.subchunk2Id = new char[4];
				wavHeader.subchunk2Id = reader.ReadChars(4);
				wavHeader.subchunk2Size = reader.ReadUInt32();
				return wavHeader;
			}
		};
		WAVHEADER defaul_wav_header =new WAVHEADER{
			chunkId = new char[]{ 'R', 'I', 'F', 'F' },
			chunkSize=0,
			format = new char[]{ 'W', 'A', 'V', 'E' },
			subchunk1Id = new char[]{ 'f', 'm', 't', ' ' },
			subchunk1Size = 16,
			audioFormat = 1,
			//audioFormat = PCM_WAV,
			numChannels =1,
			sampleRate=0,
			byteRate=0,
			blockAlign=2,
			bitsPerSample=16,
			subchunk2Id = new char[]{ 'd', 'a', 't', 'a' },
			subchunk2Size=0
		};
		unsafe struct WAV_meta
		{
			public Int16* PCM;
			public size_t n_samples_per_channel;
		};
		unsafe void MakeWavHeader(WAVHEADER* wav_header, int sample_rate, int num_samples, int num_channels)
		{
			WAVHEADER tempq =  defaul_wav_header;
			wav_header = &tempq;
			int block_align = num_channels * 2;
			size_t data_size = (nuint)(num_samples * 2);
			wav_header->chunkSize = (uint)(data_size + (uint)sizeof(WAVHEADER) - 8);
			wav_header->numChannels = (ushort)num_channels;
			wav_header->sampleRate = (uint)sample_rate;
			wav_header->byteRate = (uint)(sample_rate * block_align);
			wav_header->blockAlign = (ushort)block_align;
			wav_header->subchunk2Size = (uint)data_size;
		}
		unsafe WAV_meta ReadWAV(string wav_file, WAVHEADER* wav_header)
		{
			WAV_meta wavMeta = new WAV_meta{ PCM = null, n_samples_per_channel = 0};
			BinaryReader br = new(new FileStream(wav_file, FileMode.Open));
			WAVHEADER temp = (WAVHEADER)br.ReadBytes(sizeof(WAVHEADER));
			wav_header = &temp;
			wavMeta.n_samples_per_channel = wav_header->subchunk2Size / wav_header->numChannels / 2;
			Int16* temps = stackalloc Int16[(int)wav_header->subchunk2Size];
			wavMeta.PCM = temps;

			return wavMeta;
		}
		private static byte[] ShortToByteArray(short[] shorts)
		{
			byte[] bytes = new byte[shorts.Length * 2];
			for (int i = 0; i < shorts.Length; i++)
			{
				byte[] temp = BitConverter.GetBytes(shorts[i]);
				Buffer.BlockCopy(temp, 0, bytes, i * 2, temp.Length);
			}
			return bytes;
		}
		unsafe bool EncodeWav(string wav_file, string raw_file, WAVHEADER wav_header) {
			WAV_meta wav_meta = ReadWAV(wav_file, &wav_header);
			if (wav_meta.PCM == null) {
				return false;
			}
			UInt32 n_samples_per_channel = (uint)wav_meta.n_samples_per_channel;
			size_t encoded_size = EA_ADPCM_XAS.GetXASEncodedSize(n_samples_per_channel, wav_header.numChannels);
			byte[] encoded_data = new byte[(int)encoded_size];
			EA_ADPCM_XAS.EncodeXAS(&encoded_data, wav_meta.PCM, n_samples_per_channel, wav_header.numChannels);
			BinaryWriter bw = new(new FileStream(raw_file, FileMode.Open));
			bw.Write(encoded_data);
			bw.Close();
			return true;
		}
		static Int16 GetFileSize(string filePath)
		{
			FileInfo fileInfo = new FileInfo(filePath);
			return (short)fileInfo.Length;
		}
		unsafe bool DecodeRaw(string raw_file, string wav_file, WAVHEADER wav_header) {
			size_t raw_size = (nuint)GetFileSize(raw_file);
			BinaryReader br = new(new FileStream(raw_file, FileMode.Open));
			byte[] temps = br.ReadBytes((int)raw_size);
			void* XAS_data = &temps;
			br.Close();
			size_t n_chunks = raw_size / 76;
			size_t n_total_samples = wav_header.subchunk2Size == 0 ? n_chunks * 128 : wav_header.subchunk2Size / 2;
			int channels = wav_header.numChannels;
			Int16[] PCM_data = new Int16[sizeof(Int16) * n_total_samples];
			EA_ADPCM_XAS.DecodeXAS(XAS_data, &PCM_data, (uint)((int)n_total_samples / channels), (uint)channels);
			MakeWavHeader(&wav_header, (int)wav_header.sampleRate, (int)n_total_samples, channels);
			BinaryWriter bw = new(new FileStream(raw_file, FileMode.Open));
			bw.Write(ShortToByteArray(PCM_data));
			bw.Close();
			return true;
		}
	}
}
