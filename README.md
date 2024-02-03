# EA-ADPCM-XAS-CSharp

### Content

It allows you to encode PCM as EA-ADPCM-XAS

### Source

Original project:[GitHub - CrabJournal/EA-ADPCM-Codec](https://github.com/CrabJournal/EA-ADPCM-Codec)

### Demo

```csharp
//Encode Mode
//You need to prepare the following parameters.
uint n_samples_per_channel;
uint channels;
byte[] raw_data;//Raw data, required to be PCM (no other encoding)
//Start
using EA_ADPCM_XAS_CSharp;
uint encoded_size = EncodeXAS.GetXASEncodedSize(n_samples_per_channel, wav_header.numChannels);
byte[] encoded_data = new byte[encoded_size];
encoded_data = EncodeXAS.Encode(data, n_samples_per_channel,channels);
//encoded_ data is the encoded data
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

Resolve decoding issues

more problems
