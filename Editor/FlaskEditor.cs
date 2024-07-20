using Maranara.Marrow;
using Microsoft.CodeAnalysis.Scripting;
using SLZ.Marrow;
using SLZ.Marrow.Warehouse;
using SLZ.Marrow.Zones;
using SLZ.MarrowEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
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
        ingredientsProperty = serializedObject.FindProperty("ingredients");
        gameIngredientsProperty = serializedObject.FindProperty("gameIngredients");
        palletIngredientsProperty = serializedObject.FindProperty("palletIngredients");
    }

    private SerializedProperty ingredientsProperty;
    private SerializedProperty gameIngredientsProperty;
    private SerializedProperty palletIngredientsProperty;
    public override void OnInspectorGUI()
    {
        EditorGUILayout.LabelField("This is an external Flask. You cannot edit its contents.");
        EditorGUILayout.TextField(info.Barcode.ID);
    }

    DragAndDropManipulatorListHelper dragDropManip;
    public override VisualElement CreateInspectorGUI()
    {
        serializedObject.Update();

        if (AssetWarehouse.Instance != null && info != null && info.Pallet != null)
        {
            if (!AssetWarehouse.Instance.WorkingPallets.ContainsKey(info.Pallet.Barcode))
            {
                return null;
            }
        }
        

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
                    if (elixirType == null)
                        continue;

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

        //Base ingredient toggle
        VisualElement baseIngredientGrp = tree.Q<VisualElement>("BaseIngredientGrp");
        if (!info.useDefaultIngredients)
        {
            StyleEnum<DisplayStyle> displayHidden = baseIngredientGrp.style.display;
            displayHidden = DisplayStyle.Flex;
            baseIngredientGrp.style.display = displayHidden;
        }

        Toggle baseIngredientToggle = tree.Q<Toggle>("BaseIngredientToggle");
        baseIngredientToggle.value = info.useDefaultIngredients;
        tree.Q<Toggle>("BaseIngredientToggle").RegisterValueChangedCallback<bool>((e) => {
            info.useDefaultIngredients = e.newValue;

            StyleEnum<DisplayStyle> displayHidden = baseIngredientGrp.style.display;
            displayHidden = info.useDefaultIngredients ? DisplayStyle.None : DisplayStyle.Flex;
            baseIngredientGrp.style.display = displayHidden;

            if (info.ingredients == null || info.ingredients.Length == 0)
            {
                BaseToDefault();
            }

            EditorUtility.SetDirty(info);
        });

        //Base ingredient buttons
        tree.Q<Button>("BaseDefaults").clicked += BaseToDefault;
        tree.Q<Button>("BaseClear").clicked += BaseClear;
        tree.Q<Button>("BaseSelect").clicked += BaseSelect;

        //Serialized Lists

        tree.Q<VisualElement>("BaseIngredients").Add(new PropertyField(ingredientsProperty));
        tree.Q<VisualElement>("GameIngredients").Add(new PropertyField(gameIngredientsProperty));
        tree.Q<PropertyField>("PalletIngredients").BindProperty(palletIngredientsProperty);

        //Add ingredients
        tree.Q<Button>("GameSelect").clicked += () =>
        {
            if (!ElixirMixer.ConfirmMelonDirectory())
                return;
            SelectIngredient(ref info.gameIngredients, ElixirMixer.ML_DIR);
            EditorUtility.SetDirty(info);
            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
        };
        //Debug buttons
        tree.Q<Button>("TestFlask").clicked += TestFlask;
        tree.Q<Button>("PackFlask").clicked += PackFlask;

        return tree;
    }

    private void BaseToDefault()
    {
        info.ingredients = ElixirMixer.GetDefaultReferences(false);
        EditorUtility.SetDirty(info);
        serializedObject.ApplyModifiedProperties();
        serializedObject.Update();
    }

    private void BaseClear()
    {
        info.ingredients = new string[0];
        EditorUtility.SetDirty(info);
        serializedObject.ApplyModifiedProperties();
        serializedObject.Update();
    }

    private void BaseSelect()
    {
        if (!ElixirMixer.ConfirmMelonDirectory())
            return;

        SelectIngredient(ref info.ingredients, ElixirMixer.ML_MANAGED_DIR);
        EditorUtility.SetDirty(info);
        serializedObject.ApplyModifiedProperties();
        serializedObject.Update();
    }

    private void TestFlask()
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

    private void PackFlask()
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

    private static void SelectIngredient(ref string[] arr, string relativeToPath = "")
    {
        string path = GetIngredient(relativeToPath);
        if (!string.IsNullOrEmpty(path))
        {
            List<string> gredients = new List<string>();
            gredients.AddRange(arr);
            if (!gredients.Contains(path))
                gredients.Add(path);
            arr = gredients.ToArray();
        }
    }

    private static string GetIngredient(string relativeToPath)
    {
        if (!ElixirMixer.ConfirmMelonDirectory())
            return string.Empty;

        string GotPath = EditorUtility.OpenFilePanel("Select an Ingredient", relativeToPath, "dll");
        if (string.IsNullOrEmpty(GotPath))
            return string.Empty;

        return Path.GetRelativePath(relativeToPath, GotPath);
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
}
