using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GLib.EntityManagement;
using GLib.AssetManagement;

namespace AJ.Core
{

	public delegate void OnEnterOrExitScreen(BaseAJEntity target,bool entered);

	public abstract class BaseAJEntity : BaseEntity {

		//TODO, setup a verification system that checks everything that is required for the game to function smoothly
		[SerializeField]
		//HACK!! REMOVE THE PUBLIC MODIFIER ASAP!!
		public string _assetUID;
		//private readonly string _assetUIDInternal;
		public string AssetUID
		{
			get
			{
				return _assetUID;
			}
		}

		[SerializeField]
		private Transform _destructableBodyParent;
		private Vector3 _destructableBodyCenter;
		private List<Vector3> _childOffsets;

		[SerializeField]
		private float _normalizedExpansionRadius = 1.5f;

		[SerializeField]
		private Bounds _unScaledOrRotatedBounds;
		public Bounds UnScaledOrRotatedBounds
		{
			get
			{
				return _unScaledOrRotatedBounds;
			}
		}

		[SerializeField]
		private Bounds _bounds;
		public Bounds Bounds
		{
			get
			{
				return _bounds;
			}
		}

		//TODO :- optimize the rest of the code to use this
		// especially during spawning and destroying
		[SerializeField]
		private bool _updateBounds = true;

		private Vector3 _prevScale;
		private Quaternion _prevRot;
		private Vector3 _prevPos;
		private Vector3 _prevPosInViewport;

		//private OnEnterOrExitScreen _onEnterOrExitScreenInternal;
		private OnEnterOrExitScreen _onEnteredOrExitedScreenCallback;
		public void RegisterOnEnteredOrExitedScreenListener(OnEnterOrExitScreen cb)
		{
			_onEnteredOrExitedScreenCallback += cb;
		}
		public void UnregisterOnEnteredOrExitedScreenListener(OnEnterOrExitScreen cb)
		{
			_onEnteredOrExitedScreenCallback -= cb;
		}

		protected override void Awake()
		{
			base.Awake ();
			if (_normalizedExpansionRadius < 1.25f)
				_normalizedExpansionRadius = 1.25f;

			if (_destructableBodyParent == null) {
				Debug.LogWarning ("DestructableObjectParent not set. This is not allowed and may cause unexpected behaviour.");
			}

			_bounds = new Bounds ();
			_unScaledOrRotatedBounds = new Bounds ();

			_prevScale = transform.localScale;
			_prevRot = transform.rotation;
			_prevPos = transform.position;
			_prevPosInViewport = Camera.main.WorldToViewportPoint (transform.position);

			updateBounds (ref _unScaledOrRotatedBounds,false);
			updateBounds (ref _bounds);

			recordChildOffsets ();

			_destructableBodyCenter = _bounds.center;
		
		}

		protected override void OnUpdateInternal ()
		{
			//update bounds if required
			if (_updateBounds
				&& (!_prevScale.Equals (transform.localScale)
					|| !_prevRot.Equals (transform.rotation))) 
			{
				updateBounds (ref _bounds);
			}

			if (!_prevPos.Equals (transform.position)) {
				Vector3 offset = transform.position - _prevPos;
				_bounds.center += offset;
				_unScaledOrRotatedBounds.center += offset;
				_destructableBodyCenter += offset;
			}


			//check if object is inside or outside screen then fire event
			Vector3 posInViewport = Camera.main.WorldToViewportPoint(transform.position);
			if (_prevPosInViewport.x > 1 || _prevPosInViewport.y > 1 &&
			   (posInViewport.x > 0 && posInViewport.x < 1
			   && posInViewport.y > 0 && posInViewport.y < 1)) {
				OnEnteredOrExitedScreen (true);
				//_onEnterOrExitScreenInternal.Invoke (this, true);
			}
			else if (posInViewport.x > 1 || posInViewport.y > 1 || posInViewport.x < 0 || posInViewport.y < 0) {
				OnEnteredOrExitedScreen (false);
				//_onEnterOrExitScreenInternal.Invoke (this, false);
			}
			_prevPosInViewport = posInViewport;

			_prevPos = transform.position;
			_prevScale = transform.localScale;
			_prevRot = transform.rotation;
		}

		protected virtual void OnEnteredOrExitedScreen(bool entered)
		{
			if (_onEnteredOrExitedScreenCallback != null)
				_onEnteredOrExitedScreenCallback.Invoke (this, entered);
		}

		private void recordChildOffsets()
		{
			if (_destructableBodyParent == null)
				return;
			
			Vector3 scale = _destructableBodyParent.localScale;

			//reset scale
			_destructableBodyParent.localScale = Vector3.one;

			//faster than resizing, which will require multiple calls to add or remove
			//but will create more garbage..
			//TODO :- see if this matters.
			if (_childOffsets == null || _childOffsets.Count != _destructableBodyParent.childCount) {
				_childOffsets = new List<Vector3> (_destructableBodyParent.childCount);
				for (int i = 0; i < _destructableBodyParent.childCount; ++i) {
					_childOffsets.Add (Vector3.zero);
				}
			}
			Transform child = null;
			for (int i = 0; i < _destructableBodyParent.childCount; ++i) {
				child = _destructableBodyParent.GetChild (i);
				_childOffsets [i] = child.transform.position - _destructableBodyCenter;
			}


			_destructableBodyParent.localScale = scale;
		}

		private void updateBounds(ref Bounds boundsToUpdate, bool transformed = true)
		{
			Transform child = null;
			boundsToUpdate.center = Vector3.zero;
			boundsToUpdate.size = Vector3.zero;
			if (_destructableBodyParent != null) {
				for (int i = 0; i < _destructableBodyParent.childCount; ++i) {
					child = _destructableBodyParent.GetChild (i);
					if (transformed)
						boundsToUpdate.Encapsulate (child.GetComponent<MeshFilter> ().sharedMesh.bounds);
					else
						boundsToUpdate.Encapsulate (child.GetComponent<MeshRenderer> ().bounds);
				}
			}
		}

		protected virtual void DisplaceBodyFromCenterAnim(bool explode,float normalizedTime){
			if (_destructableBodyParent == null) {
				return;
			}
			Transform child = null;
			Vector3 newChildPos = Vector3.zero;
			for (int i = 0; i < _destructableBodyParent.childCount; ++i) {
				child = _destructableBodyParent.GetChild (i);
				Vector3 dir = child.transform.position - _destructableBodyCenter;
				dir = dir.normalized;
				dir = Vector3.Scale(dir,transform.localScale);
				float normTimeActual = explode ? normalizedTime : 1 - normalizedTime;
				float lerpVal = FrequencyVisualizer.Sinerp (1, _normalizedExpansionRadius, normTimeActual);
				newChildPos = _childOffsets[i] + (dir * lerpVal * normTimeActual);
				child.transform.position = newChildPos;
			}

		}

		private void OnDrawGizmos()
		{
			Color col = Gizmos.color;

			Gizmos.color = Color.green;
			Gizmos.DrawCube (_unScaledOrRotatedBounds.center, _unScaledOrRotatedBounds.size);

			Gizmos.color = Color.yellow;

			Gizmos.DrawCube (_bounds.center, _bounds.size);

			Gizmos.color = col;

		}

	}

}