using System;
using System.Collections;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace GLib.EntityManagement
{

	public enum EntityStates
	{
		None,
		Init,
		Spawning,
		Existing,
		Dying
	}


	//DO NOT MANUALLY DESTROY ANY ENTIIES. SET ITS STATE TO DYING INSTEAD
	public abstract class BaseEntity: MonoBehaviour
	{
		protected FSM<EntityStates> _entityFSM;

		protected virtual void Awake()
		{
			//Debug.Log ("Awake");
			_entityFSM = new FSM<EntityStates> ();
			_entityFSM.Initialize((object)this,this.GetType());
			EntityManager.Instance.RegisterEntity(this,UpdateEntity);
			InitializeEntity();
		}

		protected abstract void InitializeEntity();

		private void UpdateEntity()
		{
			//Debug.Log ("Update");
			_entityFSM.Update();
			OnUpdateInternal ();
		}

		protected virtual void OnUpdateInternal(){}

		private void DyingTerminate()
		{
			Debug.Log ("Dying");
			Destroy (this.gameObject);
		}

		private void OnDestroy()
		{
			EntityManager.Instance.UnregisterEntity(this);
			OnDestroyInternal ();
			_entityFSM.ClearResources ();
			_entityFSM = null;
		}

		protected virtual void OnDestroyInternal() {}

		public abstract void Reset();
	}


	class EntityManager : MonoBehaviour{

		//TODO we need an ordered implementation of this
		private Dictionary<BaseEntity,Action> _entityUpdateDictionary;

		private static EntityManager _instance;
		public static EntityManager Instance
		{
			get
			{
				return _instance;
			}
		}

		private void Awake()
		{
			if(EntityManager.Instance != null)
			{
				DestroyImmediate(this);
				return;
			}

			_instance = this;
			_entityUpdateDictionary = new Dictionary<BaseEntity,Action>();
		}

		public void RegisterEntity(BaseEntity entity,Action updateCallback)
		{
			_entityUpdateDictionary.Add(entity,updateCallback);
		}

		public void UnregisterEntity(BaseEntity entity)
		{
			_entityUpdateDictionary.Remove(entity);
		}

		public void Update()
		{
			foreach(KeyValuePair<BaseEntity,Action> pair in _entityUpdateDictionary)
			{
				if(pair.Key.gameObject.activeInHierarchy && pair.Key.gameObject.activeSelf)
					pair.Value.Invoke();
			}
		}

	}
}

