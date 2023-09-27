using System;
using System.Timers;
using System.IO;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace BeanBattlesMapMaker
{
    [HarmonyPatch]
    public class SetupMap : MonoBehaviour
    {
        private static GameObject[] gameObjects;

        static string[] destroyObjects = { "Coll", "col", "TP", "tp", "Post", "FENCE","Fence", "fence", "Concrete"};

        private static GameObject playerCamera;
        private static SetUpLocalPlayer localPlayer;

        public static string selectedMap;
        public static string selectedMapName;
        public static float graceTime;
        private static string curMapName;

        private static readonly Timer timer = new Timer();

        public static bool mapCreated = false;
        public static bool joinedCustomMap = false;

        public static bool endGrace;
        public static bool isServer;
        private static bool doElapsed = true;

        private static readonly List<GameObject> playerSpawns = new List<GameObject>();
        private static readonly List<GameObject> weaponSpawns = new List<GameObject>();
        private static readonly List<GameObject> vehicleSpawns = new List<GameObject>();
        private static readonly List<GameObject> newWeaponSpawns = new List<GameObject>();

        public static AssetBundle myLoadedAssetBundle;

        public static int? deathZoneScale;

        //when player joins, sets up map for them CallCmdSetUpServerPlayer
        [HarmonyPatch(typeof(SetUpLocalPlayer), "CallCmdSetUpServerPlayer")]
        static void Postfix()
        {
            if (joinedCustomMap)
            {
                CreateCustomMap();
            }
            else if (selectedMap != null && isServer)
            {
                CreateCustomMap();
            }
        }

        public static void CreateCustomMap()
        {
            playerSpawns.Clear();
            weaponSpawns.Clear();
            vehicleSpawns.Clear();
            newWeaponSpawns.Clear();

            CreateObject.playerSpawnLocations.Clear();
            CreateObject.weaponSpawnLocations.Clear();
            CreateObject.vehicleSpawnLocations.Clear();

            CreateObject.playerSpawnCount = 0;
            CreateObject.weaponSpawnCount = 0;
            CreateObject.vehicleSpawnCount = 0;

            gameObjects = FindObjectsOfType<GameObject>();

            Debug.Log("Loading map...");

            for (int i = 0; i < gameObjects.Length; i++)
            {
                if (gameObjects[i].tag == "NetStart" && !gameObjects[i].name.Contains("Team"))
                {
                    playerSpawns.Add(gameObjects[i]);
                }
                else if (gameObjects[i].tag == "WeaponSpawnPoint" && !gameObjects[i].name.Contains("Points"))
                {
                    weaponSpawns.Add(gameObjects[i]);
                }
                else if (gameObjects[i].tag == "VehicleSpawnPoint" && !gameObjects[i].name.Contains("Points"))
                {
                    vehicleSpawns.Add(gameObjects[i]);
                }
                else if (gameObjects[i].name == "Camera")
                {
                    playerCamera = gameObjects[i];
                }
                for (int j = 0; j < destroyObjects.Length; j++)
                {
                    if (gameObjects[i].name.Contains(destroyObjects[j]))
                    {
                        Destroy(gameObjects[i]);
                    }
                }
            }

            gameObjects = FindObjectsOfType<GameObject>();

            for (int k = 0; k < gameObjects.Length; k++)
            {
                if (gameObjects[k].name.Contains("Static"))
                {
                    Destroy(gameObjects[k]);
                }
            }

            MapCPointManager mapManager = FindObjectOfType<MapCPointManager>();
            curMapName = mapManager.mapName;

            mapCreated = true;

            LoadAssets();
            SpawnObjects();

            if (deathZoneScale != null)
            {
                Debug.LogError($"DeathZone Scale Adjusted: {deathZoneScale}");

                mapManager.mapL = (int)deathZoneScale;
                mapManager.mapW = (int)deathZoneScale;

                GameObject.Find("gameManager").GetComponent<GameManager>().dZScale = (int)deathZoneScale;
            }

            playerCamera.GetComponent<Camera>().useOcclusionCulling = false;
        }

        [HarmonyPatch(typeof(EnemySpawner), "NewRound")]
        [HarmonyPrefix]
        static void FixSpawns(EnemySpawner __instance, MapCPointManager stage)
        {
            if (mapCreated)
            {
                __instance.spawnPoints = weaponSpawns.ToArray();
                __instance.vehicleSpawnPoints = vehicleSpawns.ToArray();

                stage.playerSpawns = playerSpawns.ToArray();
                stage.plusSpawns = playerSpawns.ToArray();
                stage.teamSpawns = playerSpawns.ToArray();

                stage.numberOfWeaponSpawns = CreateObject.weaponSpawnCount;
                stage.numberOfVehicleSpawns = CreateObject.vehicleSpawnCount;
            }
        }

        [HarmonyPatch(typeof(ServerManager), "SpawnPlayers")]
        static void Prefix(ServerManager __instance)
        {
            localPlayer = FindObjectOfType<SetUpLocalPlayer>();

            if (MapMakerPlugin.doGrace.isOn)
            {
                graceTime = MapMakerPlugin.graceTime;
            }

            endGrace = true;
            timer.Stop();
            if ((int)graceTime > 0)
            {
                if (isServer)
                {
                    endGrace = false;
                    timer.Interval = (int)graceTime * 1000;
                    timer.AutoReset = false;

                    if (doElapsed)
                    {
                        timer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
                        doElapsed = false;
                    }
                    
                    timer.Enabled = true;
                    localPlayer.CallCmdChat(String.Empty, Color.white, $"[Server] Grace Period Started: {(int)graceTime} Seconds", true, localPlayer.playerConnectionNumber, false, -1); //string.Empty, Color.white, $"[Server] Grace Period Started: {(int)graceTime} Seconds", true, -1, false, -1
                }
            }
        }

        static void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            if (isServer)
            {
                timer.Enabled = false;
                timer.Close();
                localPlayer.CallCmdChat(String.Empty, Color.white, "[Server] Grace Period Ended!", true, localPlayer.playerConnectionNumber, false, -1);
                endGrace = true;
            }
        }

        static void LoadAssets()
        {
            CustomScriptHandler.GetDLLs(selectedMap);

            myLoadedAssetBundle = AssetBundle.LoadFromFile(Directory.GetFiles(Path.Combine(selectedMap, "assetbundle"))[0]);

            GameObject newTerrain = myLoadedAssetBundle.LoadAsset<GameObject>("Terrain.prefab");

            if (newTerrain)
            {
                Instantiate(newTerrain);
            }

            gameObjects = myLoadedAssetBundle.LoadAllAssets<GameObject>();


            foreach (GameObject gameObject in gameObjects)
            {
                if (gameObject.name == "Terrain")
                {
                    continue;
                }

                string customScriptTag = gameObject.name.Substring(gameObject.name.IndexOf("C__") + 3); //gets custom script tag
                customScriptTag = customScriptTag.TrimEnd(new char[] {' ', '(', ')', '1', '2', '3', '4', '5', '6', '7', '8', '9', '0'}); // handling Unity's ObjectName (1) for duplicates

                Debug.Log(customScriptTag);

                if (CustomScriptHandler.loadedScripts.ContainsKey(customScriptTag))
                {
                    foreach (Type customScript in CustomScriptHandler.loadedScripts[customScriptTag])
                    {
                        Debug.Log("Script Added: " + gameObject.name);
                        gameObject.AddComponent(customScript);
                    }
                }

                Instantiate(gameObject);
            }

            myLoadedAssetBundle.Unload(false);
        }

        static void SpawnObjects()
        {
            string jsonLine;
            System.IO.StreamReader jsonFile = null;

            if (File.Exists(Directory.GetFiles(Path.Combine(selectedMap, "objectdata"))[0]))
            {
                using (jsonFile = new StreamReader(Directory.GetFiles(Path.Combine(selectedMap, "objectdata"))[0]))
                {
                    while ((jsonLine = jsonFile.ReadLine()) != null)
                    {
                        if (int.TryParse(jsonLine, out int value))
                        {
                            deathZoneScale = value;

                            continue;
                        }

                        CreateObject newObject = JsonUtility.FromJson<CreateObject>(jsonLine);
                        //Debug.Log("Object Name: " + newObject.objectName);
                        if (newObject.objectName.Contains("PlayerSpawn"))
                        {
                            //Debug.Log("Player Added");
                            CreateObject.playerSpawnLocations.Add(new Vector3(newObject.x, newObject.y, newObject.z));
                            CreateObject.playerSpawnCount++;
                        }
                        else if (newObject.objectName.Contains("WeaponSpawn"))
                        {
                            //Debug.Log("Weapon Added");
                            CreateObject.weaponSpawnLocations.Add(new Vector3(newObject.x, newObject.y, newObject.z));
                            CreateObject.weaponSpawnCount++;
                        }
                        else if (newObject.objectName.Contains("VehicleSpawn"))
                        {
                            //Debug.Log("Vehicle Added");
                            CreateObject.vehicleSpawnLocations.Add(new Vector3(newObject.x, newObject.y, newObject.z));
                            CreateObject.vehicleSpawnCount++;
                        }
                    }
                } 
            }

            GenerateSpawns();
        }

        static void GenerateSpawns()
        {
            int count;

            Debug.Log("Generating Spawns");

            //PLAYER SPAWNS
            if (CreateObject.playerSpawnCount != 0)
            {
                if (CreateObject.playerSpawnCount > playerSpawns.Count)
                {
                    count = playerSpawns.Count;
                    //for each player spawn over the normal amount, instantiate a new one
                    for (int pNum = 0; pNum < CreateObject.playerSpawnCount - count; pNum++)
                    {
                        GameObject newObject = Instantiate(playerSpawns[0]);
                        playerSpawns.Add(newObject);
                    }
                    //iterate through nubmer of custom player spawns, set the location of the player spawn 
                    for (int i = 0; i < CreateObject.playerSpawnCount; i++)
                    {
                        playerSpawns[i].transform.position = new Vector3(CreateObject.playerSpawnLocations[i].x, CreateObject.playerSpawnLocations[i].y, CreateObject.playerSpawnLocations[i].z);
                    }
                }
                else
                {
                    //iterate through nubmer of custom player spawns, set the location of the player spawn 
                    for (int i = 0; i < CreateObject.playerSpawnCount; i++)
                    {
                        playerSpawns[i].transform.position = new Vector3(CreateObject.playerSpawnLocations[i].x, CreateObject.playerSpawnLocations[i].y, CreateObject.playerSpawnLocations[i].z);
                    }
                    //reverse player spawns so that custom ones are at the end, loop through normal player spawns - num custom player spawns
                    //delete all normal player spawns, keep custom player spawn
                    playerSpawns.Reverse();
                    count = playerSpawns.Count;
                    for (int j = 0; j < count - CreateObject.playerSpawnCount; j++)
                    {
                        //delete normal player spawn, remove from map
                        Destroy(playerSpawns[0]);
                        playerSpawns.RemoveAt(0);
                    }
                }
                Debug.Log("PLAYER SPAWN COUNT: " + playerSpawns.Count);
            }

            //WEAPON SPAWNS
            if (CreateObject.weaponSpawnCount != 0)
            {
                if (CreateObject.weaponSpawnCount > weaponSpawns.Count)
                {
                    Debug.Log("Greater Than");

                    count = weaponSpawns.Count;
                    for (int wNum = 0; wNum < CreateObject.weaponSpawnCount - count; wNum++)
                    {
                        GameObject newObject = Instantiate(weaponSpawns[0]);
                        weaponSpawns.Add(newObject);
                    }
                    for (int k = 0; k < CreateObject.weaponSpawnCount; k++)
                    {
                        weaponSpawns[k].transform.position = new Vector3(CreateObject.weaponSpawnLocations[k].x, CreateObject.weaponSpawnLocations[k].y, CreateObject.weaponSpawnLocations[k].z);
                    }
                }
                else
                {
                    Debug.Log("Less Than");

                    for (int k = 0; k < CreateObject.weaponSpawnCount; k++)
                    {
                        weaponSpawns[k].transform.position = new Vector3(CreateObject.weaponSpawnLocations[k].x, CreateObject.weaponSpawnLocations[k].y, CreateObject.weaponSpawnLocations[k].z);
                    }
                    weaponSpawns.Reverse();
                    count = weaponSpawns.Count;
                    for (int l = 0; l < count - CreateObject.weaponSpawnCount; l++)
                    {
                        Destroy(weaponSpawns[0]);
                        weaponSpawns.RemoveAt(0);
                    }
                    count = weaponSpawns.Count;
                    for (int q = 0; q < count; q++)
                    {
                        GameObject newObject = Instantiate(weaponSpawns[0]);
                        newObject.transform.position = weaponSpawns[0].transform.position;
                        newWeaponSpawns.Add(newObject);
                        Destroy(weaponSpawns[0]);
                        weaponSpawns.RemoveAt(0);
                    }
                }

                Debug.Log("WEAPON SPAWN COUNT: " + weaponSpawns.Count);
            }

            //VEHICLE SPAWNS

            if (curMapName != "Wild West")
            {
                if (CreateObject.vehicleSpawnCount > vehicleSpawns.Count)
                {
                    count = vehicleSpawns.Count;
                    for (int vNum = 0; vNum < CreateObject.vehicleSpawnCount - count; vNum++)
                    {
                        GameObject newObject = Instantiate(vehicleSpawns[0]);
                        vehicleSpawns.Add(newObject);
                    }
                    for (int m = 0; m < CreateObject.vehicleSpawnCount; m++)
                    {
                        vehicleSpawns[m].transform.position = new Vector3(CreateObject.vehicleSpawnLocations[m].x, CreateObject.vehicleSpawnLocations[m].y, CreateObject.vehicleSpawnLocations[m].z);
                    }
                }
                else
                {
                    for (int m = 0; m < CreateObject.vehicleSpawnCount; m++)
                    {
                        vehicleSpawns[m].transform.position = new Vector3(CreateObject.vehicleSpawnLocations[m].x, CreateObject.vehicleSpawnLocations[m].y, CreateObject.vehicleSpawnLocations[m].z);
                    }
                    vehicleSpawns.Reverse();
                    count = vehicleSpawns.Count;
                    for (int n = 0; n < count - CreateObject.vehicleSpawnCount; n++)
                    {
                        Destroy(vehicleSpawns[0]);
                        vehicleSpawns.RemoveAt(0);
                    }
                }
                Debug.Log($"VEHICLE SPAWN COUNT: {vehicleSpawns.Count}");
            }
        }

        public class CreateObject
        {
            public string objectName;
            public float x;
            public float y;
            public float z;

            public static List<Vector3> playerSpawnLocations = new List<Vector3>();
            public static List<Vector3> weaponSpawnLocations = new List<Vector3>();
            public static List<Vector3> vehicleSpawnLocations = new List<Vector3>();

            public static int playerSpawnCount = 0;
            public static int weaponSpawnCount = 0;
            public static int vehicleSpawnCount = 0;


            public CreateObject(string _objectName, float _x, float _y, float _z, float _qW, float _qX, float _qY, float _qZ, float _sX, float _sY, float _sZ, float _bounceFactor)
            {
                objectName = _objectName;
                x = _x;
                y = _y;
                z = _z;
            }
        }
    }
}
