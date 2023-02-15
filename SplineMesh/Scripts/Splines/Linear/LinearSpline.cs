using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

namespace SplineMesh
{

	public class LinearSpline : Spline
	{
		public override void AddNode(SplineNode node)
		{
			nodes.Add(node);
			if (nodes.Count != 1) {
				SplineNode previousNode = nodes[nodes.IndexOf(node) - 1];
				LinearCurve curve = new LinearCurve(previousNode, node);
				curve.Changed.AddListener(UpdateAfterCurveChanged);
				curves.Add(curve);
			}
			RaiseNodeListChanged(new ListChangedEventArgs<SplineNode>() {
				type = ListChangeType.Add,
				newItem = node 
			});

			UpdateAfterCurveChanged();
			updateLoopBinding();
		}

		public override void InsertNode(int index, SplineNode node)
		{
			if (index == 0)
			throw new Exception("Can't insert a node at index 0");

			SplineNode previousNode = nodes[index - 1];
			SplineNode nextNode = nodes[index];

			nodes.Insert(index, node);

			curves[index - 1].ConnectEnd(node);

			LinearCurve curve = new LinearCurve(node, nextNode);
			curve.Changed.AddListener(UpdateAfterCurveChanged);
			curves.Insert(index, curve);
			RaiseNodeListChanged(new ListChangedEventArgs<SplineNode>() {
				type = ListChangeType.Insert,
				newItem = node ,
				insertIndex = index
			});
			UpdateAfterCurveChanged();
			updateLoopBinding();
		}

		public override void RefreshCurves()
		{
			curves.Clear();
			for (int i = 0; i < nodes.Count - 1; i++) {
				SplineNode n = nodes[i];
				SplineNode next = nodes[i + 1];

				LinearCurve curve = new LinearCurve(n, next);
				curve.Changed.AddListener(UpdateAfterCurveChanged);
				curves.Add(curve);
			}
			RaiseNodeListChanged(new ListChangedEventArgs<SplineNode>() {
				type = ListChangeType.clear
			});
			UpdateAfterCurveChanged();
		}
	}
}