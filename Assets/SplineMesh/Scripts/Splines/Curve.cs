using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System;

namespace SplineMesh
{
	public abstract class Curve
	{
		public SplineNode n1, n2;

		/// <summary>
		/// Length of the curve in world unit.
		/// </summary>	
		public float Length { get; protected set; }
        public abstract UnityEvent Changed { get; set; }
		public abstract CurveSample GetSample(float time);
		public abstract CurveSample GetSampleAtDistance(float d);
		public abstract CurveSample GetProjectionSample(Vector3 pointToProject);
		public abstract void ConnectEnd(SplineNode n2);

		protected void AssertTimeInBounds(float time) {
            if (time < 0 || time > 1) throw new ArgumentException("Time must be between 0 and 1 (was " + time + ").");
        }
	}

}
