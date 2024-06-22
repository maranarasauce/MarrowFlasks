using Maranara.Marrow;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

[CustomEditor(typeof(Flask))]
public class FlaskEditor : Editor
{
    Flask info;

    void OnEnable()
    {
        info = target as Flask;
        toAdd = new List<MonoScript>();
        ingredientsProperty = serializedObject.FindProperty("ingredients");
        ingredientsPlusProperty = serializedObject.FindProperty("additionalIngredients");
    }

    // The currently selected Elixir in the inspector
    private MonoScript selectedElixir;
    // List that compiles all Elixirs in a given OnInspectorGUI run, to be added to the elixir list
    private List<MonoScript> toAdd;
    // Same as above but for removing an elixir
    private MonoScript toRemove;
    // Foldout bools
    private bool notAnElixir, ingredientsInfoFoldout, ingredientsFoldout, elixirInfoFoldout;
    private bool elixirListFoldout = true;
    private bool debugFoldout = false;

    private SerializedProperty ingredientsProperty;
    private SerializedProperty ingredientsPlusProperty;
    public override void OnInspectorGUI()
    {
        
        serializedObject.Update();

        GUIStyle style = EditorStyles.foldout;
        style.fontStyle = FontStyle.Bold;

        #region ElixirSelector

        if (info == null)
        {
            EditorGUILayout.LabelField("Info is null");
            return;
        }
        if (info.Elixirs == null)
        {
            EditorGUILayout.LabelField("Elixir is null");
            return;
        }

        EditorGUILayout.BeginHorizontal();
        elixirListFoldout = EditorGUILayout.Foldout(elixirListFoldout, "Elixirs", true, style);

        if (GUILayout.Button("?"))
            elixirInfoFoldout = !elixirInfoFoldout;
        EditorGUILayout.EndHorizontal();

        if (elixirInfoFoldout)
        {
            EditorStyles.label.wordWrap = true;
            EditorGUILayout.LabelField("Elixirs are Mono Scripts that will be compiled into your Flask. This means MonoBehaviours and ScriptableObjects are supported, with nested types such as structs being \"supported.\" Supported in the fact that they will compile as a part of their parent Elixir, but will not show up in any Elixir list.");
        }
        EditorGUILayout.Space(5);

        toRemove = null;

        if (elixirListFoldout)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Add an Elixir");

            MonoScript newElixir = (MonoScript)EditorGUILayout.ObjectField(selectedElixir, typeof(MonoScript), true);
            if (selectedElixir != newElixir)
            {
                selectedElixir = newElixir;
                if (selectedElixir != null)
                {
                    Type elixirType = selectedElixir.GetClass();

                    Elixir attribute = (Elixir)elixirType.GetCustomAttribute(typeof(Elixir));
                    if (attribute == null)
                        notAnElixir = true;
                    else
                        notAnElixir = false;
                }
                else
                    notAnElixir = false;
            }

            if (notAnElixir)
                EditorGUILayout.LabelField("THIS IS NOT AN ELIXIR!");
            else if (selectedElixir != null && GUILayout.Button("Add"))
            {
                toAdd.Add(selectedElixir);
                selectedElixir = null;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Add Selected"))
            {
                toAdd.AddRange(Elixir.GetSelected());
                selectedElixir = null;
            }

            if (GUILayout.Button("Add All from Current Scene"))
            {
                toAdd.AddRange(Elixir.GetAllElixirsFromScene());
                selectedElixir = null;
            }

            EditorGUILayout.EndHorizontal();

            style = EditorStyles.boldLabel;
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField($"Elixir List ({info.Elixirs.Length})", style);

            for (int i = 0; i < info.Elixirs.Length; i++)
            {
                MonoScript type = info.Elixirs[i];
                if (type == null)
                    continue;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(AssetDatabase.GUIDToAssetPath(info.elixirGUIDs[i]));
                if (GUILayout.Button("X"))
                {
                    toRemove = type;
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        if (toRemove != null || toAdd.Count != 0)
        {
            List<MonoScript> types = new List<MonoScript>();
            types.AddRange(info.Elixirs);
            if (toRemove != null)
                types.Remove(toRemove);

            if (toAdd.Count != 0)
            {
                foreach (MonoScript type in toAdd)
                {
                    if (!types.Contains(type))
                        types.Add(type);
                }
                toAdd.Clear();
            }

            info.Elixirs = types.ToArray();

            EditorUtility.SetDirty(info);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        #endregion

        GUILayout.Space(20);

        #region ReferenceSelector
        style = EditorStyles.foldout;
        style.fontStyle = FontStyle.Bold;

        EditorGUILayout.BeginHorizontal();
        ingredientsFoldout = EditorGUILayout.Foldout(ingredientsFoldout, "Ingredients", true, style);

        if (GUILayout.Button("?"))
            ingredientsInfoFoldout = !ingredientsInfoFoldout;
        EditorGUILayout.EndHorizontal();

        if (ingredientsInfoFoldout)
        {
            EditorStyles.label.wordWrap = true;
            EditorGUILayout.LabelField("Ingredients are references to other assemblies that your Flask will use. It is recommended you keep these set to default, as the compiler will omit any references to Ingredients not used in the Flask. The Ingredients are relative to the MelonLoader/Managed folder, so if you want to reference something such as another Flask, you'll need to enter the path relative to the MelonLoader/Managed folder. While you can enter the full path, it is recommended you keep the path relative in the event that either your MelonLoader directory moves, or you're working with other people who have their directory in a different location.");
        }
        GUILayout.Space(10);

        if (ingredientsFoldout)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Use Default Ingredients");
            bool defaultIngredients = EditorGUILayout.Toggle(info.useDefaultIngredients);
            EditorGUILayout.EndHorizontal();

            if (defaultIngredients != info.useDefaultIngredients)
            {
                info.useDefaultIngredients = defaultIngredients;
                if (defaultIngredients)
                {
                    if (info.ingredients == null)
                    {
                        info.ingredients = ElixirMixer.GetDefaultReferences(false);
                        EditorUtility.SetDirty(info);
                    }
                }
            }
            if (!defaultIngredients)
            {
                GUIContent content = new GUIContent()
                {
                    text = "Base Ingredients"
                };
                EditorGUILayout.PropertyField(ingredientsProperty, content);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Set Base Ingredients to Default"))
                {
                    info.ingredients = ElixirMixer.GetDefaultReferences(false);
                    EditorUtility.SetDirty(info);
                }
                if (GUILayout.Button("Clear Base Ingredients"))
                {
                    info.ingredients = new string[0];
                    EditorUtility.SetDirty(info);
                }
                if (GUILayout.Button("Select Base Ingredient"))
                {
                    SelectIngredient(ref info.ingredients);
                    EditorUtility.SetDirty(info);
                }
                EditorGUILayout.EndHorizontal();


            }
            if (defaultIngredients)
            {
                GUILayout.Space(10);
                EditorGUILayout.PropertyField(ingredientsPlusProperty);
                if (GUILayout.Button("Select Additional Ingredient"))
                {
                    SelectIngredient(ref info.additionalIngredients);
                    EditorUtility.SetDirty(info);
                }
            }
        }

        #endregion

        GUILayout.Space(20);

        #region Debugger
        style = EditorStyles.foldout;
        style.fontStyle = FontStyle.Bold;

        EditorGUILayout.BeginHorizontal();
        debugFoldout = EditorGUILayout.Foldout(debugFoldout, "Debugging", true, style);
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        if (debugFoldout)
        {
            if (GUILayout.Button("Taste Test Flask"))
            {
                Flask flask = (Flask)target;

                string title = "TasteTest";
                string buildPath = Application.temporaryCachePath;

                UnityEvent<bool> BuildEvent = new UnityEvent<bool>();
                BuildEvent.AddListener((hasErrors) =>
                {
                    ElixirMixer.TreatExportedElixir(Path.Combine(buildPath, title + ".dll"));
                });
                BuildEvent.AddListener(OnBuildComplete);

                ElixirMixer.ExportElixirs("TasteTest", buildPath, flask, BuildEvent, true);
            }
            GUILayout.Space(5);
            if (GUILayout.Button("Pack Flask into Pallet"))
            {
                Flask flask = (Flask)target;

                string palletPath = Path.GetFullPath(ElixirMixer.BuildPath(flask.Pallet));
                string flaskPath = Path.Combine(palletPath, "flasks");

                UnityEvent<bool> BuildEvent = ElixirMixer.ExportFlask(flask);
                BuildEvent.AddListener((hasErrors) =>
                {
                    EditorUtility.RevealInFinder(flaskPath);
                });

            }
        }
        GUILayout.Space(10);

        #endregion

        serializedObject.ApplyModifiedProperties();
    }

    private static void SelectIngredient(ref string[] arr)
    {
        string path = GetIngredient();
        if (!string.IsNullOrEmpty(path))
        {
            List<string> gredients = new List<string>();
            gredients.AddRange(arr);
            if (!gredients.Contains(path))
                gredients.Add(path);
            arr = gredients.ToArray();
        }
    }

    private static string GetIngredient()
    {
        if (!ElixirMixer.ConfirmMelonDirectory())
            return string.Empty;

        string GotPath = EditorUtility.OpenFilePanel("Select an Ingredient", ElixirMixer.ML_DIR, "dll");
        if (string.IsNullOrEmpty(GotPath))
            return string.Empty;

        //Check if this is a Flask reference and return accordingly
        DirectoryInfo parent = Directory.GetParent(GotPath);
        if (parent.Name == "flasks")
        {
            string crateName = parent.Parent.Name;
            string flaskName = Path.GetFileName(GotPath);
            return $"Pallet-{crateName}-{flaskName}";
        }
        return Path.GetRelativePath(ElixirMixer.ML_MANAGED_DIR, GotPath);
    }

    private static void OnBuildComplete(bool hasErrors)
    {
        if (hasErrors)
        {

        }
        else EditorUtility.DisplayDialog("Yay", "Stirred successfully with no anomalies!", "Drink the grog");

        if (EditorUtility.DisplayDialog("Flask stirring complete.", "Would you like to open the compiled folder?", "Yes"))
        {
            EditorUtility.RevealInFinder(Path.Combine(Application.temporaryCachePath, "StirTest.dll"));
        }
    }

    private void OnValidate()
    {
        notAnElixir = false;
        selectedElixir = null;
    }
}
