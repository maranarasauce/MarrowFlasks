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
using Microsoft.Build.Unity;
using System.Linq;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor.ShaderKeywordFilter;
using NUnit.Framework;

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
                Debug.Log($"Export Flask. {flask.Title}");
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
        public static string NET6_DIR
        {
            get { return Path.Combine(Directory.GetParent(ML_MANAGED_DIR).FullName, "net6"); }
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
            string title = flask.GetCompiledName();

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

        public static void ExportElixirs(string title, string outputDirectory, Flask flask, UnityEvent<bool> invokeAfterBuild, bool openOutputDir = false)
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

            List<string> references = new List<string>();

            if (flask.useDefaultIngredients)
                references.AddRange(GetDefaultReferences(true));
            else references.AddRange(AddPathToReferences(flask.ingredients, ML_MANAGED_DIR));

            if (flask.gameIngredients != null)
                references.AddRange(AddPathToReferences(flask.gameIngredients, ML_DIR));

            if (flask.palletIngredients != null)
                references.AddRange(GetFlaskReferences(flask.palletIngredients));

            BuildDLL(title, scriptPaths, references.ToArray(), outputDirectory, openOutputDir);
            
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

            asmBuilder.additionalReferences = references.ToArray();
            asmBuilder.compilerOptions = new ScriptCompilerOptions()
            {
                CodeOptimization = CodeOptimization.Release
            };

            WaitForCompile(asmBuilder);*/
        }
        
        private static bool BuildDLL(string title, string[] elixirs, string[] references, string outputPath, bool openOutputDir = false)
        {
            if (!ConfirmMelonDirectory())
                return false;

            //Construct temporary directory
            string tempDir = Path.Combine(Path.GetTempPath(), "flasktemp");
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);

            //Get MSBuild template and copy to tempdir
            string projTemplateDir = Path.GetFullPath("Packages/com.maranara.marrowflasks/Dependencies/MSBuildTemplate");

            //Get CSProj and set the title and directories
            string csProjPath = Path.Combine(projTemplateDir, "CustomMonoBehaviour.csprog");
            string csProjText = File.ReadAllText(csProjPath).Replace("$safeprojectname$", title).Replace("$BONELAB_DIR$", ML_DIR);
            
            //Parse CSProj and setup compiler
            XDocument csproj = XDocument.Parse(csProjText);
            //Get template Compile
            XElement compile = csproj.Root.Elements().Single((e) => e.ToString().Contains("Compile"));
            
            compile.RemoveAll();
            //Add references to Elixir paths
            foreach (string elixirPath in elixirs)
            {
                string elixirName = Path.GetFileName(elixirPath);
                string tempElixirPath = Path.Combine(tempDir, elixirName);

                CreateTempElixir(tempElixirPath, File.ReadAllText(elixirPath));

                XElement newCompile = new XElement("Compile");
                newCompile.SetAttributeValue("Include", Path.GetFileName(tempElixirPath));
                compile.Add(newCompile);
            }

            //Get template Reference
            XElement reference = csproj.Root.Elements().Single((e) => e.ToString().Contains("Reference"));

            reference.RemoveAll();
            //Add references to Ingredients
            foreach (string refHint in references)
            {
                XElement newRef = new XElement("Reference");
                newRef.SetAttributeValue("Include", Path.GetFileNameWithoutExtension(refHint));
                XElement newHint = new XElement("HintPath");
                newHint.SetValue(Path.Combine(ML_MANAGED_DIR, refHint));
                newRef.Add(newHint);

                reference.Add(newRef);
            }

            //Copy CSProj to temporary directory
            string finalProjPath = Path.Combine(tempDir, "CustomMonoBehaviour.csproj");
            string finalCsproj = csproj.ToString().Replace("xmlns=\"\" ", "");
            File.WriteAllText(finalProjPath, finalCsproj);

            //Build the dang project
            MSBuildBuildProfile profile = MSBuildBuildProfile.Create("Debug", false, "-t:Build -p:Configuration=Debug");
            List<MSBuildBuildProfile> profileList = new List<MSBuildBuildProfile>();
            profileList.Add(profile);
            IEnumerable<MSBuildBuildProfile> profiles = profileList;

            MSBuildProjectReference project = MSBuildProjectReference.FromMSBuildProject(finalProjPath, profiles: profiles);

            try
            {
                project.BuildProject(profile.Name);

                //Create output directory
                if (!Directory.Exists(outputPath))
                    Directory.CreateDirectory(outputPath);

                //Delete previous DLL
                if (File.Exists(Path.Combine(outputPath, $"{title}.dll")))
                    File.Delete(Path.Combine(outputPath, $"{title}.dll"));

                //Copy from tempdir to output path
                File.Copy(Path.Combine(tempDir, "bin", "Debug", "net6.0", title + ".dll"), Path.Combine(outputPath, $"{title}.dll"));
                Directory.Delete(tempDir, true);

                if (openOutputDir)
                    Application.OpenURL(outputPath);
                return true;

            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("ERROR", "Your compiled scripts had errors. Opening the output log...", "OK");
                Debug.Log(e.StackTrace);
                throw e;
            }
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

            additionalReferences.Add(Path.GetRelativePath(ML_MANAGED_DIR, Path.Combine(Directory.GetParent(ML_MANAGED_DIR).Parent.FullName, "Mods", "MarrowCauldron.dll")));

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

            foreach (string reference in Directory.GetFiles(NET6_DIR))
            {
                if (!reference.EndsWith(".dll"))
                    continue;
                additionalReferences.Add(Path.Combine(NET6_DIR, reference));
            }

            return additionalReferences.ToArray();
        }

        private static string[] GetDefaultReferencesNoPath()
        {
            List<string> additionalReferences = new List<string>();

            additionalReferences.Add("..\\..\\Mods\\MarrowCauldron.dll");

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

            foreach (string reference in Directory.GetFiles(NET6_DIR))
            {
                string fileName = Path.GetFileNameWithoutExtension(reference);

                if (!reference.EndsWith(".dll"))
                    continue;
                additionalReferences.Add($"..\\net6\\{fileName}");
            }

            return additionalReferences.ToArray();
        }

        private static string[] GetFlaskReferences(DataCardReference<Flask>[] flasks)
        {
            List<string> refs = new List<string>();
            foreach (DataCardReference<Flask> flaskRef in flasks)
            {
                if (!flaskRef.TryGetDataCard(out Flask flask))
                {
                    Debug.Log($"Could not find Flask Ingredient {flaskRef.Barcode}");
                    continue;
                }

                if (AssetWarehouse.Instance.WorkingPallets.ContainsKey(flask.Pallet.Barcode))
                {
                    //Flask is owned by user and can be compiled (if not already)
                    string palletPath = Path.GetFullPath(ElixirMixer.BuildPath(flask.Pallet));
                    string flaskPath = Path.Combine(palletPath, "flasks", $"{flask.GetCompiledName()}.dll");

                    if (File.Exists(flaskPath))
                    {
                        Debug.Log("Flask exists. No need to compile");

                        refs.Add(flaskPath);
                    } else
                    {
                        Debug.Log("Erm... Flask does not exist. Compiling...");
                        UnityEvent<bool> complete = ElixirMixer.ExportFlask(flask);
                        complete.AddListener((success) =>
                        {
                            if (success)
                            {
                                refs.Add(flaskPath);
                            } else Debug.LogError($"Could not reference Flask {flask.Title} as it had errors compiling.");
                        });
                    }
                } else
                {
                    Debug.Log("Not included in working pallets.");
                    //Flask is referenced and needs to be found
                    string ModDir = Path.Combine(ModBuilder.GamePaths[0], MarrowSDK.RUNTIME_MODS_DIRECTORY_NAME);
                    string SelfModDir = Path.Combine(Application.persistentDataPath, MarrowSDK.RUNTIME_MODS_DIRECTORY_NAME);
                    string palletPath = Path.Combine(flask.Pallet.Barcode.ID, "flasks", $"{flask.GetCompiledName()}.dll");

                    string selfPallet = Path.Combine(SelfModDir, palletPath);
                    string modPallet = Path.Combine(ModDir, palletPath);
                    if (File.Exists(selfPallet))
                        refs.Add(selfPallet);
                    else if (File.Exists(modPallet))
                        refs.Add(modPallet);
                    else
                        Debug.LogError($"Could not reference Flask {flask.Title} as it does not exist in any Mod directory.");
                }
            }
            return refs.ToArray();
        }

        private static string[] AddPathToReferences(string[] references, string relPath)
        {
            string[] newRefs = new string[references.Length];
            for (int i = 0; i < references.Length; i++)
            {
                string path = references[i];
                if (!File.Exists(path))
                {
                    //If path is relative, add on the ML path.

                    if (!path.EndsWith(".dll"))
                        path = path + ".dll";

                    string newPath = Path.Combine(relPath, path);

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
                        newRefs[i] = newPath;
                }
            }
            return newRefs;
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
            finalScript = finalScript.Replace("using SLZ.", "using Il2CppSLZ.");

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
