using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using AJ.Utilities;
using UnityEngine.Events;

public class PLPDetector {

	private double[] _fftBinsRaw;
	private float[] _fftBinsActual;
	private LomontFFT _fftScript;
	private float[][] _spectrogram;
	private int _frameIndex;
	private double[] _noveltyCurve;
	private double[] _tempo;
	private double[] _tempoMaxCoeff;
	private Vector2Int _tempogramWindow = new Vector2Int (50, 320);
	private double[][] _tempogramActual;
	private double[][] _windowedSineWaveCache;
	//private double[][] _sineWaveCache;

	private double[] _plpCurve;
	private int[] _plpCurveNormVal;

	private UnityAction<float> _beatCallback;


	//private Color[] _debugVizTexColors;
	private bool _enableDebugVisualization = true;
	private Texture2D _debugVizNovTex;
	private Texture2D _debugVizPLPTex;
	private Texture2D _debugVizTGTex;

	private int _kernelSize = 4;

	private double _bpm;
	public double BPM
	{
		get
		{
			return _bpm;
		}
	}

	private int _storedFrameCount
	{
		get
		{
			return _kernelSize * 60;
		}
	}

	private int _tempogramRange
	{
		get
		{
			return _tempogramWindow.y - _tempogramWindow.x;
		}
	}


	public PLPDetector(UnityAction<float> beatCallback,ref Texture2D debugVizNovTex, ref Texture2D debugVizPLPTex,ref Texture2D debugVizTGTex,int kernelSize = 4,int fftWindowSize = 512)
	{
		_kernelSize = kernelSize;
		_fftBinsRaw = new double[fftWindowSize];
		_fftBinsActual = new float[fftWindowSize / 2];
		//its massive i know... but the things we do for accuracy right?
		_spectrogram = new float[_storedFrameCount][];
		for (int i = 0; i < _storedFrameCount; ++i) {
			_spectrogram[i] = new float[_fftBinsActual.Length];
		}
		_frameIndex = 0;

		int frameCountExp2 = Utilities.NextNearestPowerOf2 (_storedFrameCount);
		_noveltyCurve = new double[frameCountExp2];
		_tempo = new double[frameCountExp2];
		_tempoMaxCoeff = new double[_tempogramRange];

		_tempogramActual = new double[_tempogramRange][];
		for (int i = 0; i < _tempogramActual.Length; ++i) {
			_tempogramActual [i] = new double[frameCountExp2];
		}
		_plpCurve = new double[frameCountExp2];
		_plpCurveNormVal = new int[frameCountExp2];

		//one second
		_windowedSineWaveCache = new double[_tempogramRange][];
		//_sineWaveCache = new double[_tempogramRange][];
		for (int i = 0; i < _windowedSineWaveCache.Length; ++i) {
			_windowedSineWaveCache [i] = new double[(frameCountExp2/kernelSize) * 2];
			//_sineWaveCache [i] = new double[(frameCountExp2/kernelSize) * 2];
			generateSinWave (_tempogramWindow.x + i, ref _windowedSineWaveCache [i]);
			//generateSinWave (_tempogramWindow.x + i, ref _sineWaveCache [i],false);
		}


		_beatCallback = beatCallback;

		//DEBUG!
		_debugVizNovTex = debugVizNovTex;
		_debugVizPLPTex = debugVizPLPTex;
		_debugVizTGTex = debugVizTGTex;

	}

	private void generateSinWave(int frequency, ref double[] arr,bool applyHannWindow = true)
	{
		float phase = 0;
		float phaseShiftPerIteration = frequency / (float)arr.Length ;

		//create a sin wave with the specified frequency
		for(int i = 0 ; i < arr.Length; ++i)
		{
			arr [i] = Mathf.Sin (phase); 
			phase += phaseShiftPerIteration;
		}

		if(applyHannWindow)
			//window the sin wave
			Utilities.Hann(ref arr);
	}

