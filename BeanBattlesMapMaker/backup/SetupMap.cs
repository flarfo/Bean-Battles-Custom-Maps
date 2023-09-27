using System;
using System.Timers;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;


namespace BeanBattlesMapMaker
{
    [HarmonyPatch]
    class SetupMap : MonoBehaviour
    {
        //onmatchjoined
        //NetStart == playerspawn (changing netstart works only on server)
        //WeaponSpawnPoint 
        //VehicleSpawnPoint 

        private static GameObject[] gameObjects;

        static string[] destroyObjects = { "Coll", "col", "TP", "tp", "Post", "FENCE","Fence", "fence", "Concrete"};

        private static GameObject playerCamera;
        private static SetUpLocalPlayer localPlayer;

        public static string selectedMap;
        public static float graceTime;
        private static string curMapName;

        private static readonly Timer timer = new Timer();

        private static bool mapCreated = false;
        private static bool endGrace;
        private static bool isServer;
        private static bool doElapsed = true;
        private static bool onBouncer = false;

        private static float bounceValue = 0;

        private static readonly List<GameObject> playerSpawns = new List<GameObject>();
        private static readonly List<GameObject> weaponSpawns = new List<GameObject>();
        private static readonly List<GameObject> vehicleSpawns = new List<GameObject>();
        private static readonly List<GameObject> newWeaponSpawns = new List<GameObject>();

        private static AssetBundle myLoadedAssetBundle;

        //when player joins, sets up map for them
        [HarmonyPatch(typeof(SetUpLocalPlayer), "CallCmdSetUpServerPlayer")]
        static void Postfix(SetUpLocalPlayer __instance)
        {
            //Log all game object names, delete those in destroyObjects
            if (selectedMap != null)
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

                curMapName = FindObjectOfType<MapCPointManager>().mapName;

                mapCreated = true;

                LoadTerrain();
                SpawnObjects();

                playerCamera.GetComponent<Camera>().useOcclusionCulling = false;
            }
        }

        //when player leaves, makes sure map data is reset
        //causes player to not disconnect
        [HarmonyPatch(typeof(CustomNetworkManager), "myDisconnect")]
        static void Postfix()
        {
            if (myLoadedAssetBundle)
            {
                myLoadedAssetBundle.Unload(true);
            }
            
            mapCreated = false;
        }

