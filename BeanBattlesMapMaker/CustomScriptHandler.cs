using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using UnityEngine;
using HarmonyLib;
using BepInEx;

namespace BeanBattlesMapMaker
{
    internal class CustomScriptHandler
    {
        public static Dictionary<string, List<Type>> loadedScripts;

        public static void GetDLLs(string inPath)
        {
            loadedScripts = new Dictionary<string, List<Type>>();

            if (!Directory.Exists(Path.Combine(inPath, "custom")))
            {
                return;
            }

            string[] dllPaths = Directory.GetFiles(Path.Combine(inPath, "custom"), "*.dll");

            foreach (string dll in dllPaths)
            {
                var loadedAssembly = Assembly.LoadFile(dll);

                Type[] types = loadedAssembly.GetTypes();

                foreach (Type type in types)
                {
                    Debug.Log("Loaded Type: " + type.Name);

                    if (type.GetMethod("GetTags") != null)
                    {
                        string[] objectTags = (string[])type.GetMethod("GetTags").Invoke(null, null);

                        foreach (string objectTag in objectTags)
                        {
                            if (loadedScripts.ContainsKey(objectTag))
                            {
                                loadedScripts[objectTag].Add(type);
                            }
                            else
                            {
                                loadedScripts.Add(objectTag, new List<Type>{ type });
                            }

                            Debug.Log("Tag Added to List: " + objectTag);
                        }
                    }
                }
            }
        }
    }
}