	public void UpdatePLP(ref double[] dAudioSamples, int channelCount, int sampleRate,bool applyHannWindow = true)
	{
		if (dAudioSamples == null)
			return;

		if (_fftScript == null) {
			_fftScript = new LomontFFT ();
			_fftScript.A = 1;
			_fftScript.B = -1;
		}

		System.Array.Clear (_fftBinsRaw, 0, _fftBinsRaw.Length);
		System.Array.Clear (_fftBinsActual, 0, _fftBinsActual.Length);

		//channel bin the data
		for (int sampleIndex = 0; sampleIndex < dAudioSamples.Length; sampleIndex += channelCount) {
			int binIndex = sampleIndex == 0 ? 0 : sampleIndex / channelCount;
			for (int channelIndex = 0; channelIndex < channelCount; ++channelIndex) {
				_fftBinsRaw [binIndex] = dAudioSamples [sampleIndex + channelIndex];
			}
			_fftBinsRaw [binIndex] /= channelCount;
		}

		//apply hanning window on audio samples
		if(applyHannWindow)
			Utilities.Hann(ref _fftBinsRaw);

		//perform fft.
		_fftScript.RealFFT (ref _fftBinsRaw, true);

		//aggregate fft output into fftActual
		for (int i = 2; i < _fftBinsRaw.Length; i += 2) {
			float real = (float)_fftBinsRaw [i];
			float im = (float)_fftBinsRaw [i+1];
			float val =  Mathf.Sqrt (Mathf.Pow (real, 2) + Mathf.Pow (im, 2));
			if (i == 2)
				_fftBinsActual [0] = val;
			else
				_fftBinsActual [(i / 2)-1] = val;
		}
	

		//left shift the spectrogram
		for (int i = 1; i < _spectrogram.Length; ++i) {
			LeftShiftArray<float> (ref _spectrogram [i]);
			/*for (int j = 0; j < _spectrogram [i - 1].Length; ++j) {
				_spectrogram [i - 1][j] = _spectrogram [i][j];
			}*/
		}

		//apply logarithmic compression and fill in place
		float[] toFill = _spectrogram [_spectrogram.Length-1];
		for (int i = 0; i < toFill.Length; ++i) {
			toFill [i] = _fftBinsActual [i];//Mathf.Log ((float)(1+_fftBinsActual [i]));
		}


		//compute discrete derivative
		if (_frameIndex > 0) {
			float[] prev = _frameIndex > _spectrogram.Length-1? _spectrogram[_spectrogram.Length-2]:_spectrogram [_frameIndex - 1];
			float[] curr = _frameIndex > _spectrogram.Length - 1 ? _spectrogram [_spectrogram.Length - 1] : _spectrogram [_frameIndex];


			for (int i = 0; i < curr.Length; ++i) {
				float diff = curr[i]- prev[i];
				if (diff > 0)
					curr[i] = diff;
				else
					curr[i] = 0;
			}
		}


		++_frameIndex;

		if(_frameIndex > _spectrogram.Length)
		{
			bool firstRun = _frameIndex == _spectrogram.Length + 1;
			ComputeNoveltyCurve (firstRun);
			ComputeTempogram (false,firstRun);
			UpdateBPM ();
			ComputePeriodicLocalPulse (firstRun);
			//CorrelateNoveltyCurveWithSinCurve ();

			LeftShiftArray<double> (ref _noveltyCurve);
			//LeftShiftArray<double> (ref _tempogramActual);
			for (int i = 1; i < _tempogramActual.Length; ++i) {
				LeftShiftArray<double> (ref _tempogramActual[i]);
			}
			LeftShiftArray<double> (ref _tempo);
			LeftShiftArray<double> (ref _tempoMaxCoeff);

			LeftShiftArray<double> (ref _plpCurve);
		}
	}

	private void LeftShiftArray<T>(ref T[] arr)
	{
		for (int i = 1; i < arr.Length; ++i) {
				arr [i - 1] = arr [i];
		}
	}
		

