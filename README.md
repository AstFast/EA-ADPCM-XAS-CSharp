# EA-ADPCM-XAS-CSharp

### Content

It allows decoding/encoding between PCM and EA-ADPCM-XAS

<mark>The project is currently being modified.Possible incorrect decoding</mark>

### Support

| Name     | decode | function      | encode | function      |
| -------- | ------ | ------------- | ------ | ------------- |
| XA v1    | ❌      |               | ❌      |               |
| XA v2    | *✔️    | decode_XA_v2  | *✔️    | encode_XA_v2  |
| Maxis XA | ❌      |               | ❌      |               |
| XAS v0   | ❌      |               | ❌      |               |
| XAS v1   | ✔️     | decode_XAS_v1 | ✔️     | encode_XAS_v1 |

`*:Not tested`  `**Unable to pass the test, more information is needed`

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

### TODO:

Revise XA

reduce losses

### Statement:

The decoding part used another person's code, but I don't know their name

### Credits

CrabJournal:[GitHub - CrabJournal/EA-ADPCM-Codec](https://github.com/CrabJournal/EA-ADPCM-Codec)
