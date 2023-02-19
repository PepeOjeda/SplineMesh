using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MaterialInstancePool : MonoBehaviour
{
	public static MaterialInstancePool instance{ get; private set; }
	Dictionary<int, List<MaterialInstance>> materialInstancesByID = new Dictionary<int, List<MaterialInstance>>();


	void Awake()
	{
		if(instance != null && instance!=this)
		{
			Destroy(instance);
		}
		instance = this;
	}

	public MaterialInstance getMaterialInstance(int idOfOriginal)
	{
		if(materialInstancesByID.ContainsKey(idOfOriginal))
		{
			List<MaterialInstance> mats = materialInstancesByID[idOfOriginal];
			for (int i = 0; i < mats.Count; i++)
			{
				if(mats[i].isAvailable)
				{
					mats[i].isAvailable = false;
					return mats[i];
				}
			}

			var newMat = new MaterialInstance
			{
				isAvailable = false,
				originalID = idOfOriginal,
				instance = new Material((Material)Resources.InstanceIDToObject(idOfOriginal))
			};
			mats.Add(newMat);
			return newMat;
		}
		else
		{
			var newMat = new MaterialInstance
			{
				isAvailable = false,
				originalID = idOfOriginal,
				instance = new Material((Material)Resources.InstanceIDToObject(idOfOriginal))
			};
			materialInstancesByID.Add(idOfOriginal, new List<MaterialInstance> {newMat});
			return newMat;
		}
	}

	void OnDestroy()
	{
		foreach(var kv in materialInstancesByID)
		{
			foreach(var mat in kv.Value)
			{
				Destroy(mat.instance);
			}
		}
	}
}

public class MaterialInstance
{
	public bool isAvailable;
	public int originalID;
	public Material instance;
}