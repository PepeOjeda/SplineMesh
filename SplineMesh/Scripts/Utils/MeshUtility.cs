﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SplineMesh {
    public class MeshUtility {

        /// <summary>
        /// Returns a mesh with reserved triangles to turn back the face culling.
        /// This is usefull when a mesh needs to have a negative scale.
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        public static int[] GetReversedTriangles(Mesh mesh) {
            var res = mesh.triangles.ToArray();
            var triangleCount = res.Length / 3;
            for (var i = 0; i < triangleCount; i++) {
                var tmp = res[i * 3];
                res[i * 3] = res[i * 3 + 1];
                res[i * 3 + 1] = tmp;
            }
            return res;
        }

        /// <summary>
        /// Returns a mesh similar to the given source plus given optionnal parameters.
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="source"></param>
        /// <param name="triangles"></param>
        /// <param name="vertices"></param>
        /// <param name="normals"></param>
        /// <param name="uv"></param>
        /// <param name="uv2"></param>
        /// <param name="uv3"></param>
        /// <param name="uv4"></param>
        /// <param name="uv5"></param>
        /// <param name="uv6"></param>
        /// <param name="uv7"></param>
        /// <param name="uv8"></param>
        public static void Update(Mesh mesh,
            Mesh source,
            int[] triangles = null,
            Vector3[] vertices = null,
            Vector3[] normals = null,
            Vector2[] uv = null,
            Vector2[] uv2 = null,
			Color[] colors = null) {
            mesh.hideFlags = source.hideFlags;
#if UNITY_2017_3_OR_NEWER
            mesh.indexFormat = source.indexFormat;
#endif

            mesh.vertices = vertices == null ? source.vertices : vertices;
            mesh.normals = normals == null ? source.normals : normals;
            mesh.uv = uv == null? source.uv : uv;
            mesh.uv2 = uv2 == null ? source.uv2 : uv2;
			mesh.colors = colors == null ? source.colors : colors;
			mesh.triangles = triangles == null ? source.triangles : triangles;
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
        }
    }
}
