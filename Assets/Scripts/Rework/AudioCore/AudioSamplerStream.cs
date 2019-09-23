using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ALib.Core
{

	//NOT YET IMPLEMENTED. JUST A STUB FOR NOW
	public class AudioSamplerStream : AudioSampler {

		public override AudioSamplerType SamplerType {
			get {
				return AudioSamplerType.Stream;
			}
		}

		public override int ClipFrequency {
			get {
				return base.ClipFrequency;
			}
		}

		public override int FFTBinCount {
			get {
				return base.FFTBinCount;
			}
		}

		public override int SampleCount {
			get {
				return base.SampleCount;
			}
		}

		public override bool IsInitialized {
			get {
				return base.IsInitialized;
			}
		}

		protected override void ClearResources ()
		{
			base.ClearResources ();
		}

		public override bool Initialize (params object[] requiredParameters)
		{
			return base.Initialize (requiredParameters);
		}

		protected override float[] GetSamplesInternal (int offsetSamples = 0)
		{
			throw new System.NotImplementedException ();
		}

		protected override void Pause ()
		{
			throw new System.NotImplementedException ();
		}

		protected override void Play ()
		{
			throw new System.NotImplementedException ();
		}

		protected override void Stop ()
		{
			throw new System.NotImplementedException ();
		}
	}
}