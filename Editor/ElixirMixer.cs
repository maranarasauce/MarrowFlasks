using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SLZ.Marrow.Warehouse;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using SLZ.MarrowEditor;
using SLZ.Marrow;
using System.Text.RegularExpressions;
using System;
using Microsoft.CodeAnalysis;
using UnityEngine.Events;
using Mono.Cecil;

namespace Maranara.Marrow
{
    [InitializeOnLoad]
    public class ElixirMixer
    {
        static ElixirMixer()
        {
            Flask.OnPacked = OnPack;
        }

        public static void OnPack(Flask flask)
        {
            Debug.Log($"Packing {flask.Title}");
            System.Diagnostics.StackTrace st = new System.Diagnostics.StackTrace();
            //This is horrible... Oh well!
            if (st.ToString().Contains("PackPallet"))
            {
                ExportFlask(flask);
            }
        }

        public static string BuildPath(Pallet pallet)
        {
                return AddressablesManager.EvaluateProfileValueBuildPathForPallet(pallet, AddressablesManager.ProfilePalletID);
        }
        public static string ML_DIR = null;
        public static string ML_MANAGED_DIR = null;
        public static string IL2CPP_ASSEMBLIES
        {
            get { return Path.Combine(Directory.GetParent(ML_MANAGED_DIR).FullName, "Il2CppAssemblies"); }
        }
        public static void ExportFlasksFromPallet(Pallet pallet)
        {
            List<Flask> flasks = new List<Flask>();
            foreach (DataCard card in pallet.DataCards)
            {
                if (card.GetType().IsAssignableFrom(typeof(Flask)))
                {
                    Flask flask = (Flask)card;
                    flasks.Add(flask);
                }
            }

            string palletPath = Path.GetFullPath(ElixirMixer.BuildPath(pallet));
            string flaskPath = Path.Combine(palletPath, "flasks");

            if (!Directory.Exists(flaskPath))
                Directory.CreateDirectory(flaskPath);

            IterateNextFlask(flaskPath, flasks.ToArray(), 0);
        }

        public static UnityEvent<bool> ExportFlask(Flask flask)
        {
            string title = MarrowSDK.SanitizeName(flask.Title);
            title = Regex.Replace(title, @"\s+", "");

            string palletPath = Path.GetFullPath(ElixirMixer.BuildPath(flask.Pallet));
            string flaskPath = Path.Combine(palletPath, "flasks");
            if (!Directory.Exists(flaskPath))
                Directory.CreateDirectory(flaskPath);

            UnityEvent<bool> completeCallback = new UnityEvent<bool>();
            completeCallback.AddListener((hasErrors) =>
            {
                TreatExportedElixir(Path.Combine(flaskPath, title + ".dll"));
                if (hasErrors)
                {
                    EditorUtility.DisplayDialog("Error", $"Errors detected in the {flask.Title} Flask! Check the Console for errors.", "Fine");
                }
            });
            ExportElixirs(title, flaskPath, flask, completeCallback);
            return completeCallback;
        }

        private static void IterateNextFlask(string flaskPath, Flask[] flasks, int i)
        {
            if (i > (flasks.Length - 1))
            {

                return;
            }


            Flask flask = flasks[i];
            Debug.Log(flask.Title);
            string title = MarrowSDK.SanitizeName(flask.Title);
            title = Regex.Replace(title, @"\s+", "");

            UnityEvent<bool> completeCallback = new UnityEvent<bool>();
            completeCallback.AddListener((hasErrors) =>
            {
                TreatExportedElixir(Path.Combine(flaskPath, title + ".dll"));
                if (hasErrors)
                {
                    EditorUtility.DisplayDialog("Error", $"Errors detected in the {flask.Title} Flask! Check the Console for errors.", "Fine");
                }
                else IterateNextFlask(flaskPath, flasks, i + 1);
            });

            ExportElixirs(title, flaskPath, flask, completeCallback);
        }

        //Thanks WNP!
        public static void TreatExportedElixir(string path)
        {
            var assemblyResolve = new DefaultAssemblyResolver();
            var directories = assemblyResolve.GetSearchDirectories();
            for (int i = 0; i < directories.Length; i++)
            {
                assemblyResolve.RemoveSearchDirectory(directories[i]);
            }

            assemblyResolve.AddSearchDirectory(Path.GetFullPath(ML_MANAGED_DIR));
            //assemblyResolve.AddSearchDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "..\\ScriptReferences")));

            using (var module = ModuleDefinition.ReadModule(path, new ReaderParameters() { AssemblyResolver = assemblyResolve }))
            {
                List<TypeDefinition> addDeserialize = new List<TypeDefinition>();
                //var deserialiser = module.ImportReference(mtinm.Modules[0].Types.First(t => t.FullName == "ModThatIsNotMod.MonoBehaviours.CustomMonoBehaviourHandler").Methods.First(m => m.Name == "SetFieldValues"));

                foreach (TypeDefinition typeDef in module.Types)
                {
                    if (MixerLibs.CheckParentType(typeDef))
                    {
                        var success = MixerLibs.GetOrAddPtrConstructorWithinAssembly(typeDef, module) != null;
                        //Debug.Log(success ? $"Added IntPtr contstructor to type: {typeDef.FullName}" : $"Failed to add IntPtr constructor to type: {typeDef.FullName}");
                    }
                }

                module.Write(path + ".temp");
            }
            if (File.Exists(path))
                File.Delete(path);
            File.Move(path + ".temp", path);
        }

