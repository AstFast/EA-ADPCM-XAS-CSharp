using System;
using static EA_ADPCM_XAS_CSharp.XASStruct;

namespace EA_ADPCM_XAS_CSharp
{
	internal enum UncompressedType
	{
		Normal,
		FadeToCompressed,
		FadeFromCompressed
	};
	internal unsafe class EaXaEncoder
	{
		public int currentSample = 0;
		public int previousSample = 0;
		double currentQuantError = 0;
		double previousQuantError = 0;
		
		public void clearErrors()
		{
			currentQuantError = previousQuantError = 0;
		}
		public void writeUncompressedSubblock(Span<short> inputData, Span<byte> outputData, int nSamples, UncompressedType type)
		{
			Span<byte> outIte = outputData;
			outIte[0] = 0xEE; // Mark uncompressed subblock
			outIte = outIte.Slice(1);
			Span<short> audioToWrite = inputData;
			Span<short> transformedAudio = new short[28];
			Span<byte> outputEncoded = new byte[15];
			if (type == UncompressedType.FadeToCompressed)
			{ // nSamples = 28
				currentSample = previousSample = inputData[0];
				encodeSubblock(inputData, outputEncoded, 28, transformedAudio);
				for (int i = 0; i < 28; i++)
				{
					transformedAudio[i] = (short)(((int)inputData[i] * (28 - i) + (int)transformedAudio[i] * i) / 28);
				}
				audioToWrite = transformedAudio;
				clearErrors();
			}
			else if (type == UncompressedType.FadeFromCompressed)
			{
				encodeSubblock(inputData, outputEncoded, nSamples, transformedAudio);
				for (int i = 0; i < nSamples; i++)
				{
					transformedAudio[i] = (short)(((int)transformedAudio[i] * (nSamples - i) + (int)inputData[i] * i) / nSamples);
				}
				audioToWrite = transformedAudio;
			}

			if (nSamples == 28)
			{
				currentSample = inputData[27];
				previousSample = inputData[26];
			}
			else
			{
				currentSample = previousSample = inputData[nSamples - 1];
			}

			bufferWrite16BEUnalign(ref outIte, currentSample);
			bufferWrite16BEUnalign(ref outIte, previousSample);

			int k;
			for (k = 0; k < nSamples; k++)
			{
				bufferWrite16BEUnalign(ref outIte, audioToWrite[k]);
			}
			for (; k < 28; k++)
			{ // Fill the rest
				outIte[0] = 0x45; //E// "GEND"
				outIte = outIte.Slice(1);
				outIte[0] = 0x47;//G
				outIte = outIte.Slice(1);
			}
		}
		public void encodeSubblock(Span<short> inputData,Span<byte> outputData, int nSamples, Span<short> outputAudioResult = default)
		{
			double[] maxErrors = new double[4];
			int chosenCoeff = 0;
			double[,] predErrors = new double[4,30];

			double minError = 1e21;

			int loopCurSample = 0, loopPrevSample = 0;

			for (int i = 0; i < 4; i++)
			{
				double maxAbsError = 0;
				loopCurSample = currentSample;
				loopPrevSample = previousSample;
				//double* coefPredErrors = predErrors[i];
				int coefPredErrors_index = 0;
				for (int j = 0; j < nSamples; j++)
				{
					int nextSample = inputData[j];
					double predictionError = loopPrevSample * XaFiltersOpposite[i][1] + loopCurSample * XaFiltersOpposite[i][0] + nextSample;
					predErrors[i,coefPredErrors_index] = predictionError;
					predictionError = Math.Abs(predictionError);
					if (maxAbsError < predictionError)
					{
						maxAbsError = predictionError;
					}
					loopPrevSample = loopCurSample;
					loopCurSample = nextSample;
					coefPredErrors_index++;
				}
				maxErrors[i] = maxAbsError;
				if (minError > maxAbsError)
				{
					minError = maxAbsError;
					chosenCoeff = i;
				}
				if (i == 0 && maxErrors[0] <= 7)
				{
					chosenCoeff = 0;
					break;
				}
			}

			currentSample = loopCurSample;
			previousSample = loopPrevSample;
			int maxError = Math.Min(30000, (int)maxErrors[chosenCoeff]);
			int testShift = 0x4000;
			byte shift;
			for (shift = 0; shift < 12; shift++)
			{
				if ((testShift & (maxError + (testShift >> 3))) != 0)
				{
					break;
				}
				testShift = testShift >> 1;
			}
			byte coeffHint = (byte)(chosenCoeff << 4);
			outputData[0] = (byte)((shift & 0x0F) | coeffHint);
			Span<byte> outIte = outputData.Slice(1);
			double coefPrev = XaFiltersOpposite[chosenCoeff][1];
			double coeffCur = XaFiltersOpposite[chosenCoeff][0];
			int predErrorsIte_index = 0;
			double shiftMul = 1 << shift;
			for (int i = 0; i < nSamples; i++)
			{
				double predWithQuantizError = coefPrev * previousQuantError + coeffCur * currentQuantError + predErrors[chosenCoeff, predErrorsIte_index];
				int quantValue = (int)(((int)Math.Round(predWithQuantizError * shiftMul) + 0x800) & 0xfffff000);
				quantValue = Clip_int16(quantValue);

				if (!((i & 1) == 1))
				{
					outIte[0] = (byte)((quantValue >> 8) & 0xF0);
				}
				else
				{
					outIte[0] = (byte)(outIte[0] | ((quantValue >> 12) & 0x0F));
					outIte = outIte.Slice(1);
				}

				previousQuantError = currentQuantError;
				currentQuantError = (quantValue >> shift) - predWithQuantizError;

				if (!outputAudioResult.IsEmpty)
				{
					outputAudioResult[i] = Clip_int16(inputData[i] + (int)Math.Round(currentQuantError));
				}

				predErrorsIte_index++;
			}
		}
		public void encodeSubblock(in short[] inputData,int input_index,ref byte[] outputData,int outputData_index, int nSamples,bool is_result,ref short[] outputAudioResult)
		{
			double[] maxErrors = new double[4];
			int chosenCoeff = 0;
			double[,] predErrors = new double[4,30];
			double minError = 1e21;
			int loopCurSample = 0, loopPrevSample = 0;
			for (int i = 0; i < 4; i++)
			{
				double maxAbsError = 0;
				loopCurSample = currentSample;
				loopPrevSample = previousSample;
				int coefPredErrors_index = 0;
				for (int j = 0; j < nSamples; j++)
				{
					int nextSample = inputData[j + input_index];
					double predictionError = loopPrevSample * XaFiltersOpposite[i][1] + loopCurSample * XaFiltersOpposite[i][0] + nextSample;
					predErrors[i,coefPredErrors_index] = predictionError;
					predictionError = Math.Abs(predictionError);
					if (maxAbsError < predictionError)
					{
						maxAbsError = predictionError;
					}
					loopPrevSample = loopCurSample;
					loopCurSample = nextSample;
					coefPredErrors_index++;
				}
				maxErrors[i] = maxAbsError;
				if (minError > maxAbsError)
				{
					minError = maxAbsError;
					chosenCoeff = i;
				}
				if (i == 0 && maxErrors[0] <= 7)
				{
					chosenCoeff = 0;
					break;
				}
			}

			currentSample = loopCurSample;
			previousSample = loopPrevSample;
			int maxError = Math.Min(30000, (int)maxErrors[chosenCoeff]);
			int testShift = 0x4000;
			byte shift;
			for (shift = 0; shift < 12; shift++)
			{
				if ((testShift & (maxError + (testShift >> 3))) != 0)
				{
					break;
				}
				testShift = testShift >> 1;
			}
			byte coeffHint = (byte)(chosenCoeff << 4);
			outputData[outputData_index] = (byte)((shift & 0x0F) | coeffHint);
			int outIte_index = 1 + outputData_index;
			double coefPrev = XaFiltersOpposite[chosenCoeff][1];
			double coeffCur = XaFiltersOpposite[chosenCoeff][0];
			int predErrorsIte_index = 0;
			double shiftMul = 1 << shift;
			for (int i = 0; i < nSamples; i++)
			{
				double predWithQuantizError = coefPrev * previousQuantError + coeffCur * currentQuantError + predErrors[chosenCoeff,predErrorsIte_index];
				int quantValue = (int)(((int)Math.Round(predWithQuantizError * shiftMul) + 0x800) & 0xfffff000);
				quantValue = Clip_int16(quantValue);

				if (!((i & 1)==1))
				{
					outputData[outIte_index] = (byte)((byte)(quantValue >> 8) & 0xF0);
				}
				else
				{
					outputData[outIte_index] = (byte)(outputData[outIte_index] | ((byte)(quantValue >> 12) & 0x0F));
					++outIte_index;
				}

				previousQuantError = currentQuantError;
				currentQuantError = (quantValue >> shift) - predWithQuantizError;

				if (is_result == true)
				{
					outputAudioResult[i] = Clip_int16(inputData[i + input_index] + (int)Math.Round(currentQuantError));
				}

				predErrorsIte_index++;
			}
		}
	}
}
