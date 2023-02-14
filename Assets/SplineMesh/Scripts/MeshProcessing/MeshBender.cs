using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SplineMesh {
    /// <summary>
    /// A component that creates a deformed mesh from a given one along the given spline segment.
    /// The source mesh will always be bended along the X axis.
    /// It can work on a cubic bezier curve or on any interval of a given spline.
    /// On the given interval, the mesh can be place with original scale, stretched, or repeated.
    /// The resulting mesh is stored in a MeshFilter component and automaticaly updated on the next update if the spline segment change.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [ExecuteInEditMode]
    public class MeshBender : MonoBehaviour {
        private bool isDirty = false;
        private Mesh result;
        private bool useSpline;
        private Spline spline;
        private float intervalStart, intervalEnd;
        private Curve curve;
        private Dictionary<float, CurveSample> sampleCache = new Dictionary<float, CurveSample>();

        private SourceMesh source;
        /// <summary>
        /// The source mesh to bend.
        /// </summary>
        public SourceMesh Source {
            get { return source; }
            set {
                if (value == source) return;
                SetDirty();
                source = value;
            }
        }
        
        private FillingMode mode = FillingMode.StretchToInterval;

		private UVMode _uvMode;
		public UVMode uvMode{
			get{return _uvMode; }
			set{if(value==_uvMode)
					return;
				SetDirty();
				_uvMode = value;
			}
		}

        /// <summary>
        /// The scaling mode along the spline
        /// </summary>
        public FillingMode Mode {
            get { return mode; }
            set {
                if (value == mode) return;
                SetDirty();
                mode = value;
            }
        }

		/// <summary>
		/// Value to add to the U coordinate of each vertex. Meant for using the total length before the curve start when using curve space, should be 0 otherwise.
		/// </summary>
		public float uOffset;

		/// <summary>
		/// Sets a curve along which the mesh will be bent.
		/// The mesh will be updated if the curve changes.
		/// </summary>
		/// <param name="curve">The <see cref="CubicBezierCurve"/> to bend the source mesh along.</param>
		public void SetInterval(Spline spline, Curve curve) {
            if (this.curve == curve) return;
            if (curve == null) throw new ArgumentNullException("curve");
            if (this.curve != null) {
                this.curve.Changed.RemoveListener(SetDirty);
            }
            this.curve = curve;

			if (this.spline != null) {
                // unlistening previous spline
                this.spline.CurveChanged.RemoveListener(SetDirty);
            }
            this.spline = spline;
            spline.CurveChanged.AddListener(SetDirty);
            
            useSpline = false;
            SetDirty();
        }

        /// <summary>
        /// Sets a spline's interval along which the mesh will be bent.
        /// If interval end is absent or set to 0, the interval goes from start to spline length.
        /// The mesh will be update if any of the curve changes on the spline, including curves
        /// outside the given interval.
        /// </summary>
        /// <param name="spline">The <see cref="SplineMesh"/> to bend the source mesh along.</param>
        /// <param name="intervalStart">Distance from the spline start to place the mesh minimum X.<param>
        /// <param name="intervalEnd">Distance from the spline start to stop deforming the source mesh.</param>
        public void SetInterval(Spline spline, float intervalStart, float intervalEnd = 0) { 
            if (this.spline == spline && this.intervalStart == intervalStart && this.intervalEnd == intervalEnd && useSpline) return;
            if (spline == null) throw new ArgumentNullException("spline");
            if (intervalStart < 0 || intervalStart >= spline.Length) {
                throw new ArgumentOutOfRangeException("interval start must be 0 or greater and lesser than spline length (was " + intervalStart + ")");
            }
            if (intervalEnd != 0 && intervalEnd <= intervalStart || intervalEnd > spline.Length) {
                throw new ArgumentOutOfRangeException("interval end must be 0 or greater than interval start, and lesser than spline length (was " + intervalEnd + ")");
            }
            if (this.spline != null) {
                // unlistening previous spline
                this.spline.CurveChanged.RemoveListener(SetDirty);
            }
            this.spline = spline;
            // listening new spline
            spline.CurveChanged.AddListener(SetDirty);

            curve = null;
            this.intervalStart = intervalStart;
            this.intervalEnd = intervalEnd;
            useSpline = true;
            SetDirty();
        }

        private void OnEnable() {
            if(GetComponent<MeshFilter>().sharedMesh != null) {
                result = GetComponent<MeshFilter>().sharedMesh;
            } else {
                GetComponent<MeshFilter>().sharedMesh = result = new Mesh();
                result.name = "Generated by " + GetType().Name;
            }
        }

        private void LateUpdate() {
            ComputeIfNeeded();
        }

        public void ComputeIfNeeded() {
            if (isDirty) {
                Compute();
            }
        }

        private void SetDirty() {
            isDirty = true;
        }

        /// <summary>
        /// Bend the mesh. This method may take time and should not be called more than necessary.
        /// Consider using <see cref="ComputeIfNeeded"/> for faster result.
        /// </summary>
        private  void Compute() {
            isDirty = false;
            switch (Mode) {
                case FillingMode.Once:
                    FillOnce();
                    break;
                case FillingMode.Repeat:
                    FillRepeat();
                    break;
                case FillingMode.StretchToInterval:
                    FillStretch();
                    break;
            }
        }

        private void OnDestroy() {
            if(curve != null) {
                curve.Changed.RemoveListener(Compute);
            }
        }

        /// <summary>
        /// The mode used by <see cref="MeshBender"/> to bend meshes on the interval.
        /// </summary>
        public enum FillingMode {
            /// <summary>
            /// In this mode, source mesh will be placed on the interval by preserving mesh scale.
            /// Vertices that are beyond interval end will be placed on the interval end.
            /// </summary>
            Once,
            /// <summary>
            /// In this mode, the mesh will be repeated to fill the interval, preserving
            /// mesh scale.
            /// This filling process will stop when the remaining space is not enough to
            /// place a whole mesh, leading to an empty interval.
            /// </summary>
            Repeat,
            /// <summary>
            /// In this mode, the mesh is deformed along the X axis to fill exactly the interval.
            /// </summary>
            StretchToInterval
        }

        private void FillOnce() {
            sampleCache.Clear();
            var bentVertices = new List<MeshVertex>(source.Vertices.Count);
			var colors = new List<Color>();
            // for each mesh vertex, we found its projection on the curve
            foreach (var vert in source.Vertices) {
                float distance = vert.position.x - source.MinX;
                CurveSample sample;
                if (!sampleCache.TryGetValue(distance, out sample)) {
                    if (!useSpline) {
                        if (distance > curve.Length) distance = curve.Length;
                        sample = curve.GetSampleAtDistance(distance);
                    } else {
                        float distOnSpline = intervalStart + distance;
                        if (distOnSpline > spline.Length) {
                            if (spline.IsLoop) {
                                while (distOnSpline > spline.Length) {
                                    distOnSpline -= spline.Length;
                                }
                            } else {
                                distOnSpline = spline.Length;
                            }
                        }
                        sample = spline.GetSampleAtDistance(distOnSpline);
                    }
                    sampleCache[distance] = sample;
                }

				colors.Add(new Color(sample.location.x, sample.location.y, sample.location.z, 1));

                bentVertices.Add(sample.GetBent(vert));
            }

            MeshUtility.Update(result,
                source.Mesh,
                source.Triangles,
                bentVertices.Select(b => b.position),
                bentVertices.Select(b => b.normal),
				colors: colors);
        }

        private void FillRepeat() {
            float intervalLength = useSpline?
                (intervalEnd == 0 ? spline.Length : intervalEnd) - intervalStart :
                curve.Length;
            int repetitionCount = Mathf.FloorToInt(intervalLength / source.Length);


			// building triangles and UVs for the repeated mesh
			var triangles = new List<int>();
            var uv = new List<Vector2>();
            var uv2 = new List<Vector2>();
            var uv3 = new List<Vector2>();
            var uv4 = new List<Vector2>();
            var uv5 = new List<Vector2>();
            var uv6 = new List<Vector2>();
            var uv7 = new List<Vector2>();
            var uv8 = new List<Vector2>();

			var colors = new List<Color>();

			for (int i = 0; i < repetitionCount; i++) {
                foreach (var index in source.Triangles) {
                    triangles.Add(index + source.Vertices.Count * i);
                }
				
				//MODIFICATION FROM THE ORIGINAL! Offset the Uvs of each repetition
                uv.AddRange( uvsRepeatingFill(source.Mesh.uv, i, repetitionCount) );

                uv2.AddRange( uvsRepeatingFill(source.Mesh.uv2, i, repetitionCount) );
                uv3.AddRange( uvsRepeatingFill(source.Mesh.uv3, i, repetitionCount) );
                uv4.AddRange( uvsRepeatingFill(source.Mesh.uv4, i, repetitionCount) );
#if UNITY_2018_2_OR_NEWER
                uv5.AddRange( uvsRepeatingFill(source.Mesh.uv5, i, repetitionCount) );
                uv6.AddRange( uvsRepeatingFill(source.Mesh.uv6, i, repetitionCount) );
                uv7.AddRange( uvsRepeatingFill(source.Mesh.uv7, i, repetitionCount) );
                uv8.AddRange( uvsRepeatingFill(source.Mesh.uv8, i, repetitionCount) );
#endif
            }

            // computing vertices and normals
            var bentVertices = new List<MeshVertex>(source.Vertices.Count);
            float offset = 0;
            for (int i = 0; i < repetitionCount; i++) {

                sampleCache.Clear();
                // for each mesh vertex, we found its projection on the curve
                foreach (var vert in source.Vertices) {
                    float distance = vert.position.x - source.MinX + offset;
                    CurveSample sample;
                    if (!sampleCache.TryGetValue(distance, out sample)) {
                        if (!useSpline) {
                            if (distance > curve.Length) continue;
                            sample = curve.GetSampleAtDistance(distance);
                        } else {
                            float distOnSpline = intervalStart + distance;
                            //if (true) { //spline.isLoop) {
                                while (distOnSpline > spline.Length) {
                                    distOnSpline -= spline.Length;
                                }
                            //} else if (distOnSpline > spline.Length) {
                            //    continue;
                            //}
                            sample = spline.GetSampleAtDistance(distOnSpline);
                        }
                        sampleCache[distance] = sample;
                    }
					colors.Add(new Color(sample.location.x, sample.location.y, sample.location.z, 1));
					bentVertices.Add(sample.GetBent(vert));
                }
                offset += source.Length;
            }

            MeshUtility.Update(result,
                source.Mesh,
                triangles,
                bentVertices.Select(b => b.position),
                bentVertices.Select(b => b.normal),
                uv,
                uv2,
                uv3,
                uv4,
                uv5,
                uv6,
                uv7,
                uv8,
				colors: colors);
        }

        private void FillStretch() {
            var bentVertices = new List<MeshVertex>(source.Vertices.Count);
			var colors = new List<Color>();
            sampleCache.Clear();
            // for each mesh vertex, we found its projection on the curve
            foreach (var vert in source.Vertices) {
                float distanceRate = source.Length == 0 ? 0 : Math.Abs(vert.position.x - source.MinX) / source.Length;
                CurveSample sample;
                if (!sampleCache.TryGetValue(distanceRate, out sample)) {
					if (!useSpline) {
                        sample = curve.GetSampleAtDistance(curve.Length * distanceRate);
                    } else {
                        float intervalLength = intervalEnd == 0 ? spline.Length - intervalStart : intervalEnd - intervalStart;
                        float distOnSpline = intervalStart + intervalLength * distanceRate;
                        if(distOnSpline > spline.Length) {
                            distOnSpline = spline.Length;
                            Debug.Log("dist " + distOnSpline + " spline length " + spline.Length + " start " + intervalStart);
                        }

                        sample = spline.GetSampleAtDistance(distOnSpline);
                    }
                    sampleCache[distanceRate] = sample;
                }
				
				colors.Add(new Color(sample.location.x, sample.location.y, sample.location.z, 1));

				if(sample.tangent != Vector3.zero)
                	bentVertices.Add(sample.GetBent(vert));
				else
					bentVertices.Add(vert);
			}

			var uvList = source.Mesh.uv;
			if (!useSpline)
			{
				if(uvMode == UVMode.Extend)
					uvList = uvList.Select(uv => new Vector2(uv.x * curve.Length, uv.y) + new Vector2(uOffset, 0)).ToArray();
				else if(uvMode == UVMode.Stretch)
					uvList = uvList.Select(
						uv => {
							Vector2 newUVS = new Vector2(uv.x * curve.Length, uv.y) + new Vector2(uOffset, 0);
							newUVS.x /= spline.Length;
							return newUVS;
						}).ToArray();
				else{} //Repeat

			}
			else
			{
				if(uvMode == UVMode.Extend)
					uvList = uvList.Select(uv => new Vector2(uv.x * spline.Length, uv.y)).ToArray();
			}
		
			MeshUtility.Update(result,
                source.Mesh,
                source.Triangles,
                bentVertices.Select(b => b.position),
                bentVertices.Select(b => b.normal),
				uv:uvList,
				colors: colors
				);
            if (TryGetComponent(out MeshCollider collider)) {
                collider.sharedMesh = result;
            }
        }

		private IEnumerable<Vector2> uvsRepeatingFill(Vector2[] inputUvs, int index, int numSegments){
			if(uvMode == UVMode.Repeat)
				return inputUvs;
			else if(uvMode == UVMode.Stretch)
				return inputUvs.Select(uv => new Vector2(uv.x / numSegments, uv.y) + new Vector2(index/(float)numSegments, 0));
			else //UVMode.Extend
				return inputUvs.Select(uv => uv+new Vector2(index, 0));
		}
    }
}