        public static void ExportElixirs(string title, string outputDirectory, Flask flask, UnityEvent<bool> invokeAfterBuild)
        {
            if (!ConfirmMelonDirectory())
                return;

            List<string> exportedScriptPaths = new List<string>();

            string tempDir = Path.Combine(Application.dataPath, $".FLASK_GEN_{GUID.Generate()}-{title}");
            Directory.CreateDirectory(tempDir);

            foreach (MonoScript type in flask.Elixirs)
            {
                string path = AssetDatabase.GetAssetPath(type);
                string newPath = Path.Combine(tempDir, Path.GetFileName(path));

                CreateTempElixir(newPath, type.text);

                exportedScriptPaths.Add(newPath);
            }

            string[] scriptPaths = exportedScriptPaths.ToArray();
            string targetFlask = Path.Combine(outputDirectory, title + ".dll");
            BuildDLL(title, scriptPaths, outputDirectory);
            
            /*AssemblyBuilder asmBuilder = new AssemblyBuilder(Path.Combine(outputDirectory, title + ".dll"), exportedScriptPaths.ToArray());

            
            asmBuilder.buildTarget = BuildTarget.StandaloneWindows64;
            asmBuilder.buildTargetGroup = BuildTargetGroup.Standalone;
            asmBuilder.compilerOptions = new ScriptCompilerOptions()
            {
                AllowUnsafeCode = true
            };

            asmBuilder.buildFinished += ((arg1, arg2) =>
            {
                bool hasErrors = AsmBuilder_buildFinished(arg1, arg2, tempDir, title);
                invokeAfterBuild?.Invoke(hasErrors);
            });

            asmBuilder.excludeReferences = asmBuilder.defaultReferences;

            List<string> references = new List<string>();

            if (flask.useDefaultIngredients)
                references.AddRange(GetDefaultReferences(true));
            else references.AddRange(AddPathToReferences(flask.ingredients));

            if (flask.additionalIngredients != null)
                references.AddRange(AddPathToReferences(flask.additionalIngredients));

            asmBuilder.additionalReferences = references.ToArray();
            asmBuilder.compilerOptions = new ScriptCompilerOptions()
            {
                CodeOptimization = CodeOptimization.Release
            };

            WaitForCompile(asmBuilder);*/
        }
        
        private static void BuildDLL(string title, string[] elixirs, string outputPath)
        {

        }

        /*private async static void WaitForCompile(AssemblyBuilder builder)
        {
            while (EditorApplication.isCompiling)
            {
                await Task.Delay(1000);
            }

            builder.Build();
        }*/

        #region ReferenceUtils
        public static string[] GetDefaultReferences(bool withPath)
        {
            ConfirmMelonDirectory();

            if (!withPath)
            {
                return GetDefaultReferencesNoPath();
            }

            List<string> additionalReferences = new List<string>();
            additionalReferences.Add(Path.Combine(ML_DIR, Path.Combine("net6","MelonLoader.dll")));
            additionalReferences.Add(Path.Combine(ML_DIR, Path.Combine("net6", "0Harmony.dll")));

            foreach (string reference in Directory.GetFiles(ML_MANAGED_DIR))
            {
                if (!reference.EndsWith(".dll"))
                    continue;

                string fileName = Path.GetFileNameWithoutExtension(reference);

                if (!(fileName == "netstandard"))
                {
                    additionalReferences.Add(reference);
                }
            }

            foreach (string reference in Directory.GetFiles(IL2CPP_ASSEMBLIES))
            {
                if (!reference.EndsWith(".dll"))
                    continue;
                additionalReferences.Add(Path.Combine(IL2CPP_ASSEMBLIES, reference));
            }

            return additionalReferences.ToArray();
        }

        private static string[] GetDefaultReferencesNoPath()
        {
            List<string> additionalReferences = new List<string>();
            additionalReferences.Add("..\\net6\\MelonLoader.dll");
            additionalReferences.Add("..\\net6\\0Harmony.dll");
            foreach (string reference in Directory.GetFiles(ML_MANAGED_DIR))
            {
                if (!reference.EndsWith(".dll"))
                    continue;

                string fileName = Path.GetFileNameWithoutExtension(reference);

                if (!(fileName == "netstandard"))
                {
                    additionalReferences.Add(fileName);
                }
            }

            foreach (string reference in Directory.GetFiles(IL2CPP_ASSEMBLIES))
            {
                string fileName = Path.GetFileNameWithoutExtension(reference);

                if (!reference.EndsWith(".dll"))
                    continue;
                additionalReferences.Add($"..\\Il2CppAssemblies\\{fileName}");
            }
            return additionalReferences.ToArray();
        }

