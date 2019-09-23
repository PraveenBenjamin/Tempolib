using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GLib.AssetManagement;
using AJ.Core;

namespace AJ.WeaponSystems
{
	public class BulletPoolManager : MonoBehaviour {

		private static BulletPoolManager _instance;
		public static BulletPoolManager Instance
		{
			get
			{
				return _instance;
			}
		}

		private Dictionary<string,int> _activeWeapons = new Dictionary<string, int>();

		private void Awake()
		{
			if (BulletPoolManager.Instance != null) {
				DestroyImmediate (this);
				return;
			}

			_instance = this;
		}

		public void RegisterBulletUID(string bulletUID)
		{
			if (_activeWeapons.ContainsKey (bulletUID)) {
				_activeWeapons [bulletUID] += 1;
			} else {
				_activeWeapons.Add (bulletUID, 1);
				PrefabInstantiator.Instance.InitializePrefabPool (bulletUID, Constants.DefaultBulletPoolSize);
			}
		}

		public void UnregisterBulletUID(string bulletUID)
		{
			if (_activeWeapons.ContainsKey (bulletUID)) {
				_activeWeapons [bulletUID] -= 1;


				if (_activeWeapons [bulletUID] == 0) {
					_activeWeapons.Remove (bulletUID);
				}
			}
		}

		private void OnEnteredOrExitedScreen(BaseAJEntity bullet, bool entered)
		{
			if (!entered) {
				bullet.UnregisterOnEnteredOrExitedScreenListener (OnEnteredOrExitedScreen);
				ReturnBulletToPool (bullet.AssetUID, bullet.gameObject);
			}
		}

		public GameObject RetrieveBulletFromPool(string bulletUID)
		{
			GameObject bullet = PrefabInstantiator.Instance.GetPrefabFromPool (bulletUID);
			BaseAJEntity aje = bullet.GetComponent<BaseAJEntity> ();
			aje.RegisterOnEnteredOrExitedScreenListener(OnEnteredOrExitedScreen);
			return bullet;
		}

		public void ReturnBulletToPool(string bulletUID,GameObject bullet)
		{
			PrefabInstantiator.Instance.ReturnPrefabToPool (bulletUID,bullet);
		}

		public void ClearResources()
		{
			foreach (KeyValuePair<string,int> pair in _activeWeapons) {
				PrefabInstantiator.Instance.DeletePrefabPool (pair.Key);
			}
			_activeWeapons.Clear ();
		}


	}
}
