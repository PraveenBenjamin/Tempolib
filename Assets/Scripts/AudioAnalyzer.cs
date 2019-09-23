using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioAnalyzer: MonoBehaviour 
{
	LomontFFT _fft;
	public static AudioAnalyzer _instance;
	void Awake()
	{
		if (AudioAnalyzer._instance != null) {
			Destroy (this);
		}
		_fft = new LomontFFT ();
		_instance = this;

		InitializeAnalyzer ();
	}

	private void InitializeAnalyzer()
	{
		//what do i need this for? lets just put some shtuff in here for now
		_fft.A = 1;
		_fft.B = -1;
	}

	public void TimetoFreq(ref double[] audioSamples, int fftType = 0)
	{
		switch (fftType) {
		case 0:
			{
				_fft.FFT (audioSamples, true);
			}
			break;
		case 1:
			{
				_fft.RealFFT (ref audioSamples, true);
			}
			break;
		case 2:
			{
				_fft.TableFFT (audioSamples, true);
			}
			break;
		}
	}

	public static void BinFFTOutput(ref double[] fftOutput,ref double[] bins, int[] binRanges, int fftSampleBand)
	{
		if (fftOutput == null || fftOutput.Length <= 0)
			return;

		if (bins == null || bins.Length != binRanges.Length)
		{
			bins = new double[binRanges.Length];
		}

		//bin frequency gain in the desired bands

		//disregard first sample because it contains the overall gain in the sample set.
		for (int i = 0; i < fftOutput.Length; ++i) {
			double freqOfSample = (i * fftSampleBand) / fftOutput.Length;
			int binIndex = 0;
			for (; binIndex < binRanges.Length-1; ++binIndex) {
				if (freqOfSample < binRanges [binIndex])
					break;
			}

			bins [binIndex] += Mathf.Abs((float)fftOutput [i]);
		}
	}


}