	private void ComputeNoveltyCurve(bool firstRun = false)
	{
		if (_frameIndex < _spectrogram.Length)
			return;

		//System.Array.Clear (_noveltyCurve,_noveltyCurve.Length - (_noveltyCurve.Length/_kernelSize),(_noveltyCurve.Length/_kernelSize));


		int startIndex = firstRun? 0:_spectrogram.Length - (_spectrogram.Length/_kernelSize);
		int endIndex = _spectrogram.Length;


		for (int frameIndex = startIndex; frameIndex < endIndex; ++frameIndex) {
			float[] frame = _spectrogram[frameIndex];
			double val = _noveltyCurve [frameIndex];
			//accumulate energy
			for (int binIndex = 0; binIndex < frame.Length; ++binIndex) {
				val += frame [binIndex];
			}
			//remove local average energy
			_noveltyCurve [frameIndex] = val - (val/frame.Length);
		}

		//DEBUG!
		VisualizeData((double val) =>
			{
				return (int)(val/_debugVizNovTex.height) * 10;	
			},ref _noveltyCurve,ref _debugVizNovTex);
	}

	private void ComputeTempogram(bool applyHannWindow = true,bool firstRun = false)
	{
		if (_frameIndex < _spectrogram.Length)
			return;

		double[] frame;
		double[] sineWave;

		int startIndex = firstRun?0:_tempoMaxCoeff.Length - (_tempoMaxCoeff.Length/_kernelSize);
		int endIndex = _tempoMaxCoeff.Length;
		for (int i = startIndex; i < endIndex; ++i) {
			_tempoMaxCoeff [i] = double.MaxValue * -1;
		}

		double correlationCoeff = 0;
		int shiftCounter = 0;
		int relativeNovIndex = 0;
		int halfSineWaveSize = 0;
		//iterate through each frame

		startIndex = 0;
		endIndex = _tempogramActual.Length;

		for (int frameIndex = startIndex; frameIndex < endIndex; ++frameIndex) {

			//cache contents of frame
			frame = _tempogramActual [frameIndex];

			//cache the sine wave generated for this tempo
			//for (int sineWaveFormIndex = 0; sineWaveFormIndex < _windowedSineWaveCache.Length; ++sineWaveFormIndex) {

			sineWave = _windowedSineWaveCache [frameIndex];

			halfSineWaveSize = (int)(sineWave.Length * 0.5f);

			//iterate through each column of the frame
			//each entry in a column will be filled with the correlation coefficient

			startIndex = firstRun ? 0 : frame.Length - (frame.Length / _kernelSize);
			endIndex = frame.Length;

			for (int frameDatumIndex = startIndex; frameDatumIndex < endIndex; ++frameDatumIndex) {

				correlationCoeff = 0;

				//correlate the sine wave with a suitable portion of the novelty curve
				shiftCounter = frameDatumIndex - halfSineWaveSize;
				if (shiftCounter > 0)
					shiftCounter = 0;
				else
					shiftCounter *= -1;

				for (int sineWaveDatumIndex = shiftCounter; sineWaveDatumIndex < sineWave.Length; ++sineWaveDatumIndex) {

					relativeNovIndex = frameDatumIndex + (sineWaveDatumIndex - halfSineWaveSize);

					if (relativeNovIndex >= _noveltyCurve.Length - 1)
						break;

					correlationCoeff += _noveltyCurve [relativeNovIndex] * sineWave [sineWaveDatumIndex];
				}

				frame [frameDatumIndex] = correlationCoeff;
			}
		}

		//set tempo of the time instance to be the tempo with max correlation to the corresponding pulse
		double maxCorrelation = 0;
		double correlationVal = 0;

		startIndex = firstRun?0:_tempo.Length - (_tempo.Length/_kernelSize);
		endIndex = _tempo.Length;

		for (int i = startIndex; i < endIndex; ++i) {
			maxCorrelation = double.MaxValue * -1;
			for (int j = 0; j < _tempogramActual.Length; ++j) {
				correlationVal = _tempogramActual [j] [i];
				if (correlationVal > maxCorrelation) {
					maxCorrelation = correlationVal;
					_tempo [i] = _tempogramWindow.x + j;
				}
			}
		}

		VisualizeData2D ((int row, int col) => {
			double val = _tempogramActual[row][col];
			if(val <= 0)
				return Color.black;
			else if(val <= 1000.0f)
				return Color.Lerp(Color.black,Color.blue,(float)(_tempogramActual[row][col]/1000.0f));
			else if (val <= 2000.0f)
				return Color.Lerp(Color.blue,Color.green,(float)(_tempogramActual[row][col]/1000.0f));
			else
				return Color.Lerp(Color.green,Color.red,(float)(_tempogramActual[row][col]/1000.0f));
		},Color.black, ref _tempogramActual, ref _debugVizTGTex);

	}

