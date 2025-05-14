using System.Collections.Generic;
using UnityEngine;
namespace _3Dimensions.TrafficSystem.Runtime
{
    public class TrafficManager : MonoBehaviour
    {
        private static TrafficManager _instance;
        public static TrafficManager Instance
        {
            get
            {
                if (!_instance) _instance = FindObjectOfType<TrafficManager>(true);
                return _instance;
            }
        }
        
        
        public int spawnLimit = 20;
        public List<GameObject> spawnedVehicles = new List<GameObject>();

        [Header("Auto Connections")] public float autoLaneConnectDistance = 1.5f;

        public float gizmosScale = 2;
        public float gizmosHeight = 1;

        private void Awake()
        {
            spawnedVehicles.Clear();
        }

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
                trafficLanes[i].autoConnectEndpointDistance = autoLaneConnectDistance;
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
    [UnityEditor.CustomEditor(typeof(TrafficManager))]
    [UnityEditor.CanEditMultipleObjects]
    public class TrafficSystemEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            TrafficManager myTarget = (TrafficManager)target;

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
