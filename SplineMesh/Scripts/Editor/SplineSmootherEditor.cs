using UnityEditor;
using UnityEngine;

namespace SplineMesh
{
	[CustomEditor(typeof(SplineSmoother))]
	public class SplineSmootherEditor : Editor
	{
		SplineSmoother smoother;
		void OnEnable()
		{
			smoother = (SplineSmoother)target;
		}
		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			bool newValueAutoSmooth = GUILayout.Toggle(smoother.autoSmoothOnChange, "Auto smooth on change");
			if(newValueAutoSmooth!=smoother.autoSmoothOnChange)
				Undo.RecordObject(target, "toggle auto smooth");
			smoother.autoSmoothOnChange = newValueAutoSmooth;

			if(GUILayout.Button("Smooth now"))
				smoother.SmoothAll();
		}
	}
}