	private void UpdateBPM()
	{
		_bpm = 0;
		if (_frameIndex < _spectrogram.Length)
			return;

		//only consider the tempo of the second that went by, since the tempo of the current second
		//is still stabilizing

		//int bpm = 0;
		int oneSecondSampleCount = (_tempo.Length/_kernelSize);
		int i = oneSecondSampleCount * (_kernelSize-2);
		for (; i < _tempo.Length - oneSecondSampleCount; ++i) {
			_bpm += _tempo [i];
		}
		_bpm /= oneSecondSampleCount;
		//Debug.Log (_bpm);
	}


	private void ComputePeriodicLocalPulse(bool firstRun = false)
	{
		int shiftCounter = 0;
		int halfSineWaveSize = (int)(_windowedSineWaveCache [0].Length * 0.5f);
		int relativePLPIndex = 0;
		int tempo = 0;
		double[] sineWaveform = null;


		int secondSize = (int) (_tempo.Length * (1.0f / _kernelSize));
		int startIndex = 0;//firstRun? 0: secondSize * (_kernelSize-1);
		int endIndex = _tempo.Length;


		System.Array.Clear (_plpCurveNormVal, 0, _plpCurveNormVal.Length);
		System.Array.Clear (_plpCurve, 0, _plpCurve.Length);
		//System.Array.Clear (_plpCurve, Mathf.Max(0,startIndex-1), secondSize);



		for (int tempoIndex = _tempo.Length-1; tempoIndex > 0/*  (_tempo.Length - secondSize)*/; --tempoIndex) {
			
			tempo = (int)_tempo [tempoIndex];
			if (tempo <= 0)
				continue;

			int waveformIndex = tempo - _tempogramWindow.x;
			sineWaveform = _windowedSineWaveCache [waveformIndex];

			shiftCounter = halfSineWaveSize + ((_tempo.Length-1) - tempoIndex);
			if (shiftCounter >= sineWaveform.Length)
				shiftCounter = sineWaveform.Length - 1;
			
			for (int sineIndex = shiftCounter; sineIndex >= 0; --sineIndex) {
				relativePLPIndex = tempoIndex +  (sineIndex - halfSineWaveSize);
				if (relativePLPIndex < 0)
					break;
				_plpCurve [relativePLPIndex] += sineWaveform [sineIndex];
				_plpCurveNormVal [relativePLPIndex] += 1;
			}
		}



		/*for (int tempoIndex = startIndex; tempoIndex < endIndex; ++tempoIndex) {

			shiftCounter = tempoIndex - halfSineWaveSize;

			if (shiftCounter > 0)
				shiftCounter = 0;
			else
				shiftCounter *= -1;
			
			tempo = (int)_tempo [tempoIndex];
			if (tempo <= 0)
				continue;
			
			int waveformIndex = tempo - _tempogramWindow.x;
			sineWaveform = _windowedSineWaveCache [waveformIndex];

			for (int sineIndex = shiftCounter; sineIndex < sineWaveform.Length; ++sineIndex) {
				relativePLPIndex = tempoIndex + (sineIndex - halfSineWaveSize);
				if (relativePLPIndex >= _plpCurve.Length)
					break;

				//if (sineWaveform [sineIndex] < 0)
				//	continue;
				//else {

				//accumulation of sine waveform energies
				_plpCurve [relativePLPIndex] += sineWaveform [sineIndex];
				_plpCurveNormVal [relativePLPIndex] += 1;

				//}
			}
		}*/

		/*for (int i = 0; i < endIndex; ++i) {
			//if(_plpCurveNormVal[i] > 0)
			_plpCurve [i] /= halfSineWaveSize;
		}*/

		for (int i = 0; i < endIndex; ++i) {
			
			//halfwave rectification
			if (_plpCurve [i] < 0) {
				_plpCurve [i] = 0;
				continue;
			}

			//normalization
			if(_plpCurveNormVal[i] > 0)
				_plpCurve [i] /= _plpCurveNormVal[i] + (_plpCurve[i] > 0? 1:0);
		}


		//debug visualization
		VisualizeData((double datum) =>{
			int toReturn = (int)(_debugVizPLPTex.height* datum) ;
			return toReturn;	
		},ref _plpCurve,ref _debugVizPLPTex);
	}



