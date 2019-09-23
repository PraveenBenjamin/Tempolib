using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class FSM<T> where T : struct, IConvertible
{
	public delegate int StateInitTerminateEvent(int status);
	public delegate void StateUpdateEvent();

	private class State
	{
		public int StateStatus;

		public StateInitTerminateEvent OnInit = null;
		public StateUpdateEvent OnUpdate = null;
		public StateInitTerminateEvent OnTerminate = null;

		public State()
		{
			this.OnInit = null;
			this.OnUpdate = null;
			this.OnTerminate = null;
			StateStatus = 0;
		}

		~State()
		{
			StateStatus = -1;
			OnInit = null;
			OnUpdate = null;
			OnTerminate = null;
		}
	}

	private T _defaultVal;
	private T _currentState;
	private int _currentStateIndex;
	private T[] _nextStates;
	private State[] _states;
	System.Array _enumValues;

	const string InitSuffix = "Init";
	const string UpdateSuffix = "Update";
	const string TerminateSuffix = "Terminate";

	public bool Initialize(object target, Type targetType)
	{
		System.Array values = System.Enum.GetValues(typeof(T));
		if(values == null || values.Length == 0)
		{
			Debug.LogError("Enum not found or not enough values in enum");
			return false;
		}

		_enumValues = values;

		_defaultVal = (T)System.Convert.ChangeType(values.GetValue(0),typeof(T));
		_currentState = _defaultVal;
		_nextStates = new T[2];
		_nextStates[0] = _defaultVal;
		_nextStates[1] = _defaultVal;
		_currentStateIndex = 0;

		Type thisType = targetType;

		//populate state delegates
		_states = new State[values.Length-1];
		BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
		for(int i = 0 ; i < _states.Length;++i)
		{
			_states[i] = new State();
			MethodInfo[] allMethods = thisType.GetMethods(bindingFlags);
			string evName = values.GetValue(i+1).ToString()+InitSuffix;
			MethodInfo mi = thisType.GetMethod(evName,bindingFlags);
			if(mi != null)
			{
				_states[i].OnInit = (StateInitTerminateEvent)Delegate.CreateDelegate(typeof(StateInitTerminateEvent),target,evName,false);
			}

			evName = values.GetValue(i+1).ToString()+UpdateSuffix;
			mi = thisType.GetMethod(evName,bindingFlags);
			if(mi != null)
			{
				_states[i].OnUpdate = (StateUpdateEvent)Delegate.CreateDelegate(typeof(StateUpdateEvent),target,evName,false);
			}

			evName = values.GetValue(i+1).ToString()+TerminateSuffix;
			mi = thisType.GetMethod(evName,bindingFlags);
			if(mi != null)
			{
				_states[i].OnTerminate = (StateInitTerminateEvent)Delegate.CreateDelegate(typeof(StateInitTerminateEvent),target,evName,false);
			}
		}

		return true;
	}

	public void SetState(T state)
	{
		if(state.Equals(_defaultVal))
			return;

		if(_currentState.Equals(state))
			return;

		if(_nextStates[0].Equals(state))
			return;
		if(_nextStates[0].Equals(_defaultVal))
			_nextStates[0] = state;
		else
			_nextStates[1] = state;
	}


	public T GetCurrentState()
	{
		return _currentState;
	}

	private bool UpdateCurrentState()
	{
		if(_currentStateIndex == -1)
			return false;

		if(!_nextStates[0].Equals(_defaultVal))
		{
			State curr = _states[_currentStateIndex];
			if(curr.OnTerminate != null)
			{
				curr.StateStatus = curr.OnTerminate(curr.StateStatus);
				if(curr.StateStatus != 0)
					return false;
			}
			_currentState = _nextStates[0];
			_nextStates[0] = _nextStates[1];
			_nextStates[1] = _defaultVal;

			//to account for the none state
			_currentStateIndex = System.Array.IndexOf(_enumValues,_currentState) - 1;

			curr = _states[_currentStateIndex];
			if(curr.OnInit != null)
			{
				curr.StateStatus = curr.OnInit(curr.StateStatus);
				if(curr.StateStatus != 0)
					return false;
			}
			_states[_currentStateIndex].StateStatus = 0;
		}

		return true;
	}

	public void Update()
	{
		if(UpdateCurrentState())
		{
			if(_states[_currentStateIndex].OnUpdate != null)
				_states[_currentStateIndex].OnUpdate();
		}
	}

	public void ClearResources()
	{
		System.Array.Clear(_states,0,_states.Length);
		_states = null;
		System.Array.Clear(_nextStates,0,_nextStates.Length);
		_nextStates = null;
	}
}

