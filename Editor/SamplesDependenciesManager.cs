using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Plastic.Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.UI;
using UnityEngine;
namespace kevincastejon.test.package.samples.dependencies
{
    [InitializeOnLoad]
    public class SamplesDependenciesManager
    {
        static SamplesDependenciesManager()
        {
            string packageName = GetPackageNameOfThisScript();
            string packageVersion = GetInstalledVersion(packageName);
            PackageJSONModel packageJSONModel = JsonConvert.DeserializeObject<PackageJSONModel>(ReadPackageJson(packageName));
            Dictionary<Sample, Sample> missingSamples = new();
            List<Sample> samples = Sample.FindByPackage(packageName, packageVersion).ToList();

            PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone, out string[] defs);
            string defsString = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone);
            List<string> defsToRemove = new();
            List<string> defsToAdd = new();
            foreach (string def in defs)
            {
                if (def.StartsWith(packageName.Replace('-', '_').ToUpper() + "___"))
                {
                    defsToRemove.Add(def);
                }
            }

            foreach (Sample sample in samples)
            {
                int ind = sample.importPath.IndexOf("Assets/Samples");
                ind = ind == -1 ? sample.importPath.IndexOf("Assets\\Samples") : ind;
                if (sample.isImported)
                {
                    defsToAdd.Add(packageName.Replace('-', '_').ToUpper() + "___SAMPLE___IMPORTED___" + sample.displayName.Replace(" ", "").ToUpper());
                }
            }
            foreach (string def in defsToRemove)
            {
                defsString = defsString.Replace(def, "");
            }
            foreach (string def in defsToAdd)
            {
                defsString += ";" + def;
            }
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Standalone, defsString);
            foreach (Sample sample in samples)
            {
                if (!sample.isImported)
                    continue;

                SampleModel sampleModel = packageJSONModel.Samples.FirstOrDefault(x => x.DisplayName == sample.displayName);
                if (sampleModel?.SampleDependencies == null)
                    continue;

                foreach (SampleDependencyModel sampleDependency in sampleModel.SampleDependencies)
                {
                    if (string.IsNullOrEmpty(sampleDependency.Package) || sampleDependency.Package == packageName)
                    {
                        int depIndex = samples.FindIndex(x => x.displayName == sampleDependency.Sample);
                        if (depIndex != -1)
                        {
                            Sample s = samples[depIndex];
                            if (!s.isImported)
                            {
                                missingSamples.Add(s, sample);
                            }
                        }
                    }
                    else 
                    {
                        string otherPackage = sampleDependency.Package;
                        string installedVersion = GetInstalledVersion(otherPackage);

                        if (installedVersion == null)
                        {
                            
                            InstallPackage(otherPackage, () =>
                            {
                                ImportSampleFromPackage(otherPackage, sampleDependency.Sample);
                                AssetDatabase.Refresh();
                            });
                        }
                        else
                        {
                            ImportSampleFromPackage(otherPackage, sampleDependency.Sample);
                        }
                    }
                }
            }

            if (missingSamples.Count == 0)
            {
                return;
            }
            StringBuilder message = new("The following samples are missing : \n");
            foreach (var samplePair in missingSamples)
            {
                Sample depSample = samplePair.Key;
                Sample baseSample = samplePair.Value;
                message.AppendLine("- " + depSample.displayName + " (dependency of " + baseSample.displayName + ")\n"); 
            }
            message.AppendLine("Do you want to import these samples ?");
            bool install = EditorUtility.DisplayDialog(packageName + " sample dependencies manager", message.ToString(), "Ok", "No");
            if (install)
            {
                foreach (var samplePair in missingSamples)
                {
                    Sample depSample = samplePair.Key;
                    depSample.Import(Sample.ImportOptions.OverridePreviousImports);
                    Debug.Log(depSample.displayName + " has been imported.");
                }
                EditorApplication.update += OneShotFrameWaiter;
            }
        }

        private static void OneShotFrameWaiter()
        {
            EditorApplication.update -= OneShotFrameWaiter;
            AssetDatabase.Refresh();
        }
        private static void InstallPackage(string packageName, Action onInstalled = null)
        {
            var addRequest = Client.Add(packageName);
            EditorApplication.update += () =>
            {
                if (addRequest.IsCompleted)
                {
                    if (addRequest.Status == StatusCode.Success)
                    {
                        Debug.Log($"Package {packageName} installed successfully.");
                        onInstalled?.Invoke();
                    }
                    else
                    {
                        Debug.LogError($"Failed to install package {packageName}: {addRequest.Error.message}");
                    }
                    EditorApplication.update -= null;
                }
            };
        }
        private static void ImportSampleFromPackage(string packageName, string sampleDisplayName)
        {
            string version = GetInstalledVersion(packageName);
            var samples = Sample.FindByPackage(packageName, version);
            foreach (var s in samples)
            {
                if (s.displayName == sampleDisplayName && !s.isImported)
                {
                    s.Import(Sample.ImportOptions.OverridePreviousImports);
                    Debug.Log($"Sample '{sampleDisplayName}' from package '{packageName}' has been imported.");
                    return;
                }
            }
            Debug.LogWarning($"Sample '{sampleDisplayName}' not found in package '{packageName}'.");
        }

        private static string GetPackageNameOfThisScript()
        {
            var script = MonoScript.FromScriptableObject(ScriptableObject.CreateInstance<SamplesDependenciesManagerReference>());
            string scriptPath = AssetDatabase.GetAssetPath(script);

            if (scriptPath.StartsWith("Packages/"))
            {
                string[] parts = scriptPath.Split('/');
                if (parts.Length >= 2)
                    return parts[1];
            }
            return null;
        }
        private static string GetInstalledVersion(string packageName)
        {
            var listRequest = Client.List(true);
            while (!listRequest.IsCompleted) { }

            if (listRequest.Status == StatusCode.Success)
            {
                foreach (var package in listRequest.Result)
                {
                    if (package.name == packageName)
                    {
                        return package.version;
                    }
                }
            }
            else
            {
                Debug.LogError("Failed to get package list: " + listRequest.Error.message);
            }
            return null;
        }
        private static string GetResolvedPath(string packageName)
        {
            var list = Client.List(true);
            while (!list.IsCompleted) { }

            foreach (var pkg in list.Result)
            {
                if (pkg.name == packageName)
                    return pkg.resolvedPath;
            }
            return null;
        }
        private static string ReadPackageJson(string packageName)
        {
            string path = GetResolvedPath(packageName);
            path = Path.Combine(path, "package.json");
            if (!File.Exists(path))
            {
                Debug.LogError($"[PackageJsonReader] package.json not found at path: {path}");
                return null;
            }
            return File.ReadAllText(path);
        }
    }
    class PackageJSONModel
    {
        public string Name;
        public string Version;
        public SampleModel[] Samples { get; set; }
    }
    class SampleModel
    {
        public string DisplayName;
        public SampleDependencyModel[] SampleDependencies { get; set; }
    }
    class SampleDependencyModel
    {
        public string Package { get; set; }
        public string Sample { get; set; }
    }
}
