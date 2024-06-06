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

If you want to be compatible with Net6, please do not consider SIMD

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

### Credits

[CrabJournal](https://github.com/CrabJournal/EA-ADPCM-Codec/commits?author=CrabJournal):[GitHub - CrabJournal/EA-ADPCM-Codec](https://github.com/CrabJournal/EA-ADPCM-Codec)
