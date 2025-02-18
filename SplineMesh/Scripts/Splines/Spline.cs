﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.Events;

namespace SplineMesh {
    /// <summary>
    /// A curved line made of oriented nodes.
    /// Each segment is a cubic Bézier curve connected to spline nodes.
    /// It provides methods to get positions and tangent along the spline, specifying a distance or a ratio, plus the curve length.
    /// The spline and the nodes raise events each time something is changed.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteInEditMode]
    public abstract class Spline : MonoBehaviour{
        /// <summary>
        /// The spline nodes.
        /// Warning, this collection shouldn't be changed manualy. Use specific methods to add and remove nodes.
        /// It is public only for the user to enter exact values of position and direction in the inspector (and serialization purposes).
        /// </summary>
        public List<SplineNode> nodes = new List<SplineNode>(20);

        /// <summary>
        /// The generated curves. Should not be changed in any way, use nodes instead.
        /// </summary>
        [HideInInspector]
        public List<Curve> curves = new List<Curve>(20);

        /// <summary>
        /// The spline length in world units.
        /// </summary>
        public float Length;

        [SerializeField]
        protected bool isLoop;

        public bool IsLoop {
            get { return isLoop; }
            set {
                isLoop = value;
                updateLoopBinding();
            }
        }


        /// <summary>
        /// Event raised when the node collection changes
        /// </summary>
        public event ListChangeHandler<SplineNode> NodeListChanged;

        /// <summary>
        /// Event raised when one of the curve changes.
        /// </summary>
        [HideInInspector]
        public UnityEvent CurveChanged = new UnityEvent();

        /// <summary>
        /// Clear the nodes and curves, then add two default nodes for the reset spline to be visible in editor.
        /// </summary>
        public void Reset(bool raiseEvent) {
            nodes.Clear();
            curves.Clear();
            AddNode(new SplineNode(new Vector3(5, 0, 0), new Vector3(5, 0, -3)));
            AddNode(new SplineNode(new Vector3(10, 0, 0), new Vector3(10, 0, 3)));
			if(!raiseEvent)
				return;

            RaiseNodeListChanged(new ListChangedEventArgs<SplineNode>() {
                type = ListChangeType.clear
            });
            UpdateAfterCurveChanged();
        }

        protected void OnEnable() {
            RefreshCurves();
        }

        public ReadOnlyCollection<Curve> GetCurves() {
            return curves.AsReadOnly();
        }

        protected void RaiseNodeListChanged(ListChangedEventArgs<SplineNode> args) {
            if (NodeListChanged != null)
                NodeListChanged.Invoke(this, args);
        }

        protected void UpdateAfterCurveChanged() {
            Length = 0;
            foreach (var curve in curves) {
                Length += curve.Length;
            }
            CurveChanged.Invoke();
        }

        /// <summary>
        /// Returns an interpolated sample of the spline, containing all curve data at this time.
        /// Time must be between 0 and the number of nodes.
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public CurveSample GetSample(float t) {
            int index = GetNodeIndexForTime(t);
            return curves[index].GetSample(t - index);
        }

        /// <summary>
        /// Returns the curve at the given time.
        /// Time must be between 0 and the number of nodes.
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public Curve GetCurve(float t) {
            return curves[GetNodeIndexForTime(t)];
        }

        protected int GetNodeIndexForTime(float t) {
            if (t < 0 || t > nodes.Count - 1) {
                throw new ArgumentException(string.Format("Time must be between 0 and last node index ({0}). Given time was {1}.", nodes.Count - 1, t));
            }
            int res = Mathf.FloorToInt(t);
            if (res == nodes.Count - 1)
                res--;
            return res;
        }

		/// <summary>
		/// Refreshes the spline's internal list of curves.
		// </summary>
		public abstract void RefreshCurves();

