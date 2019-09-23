using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AJ.Utilities;

namespace ALib.Core
{

	public enum AudioSamplerType
	{
		None,
		Local,
		Stream
	}


	public enum AudioPlaybackState
	{
		Stop,
		Playing,
		Paused
	}

	public abstract class AudioSampler : MonoBehaviour {

		//public properties
		public static AudioSampler Instance
		{
			get
			{
				return _instance;
			}
		}


		public virtual int ClipFrequency
		{
			get
			{
				return ClipNotLoadedSentinel;
			}
		}

		public virtual int SampleCount
		{
			get
			{
				return ClipNotLoadedSentinel;
			}
		}

		public virtual int FFTBinCount
		{
			get
			{ 
				return ClipNotLoadedSentinel;
			}
		}

		public virtual bool IsInitialized
		{
			get
			{
				return false;
			}
		}

		public virtual int ChannelCount
		{
			get
			{
				return ClipNotLoadedSentinel;
			}
		}

		public AudioPlaybackState AudioPlaybackState
		{
			get 
			{
				return _audioPlaybackState;
			}
		}

		//protected fields
		protected static AudioSampler _instance;

		protected float[] _audioSamples;
		//cant really fight this... its overhead vs runtime allocation and i picked overhead
		protected double[] _dAudioSamples;
		protected double[] _fftBinsOutput;
		protected double[] _fftBinsActual;

		private LomontFFT _fftScript;

		//DONT LIKE HOW THIS IS SETUP. THINK OF A BETTER WAY TO INSTANTIATE THE HOST OBJECT
		private static GameObject _samplerGo;

		//private fields
		private AudioPlaybackState _audioPlaybackState;

		//constants
		public const int ClipNotLoadedSentinel = -1;

		public virtual AudioSamplerType SamplerType
		{
			get
			{
				return AudioSamplerType.None;
			}
		}


		private void Awake()
		{
			if (AudioSampler.Instance != null)
				DestroyImmediate (this);
		}

		private void SetInstance()
		{
			if (AudioSampler.Instance == null) {
				_instance = this;
			}
			else
				Debug.LogError("Another AudioSampler instance already exists. Destroy existing before switching instances");
		}

		public static void InstantiateSampler(AudioSamplerType toSwitchTo)
		{
			if (AudioSampler.Instance != null)
				DestroyImmediate (AudioSampler.Instance);


			if (_samplerGo == null)
				_samplerGo = new GameObject ("AudioSampler");
			

			AudioSampler addedSampler = null;
			switch (toSwitchTo) {
			case AudioSamplerType.Local:
				{
					addedSampler = _samplerGo.AddComponent<AudioSamplerLocal> ();
				}
				break;
			case AudioSamplerType.Stream:
				{
					addedSampler = _samplerGo.AddComponent<AudioSamplerStream> ();

				}
				break;
			}

			if (addedSampler != null) {
				addedSampler.SetInstance ();
			} else {
				Debug.LogError ("Unable to switch to specified audio sampler type. Setting type to None instead");
			}
		}

		public virtual bool Initialize(params object[] requiredParameters)
		{
			return false;
		}

		protected virtual void ClearResources()
		{
			throw new System.NotImplementedException();
		}

		protected abstract void Pause ();

		protected abstract void Play ();

		protected abstract void Stop ();

		public void SetPlaybackState(AudioPlaybackState state)
		{
			if (state == _audioPlaybackState)
				return;
			
			switch (state) {
			case AudioPlaybackState.Stop:
				{
					Stop ();
				}
				break;
			case AudioPlaybackState.Paused:
				{
					Pause ();
				}
				break;
			case AudioPlaybackState.Playing:
				{
					Play ();
				}
				break;
			}

			_audioPlaybackState = state;
		}


		protected virtual void OnDestroy()
		{
			ClearResources ();

			if (_audioSamples != null)
				System.Array.Clear (_audioSamples, 0, _audioSamples.Length);
			
			if (_dAudioSamples != null)
				System.Array.Clear (_dAudioSamples, 0, _dAudioSamples.Length);

			if (_fftBinsOutput != null)
				System.Array.Clear (_fftBinsOutput, 0, _fftBinsOutput.Length);
			
			if (_fftBinsActual != null)
				System.Array.Clear (_fftBinsActual, 0, _fftBinsActual.Length);

			GameObject.Destroy (_samplerGo);
		}

		protected virtual float[] GetSamplesInternal(int offsetSamples = 0)
		{
			return null;
		}

		public double[] GetSamples (int offsetSamples = 0)
		{
			_audioSamples = GetSamplesInternal (offsetSamples);

			//TODO :- find a more efficient way to get samples as doubles
			if (_audioSamples != null)
				for (int i = 0; i < _audioSamples.Length; ++i)
					_dAudioSamples [i] = (double)_audioSamples [i];
			
			return _dAudioSamples;
		}

