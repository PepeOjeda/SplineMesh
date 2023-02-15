using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;

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
        //private Dictionary<float, CurveSample> sampleCache = new Dictionary<float, CurveSample>();

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
				onSourceMeshChanged();
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

        private void Awake() {
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

		struct MeshData
		{
			public Color[] colors;
			public Vector3[] bentPositions;
			public Vector3[] bentNormals;
			public Vector2[] uv;
			public Vector2[] uv2;
		}
		MeshData meshData;

		void onSourceMeshChanged()
		{
			meshData.bentPositions = new Vector3[source.Vertices.Count];
			meshData.bentNormals = new Vector3[source.Vertices.Count];
			meshData.colors = new Color[source.Vertices.Count];
			meshData.uv = new Vector2[source.Vertices.Count];
			meshData.uv2 = new Vector2[source.Vertices.Count];
		}

        private void FillOnce() {
			// for each mesh vertex, we found its projection on the curve
			for (int i = 0; i < source.Vertices.Count; i++)
			{
				var vert = source.Vertices[i];
				float distance = vert.position.x - source.MinX;
				CurveSample sample;
				{
					if (!useSpline)
					{
						if (distance > curve.Length) distance = curve.Length;
						sample = curve.GetSampleAtDistance(distance);
					}
					else
					{
						float distOnSpline = intervalStart + distance;
						if (distOnSpline > spline.Length)
						{
							if (spline.IsLoop)
							{
								while (distOnSpline > spline.Length)
								{
									distOnSpline -= spline.Length;
								}
							}
							else
							{
								distOnSpline = spline.Length;
							}
						}
						sample = spline.GetSampleAtDistance(distOnSpline);
					}
				}

				meshData.colors[i] = (new Color(sample.location.x, sample.location.y, sample.location.z, 1));

				MeshVertex bentSample;
				if(sample.tangent != Vector3.zero)
                	bentSample=(sample.GetBent(vert));
				else
					bentSample=(vert);
					
				meshData.bentPositions[i] = bentSample.position;
				meshData.bentNormals[i] = bentSample.normal;
			}

			MeshUtility.Update(result,
                source.Mesh,
                source.Triangles,
                meshData.bentPositions,
                meshData.bentNormals,
				colors: meshData.colors);
        }

		int lastRepetitionCount = -1;
		private void FillRepeat() {
            float intervalLength = useSpline?
                (intervalEnd == 0 ? spline.Length : intervalEnd) - intervalStart :
                curve.Length;
            int repetitionCount = Mathf.FloorToInt(intervalLength / source.Length);


			// building triangles and UVs for the repeated mesh
			var triangles = new int[repetitionCount * source.Mesh.triangles.Length];

			if(repetitionCount != lastRepetitionCount)
			{
				meshData.bentPositions = new Vector3[repetitionCount * source.Mesh.vertices.Length];
				meshData.bentNormals = new Vector3[repetitionCount * source.Mesh.normals.Length];
				meshData.colors = new Color[repetitionCount * source.Mesh.vertices.Length];
				meshData.uv = new Vector2[repetitionCount * source.Mesh.uv.Length];
				meshData.uv2 = new Vector2[repetitionCount * source.Mesh.uv2.Length];
			}

			int startIndex = 0;
			for (int repetition = 0; repetition < repetitionCount; repetition++) {
				for (int i = 0; i < source.Mesh.triangles.Length; i++)
					triangles[i] = (source.Mesh.triangles[i] + source.Vertices.Count * repetition);
			
				uvsRepeatingFill(source.Mesh.uv, meshData.uv, startIndex, repetition, repetitionCount);
				uvsRepeatingFill(source.Mesh.uv2, meshData.uv2, startIndex, repetition, repetitionCount);
				startIndex += source.Mesh.triangles.Length;
			}

            // computing vertices and normals
            float offset = 0;
            for (int repetition = 0; repetition < repetitionCount; repetition++) {

                // for each mesh vertex, we found its projection on the curve
                for (int i = 0; i < source.Vertices.Count; i++)
				{
					var vert = source.Vertices[i];
                    float distance = vert.position.x - source.MinX + offset;
                    CurveSample sample;
					if (!useSpline) {
						if (distance > curve.Length) continue;
						sample = curve.GetSampleAtDistance(distance);
					} else {
						float distOnSpline = intervalStart + distance;
						while (distOnSpline > spline.Length) {
							distOnSpline -= spline.Length;
						}
						sample = spline.GetSampleAtDistance(distOnSpline);
                        
                    }
					MeshVertex bentSample;
					if(sample.tangent != Vector3.zero)
						bentSample=(sample.GetBent(vert));
					else
						bentSample=(vert);
						
					meshData.bentPositions[i] = bentSample.position;
					meshData.bentNormals[i] = bentSample.normal;
                }
                offset += source.Length;
            }

			lastRepetitionCount = repetitionCount;
			MeshUtility.Update(result,
                source.Mesh,
                triangles,
                meshData.bentPositions,
                meshData.bentNormals,
                meshData.uv,
                meshData.uv2,
				colors: meshData.colors);
        }

        private void FillStretch() {
			// for each mesh vertex, we found its projection on the curve

			Vector2[] sourceUVs = source.Mesh.uv;
			for (int i = 0; i < source.Vertices.Count; i++)
			{
				var vert = source.Vertices[i];
                float distanceRate = source.Length == 0 ? 0 : Math.Abs(vert.position.x - source.MinX) / source.Length;
                CurveSample sample;
				{
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
                }
				
				meshData.colors[i]=(new Color(sample.location.x, sample.location.y, sample.location.z, 1));

				MeshVertex bentSample;
				if(sample.tangent != Vector3.zero)
                	bentSample=(sample.GetBent(vert));
				else
					bentSample=(vert);
					
				meshData.bentPositions[i] = bentSample.position;
				meshData.bentNormals[i] = bentSample.normal;
				
				Vector2 uv = sourceUVs[i];
				if (!useSpline)
				{
					if(uvMode == UVMode.Extend)
						meshData.uv[i] = new Vector2(uv.x * curve.Length, uv.y) + new Vector2(uOffset, 0);
					else if(uvMode == UVMode.Stretch)
					{
						Vector2 newUVS = new Vector2(uv.x * curve.Length, uv.y) + new Vector2(uOffset, 0);
						newUVS.x /= spline.Length;
						meshData.uv[i] = newUVS;
					}
					else if (uvMode == UVMode.Repeat)
					{
						meshData.uv[i] = uv;
					}

				}
				else
				{
					if(uvMode == UVMode.Extend)
						meshData.uv[i] = new Vector2(uv.x * curve.Length, uv.y) + new Vector2(uOffset, 0);
					else if (uvMode == UVMode.Stretch)
					{
						meshData.uv[i] = uv;
					}
					else if (uvMode == UVMode.Repeat)
					{
						meshData.uv[i] = uv;
					}
				}
			}

		
			MeshUtility.Update(result,
                source.Mesh,
                source.Triangles,
                meshData.bentPositions,
                meshData.bentNormals,
				uv:meshData.uv,
				colors: meshData.colors
				);
            if (TryGetComponent(out MeshCollider collider)) {
                collider.sharedMesh = result;
            }
        }

		private void uvsRepeatingFill(Vector2[] inputUvs, Vector2[] outputUvs, int startIndex, int repetition, int numSegments){
			if(uvMode == UVMode.Repeat)
				outputUvs =  inputUvs;
			else if(uvMode == UVMode.Stretch)
				for (int i = 0; i < inputUvs.Length; i++)
				{
					var uv = inputUvs[i];
					outputUvs[i+ startIndex] = new Vector2(uv.x / numSegments, uv.y) + new Vector2(repetition / (float)numSegments, 0);
				}
			else //UVMode.Extend
				for (int i = 0; i < inputUvs.Length; i++)
				{
					var uv = inputUvs[i];
					outputUvs[i+ startIndex] = uv+new Vector2(repetition, 0);
				}
		}
    }
}