		/// <summary>
		/// Returns an interpolated sample of the spline, containing all curve data at this distance.
		/// Distance must be between 0 and the spline length.
		/// </summary>
		/// <param name="d"></param>
		/// <returns></returns>
		public CurveSample GetSampleAtDistance(float d) {
            if (d < 0 || d > Length)
                throw new ArgumentException(string.Format("Distance must be between 0 and spline length ({0}). Given distance was {1}.", Length, d));
            foreach (Curve curve in curves) {
                // test if distance is approximatly equals to curve length, because spline
                // length may be greater than cumulated curve length due to float precision
                if(d > curve.Length && d < curve.Length + 0.0001f) {
                    d = curve.Length;
                }
                if (d > curve.Length) {
                    d -= curve.Length;
                } else {
                    return curve.GetSampleAtDistance(d);
                }
            }
            throw new Exception("Something went wrong with GetSampleAtDistance.");
        }

		/// <summary>
		/// Adds a node at the end of the spline.
		/// </summary>
		/// <param name="node"></param>
		public abstract void AddNode(SplineNode node);
		/// <summary>
		/// Insert the given node in the spline at index. Index must be greater than 0 and less than node count.
		/// </summary>
		/// <param name="index"></param>
		/// <param name="node"></param>
		public abstract void InsertNode(int index, SplineNode node);

		/// <summary>
		/// Remove the given node from the spline. The given node must exist and the spline must have more than 2 nodes.
		/// </summary>
		/// <param name="node"></param>
		public void RemoveNode(SplineNode node) {
            int index = nodes.IndexOf(node);

            if (nodes.Count <= 2) {
                throw new Exception("Can't remove the node because a spline needs at least 2 nodes.");
            }

            Curve toRemove = index == nodes.Count - 1 ? curves[index - 1] : curves[index];
            if (index != 0 && index != nodes.Count - 1) {
                SplineNode nextNode = nodes[index + 1];
                curves[index - 1].ConnectEnd(nextNode);
            }

            nodes.RemoveAt(index);
            toRemove.Changed.RemoveListener(UpdateAfterCurveChanged);
            curves.Remove(toRemove);

            RaiseNodeListChanged(new ListChangedEventArgs<SplineNode>() {
                type = ListChangeType.Remove,
                removedItem =  node ,
                removeIndex = index
            });
            UpdateAfterCurveChanged();
            updateLoopBinding();
        }

        SplineNode start, end;
        protected void updateLoopBinding() {
            if(start != null) {
                start.Changed -= StartNodeChanged;
            }
            if(end != null) {
                end.Changed -= EndNodeChanged;
            }
            if (isLoop) {
                start = nodes[0];
                end = nodes[nodes.Count - 1];
                start.Changed += StartNodeChanged;
                end.Changed += EndNodeChanged;
                StartNodeChanged(null, null);
            } else {
                start = null;
                end = null;
            }
        }

        protected void StartNodeChanged(object sender, EventArgs e) {
            end.Changed -= EndNodeChanged;
            end.Position = start.Position;
            end.Direction = start.Direction;
            end.Scale = start.Scale;
            end.Up = start.Up;
            end.Changed += EndNodeChanged;
        }

        protected void EndNodeChanged(object sender, EventArgs e) {
            start.Changed -= StartNodeChanged;
            start.Position = end.Position;
            start.Direction = end.Direction;
            start.Scale = end.Scale;
            start.Up = end.Up;
            start.Changed += StartNodeChanged;
        }

        public CurveSample GetProjectionSample(Vector3 pointToProject) {
            CurveSample closest = default(CurveSample);
            float minSqrDistance = float.MaxValue;
            foreach (var curve in curves) {
                var projection = curve.GetProjectionSample(pointToProject);
                if (curve == curves[0]) {
                    closest = projection;
                    minSqrDistance = (projection.location - pointToProject).sqrMagnitude;
                    continue;
                }
                var sqrDist = (projection.location - pointToProject).sqrMagnitude;
                if (sqrDist < minSqrDistance) {
                    minSqrDistance = sqrDist;
                    closest = projection;
                }
            }
            return closest;
        }
    }

    public enum ListChangeType {
        Add,
        Insert,
        Remove,
        clear,
    }
    public class ListChangedEventArgs<T> : EventArgs {
        public ListChangeType type;
        public T newItem;
        public T removedItem;
        public int insertIndex, removeIndex;
    }
    public delegate void ListChangeHandler<T2>(object sender, ListChangedEventArgs<T2> args);

}