		/// <summary>
		/// Performs the FFT on the provided samples.
		/// Will bin channels internally
		/// </summary>
		/// <returns>The FF.</returns>
		/// <param name="dAudioSamples">D audio samples.</param>
		/// <param name="fftType">Fft type.</param>
		/// <param name="forwardFFT">If set to <c>true</c> forward FF.</param>
		public double[] GetFFTOutputBins(ref double[] dAudioSamples, int fftType = 1, bool forwardFFT = true,bool applyHannWindowing = true)
		{
			if (!IsInitialized)
				return null;

			if (_fftScript == null) {
				_fftScript = new LomontFFT ();
				_fftScript.A = 1;
				_fftScript.B = -1;
			}

			if (_fftBinsOutput == null || _fftBinsOutput.Length != FFTBinCount) {
				_fftBinsOutput = new double[FFTBinCount];
				_fftBinsActual = new double[_fftBinsOutput.Length/ChannelCount];
			}

			System.Array.Clear (_fftBinsOutput, 0, _fftBinsOutput.Length);

			for (int sampleIndex = 0; sampleIndex < _dAudioSamples.Length; sampleIndex += ChannelCount) {
				for (int channelIndex = 0; channelIndex < ChannelCount; ++channelIndex) {
					int binIndex = sampleIndex == 0 ? 0 : sampleIndex / ChannelCount;
					double valToAdd = Mathf.Pow ((float)_dAudioSamples [sampleIndex + channelIndex], 4);
					//double valToAdd = _dAudioSamples [sampleIndex + channelIndex];
					_fftBinsOutput [binIndex] += _dAudioSamples [sampleIndex + channelIndex];
				}
			}

			for (int i = 0; i < _fftBinsOutput.Length; ++i) {
				_fftBinsOutput [i] /= ChannelCount;
			}


			//apply hanning window
			if(applyHannWindowing)
				Utilities.Hann(ref _fftBinsOutput);

			switch (fftType) {
			case 0:
				{
					Debug.LogWarning ("COMPLEXFFT NOT SUPPORTED YET");
					//_fftScript.FFT (_fftBinsOutput, forwardFFT);
				}
				break;
			case 1:
				{
					_fftScript.RealFFT (ref _fftBinsOutput, forwardFFT);
					_fftBinsOutput [0] = 0;
					_fftBinsOutput [1] = 0;
					for (int i = 2; i < _fftBinsOutput.Length; i += 2) {
						float real = (float)_fftBinsOutput [i];
						float im = (float)_fftBinsOutput [i+1];
						float val =  Mathf.Sqrt (Mathf.Pow (real, 2) + Mathf.Pow (im, 2));
						if (i == 2)
							_fftBinsActual [0] = val;
						else
							_fftBinsActual [(i / 2)-1] = val;
					}
				}
				break;
			case 2:
				{
					Debug.LogWarning ("TABLEFFT NOT SUPPORTED YET");
					//_fftScript.TableFFT (_fftBinsOutput, forwardFFT);
				}
				break;
			}

			return _fftBinsActual;
		}

		public static void NormalizeFFTOutput(ref double[] fftOutput,float clipSampleRate)
		{
			//float clampMax = 256;
			for (int i = 0; i < fftOutput.Length; ++i) {
				//float val = Mathf.Clamp((float)(fftOutput[i]/256),0,fftOutput.Length);
				//float val = (float)Utilities.Sinerp (0.01f, 1.0f, (float)fftOutput[i]/(fftOutput.Length*0.25f),1);//levelsToLerp);
				//float freqOfBin = (clipSampleRate/fftOutput.Length) * i;
				float val = Mathf.Lerp (0.01f, 1.0f, (float)(fftOutput [i]/120));
				//float val = Utilities.Sinerp (0.01f, 1.0f, (float)(fftOutput [i]/(fftOutput.Length*0.25f)), 1);
				fftOutput [i] = val;
			}
		}

		public static void BinFFTOutput(ref double[] fftSrc,ref double[] dst,int channelCount = 2,int clipFrequency = 44100, params Vector2Int[] rangeDef)
		{
			if (fftSrc == null 
				|| fftSrc.Length == 0 
				|| dst == null
				|| dst.Length == 0
				|| rangeDef == null 
				|| rangeDef.Length == 0) 
			{
				Debug.LogError ("One or more parameters null or empty. Aborting");
				return;
			}

			if (dst.Length != rangeDef.Length) {
				Debug.LogError ("Dst array length != rangeDef.length. Aborting");
				return;
			}

			int[] frequencesPerFinalBin = new int[dst.Length];

			for (int i = 0; i < fftSrc.Length; ++i) {
				double freqOfSample = (i * (clipFrequency / channelCount) ) / fftSrc.Length;
				int binIndex = 0;
				for (; binIndex < rangeDef.Length-1; ++binIndex) {
					if (freqOfSample >= rangeDef [binIndex].x && freqOfSample <= rangeDef [binIndex].y) {
						if(fftSrc [i] > 0)
							frequencesPerFinalBin [binIndex] += 1;
						dst [binIndex] += Mathf.Abs ((float)fftSrc [i]);
					}
				}
			}

			for (int i = 0; i < dst.Length; ++i) {
				if (frequencesPerFinalBin [i] > 0) {
					dst [i] /= frequencesPerFinalBin [i];
				}
			}
		}
	}
}
