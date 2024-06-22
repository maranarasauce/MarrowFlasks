using System;
using System.Collections.Generic;
using System.Reflection;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum)]
public class Elixir : Attribute
{
    public static void Log(string message)
    {
        Debug.Log(message);
    }

    public static void Log(object message)
    {
        Debug.Log(message);
    }
#if UNITY_EDITOR
    public static MonoScript[] GetSelected()
    {
        List<MonoScript> elixirs = new List<MonoScript>();
        var objs = Selection.objects;
        foreach (var obj in objs)
        {
            Type type = obj.GetType();

            if (type != typeof(MonoScript))
                continue;

            MonoScript script = obj as MonoScript;

            Elixir attribute = (Elixir)script.GetClass().GetCustomAttribute(typeof(Elixir));
            if (attribute == null)
                continue;
            elixirs.Add(script);
        }
        return elixirs.ToArray();
    }

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