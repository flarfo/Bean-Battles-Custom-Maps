using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MatchUp;
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
        static void Postfix(CustomNetworkManager __instance)
        {
            SetupMap.isServer = false;
        }

        [HarmonyPatch(typeof(Matchmaker), "CreateMatch", new Type[] { typeof(int), typeof(Dictionary<string, MatchData>), typeof(Action<bool, Match>) })]
        [HarmonyPostfix]
        static void MatchNamePatch(ref Dictionary<string, MatchData> matchData)
        {
            if (SetupMap.selectedMapName != null)
            {
                Debug.Log("MatchData Edited");

                matchData["Match Name"] += " | CM: " + SetupMap.selectedMapName;
            }
        }

        [HarmonyPatch(typeof(Matchmaker), "JoinMatch")]
        [HarmonyPrefix]
        static bool MatchNameJoin(ref Match match)
        {
            string matchName = match.matchData["Match Name"].ToString();

            if (matchName.Contains("| CM: "))
            {
                string customMapName = matchName.Substring(matchName.LastIndexOf(':') + 2);

                if (MapMakerPlugin.mapsList.ContainsKey(customMapName))
                {
                    SetupMap.joinedCustomMap = true;

                    SetupMap.selectedMap = MapMakerPlugin.mapsList[customMapName];

                    return true;
                }

                GameObject.Find("NetworkManager").GetComponent<CustomNetworkManager>().gameLog.NewLog("Custom Map not Found!");

                return false;
            }

            return true;
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
