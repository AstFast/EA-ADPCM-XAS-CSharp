# EA-ADPCM-XAS-CSharp

### Content

It allows decoding/encoding between PCM and EA XAS

<mark>The project is currently being modified.Possible incorrect decoding</mark>

The project may support lower levels .Net version, but requires modification

### Support

| Name     | decode | function        | encode | function        |
| -------- | ------ | --------------- | ------ | --------------- |
| XA v1    | ❌      |                 | ✔️     | encode_EA_XA_R1 |
| XA v2    | ❌      |                 | ✔️     | encode_EA_XA_R2 |
| XA v3    | ❌      |                 | ✔️     | encode_EA_XA_R3 |
| Maxis XA | ✔️     | decode_Maxis_XA | ✔️     | encode_Maxis_XA |
| XAS v0   | ❌      |                 | ❌      |                 |
| XAS v1   | ✔️     | decode_XAS_v1   | ✔️     | encode_XAS_v1   |

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

### Credits

CrabJournal:[GitHub - CrabJournal/EA-ADPCM-Codec](https://github.com/CrabJournal/EA-ADPCM-Codec)

lgdel:[GitHub-lgdel/eaxas](https://github.com/lgdel/eaxas)

[XA | SimsTek Wiki | Fandom](https://simstek.fandom.com/wiki/XA)

xas_decode.exe

[vgmstream](https://github.com/vgmstream/vgmstream)

### Digression

I strongly recommend using EA's public tools (if possible, regardless of the method), as they can ensure sufficient accuracy.

### License

GPL-3.0 License