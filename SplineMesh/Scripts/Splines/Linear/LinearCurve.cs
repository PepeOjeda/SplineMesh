using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace SplineMesh
{
	/// <summary>
	/// Name's kind of absurd, but you get the idea
	/// </summary>
	[Serializable]
	public class LinearCurve : Curve
	{

		/// <summary>
		/// This event is raised when of of the control points has moved.
		/// </summary>
		public override UnityEvent Changed { get => _Changed; set { _Changed = value; } }
		UnityEvent _Changed = new UnityEvent();

		public LinearCurve(SplineNode n1, SplineNode n2)
		{
			this.n1 = n1;
            this.n2 = n2;
			n1.Changed += (_,_) =>onChanged();
			n2.Changed += (_,_) =>onChanged();
			onChanged();
		}

		private void onChanged()
		{
			Length = Vector3.Distance(n1.Position, n2.Position);
			Changed?.Invoke();
		}

		public override void ConnectEnd(SplineNode n2)
		{
			this.n2 = n2;
		}

		public override CurveSample GetProjectionSample(Vector3 pointToProject)
		{
			Vector3 curveVec = (n2.Position - n1.Position).normalized;
			Vector3 vecToProject = pointToProject - n1.Position;
			float distance = Vector3.Dot(curveVec, vecToProject);
			return GetSampleAtDistance(distance);
		}

		public override CurveSample GetSample(float time)
		{
			return new CurveSample(Vector3.Lerp(n1.Position, n2.Position, time),
					(n2.Position - n1.Position).normalized,
					Vector3.Lerp(n1.Up, n2.Up, time),
					Vector2.Lerp(n1.Scale, n2.Scale, time),
					(n1.Position - n2.Position).magnitude,
					time,
					this
					);
		}

		public override CurveSample GetSampleAtDistance(float d)
		{
            if (d < 0 || d > Length)
                throw new ArgumentException("Distance must be positive and less than curve length. Length = " + Length + ", given distance was " + d);
			return GetSample(d / Length);
		}
	}
}