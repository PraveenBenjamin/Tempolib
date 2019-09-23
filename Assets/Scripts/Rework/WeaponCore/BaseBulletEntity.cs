using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AJ.Core;
using UnityEngine.Events;

public class BaseBulletEntity : BaseAJEntity {

	[SerializeField]
	private float _bulletSpeed;
	//private readonly float _bulletSpeedInternal;
	public float BulletSpeed
	{
		get
		{
			return _bulletSpeed;
		}
	}

	public override void Reset ()
	{
		_entityFSM.SetState (GLib.EntityManagement.EntityStates.Init);
	}

	protected override void InitializeEntity ()
	{
		_entityFSM.SetState (GLib.EntityManagement.EntityStates.Init);
	}

	protected int InitInit(int status)
	{
		_entityFSM.SetState (GLib.EntityManagement.EntityStates.Existing);
		return 0;
	}

	protected override void OnEnteredOrExitedScreen (bool entered)
	{
		if(!entered)
			_entityFSM.SetState (GLib.EntityManagement.EntityStates.Init);

		//send callback down the line
		base.OnEnteredOrExitedScreen (entered);
	}

	protected void ExistingUpdate()
	{
		//Just keep swimming!
		transform.position += Vector3.forward * Time.deltaTime * BulletSpeed;
	}

}
