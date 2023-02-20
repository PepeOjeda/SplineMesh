using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MaterialInstancePool
{
	private static MaterialInstancePool _instance;
	public static MaterialInstancePool instance{ 
		get{
			if(_instance == null)
				_instance = new MaterialInstancePool();
			return _instance;
		}
	}
	Dictionary<int, List<MaterialInstance>> materialInstancesByID = new Dictionary<int, List<MaterialInstance>>();

	[RuntimeInitializeOnLoadMethod]
	static void Initialize()
	{
		_instance = new MaterialInstancePool();
	}
	
	private MaterialInstancePool()
	{
		if(_instance != null && _instance!=this)
		{
			_instance.CleanUp();
		}
		_instance = this;
		Application.quitting += CleanUp;
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

	void CleanUp()
	{
		foreach (var kv in materialInstancesByID)
		{
			foreach (var mat in kv.Value)
			{
				UnityEngine.Object.Destroy(mat.instance);
			}
		}
		Application.quitting -= CleanUp;
	}
}

public class MaterialInstance
{
	public bool isAvailable;
	public int originalID;
	public Material instance;
}