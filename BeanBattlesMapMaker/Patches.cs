using GG.BeanBattles;
using GG.Shared;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace BeanBattlesMapMaker
{
    [HarmonyPatch]
    class Patches
    {
        [HarmonyPatch(typeof(GameManager), "MoveNextStage")]
        [HarmonyPostfix]
        static void RoundStarted()
        {
            Debug.Log("Debug: SpawnPlayers called");

            if (SetupMap.mapCreated)
            {
                foreach (string tag in CustomScriptHandler.loadedScripts.Keys)
                {
                    foreach (Type customScript in CustomScriptHandler.loadedScripts[tag])
                    {
                        MethodInfo resetMethod = customScript.GetMethod("ResetObject");

                        foreach (object go in GameObject.FindObjectsOfType(customScript))
                        {
                            //Debug.Log("Reset: " + go.GetType());
                            resetMethod.Invoke(go, null);
                        }
                    }
                }

                /*foreach (ScriptBaseClass customScript in GameObject.FindObjectsOfType<ScriptBaseClass>())
                {
                    Debug.Log("Reset: " + customScript.name);
                    customScript.ResetObject();
                }*/
            }
        }


        //when player leaves, makes sure map data is reset
        //causes player to not disconnect
        [HarmonyPatch(typeof(CustomNetworkManager), "myDisconnect")]
        static void Postfix()
        {
            if (SetupMap.myLoadedAssetBundle)
            {
                SetupMap.myLoadedAssetBundle.Unload(true);
            }

            SetupMap.mapCreated = false;
            SetupMap.joinedCustomMap = false;
            SetupMap.deathZoneScale = null;
        }

        //damage patch, no damage if during grace
        [HarmonyPatch(typeof(Health), "TakeDamage", new[] {typeof(int), typeof(NetworkInstanceId),
            typeof(NetworkInstanceId), typeof(string), typeof(Vector3), typeof(int), typeof(Vector3)})]
        static bool Prefix(int amount, NetworkInstanceId target, NetworkInstanceId source, string weapon, Vector3 z, int force, Vector3 hitPosition)
        {
            if (!SetupMap.endGrace)
            {
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(CustomNetworkManager), "HostAMatch")]
        static void Postfix(bool tm)
        {
            SetupMap.isServer = true;
        }

        [HarmonyPatch(typeof(CustomNetworkManager), "OnJoinMatch")]
        static bool Prefix(bool success, ref GG.Shared.Match match, CustomNetworkManager __instance)
        {
            string matchName = match.Name;

            if (matchName.Contains("| CM: "))
            {
                string customMapName = matchName.Substring(matchName.LastIndexOf(':') + 2);

                if (MapMakerPlugin.mapsList.ContainsKey(customMapName))
                {
                    SetupMap.joinedCustomMap = true;

                    SetupMap.selectedMap = MapMakerPlugin.mapsList[customMapName];

                    return true;
                }

                __instance.gameLog.NewLog("Custom Map not Found!");

                return false;
            }

            SetupMap.isServer = false;
            return true;
        }

        [HarmonyPatch(typeof(GGServerManager), "CreateMatch")]
        [HarmonyPrefix]
        static void MatchNamePatch(ref string name)
        {
            if (SetupMap.selectedMapName != null)
            {
                Debug.Log("MatchData Edited");
                name += " | CM: " + SetupMap.selectedMapName;
            }
        }

        [HarmonyPatch(typeof(Health), "Respawn")]
        [HarmonyPostfix]
        static void RemoveSpectatorCulling()
        {
            if (SetupMap.joinedCustomMap || (SetupMap.selectedMap != null && SetupMap.isServer))
            {
                GameObject specCam = GameObject.Find("PlayerFreeFlyCamera(Clone)");

                if (specCam)
                {
                    specCam.GetComponent<Camera>().useOcclusionCulling = false;
                }
            }
        }
    }

    public static class PathExtension
    {
        public static string CombinePathWith(this string path1, string path2)
        {
            return Path.Combine(path1, path2);
        }
    }
}
