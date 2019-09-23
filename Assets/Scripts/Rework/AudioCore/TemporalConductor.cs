using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.PlayerLoop;

namespace ALib.Core
{

	///The temporal conductor performs beat detection and offers event registration to any class that wishes to listen for streams and beats in the audio sample
	class TemporalConductor : MonoBehaviour {

		private AudioSampler samplerInstance
		{
			get
			{
				return AudioSampler.Instance;
			}
		}

		private static TemporalConductor _instance;
		public static TemporalConductor Instance
		{
			get
			{
				return _instance;
			}
		}


		private List<BaseTemporalConductorEvent> _registeredEvents;
		private double[] _prevFFTOutput;
		private double[] _currentFFTOutput;

		public double _debugMaxEnergy = 0;
		public double _debugMinEnergy = 0;

		private void Awake()
		{
			if(TemporalConductor.Instance == null)
			{
				_instance = this;
				_registeredEvents = new List<BaseTemporalConductorEvent>();
			}
			else
			{
				DestroyImmediate(this);
			}
		}

		private void LateUpdate()
		{
			//In case streaming stops
			if(_currentFFTOutput == null || _currentFFTOutput.Length == 0)
			{
				//clear array memory and return;
				_currentFFTOutput = null;
				_prevFFTOutput = null;
				return;
			}

			//if streaming source changes.
			if((_prevFFTOutput == null && _currentFFTOutput != null) || (_prevFFTOutput != null && _prevFFTOutput.Length != _currentFFTOutput.Length))
			{
				//Resize arrays
				_prevFFTOutput = new double[_currentFFTOutput.Length];
			}

			//ensure we fill in the prev output if all is favourable
			for(int i = 0 ; i < _currentFFTOutput.Length ; ++i)
			{
				_prevFFTOutput[i] = _currentFFTOutput[i];
			}

		}

		//TODO :- shift to the sampler
		public static double RetrieveEnergyInRange(ref double[] channelBinnedFFTOutput, Vector2Int frequencyRange, int clipFrequency = 44100)
		{
			if (channelBinnedFFTOutput== null || channelBinnedFFTOutput.Length == 0)
				return 0;
			int maxUniqueFrequencies = channelBinnedFFTOutput.Length * 2;
			float frequencyResolution = clipFrequency/maxUniqueFrequencies;
			double energy = 0;
			int count = 0;
			for(int i = 0 ; i < channelBinnedFFTOutput.Length; ++i)
			{
				float maxSampleFrequency = frequencyResolution * (i+1);
				//lesser than maxRange and max-freqRes is greater than minRange
				if(maxSampleFrequency <= frequencyRange.y && maxSampleFrequency - frequencyResolution >= frequencyRange.x)
				{
					count += 1;
					energy += channelBinnedFFTOutput[i];
				}

				if(maxSampleFrequency >= frequencyRange.y)
					break;
			}

			energy /= count;
			return energy;
		}


		private void Update()
		{

			double[] audioSamples = samplerInstance.GetSamples ();
			double[] currentFFTOutput = samplerInstance.GetFFTOutputBins (ref audioSamples);

			if (currentFFTOutput == null || currentFFTOutput.Length == 0)
				return;
			
			if (_currentFFTOutput == null || _currentFFTOutput.Length == 0 || _currentFFTOutput.Length != currentFFTOutput.Length) {
				_currentFFTOutput = new double[currentFFTOutput.Length];
			}


			for (int i = 0; i < _currentFFTOutput.Length; ++i) {
				_currentFFTOutput [i] = currentFFTOutput [i];
			}

			AudioSampler.NormalizeFFTOutput (ref _currentFFTOutput,AudioSampler.Instance.ClipFrequency);

			for (int i = 0; i < _currentFFTOutput.Length; ++i) {
				if (_currentFFTOutput [i] < _debugMinEnergy)
					_debugMinEnergy = _currentFFTOutput [i];
				else if (_currentFFTOutput [i] > _debugMaxEnergy)
					_debugMaxEnergy = _currentFFTOutput [i];
			}

			for(int i = 0 ;i < _registeredEvents.Count; ++i)
			{
				BaseTemporalConductorEvent temporalEvent = _registeredEvents[i];

				//remove destroyed Events
				if(temporalEvent.Type == BaseTemporalConductorEvent.EventType.None)
				{
					_registeredEvents.RemoveAt(i);
					--i;
					continue;
				}

				float energy = (float)RetrieveEnergyInRange(ref _currentFFTOutput,temporalEvent.FrequencyRange,44100);
				float prevEnergy = (float)RetrieveEnergyInRange (ref _prevFFTOutput,temporalEvent.FrequencyRange,44100);
				float delta = energy - prevEnergy;

				switch(temporalEvent.Type)
				{
				case BaseTemporalConductorEvent.EventType.Stream:
					{
						temporalEvent.Invoke(energy,delta);
					}
					break;
				case BaseTemporalConductorEvent.EventType.Beat:
					{
						BeatEvent beatEvent = (BeatEvent)temporalEvent;
						beatEvent.UpdateBeatEvent (energy);

						float minNormalizedDelta = beatEvent.MinNormalizedDelta;
						if (minNormalizedDelta <= 0)
							break;

						float traversalVal = ((1 - beatEvent.AverageEnergy) * minNormalizedDelta);
						bool isBeat = delta > traversalVal;

						//if delta is greater than the specified threshold, invoke event
						if(isBeat)
						{
							temporalEvent.Invoke(energy,delta);
						}
					}
					break;
				}
			}
		}

		public void RegisterEvent(BaseTemporalConductorEvent eventToRegister)
		{
			if (!_registeredEvents.Contains (eventToRegister))
				_registeredEvents.Add (eventToRegister);
		}

		//clear it all!
		private void OnDestroy()
		{
			_registeredEvents.Clear();
			_registeredEvents = null;
			_currentFFTOutput = null;
			_prevFFTOutput = null;
		}

	}
}

