# EA-ADPCM-XAS-CSharp

### Content

It allows decoding/encoding between PCM and EA-ADPCM-XAS

<div style="background-color: #FF3000">This is a branch that supports Net7 and only has XAS v1</div>

### Support

| Name     | decode | function                       | encode | function      |
| -------- | ------ | ------------------------------ | ------ | ------------- |            |
| XAS v1   | ✔️     | decode_XAS_v1 | ✔️     | encode_XAS_v1 |

### Demo

```csharp
//You need to prepare the following parameters.
int n_samples_per_channel;
uint channels;
byte[] raw_data;//Raw data, required to be PCM (no other encoding)
//Start
using EA_ADPCM_XAS_CSharp;
//You need to obtain the true size after encoding, otherwise it will result in unnecessary 0x00
byte[]encoded_data = EA_ADPCM.XAS.encode_XAS_v1(data, n_samples_per_channel,channels);
//encoded_ data is the encoded data
```

### 

### Credits

CrabJournal:[GitHub - CrabJournal/EA-ADPCM-Codec](https://github.com/CrabJournal/EA-ADPCM-Codec)
