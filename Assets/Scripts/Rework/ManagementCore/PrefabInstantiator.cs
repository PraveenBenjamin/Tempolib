using System;
using System.Collections.Generic;
using UnityEngine;
using GLib.EntityManagement;
using AJ.Core;

namespace GLib.AssetManagement
{

	//doesnt have to be a monobehaviour but whatever :/
	class PrefabInstantiator : MonoBehaviour {

		[SerializeField]
		private BaseAJEntity[] _allSpawnables;
		private Dictionary<string, GameObject> _allPrefabs;

		private GameObject _instantiatedAssetsParent;
		private Dictionary<string, GameObject> _instantiatedAssets;
		private Dictionary<string,GameObject> _prefabPools;

		private static PrefabInstantiator _instance;
		public static PrefabInstantiator Instance
		{
			get
			{
				return _instance;
			}
		}

		public void Awake()
		{
			if(PrefabInstantiator.Instance != null)
			{
				DestroyImmediate(this);
				return;
			}

			//HACK!!!
			_allPrefabs = new Dictionary<string, GameObject>();
			for (int i = 0; i < _allSpawnables.Length; ++i) {
				_allPrefabs.Add (_allSpawnables [i]._assetUID, _allSpawnables [i].gameObject);
			}
			//END HACK!!!

			if(_allPrefabs == null || _allPrefabs.Count == 0)
			{
				Debug.LogError("No assets made available to the asset manager. Please check the preprocessing protocol for this class");
				DestroyImmediate(this);
				return;
			}



			_instance = this;
			_instantiatedAssetsParent = new GameObject("InstantiatedPrefabs");
			_instantiatedAssetsParent.SetActive(false);
			_instantiatedAssetsParent.transform.SetParent(_instance.gameObject.transform);
			_instantiatedAssets = new Dictionary<string,GameObject>();
			_prefabPools = new Dictionary<string,GameObject>();
		}

		public GameObject InstantiatePrefab(string assetUID)
		{
			if(!_allPrefabs.ContainsKey(assetUID))
			{
				Debug.LogError("Asset for assetUID "+assetUID+" not found. returning null");
				return null;
			}

			GameObject toReturn = null;
			if(!_instantiatedAssets.ContainsKey(assetUID))
			{
				GameObject baseObject = GameObject.Instantiate(_allPrefabs[assetUID]);
				//remove the "(Clone)" suffix
				baseObject.name = _allPrefabs[assetUID].name;
				baseObject.transform.SetParent(_instantiatedAssetsParent.transform,false);
				_instantiatedAssets.Add(assetUID,baseObject);
			}
			toReturn = GameObject.Instantiate(_instantiatedAssets[assetUID]);
			toReturn.name = _allPrefabs[assetUID].name;
			return toReturn;
		}


		//WARNING :- dont use this if you know the objects are in use.
		//wait until the pool is unused.
		public void InitializePrefabPool(string assetUID,int objectCount)
		{
			GameObject poolParent = null;
			if(!_prefabPools.ContainsKey(assetUID))
			{
				poolParent = new GameObject(assetUID+"Parent");
				poolParent.transform.SetParent(_instantiatedAssetsParent.transform,false);
				_prefabPools.Add(assetUID,poolParent);
			}
			else
			{
				Debug.LogWarning("Pool already exists for assetUID "+assetUID+". Resizing/Refreshing pool instead");
			}

			if(objectCount == 0)
			{
				DeletePrefabPool(assetUID);
				return;
			}

			poolParent = _prefabPools[assetUID];

			if(objectCount > poolParent.transform.childCount)
			{
				for(int i = poolParent.transform.childCount ; i <objectCount; ++i)
				{
					GameObject instantiatedObj = InstantiatePrefab(assetUID);
					instantiatedObj.transform.SetParent(poolParent.transform);
				}
			}
			else if(objectCount < poolParent.transform.childCount)
			{
				for(int i = poolParent.transform.childCount ; i > objectCount; --i)
				{
					GameObject poolObject = poolParent.transform.GetChild(0).gameObject;
					poolObject.transform.SetParent(null);
					Destroy(poolObject.gameObject);
				}
			}
		}

		public GameObject GetPrefabFromPool(string assetUID)
		{
			if(_prefabPools.ContainsKey(assetUID))
			{
				GameObject poolParent = _prefabPools[assetUID];
				if(poolParent.transform.childCount == 0)
				{
					//add another to pool
					GameObject instantiatedObj = InstantiatePrefab(assetUID);
					instantiatedObj.transform.SetParent(poolParent.transform);
				}
				GameObject poolObject = _prefabPools[assetUID].transform.GetChild(poolParent.transform.childCount-1).gameObject;
				poolObject.transform.SetParent(null);
				return poolObject;
			}
			else
			{
				Debug.LogWarning("No Pools initialized for "+assetUID+" attempting to instantiating one instead");
				return InstantiatePrefab(assetUID);
			}

		}

		public void ReturnPrefabToPool(string assetUID, GameObject toReturn)
		{
			if(!_prefabPools.ContainsKey(assetUID))
			{
				Debug.LogWarning("Attempting to return to non-existent pool. Returning.");
				return;
			}

			if(toReturn.GetType().IsSubclassOf(typeof(BaseEntity)))
			{
				BaseEntity en = (BaseEntity)System.Convert.ChangeType(toReturn,typeof(BaseEntity));
				en.Reset();
			}

			toReturn.transform.SetParent(_prefabPools[assetUID].transform,false);
		}


		public void DeletePrefabPool(string assetUID)
		{
			if(!_prefabPools.ContainsKey(assetUID))
			{
				Debug.LogWarning("No pool object found with assetUID "+assetUID);
				return;
			}

			GameObject poolParent = _prefabPools[assetUID];
			_prefabPools.Remove(assetUID);
			GameObject.Destroy(poolParent);
		}
	}

}