	private void VisualizeData(System.Func<double,int> heightCallback,ref double[] data, ref Texture2D tex,int windowSamples = -1)
	{
		if (!_enableDebugVisualization)
			return;
		
		Color[] vizTexCols = tex.GetPixels();
		int height = 0;

		if (tex.width != data.GetUpperBound (0) + 1 ) {
			tex.Resize ( data.GetUpperBound (0) + 1,tex.height);
		}

		for (int i = 0; i < vizTexCols.Length; ++i) {
			vizTexCols [i] = Color.black;
		}
		//tex.SetPixels (vizTexCols);
		//tex.Apply ();

		for (int i = 0; i < data.Length; ++i) {

			if(windowSamples != -1)
			{
				int m = i%windowSamples;
				if(m == 0)
				{
					for (int j = 0; j < tex.height; ++j) {
						vizTexCols [(j*tex.width) + i] = Color.green;
					}
				}
			}
			height = heightCallback.Invoke (data [i]);// (data [i]);  (int)((data [i]/ tex.height)  * 10);
			if (height > tex.height - 1)
				height = tex.height - 1;
			//vizTexCols [i * height] = Color.red;
			for (int j = 0; j < height; ++j) {
				vizTexCols [(j*tex.width) + i] = Color.red;
			}
		}
		tex.SetPixels (vizTexCols);
		tex.Apply ();
	}

	private void VisualizeData2D(System.Func<int,int,Color> colorCallback,Color bgColor,ref double[][] data, ref Texture2D tex,int windowSamples = -1)
	{
		if (!_enableDebugVisualization)
			return;
		
		if (tex.height != data.GetUpperBound (0) + 1 || tex.width != data[0].GetUpperBound (0) + 1) {
			tex.Resize ( data[0].GetUpperBound (0)+1,data.GetUpperBound (0)+1);
		}

		Color[] vizTexCols = tex.GetPixels();

		for (int i = 0; i < vizTexCols.Length; ++i) {
			vizTexCols [i] = bgColor;
		}

		for (int rowIndex = 0; rowIndex < tex.height; ++rowIndex) {
			for (int colIndex = 0; colIndex < tex.width; ++colIndex) {
			vizTexCols [(rowIndex * tex.width) + colIndex] = colorCallback != null ? colorCallback.Invoke (rowIndex, colIndex) : Color.Lerp (bgColor, Color.red,(float) data [rowIndex] [colIndex] / tex.width);	
			}
		}
		tex.SetPixels (vizTexCols);
		tex.Apply ();
	}

	public void ClearResources()
	{
		if (_fftBinsRaw != null)
			System.Array.Clear (_fftBinsRaw, 0, _fftBinsRaw.Length);
		_fftBinsRaw = null;

		if (_fftBinsActual != null)
			System.Array.Clear (_fftBinsActual, 0, _fftBinsActual.Length);
		_fftBinsActual = null;

		if (_noveltyCurve != null)
			System.Array.Clear (_noveltyCurve, 0, _noveltyCurve.Length);
		_noveltyCurve = null;


		if (_tempogramActual != null)
			System.Array.Clear (_tempogramActual, 0, _tempogramActual.Length);
		_tempogramActual = null;

		if (_windowedSineWaveCache != null)
			System.Array.Clear (_windowedSineWaveCache, 0, _windowedSineWaveCache.Length);
		_windowedSineWaveCache = null;


		if (_plpCurve != null)
			System.Array.Clear (_plpCurve, 0, _plpCurve.Length);
		_plpCurve = null;

	}

}
