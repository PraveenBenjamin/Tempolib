using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ALib.Core
{
	public delegate void TemporalConductorCallback(Vector2Int frequencyRange, float normalizedEnergy,float deltaEnergy = 0);

	//base class for any event the conductor wishes to offer registration for.
	public abstract class BaseTemporalConductorEvent
	{
		public enum EventType
		{
			None,
			Stream,
			Beat
		}

		protected Vector2Int _frequencyRange;
		public Vector2Int FrequencyRange
		{
			get
			{
				return _frequencyRange;
			}
		}

		protected TemporalConductorCallback _callback;
		private EventType _type;
		public virtual EventType Type
		{
			get
			{
				return EventType.None;
			}
		}

		//make the programmer want it :p
		public void SetCallback(TemporalConductorCallback cb)
		{
			_callback = cb;
		}


		public virtual void Invoke(float normalizedEnergy, float deltaEnergy = 0)
		{
			if(_callback != null)
				_callback.Invoke(_frequencyRange,normalizedEnergy,deltaEnergy);
		}

		public BaseTemporalConductorEvent()
		{
			_frequencyRange = Vector2Int.one * -1;
		}

		public BaseTemporalConductorEvent(Vector2Int frequencyRange)
		{
			_frequencyRange = frequencyRange;
		}

		~BaseTemporalConductorEvent()
		{
			_callback = null;
			_type = EventType.None;
		}
	}

	//event that will be fired every update and present the energy and delta energy for the specified frequency range
	public class AudioStreamEvent : BaseTemporalConductorEvent
	{
		public override EventType Type
		{
			get
			{
				return EventType.Stream;
			}
		}

		public AudioStreamEvent (Vector2Int frequencyRange) : base (frequencyRange){}
	}


	//event that will be fired every time the delta energy for the specified frequency range increases or falls below a specified threshold.
	public class BeatEvent : BaseTemporalConductorEvent
	{
		public override EventType Type
		{
			get
			{
				return EventType.Beat;
			}
		}

		private float _minNormalizedDelta;
		public float MinNormalizedDelta
		{
			get
			{
				return _minNormalizedDelta;
			}
		}

		private int _maxEnergyHistoryFrames;
		private Queue<float> _energyHistory;

		public void UpdateBeatEvent(float normalizedHistory)
		{
			if (_energyHistory.Count == _maxEnergyHistoryFrames)
				_energyHistory.Dequeue ();

			_energyHistory.Enqueue (normalizedHistory);
		}

		public float AverageEnergy
		{
			get
			{
				float val = 0;
				foreach (float f in _energyHistory) {
					val += f;
				}

				return val / _energyHistory.Count;
			}
		}

		public BeatEvent( Vector2Int frequencyRange, float minNormalizedDelta,int halfFFTWindowSize) : base(frequencyRange)
		{
			_minNormalizedDelta = minNormalizedDelta;

			_maxEnergyHistoryFrames = halfFFTWindowSize;

			if(_energyHistory != null)
				_energyHistory.Clear();

			_energyHistory = new Queue<float>();
		}
	}
}

