using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using BepInEx;
using HarmonyLib;

namespace BeanBattlesMapMaker
{
    [BepInPlugin("flarfo.beanbattles.mapmaker", "Bean Battles Map Maker", "0.0.1")]
    public class MapMakerPlugin : BaseUnityPlugin
    {
        private readonly Harmony harmony = new Harmony("Bean Battles Map Maker");

        public Rect windowRect = new Rect(20, 20, 360, 500);
        public bool showGUI = true;
        public string labelText;
        public float hSliderValue = 0.0f;

        public string[] mapDirectories;
        char[] endDirectory;

        public void Awake()
        {
            Logger.LogInfo("Running Bean Battles Map Maker!");
            harmony.PatchAll();
            endDirectory = Application.dataPath.Split('/')[Application.dataPath.Split('/').Length-1].ToCharArray();
            mapDirectories = Directory.GetDirectories(Application.dataPath.TrimEnd(endDirectory) + "Maps/");
            Debug.Log("MAP DIRECTORY: " + mapDirectories[0]);
        }

        internal void OnGUI()
        {
            if (showGUI)
            {
                GUI.color = Color.white;
                GUI.contentColor = Color.white;
                windowRect = GUI.Window(0, windowRect, DoMyWindow, "Bean Battles Map Selector (F1)");
            }
        }

        void DoMyWindow(int windowId)
        {
            for (int i = 0; i < mapDirectories.Length; i++)
            {
                if (SetupMap.selectedMap == mapDirectories[i] + "/")
                    GUI.color = Color.green;
                else
                    GUI.color = Color.red;
                if (GUI.Button(new Rect(10, 40*(i+1), 340, 30), mapDirectories[i].Split('/')[mapDirectories[i].Split('/').Length - 1]))
                {
                    if (SetupMap.selectedMap == mapDirectories[i] + "/")
                    {
                        SetupMap.selectedMap = null;
                    }
                    else
                    {
                        SetupMap.selectedMap = mapDirectories[i] + "/";
                    }
                }
            }
            GUI.color = Color.white;
            if (GUI.Button(new Rect(10, 425, 340, 30), $"Grace Period: {(int)SetupMap.graceTime}"))
            {

            }
            SetupMap.graceTime = GUI.HorizontalSlider(new Rect(10, 460, 340, 30), SetupMap.graceTime, 0.0F, 120.0f);

            GUI.DragWindow(new Rect(0, 0, 10000, 10000));
        }

        internal void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                showGUI = !showGUI;
            }
        }
    }
}