        [HarmonyPatch(typeof(ServerManager), "SpawnPlayers")]
        static void Prefix(ServerManager __instance)
        {
            localPlayer = FindObjectOfType<SetUpLocalPlayer>();

            if (mapCreated)
            {
                __instance.currentStage.playerSpawns = playerSpawns.ToArray();
                __instance.currentStage.plusSpawns = playerSpawns.ToArray();
                __instance.currentStage.teamSpawns = playerSpawns.ToArray();

                __instance.currentStage.numberOfWeaponSpawns = CreateObject.weaponSpawnCount;
                __instance.currentStage.numberOfVehicleSpawns = CreateObject.vehicleSpawnCount;
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

        [HarmonyPatch(typeof(Health), "TakeDamage", new[] {typeof(int), typeof(NetworkInstanceId),
            typeof(NetworkInstanceId), typeof(string), typeof(Vector3), typeof(int), typeof(Vector3)})]
        static bool Prefix(int amount, NetworkInstanceId target, NetworkInstanceId source, string weapon, Vector3 z, int force, Vector3 hitPosition)
        {
            if (!endGrace)
            {
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(CustomNetworkManager), "HostAMatch")]
        static void Postfix(bool tm)
        {
            isServer = true;
        }

        [HarmonyPatch(typeof(CustomNetworkManager), "OnJoinMatch")]
        static void Postfix(CustomNetworkManager __instance)
        {
            isServer = false;
        }

        [HarmonyPatch(typeof(EnemySpawner), "RemoveSpawnsTooFar")]
        static bool Prefix()
        {
            if (mapCreated)
            {
                Debug.Log("Removed Spawns too Far");
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(Movement), "OnTriggerStay")]
        static void Prefix(Movement __instance, Collider other)
        {
            if (mapCreated)
            {
                if (__instance.triggered)
                {
                    if (other.name.Contains("bouncer"))
                    {
                        onBouncer = true;
                        bounceValue = float.Parse(other.name.Split(' ', '(')[1]) * 45;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Movement), "OnTriggerExit")]
        static void Postfix(Collider other)
        {
            if (mapCreated)
            {
                if (other.name.Contains("bouncer"))
                {
                    onBouncer = false;
                }
            }
        }

        [HarmonyPatch(typeof(Movement), "Jump")]
        static void Postfix(Movement __instance)
        {
            if (onBouncer)
            {
                if (Input.GetKeyDown(__instance.PlayerJump))
                {
                     __instance.RB.AddForce(new Vector3(0, bounceValue, 0));
                }
            } 
        }

        static void LoadTerrain()
        {
            myLoadedAssetBundle = AssetBundle.LoadFromFile(Directory.GetFiles(selectedMap + "AssetBundle")[0]);

            GameObject newTerrain = myLoadedAssetBundle.LoadAsset<GameObject>("Terrain.prefab");

            if (newTerrain)
            {
                Instantiate(newTerrain);
            }

            myLoadedAssetBundle.Unload(false);
        }

        static void SpawnObjects()
        {
            string jsonLine;
            System.IO.StreamReader jsonFile = null;

            //List<CreateObject> instObjects = new List<CreateObject>();

            if (File.Exists(Directory.GetFiles(selectedMap + "objectData")[0]))
            {
                jsonFile = new StreamReader(Directory.GetFiles(selectedMap + "objectData")[0]);
            }

            while ((jsonLine = jsonFile.ReadLine()) != null)
            {
                CreateObject newObject = JsonUtility.FromJson<CreateObject>(jsonLine);
                Debug.Log("Object Name: " + newObject.objectName);
                if (newObject.objectName.Contains("PlayerSpawn"))
                {
                    Debug.Log("Player Added");
                    CreateObject.playerSpawnLocations.Add(new Vector3(newObject.x, newObject.y, newObject.z));
                    CreateObject.playerSpawnCount++;
                }
                else if (newObject.objectName.Contains("WeaponSpawn"))
                {
                    Debug.Log("Weapon Added");
                    CreateObject.weaponSpawnLocations.Add(new Vector3(newObject.x, newObject.y, newObject.z));
                    CreateObject.weaponSpawnCount++;
                }
                else if (newObject.objectName.Contains("VehicleSpawn"))
                {
                    Debug.Log("Vehicle Added");
                    CreateObject.vehicleSpawnLocations.Add(new Vector3(newObject.x, newObject.y, newObject.z));
                    CreateObject.vehicleSpawnCount++;
                }
                else if (newObject.objectName.Contains("Terrain"))
                {

                }
                /*else
                {
                    instObjects.Add(newObject);
                }*/
            }

            GenerateSpawns();

            /*if (instObjects.Count > 0)
            {
                CreateObject.InstantiateTest(instObjects.ToArray());
            }*/

            myLoadedAssetBundle = AssetBundle.LoadFromFile(Directory.GetFiles(selectedMap + "AssetBundle")[0]);
            gameObjects = myLoadedAssetBundle.LoadAllAssets<GameObject>();

            foreach (GameObject gameObject in gameObjects)
            {
                Instantiate(gameObject);
            }

            myLoadedAssetBundle.Unload(false);
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
                        Debug.Log($"New Spawn Generated at X: {CreateObject.playerSpawnLocations[i].x} Y: {CreateObject.playerSpawnLocations[i].y} Z: {CreateObject.playerSpawnLocations[i].z}");
                    }
                }
                else
                {
                    //iterate through nubmer of custom player spawns, set the location of the player spawn 
                    for (int i = 0; i < CreateObject.playerSpawnCount; i++)
                    {
                        playerSpawns[i].transform.position = new Vector3(CreateObject.playerSpawnLocations[i].x, CreateObject.playerSpawnLocations[i].y, CreateObject.playerSpawnLocations[i].z);
                        Debug.Log($"New Spawn Generated at X: {CreateObject.playerSpawnLocations[i].x} Y: {CreateObject.playerSpawnLocations[i].y} Z: {CreateObject.playerSpawnLocations[i].z}");
                    }
                    //reverse player spawns so that custom ones are at the end, loop through normal player spawns - num custom player spawns
                    //delete all normal player spawns, keep custom player spawn
                    playerSpawns.Reverse();
                    count = playerSpawns.Count;
                    for (int j = 0; j < count - CreateObject.playerSpawnCount; j++)
                    {
                        //delete normal player spawn, remove from map
                        Debug.Log("Player Spawn: " + playerSpawns[0]);
                        Destroy(playerSpawns[0]);
                        playerSpawns.RemoveAt(0);
                        Debug.Log($"Excess Player Spawn Destroyed! {j}");
                    }
                }
                Debug.Log("PLAYER SPAWN COUNT: " + playerSpawns.Count);
            }

            //WEAPON SPAWNS
            if (CreateObject.weaponSpawnCount != 0)
            {
                Debug.Log("WEAPON SPAWN COUNT: " + weaponSpawns.Count);
                if (CreateObject.weaponSpawnCount > weaponSpawns.Count)
                {
                    Debug.Log("More Custom Weapon Spawns");
                    count = weaponSpawns.Count;
                    for (int wNum = 0; wNum < CreateObject.weaponSpawnCount - count; wNum++)
                    {
                        GameObject newObject = Instantiate(weaponSpawns[0]);
                        weaponSpawns.Add(newObject);
                    }
                    for (int k = 0; k < CreateObject.weaponSpawnCount; k++)
                    {
                        weaponSpawns[k].transform.position = new Vector3(CreateObject.weaponSpawnLocations[k].x, CreateObject.weaponSpawnLocations[k].y, CreateObject.weaponSpawnLocations[k].z);
                        Debug.Log($"New Weapon Spawn Generated at X: {CreateObject.weaponSpawnLocations[k].x} Y: {CreateObject.weaponSpawnLocations[k].y} Z: {CreateObject.weaponSpawnLocations[k].z}");
                    }
                }
                else
                {
                    Debug.Log("More Normal Weapon Spawns");
                    for (int k = 0; k < CreateObject.weaponSpawnCount; k++)
                    {
                        weaponSpawns[k].transform.position = new Vector3(CreateObject.weaponSpawnLocations[k].x, CreateObject.weaponSpawnLocations[k].y, CreateObject.weaponSpawnLocations[k].z);
                        Debug.Log($"New Weapon Spawn Generated at X: {CreateObject.weaponSpawnLocations[k].x} Y: {CreateObject.weaponSpawnLocations[k].y} Z: {CreateObject.weaponSpawnLocations[k].z}");
                    }
                    weaponSpawns.Reverse();
                    count = weaponSpawns.Count;
                    for (int l = 0; l < count - CreateObject.weaponSpawnCount; l++)
                    {
                        Destroy(weaponSpawns[0]);
                        weaponSpawns.RemoveAt(0);
                        Debug.Log($"Excess Weapon Spawn Destroyed! {l}");
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
                    Debug.Log("More Custom Spawns");
                    count = vehicleSpawns.Count;
                    for (int vNum = 0; vNum < CreateObject.vehicleSpawnCount - count; vNum++)
                    {
                        GameObject newObject = Instantiate(vehicleSpawns[0]);
                        vehicleSpawns.Add(newObject);
                    }
                    for (int m = 0; m < CreateObject.vehicleSpawnCount; m++)
                    {
                        Debug.Log($"New Vehicle Spawn Generated at X: {CreateObject.vehicleSpawnLocations[m].x} Y: {CreateObject.vehicleSpawnLocations[m].y} Z: {CreateObject.vehicleSpawnLocations[m].z}");
                        vehicleSpawns[m].transform.position = new Vector3(CreateObject.vehicleSpawnLocations[m].x, CreateObject.vehicleSpawnLocations[m].y, CreateObject.vehicleSpawnLocations[m].z);
                    }
                }
                else
                {
                    Debug.Log("More Normal Spawns");
                    for (int m = 0; m < CreateObject.vehicleSpawnCount; m++)
                    {
                        vehicleSpawns[m].transform.position = new Vector3(CreateObject.vehicleSpawnLocations[m].x, CreateObject.vehicleSpawnLocations[m].y, CreateObject.vehicleSpawnLocations[m].z);
                        Debug.Log($"New Vehicle Spawn Generated at X: {CreateObject.vehicleSpawnLocations[m].x} Y: {CreateObject.vehicleSpawnLocations[m].y} Z: {CreateObject.vehicleSpawnLocations[m].z}");
                    }
                    vehicleSpawns.Reverse();
                    count = vehicleSpawns.Count;
                    for (int n = 0; n < count - CreateObject.vehicleSpawnCount; n++)
                    {
                        Debug.Log("Vehicle Spawn: " + vehicleSpawns[0]);
                        Destroy(vehicleSpawns[0]);
                        vehicleSpawns.RemoveAt(0);
                        Debug.Log($"Excess Vehicle Spawn Destroyed! {n}");
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
            /*public float qW;
            public float qX;
            public float qY;
            public float qZ;
            public float sX;
            public float sY;
            public float sZ;
            public float bounceFactor;*/

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
                /*qW = _qW;
                qX = _qX;
                qY = _qY;
                qZ = _qZ;
                sX = _sX;
                sY = _sY;
                sZ = _sZ;
                bounceFactor = _bounceFactor;*/
            }

            /*public static void InstantiateTest(CreateObject[] instObjects)
            {
                GameObject[] gameObjects;
                Dictionary<string, GameObject> objectPrefabs = new Dictionary<string, GameObject>();

                myLoadedAssetBundle = AssetBundle.LoadFromFile(Directory.GetFiles(selectedMap + "AssetBundle")[0]);
                gameObjects = myLoadedAssetBundle.LoadAllAssets<GameObject>();

                for (int j = 0; j < gameObjects.Length; j++)
                {
                    Debug.Log("GameObject Name: " + gameObjects[j].name);
                    objectPrefabs.Add(gameObjects[j].name, gameObjects[j]);
                    //gameobjects have no network identity, throws error which lags game. adding network identity deletes all objects ?
                }

                for (int i = 0; i < instObjects.Length; i++)
                {
                    CreateObject curObject = instObjects[i];
                    string name;
                    if (curObject.objectName.EndsWith(")") && char.IsNumber(curObject.objectName[curObject.objectName.Length - 2]))
                    {
                        name = curObject.objectName.TrimEnd(')');
                        name = name.TrimEnd(new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' });
                        name = name.TrimEnd(new char[] {'(',' '});
                    }
                    else
                    {
                        name = curObject.objectName;
                    }
                    Debug.Log(name);

                    if (objectPrefabs.ContainsKey(name))
                    {
                        GameObject gameObject = objectPrefabs[name];
                        /*if (instObjects[i].doCollision)
                        {
                            gameObject.AddComponent<MeshCollider>();
                            if (instObjects[i].convex)
                            {
                                Debug.Log(instObjects[i].objectName + " Convex");
                                gameObject.GetComponent<MeshCollider>().convex = true;
                            }
                        }
                        if (instObjects[i].bounceFactor > 0)
                        {
                            gameObject.name = "bouncer " + instObjects[i].bounceFactor;
                        }
                        gameObject.transform.localScale = new Vector3(curObject.sX, curObject.sY, curObject.sZ);

                        Instantiate(gameObject, new Vector3(curObject.x, curObject.y, curObject.z),
                            new Quaternion(curObject.qX, curObject.qY, curObject.qZ, curObject.qW));

                        gameObject.transform.localScale = new Vector3(curObject.sX, curObject.sY, curObject.sZ);
                    }
                }

                myLoadedAssetBundle.Unload(false);*/
        }
    }
}
