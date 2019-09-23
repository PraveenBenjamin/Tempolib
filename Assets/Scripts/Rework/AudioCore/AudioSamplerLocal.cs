using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AJ.Utilities;

namespace ALib.Core
{

	public class AudioSamplerLocal : AudioSampler {

		//properties
		public override AudioSamplerType SamplerType {
			get {
				return AudioSamplerType.Local;
			}
		}
		public AudioClip Clip
		{
			get
			{
				return _clip;
			}
		}

		private AudioSource _audioSource;

		public override int ClipFrequency
		{
			get
			{
				if (_clip != null)
					return _clip.frequency;
				return base.ClipFrequency;
			}
		}

		public override int SampleCount
		{
			get
			{
				if (_clip != null)
					return Utilities.NextNearestPowerOf2(ClipFrequency / 30);
				return base.SampleCount;
			}
		}

		public override int FFTBinCount
		{
			get
			{ 
				if (_clip != null)
					return SampleCount/ ChannelCount;
				return base.FFTBinCount;
			}
		}

		public override int ChannelCount {
			get {
				if (_clip != null)
					return _clip.channels;
				return base.ChannelCount;
			}
		}

		public override bool IsInitialized {
			get {
				return _clip != null;
			}
		}


		//private fields
		private AudioClip _clip;



		protected override void ClearResources ()
		{
			if (_clip != null) {
				Stop ();
				_clip.UnloadAudioData ();
				_clip = null;
			}
		}

		protected override void OnDestroy ()
		{
			base.OnDestroy ();
			Destroy (_audioSource);
		}


		protected override void Play()
		{
			if (_audioSource != null && _audioSource.clip != null) {
				if (_audioSource.time != 0)
					_audioSource.UnPause ();
				else
					_audioSource.Play ();
			}
		}

		protected override void Stop()
		{
			if (_audioSource != null && _audioSource.clip != null)
				_audioSource.Stop ();
		}

		protected override void Pause()
		{
			if (_audioSource != null && _audioSource.clip != null)
				_audioSource.Pause ();
		}



		/// <summary>
		/// Initialize a local audio sampler.
		/// Must be called before use
		/// </summary>
		/// <param name="requiredParameters">Required parameters.</param>
		/// Send in the audio clip in the first index of the object[] 
		public override bool Initialize (params object[] requiredParameters)
		{
			//clear currently used resources
			if (AudioSampler.Instance.IsInitialized)
				ClearResources ();


			if (requiredParameters != null && requiredParameters.Length > 0) {

				bool isInitialized = false;
				try
				{
					_clip = (AudioClip)requiredParameters [0];

					//clear audio sample bucket
					if(_audioSamples == null)
					{
						_audioSamples = new float[SampleCount];
						_dAudioSamples = new double[SampleCount];
					}
					else
					{
						if(_audioSamples.Length != SampleCount)
						{
							_audioSamples = new float[SampleCount];
							_dAudioSamples = new double[SampleCount];
						}
						else
						{
							System.Array.Clear(_audioSamples,0,_audioSamples.Length);
							System.Array.Clear(_dAudioSamples,0,_dAudioSamples.Length);
						}
					}

					//setup audio source
					if(_audioSource == null)
						_audioSource = gameObject.AddComponent<AudioSource>();

					_audioSource.clip = _clip;
					//set isInitialized flag
					isInitialized = _clip != null;
					
				}
				catch (System.Exception e) {
					Debug.LogError ("Initialization of AudioSamplerLocal failed: "+e.Message);

					_clip = null;
				}


				return isInitialized;
			}
			return base.Initialize (requiredParameters);
		}

		/// <summary>
		/// Gets audio samples from currentTimeSamples+offsetSamples to currentTimeSamples+offsetSamples+SampleCount.
		/// returns a reference of the array. Does not return a copy of the samples. Do not modify contents of array.
		/// </summary>
		/// <returns>The samples.</returns>
		/// <param name="offsetSamples">Offset samples.</param>
		protected override float[] GetSamplesInternal (int offsetSamples = 0)
		{
			if (!IsInitialized)
				return null;
			
			offsetSamples = _audioSource.timeSamples + offsetSamples;

			if (offsetSamples > 0 || offsetSamples < (_clip.samples - SampleCount)) {
				_clip.GetData (_audioSamples, offsetSamples);
				return _audioSamples;
			}

			Debug.LogWarning ("Unable to retrieve samples. Presumably due to seeking too far behind or too far ahead. " +
				"Please check offsetSamples. " +
				"Disregard this warning if it appears at the end of the seek time");
			
			return null;
		}


	}
}