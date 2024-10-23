using Newtonsoft.Json.Linq;
using SLZ.Marrow;
using SLZ.Marrow.Warehouse;
using SLZ.Serialize;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Flask : DataCard
{
    public string[] elixirGUIDs;
    public bool useDefaultIngredients = true;
    public string[] ingredients;
    public string[] gameIngredients;
    public DataCardReference<Flask>[] palletIngredients;

#if UNITY_EDITOR
    public delegate void PackDelegate(Flask flask);
    public static PackDelegate OnPacked;

    public MonoScript[] Elixirs
    {
        get
        {
            if (_elixirCache == null || _elixirCache.Length == 0 && (elixirGUIDs != null && elixirGUIDs.Length != 0))
            {
                if (elixirGUIDs != null)
                {
                    SetCacheToNames();
                }
                else
                {
                    _elixirCache = new MonoScript[0];
                }
            }
            return _elixirCache;
        }
        set
        {
            _elixirCache = value;

            List<string> fullNames = new List<string>();
            foreach (MonoScript mscript in _elixirCache)
            {
                if (mscript == null)
                    continue;

                fullNames.Add(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(mscript)));
            }
            elixirGUIDs = fullNames.ToArray();
        }
    }

    public string GetCompiledName()
    {
        string title = MarrowSDK.SanitizeName(Title);
        title = Regex.Replace(title, @"\s+", "");
        return title;
    }

    public MonoScript[] LoadCacheFromNames()
    {
        List<MonoScript> types = new List<MonoScript>();

        foreach (string typeName in elixirGUIDs)
        {
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(typeName));
            if (script == null)
            {
                Debug.Log($"{typeName} could not be found");
            }
            types.Add(script);
        }
        return types.ToArray();
    }

    public void SetCacheToNames()
    {
        MonoScript[] scripts = LoadCacheFromNames();
        if (scripts != null)
        {
            _elixirCache = scripts;
        }
    }

    private MonoScript[] _elixirCache;
    public override void Pack(ObjectStore store, JObject json)
    {
        OnPacked?.Invoke(this);
        base.Pack(store, json);
    }

    public static void CreateFlaskInfo()
    {
        string flaskTitle = SceneManager.GetActiveScene().name;
        Flask asset = ScriptableObject.CreateInstance<Flask>();
        asset.useDefaultIngredients = true;
        asset.Elixirs = Elixir.GetAllElixirsFromScene();
        string guid = GUID.Generate().ToString();
        asset.Title = $"Flask {guid}";
        asset.Barcode = new Barcode($"Flask.{guid}");
        AssetDatabase.CreateAsset(asset, $"Assets/Flask {guid}.asset");
        AssetDatabase.SaveAssets();

        EditorUtility.FocusProjectWindow();

        Selection.activeObject = asset;
    }
#endif
}
