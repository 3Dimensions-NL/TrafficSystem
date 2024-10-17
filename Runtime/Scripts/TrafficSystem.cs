using System.Collections.Generic;
using UnityEngine;

namespace _3Dimensions.TrafficSystem
{
    public class TrafficSystem : MonoBehaviour
    {
        private static TrafficSystem _instance;
        public static TrafficSystem Instance
        {
            get
            {
                if (!_instance) _instance = FindObjectOfType<TrafficSystem>(true);
                return _instance;
            }
        }
        
        
        public int spawnLimit = 20;
        public List<GameObject> spawnedVehicles = new();

        [Header("Auto Connections")] public float autoLaneConnectDistance = 0.2f;


        private void OnDisable()
        {
            ResetTraffic();
        }

        private void OnDestroy()
        {
            ResetTraffic();
        }

        public void ResetTraffic()
        {
            foreach (GameObject vehicle in spawnedVehicles)
            {
                Destroy(vehicle);
            }
        }

        #if UNITY_EDITOR
        public void AutoConnectLanes()
        {
            TrafficLane[] trafficLanes = FindObjectsOfType<TrafficLane>(true);
            
            for (int i = 0; i < trafficLanes.Length; i++)
            {
                UnityEditor.EditorUtility.DisplayProgressBar("Connecting Lanes",
                    "Connecting lane " + i + " from " + trafficLanes.Length, (float)i / (float)trafficLanes.Length);

                trafficLanes[i].ConnectToNearestStartPoint();
            }
            
            UnityEditor.EditorUtility.ClearProgressBar();
        }

        public void GenerateSpline()
        {
            TrafficLane[] trafficLanes = FindObjectsOfType<TrafficLane>(true);
            
            for (int i = 0; i < trafficLanes.Length; i++)
            {
                UnityEditor.EditorUtility.DisplayProgressBar("Connecting Lanes",
                    "Connecting lane " + i + " from " + trafficLanes.Length, (float)i / (float)trafficLanes.Length);

                trafficLanes[i].GenerateSpline();
            }
            
            UnityEditor.EditorUtility.ClearProgressBar();
        }
        #endif
    }
    
#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(TrafficSystem))]
    [UnityEditor.CanEditMultipleObjects]
    public class TrafficSystemEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            TrafficSystem myTarget = (TrafficSystem)target;

            DrawDefaultInspector();

            if (GUILayout.Button("Auto Connect Lanes"))
            {
                myTarget.AutoConnectLanes();
            }
            
            if (GUILayout.Button("Generate Splines"))
            {
                myTarget.GenerateSpline();
            }
        }
    }
#endif
}
