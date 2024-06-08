using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class Elixir : Attribute
{
#if UNITY_EDITOR
    public static MonoScript[] GetAllElixirsFromScene()
    {
        List<MonoScript> elixirs = new List<MonoScript>();
        MonoBehaviour[] mbs = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
        for (int i = 0; i < mbs.Length; i++)
        {
            MonoBehaviour mb = mbs[i];
            Type type = mb.GetType();

            EditorUtility.DisplayProgressBar("Alchemy", $"Checking {type.Name}...", i / mbs.Length);
            Elixir attribute = (Elixir)type.GetCustomAttribute(typeof(Elixir));
            if (attribute == null)
                continue;
            else elixirs.Add(MonoScript.FromMonoBehaviour(mb));
        }
        EditorUtility.ClearProgressBar();
        return elixirs.ToArray();
    }
#endif
}

[AttributeUsage(AttributeTargets.Class)]
public class DontAssignIntPtr : Attribute
{
    
}