        private static string[] AddPathToReferences(string[] references)
        {
            for (int i = 0; i < references.Length; i++)
            {
                string path = references[i];
                if (!File.Exists(path))
                {
                    //If path is relative, add on the ML path.

                    if (!path.EndsWith(".dll"))
                        path = path + ".dll";

                    string newPath = Path.Combine(ML_MANAGED_DIR, path);

                    //Check if this is a flask reference
                    if (path.StartsWith("Pallet-"))
                    {
                        path = path.Remove(0, 7);

                        string[] splitPath = path.Split('-', StringSplitOptions.None);

                        string crateName = Path.Combine(splitPath[0], "flasks");
                        string flaskName = splitPath[1];

                        string slzLocalLow = Path.Combine(Directory.GetParent(Application.persistentDataPath).Parent.FullName, "Stress Level Zero");

                        //TODO
                        //Currently, game name is hardcoded since there is no way to tell which game is to be selected. Hope this is fixed in an SDK patch.
                        string gameLocalPath = Path.Combine(slzLocalLow, MarrowSDK.GAME_NAMES[0]);
                        string modPath = Path.Combine(gameLocalPath, "Mods");

                        newPath = Path.Combine(modPath, Path.Combine(crateName, flaskName));
                    }

                    if (File.Exists(newPath))
                        references[i] = newPath;
                }
            }
            return references;
        }
        #endregion

        private static void CreateTempElixir(string path, string allText)
        {
            allText = "#define FLASK_ONLY\n" + allText;
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(allText);
            CompilationUnitSyntax root = syntaxTree.GetCompilationUnitRoot();
            ClassDeclarationSyntax rootClass = null;

            // Remove all attributes using a rewriter class
            root = new MixerLibs.AttributeRemoverRewriter().Visit(root).SyntaxTree.GetCompilationUnitRoot();

            // Convert the final script to a string and switch UnityAction for System.Action
            string finalScript = root.NormalizeWhitespace().ToFullString();
            finalScript = finalScript.Replace("[Elixir]", "");
            finalScript = finalScript.Replace("[DontAssignIntPtr]", "");
            finalScript = finalScript.Replace("new UnityAction", "new System.Action");
            finalScript = finalScript.Replace("new UnityEngine.Events.UnityAction", "new System.Action");
            // Swap StartCoroutine for MelonCoroutines.Start
            finalScript = finalScript.Replace("this.StartCoroutine(", "MelonLoader.MelonCoroutines.Start(");
            finalScript = finalScript.Replace("base.StartCoroutine(", "MelonLoader.MelonCoroutines.Start(");
            finalScript = finalScript.Replace("StartCoroutine(", "MelonLoader.MelonCoroutines.Start(");

            using (StreamWriter sw = File.CreateText(path))
            {
                sw.Write(finalScript);
            }
        }

        /*private static bool AsmBuilder_buildFinished(string arg1, string tempDir, string title)
        {
            bool hasErrors = false;

            foreach (CompilerMessage msg in arg2)
            {
                switch (msg.type)
                {
                    case CompilerMessageType.Info:
                        Debug.Log(msg.message);
                        break;
                    case CompilerMessageType.Error:
                        hasErrors = true;
                        Debug.LogError(msg.message);
                        break;
                    case CompilerMessageType.Warning:
                        Debug.LogWarning(msg.message);
                        break;
                }
                
            }

            bool deleteTempFiles = true;
            if (hasErrors)
            {
                if (EditorUtility.DisplayDialog("Error", $"Errors detected in the Flask! Check the Console for errors.", "View generated scripts", "Done"))
                {
                    deleteTempFiles = false;
                    EditorUtility.RevealInFinder(tempDir);
                } 
            }

            if (deleteTempFiles)
            {
                foreach (string file in Directory.GetFiles(tempDir))
                {
                    File.Delete(file);
                }
                Directory.Delete(tempDir);
            }
            

            return hasErrors;
        }*/

        public static bool ConfirmMelonDirectory()
        {
            if (string.IsNullOrEmpty(ML_DIR))
            {
                bool solved = false;
                foreach (var gamePath in ModBuilder.GamePathDictionary)
                {
                    string gamePathSS = Path.Combine(gamePath.Value, "cauldronsave.txt");

                    if (File.Exists(gamePathSS))
                    {
                        string mlPath = File.ReadAllText(gamePathSS);
                        ML_DIR = mlPath.Replace("\n", "").Replace("\r", "");
                        ML_MANAGED_DIR = Path.Combine(ML_DIR, "Managed");
                        solved = true;
                    }
                    else
                        continue;
                }

                if (!solved)
                {
                    EditorUtility.DisplayDialog("Help me out!", "Your MelonLoader directory isn't set. Please launch BONELAB with the MarrowCauldron mod at least once.", "Sure thing");
                    return false;
                }
            }
            return true;
        }
    }

}
