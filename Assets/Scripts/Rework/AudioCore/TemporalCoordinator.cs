using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ALib.Core
{

	public class TemporalCoordinator : MonoBehaviour {

		private class BeatEventDatum
		{
			public event TemporalConductorCallback Event;

			public void Invoke(Vector2Int frequencyRange, float normalizedEnergy, float deltaEnergy = 0)
			{
				Event.Invoke (frequencyRange, normalizedEnergy, deltaEnergy);
			}
		}

		private class TemporalCoordinatorEvent
		{
			public event TemporalConductorCallback StreamEvent;
			public Dictionary<float,BeatEventDatum> BeatEvents;

			public bool HasStreamEvents = false;

			public HashSet<TemporalConductorCallback> Callbacks;

			public TemporalCoordinatorEvent()
			{
				BeatEvents = new Dictionary<float, BeatEventDatum>();
				Callbacks = new HashSet<TemporalConductorCallback>();
			}

			public void InvokeStreamEvent(Vector2Int frequencyRange, float normalizedEnergy, float deltaEnergy = 0)
			{
				StreamEvent.Invoke (frequencyRange, normalizedEnergy, deltaEnergy);
			}


			public void InvokeBeatEvent(Vector2Int frequencyRange, float normalizedEnergy, float deltaEnergyKey,float deltaEnergyActual)
			{
				if(BeatEvents.ContainsKey(deltaEnergyKey))
					BeatEvents[deltaEnergyKey].Invoke (frequencyRange, normalizedEnergy, deltaEnergyActual);
			}
		}

		private static TemporalCoordinator _instance;
		public static TemporalCoordinator Instance
		{
			get
			{
				return _instance;
			}
		}

		private TemporalConductor _conductorInstance
		{
			get
			{
				return TemporalConductor.Instance;
			}
		}

		private Dictionary<Vector2Int,TemporalCoordinatorEvent> _registeredEvents; 

		private void Awake()
		{
			if(TemporalCoordinator.Instance != null)
			{
				DestroyImmediate (this);
				return;
			}

			_instance = this;
			initialize ();
		}

		private void initialize()
		{
			_registeredEvents = new Dictionary<Vector2Int, TemporalCoordinatorEvent> ();
		}

		public void RegisterStreamEvent(Vector2Int frequencyRange, TemporalConductorCallback callback)
		{
			TemporalCoordinatorEvent te = null;

			if (!_registeredEvents.ContainsKey (frequencyRange)) {
				
				te = new TemporalCoordinatorEvent ();
				_registeredEvents.Add (frequencyRange, te);
			}

			if (!_registeredEvents [frequencyRange].HasStreamEvents) {
				te.HasStreamEvents = true;
				AudioStreamEvent ase = new AudioStreamEvent (frequencyRange);
				ase.SetCallback (streamInternalCallback);
				_conductorInstance.RegisterEvent (ase);
			}


			te = _registeredEvents [frequencyRange];
			if (te.Callbacks.Contains (callback)) {
				Debug.LogWarning ("Attempting to register same callback twice. This is not allowed. Please supply different method signature as callback.");
				return;
			}
			te.StreamEvent += callback;
		}

		public void RegisterBeatEvent(Vector2Int frequencyRange, float beatDelta,int halfFFTWindow,TemporalConductorCallback callback)
		{
			if (beatDelta == 0) {
				Debug.LogError ("Attempting to register beat event with 0 beat delta. This is not allowed.");
				return;
			}

			TemporalCoordinatorEvent te = null;

			if (!_registeredEvents.ContainsKey (frequencyRange)) {
				te = new TemporalCoordinatorEvent ();
				_registeredEvents.Add (frequencyRange, te);
			}

			te = _registeredEvents [frequencyRange];

			if (!te.BeatEvents.ContainsKey (beatDelta)) {
				te.BeatEvents.Add (beatDelta, new BeatEventDatum());
				BeatEvent be = new BeatEvent (frequencyRange, beatDelta,halfFFTWindow);
				be.SetCallback (beatInternalCallback);
				_conductorInstance.RegisterEvent (be);
			}

			te.BeatEvents [beatDelta].Event += callback;
		}

		public void UnregisterStreamEvent(Vector2Int frequencyRange, TemporalConductorCallback callback)
		{
			if (!_registeredEvents.ContainsKey (frequencyRange))
				return;
			TemporalCoordinatorEvent te = _registeredEvents [frequencyRange];

			if (te.Callbacks.Contains (callback)) {
				te.StreamEvent -= callback;
				te.Callbacks.Remove (callback);
			}

		}

		public void UnregisterBeatEvent(Vector2Int frequencyRange, float beatDelta,TemporalConductorCallback callback)
		{
			if (!_registeredEvents.ContainsKey (frequencyRange))
				return;
			TemporalCoordinatorEvent te = _registeredEvents [frequencyRange];

			if (te.Callbacks.Contains (callback) && te.BeatEvents.ContainsKey (beatDelta)) {
				te.BeatEvents [beatDelta].Event -= callback;
				te.Callbacks.Remove (callback);
			}
		}


		private void streamInternalCallback(Vector2Int frequencyRange, float normalizedEnergy,float deltaEnergy = 0)
		{
			TemporalCoordinatorEvent te = _registeredEvents [frequencyRange];
			if (te == null) {
				_registeredEvents.Remove (frequencyRange);
				return;
			}

			//trigger the event
			te.InvokeStreamEvent (frequencyRange, normalizedEnergy, deltaEnergy);
		}

		private void beatInternalCallback(Vector2Int frequencyRange, float normalizedEnergy,float deltaEnergy = 0)
		{
			TemporalCoordinatorEvent te = _registeredEvents [frequencyRange];
			if (te == null) {
				_registeredEvents.Remove (frequencyRange);
				return;
			}

			//trigger the event
			foreach (KeyValuePair<float,BeatEventDatum> pair in te.BeatEvents) {
				if (pair.Key <= deltaEnergy) {
					te.InvokeBeatEvent (frequencyRange, normalizedEnergy, pair.Key,deltaEnergy);
				}
			}
		}

	}

}