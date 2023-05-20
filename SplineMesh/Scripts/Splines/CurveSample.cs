using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SplineMesh {
    /// <summary>
    /// Imutable class containing all data about a point on a cubic bezier curve.
    /// </summary>
    public struct CurveSample
    {
        public Vector3 location;
        public Vector3 tangent;
        public Vector3 up;
        public Vector2 scale;
        public float distanceInCurve;
        public float timeInCurve;
        public Curve curve;

        private Quaternion rotation;

        /// <summary>
        /// Rotation is a look-at quaternion
        /// </summary>
        public Quaternion Rotation {
            get {
                if (rotation == Quaternion.identity) {
                    rotation = Quaternion.LookRotation(tangent, up);
                }
                return rotation;
            }
        }

        public CurveSample(Vector3 location, Vector3 tangent, Vector3 up, Vector2 scale, float distanceInCurve, float timeInCurve, Curve curve) {
			this.location = location;
            this.tangent = tangent;
            this.up = up;
            this.scale = scale;
            this.distanceInCurve = distanceInCurve;
            this.timeInCurve = timeInCurve;
            this.curve = curve;
            rotation = Quaternion.identity;
		}

		public void ChangeAllValues(Vector3 location, Vector3 tangent, Vector3 up, Vector2 scale, float distanceInCurve, float timeInCurve, Curve curve)
		{
			this.location = location;
            this.tangent = tangent;
            this.up = up;
            this.scale = scale;
            this.distanceInCurve = distanceInCurve;
            this.timeInCurve = timeInCurve;
            this.curve = curve;
            rotation = Quaternion.identity;
		}

        public override bool Equals(object obj) {
            if (obj == null || GetType() != obj.GetType()) {
                return false;
            }
            CurveSample other = (CurveSample)obj;
            return location == other.location &&
                tangent == other.tangent &&
                up == other.up &&
                scale == other.scale &&
                distanceInCurve == other.distanceInCurve &&
                timeInCurve == other.timeInCurve;

        }

        public override int GetHashCode() {
            return base.GetHashCode();
        }

        public static bool operator ==(CurveSample cs1, CurveSample cs2) {
            return cs1.curve==cs2.curve &&
				cs1.location == cs2.location &&
				cs1.tangent == cs2.tangent &&
				cs1.up == cs2.up &&
				cs1.scale == cs2.scale &&
				cs1.distanceInCurve == cs2.distanceInCurve &&
				cs1.timeInCurve == cs2.timeInCurve &&
				cs1.rotation == cs2.rotation;
        }

        public static bool operator !=(CurveSample cs1, CurveSample cs2) {
            return !(cs1==cs2);
        }

        /// <summary>
        /// Linearly interpolates between two curve samples.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="t"></param>
        /// <returns></returns>
        public static CurveSample Lerp(CurveSample a, CurveSample b, float t) {
            return new CurveSample(
                Vector3.Lerp(a.location, b.location, t),
                Vector3.Lerp(a.tangent, b.tangent, t).normalized,
                Vector3.Lerp(a.up, b.up, t),
                Vector2.Lerp(a.scale, b.scale, t),
                Mathf.Lerp(a.distanceInCurve, b.distanceInCurve, t),
                Mathf.Lerp(a.timeInCurve, b.timeInCurve, t),
                a.curve);
        }

        public MeshVertex GetBent(MeshVertex res) {

            // application of scale
            res.position = Vector3.Scale(res.position, new Vector3(0, scale.y, scale.x));

            // application of the rotation + location
            Quaternion q = Rotation * Quaternion.Euler(0, -90, 0);
            res.position = q * res.position + location;
            res.normal = q * res.normal;
            return res;
        }
    }
}
