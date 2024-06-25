using Maranara.Marrow;
using Microsoft.CodeAnalysis.Scripting;
using SLZ.Marrow;
using SLZ.Marrow.Zones;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

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

    DragAndDropManipulatorListHelper dragDropManip;
    public override VisualElement CreateInspectorGUI()
    {

        string VISUALTREE_PATH = AssetDatabase.GUIDToAssetPath("b4f4c7462f8ee1a45a17b4fdf075b98d");
        VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(VISUALTREE_PATH);
        VisualElement tree = visualTree.Instantiate();

        //Elixir List
        string ELEMENT_PATH = AssetDatabase.GUIDToAssetPath("fc0a6e9674adeac49a0a82ffae009a25");
        itemTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ELEMENT_PATH);

        listParent = tree.Q<VisualElement>("ElixirItem").parent;
        RefreshElixirList();

        //Add From Buttons
        tree.Q<Button>("AddFromSelected").clicked += AddElixirsFromSelected;
        tree.Q<Button>("AddFromScene").clicked += AddElixirsFromScene;

        // Drag and Drop
        dragDropManip = new(tree);
        VisualElement zoneLinkDragDropTarget = tree.Q<VisualElement>("zoneLinkDragDropTarget");
        Label zoneLinkDragDropHint = tree.Q<Label>("zoneLinkDragDropHint");
        Label preDragHintText = tree.Q<Label>("preDragHintText");
        IMGUIContainer imguiValidationContainer = tree.Q<IMGUIContainer>("imguiValidationContainer");
        zoneLinkDragDropTarget.RegisterCallback<DragUpdatedEvent>(evt =>
        {
            DropAreaDragActive(zoneLinkDragDropTarget, zoneLinkDragDropHint, preDragHintText);
        });
        zoneLinkDragDropTarget.RegisterCallback<DragLeaveEvent>(evt =>
        {
            DropAreaDefaults(zoneLinkDragDropTarget, zoneLinkDragDropHint, preDragHintText);
        });
        zoneLinkDragDropTarget.RegisterCallback<DragPerformEvent>(evt =>
        {
            List<MonoScript> elixirList = info.Elixirs.ToList();
            foreach (var droppedObject in dragDropManip.droppedObjects)
            {
                MonoScript droppedGO = (MonoScript)droppedObject;
                if (droppedGO != null)
                {
                    Type elixirType = droppedGO.GetClass();

                    Elixir attribute = (Elixir)elixirType.GetCustomAttribute(typeof(Elixir));
                    if (attribute != null)
                    {
                        if (!elixirList.Contains(droppedGO))
                            elixirList.Add(droppedGO);
                    }
                }
            }

            info.Elixirs = elixirList.ToArray();

            RefreshElixirList();

            EditorUtility.SetDirty(info);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            DropAreaDefaults(zoneLinkDragDropTarget, zoneLinkDragDropHint, preDragHintText);
            zoneLinkDragDropTarget.RemoveFromClassList("drop-area--dropping");
        });
        imguiValidationContainer.onGUIHandler = () =>
        {
            DropAreaDefaults(zoneLinkDragDropTarget, zoneLinkDragDropHint, preDragHintText);
            zoneLinkDragDropTarget.RemoveFromClassList("drop-area--dropping");
        };
        return tree;
    }

    private void AddElixirsFromScene()
    {
        List<MonoScript> elixirList = info.Elixirs.ToList();
        MonoScript[] scripts = Elixir.GetAllElixirsFromScene();
        foreach (var script in scripts)
        {
            if (elixirList.Contains(script))
                continue;
            elixirList.Add(script);
        }
        info.Elixirs = elixirList.ToArray();

        RefreshElixirList();

        EditorUtility.SetDirty(info);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private void AddElixirsFromSelected()
    {
        List<MonoScript> elixirList = info.Elixirs.ToList();
        MonoScript[] scripts = Elixir.GetSelected();
        foreach (var script in scripts)
        {
            if (elixirList.Contains(script))
                continue;
            elixirList.Add(script);
        }
        info.Elixirs = elixirList.ToArray();

        RefreshElixirList();

        EditorUtility.SetDirty(info);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private VisualTreeAsset itemTemplate;
    private VisualElement[] elixirList;
    private VisualElement listParent;
    private void RefreshElixirList()
    {
        if (itemTemplate == null)
            return;
        if (elixirList != null)
        {
            foreach (VisualElement elixir in elixirList)
            {
                elixir.RemoveFromHierarchy();
            }
        }

        elixirList = new VisualElement[info.elixirGUIDs.Length]; 
        for (int i = 0; i < elixirList.Length; i++)
        {
            VisualElement e = itemTemplate.Instantiate();

            StyleEnum<DisplayStyle> style = e.style.display;
            style.value = DisplayStyle.Flex;
            e.style.display = style;

            e.Q<Label>("Path").text = AssetDatabase.GUIDToAssetPath(info.elixirGUIDs[i]);

            string link = "LinkMid";
            if (elixirList.Length == 1 || i == elixirList.Length - 1)
                link = "LinkBtm";
            else if (i == 0)
                link = "LinkTop";

            e.Q<VisualElement>(link).style.display = style;

            MonoScript type = info.Elixirs[i];
            VisualElement assetIcon = e.Q<VisualElement>("AssetIcon");
            Background bg = assetIcon.style.backgroundImage.value;
            bg.texture = AssetDatabase.GetCachedIcon(AssetDatabase.GetAssetPath(type)) as Texture2D;
            assetIcon.style.backgroundImage = bg;

            Button remove = e.Q<Button>("Remove");
            remove.clicked += () => Remove_clicked(type);

            listParent.Add(e);
            elixirList[i] = e;
        }
    }

    private void Remove_clicked(MonoScript guid)
    {
        List<MonoScript> elixirList = info.Elixirs.ToList();
        elixirList.Remove(guid);
        info.Elixirs = elixirList.ToArray();

        RefreshElixirList();

        EditorUtility.SetDirty(info);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private void DropAreaDefaults(VisualElement zoneLinkDragDropTarget, Label zoneLinkDragDropHint, Label preDragHintText)
    {
        preDragHintText.style.display = DisplayStyle.Flex;
        zoneLinkDragDropTarget.style.borderTopWidth = 0;
        zoneLinkDragDropTarget.style.borderBottomWidth = 0;
        zoneLinkDragDropTarget.style.borderLeftWidth = 0;
        zoneLinkDragDropTarget.style.borderRightWidth = 0;
        zoneLinkDragDropHint.style.display = DisplayStyle.None;
    }

    private void DropAreaDragActive(VisualElement zoneLinkDragDropTarget, Label zoneLinkDragDropHint, Label preDragHintText)
    {
        preDragHintText.style.display = DisplayStyle.None;
        zoneLinkDragDropTarget.style.borderTopWidth = 1;
        zoneLinkDragDropTarget.style.borderBottomWidth = 1;
        zoneLinkDragDropTarget.style.borderLeftWidth = 1;
        zoneLinkDragDropTarget.style.borderRightWidth = 1;
        zoneLinkDragDropHint.style.display = DisplayStyle.Flex;
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
