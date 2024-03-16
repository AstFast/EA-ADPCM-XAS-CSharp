# EA-ADPCM-XAS-CSharp

### Content

It allows decoding/encoding between PCM and EA-ADPCM-XAS

### Functional support

- [x] Encoded

- [x] Decoded

### Source

Original project:[GitHub - CrabJournal/EA-ADPCM-Codec](https://github.com/CrabJournal/EA-ADPCM-Codec)

### Compile

I compiled it using VS2022

Release version requires `Net8` environment

If you need to be compatible with lower versions, please open your compilation software and delete the section about SIMD, then make slight modifications (mainly using the Net8 writing method for a small part, which is more convenient). You need to change it to consider it as a writing method below Net8 (Don't worry, it's very simple. You just need to change `[]` back to `new <T>[]{}`)

### Demo

```csharp
//
//Encode Mode
//You need to prepare the following parameters.
uint n_samples_per_channel;
uint channels;
byte[] raw_data;//Raw data, required to be PCM (no other encoding)
//Start
using EA_ADPCM_XAS_CSharp;
uint encoded_size = EncodeXAS.GetXASEncodedSize(n_samples_per_channel, channels);
byte[] encoded_data = new byte[encoded_size];
encoded_data = EncodeXAS.Encode(data, n_samples_per_channel,channels);
//encoded_ data is the encoded data


//Decode Mode
uint channels;
uint Sample;//it is writed in wav file
byte[] raw;
//I can only write this temporarily
var decode_data = DecodeXAS.Decode(raw,channels);
//This function is only intended to support lower versions
//end
//if you want get more quick decode you should use Using DecodeSIMD() function
var decode_data = DecodeXAS.DecodeSIMD(raw,channels);
```

### Problem

##### Write data differently

I found that the code I ported to C # had different results after encoding PCM, and there seemed to be a significant difference.

After comparison, I found that this may be an issue with my handling of writing data.

The principle is as follows:

![](https://github.com/AstFast/EA-ADPCM-XAS-CSharp/raw/master/map.png)

Simply put, it is the difference between the struct I created and the struct written by the original author when writing (there may be other differences). When checking for the difference, I am unable to know and proofread them. The reason may be that I understand that the original author's struct occupies 4 bytes, so I replaced it with the same 4-byte data type (I checked and converted them to the same type and found that the values are the same, but after writing, they are different?). Or is this caused by the different writing mechanisms (or encoding) between C # and C++? I don't know. But now it can still be played. (This may cause the decoded PCM data to be different).

I don't intend to solve this problem (or wait until I discover the problem).

##### Different after encoding and decoding

ADPCM is a lossy encoding format, so the data decoded after encoding may be different from the original data

##### Speed

Although not as fast as C++, the speed is still acceptable within a certain range

### TODO:

Optimize decoding code

Provide faster decoding methods?

more problems

### Statement:

The decoding part used another person's code, but I don't know their name
