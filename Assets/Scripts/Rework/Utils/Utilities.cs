using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AJ.Utilities
{
	public static class Utilities {

		public static int NextNearestPowerOf2(int n)
		{
			n = Mathf.Abs (n);
			n--;
			n |= n >> 1;   // Divide by 2^k for consecutive doublings of k up to 32,
			n |= n >> 2;   // and then or the results.
			n |= n >> 4;
			n |= n >> 8;
			n |= n >> 16;
			return ++n;   
		}

		public static float Sinerp(float start, float end, float value,int levels = 1)
		{
			if (levels < 1)
				levels = 1;

			float toReturn = value;
			for (int i = 0; i < levels; ++i) {
				toReturn = Mathf.Lerp(start, end, Mathf.Sin(toReturn * Mathf.PI * 0.5f));
			}
			return toReturn;

		}


		public static void Hann(ref double[] samples)
		{
			for (int i = 0; i < samples.Length; i++) {
				double multiplier = 0.5 * (1 - Mathf.Cos(2*Mathf.PI*i/samples.Length-1));
				samples[i] = multiplier * samples[i];
			}
		}
	}
}