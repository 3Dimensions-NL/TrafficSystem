using System.Collections.Generic;
using UnityEngine;
namespace _3Dimensions.TrafficSystem.Runtime
{
#if UNITY_EDITOR
    [ExecuteInEditMode]
    [UnityEditor.CanEditMultipleObjects]
#endif
    public class TrafficCrossing : MonoBehaviour
    {
        public float searchRadius = 15;
        public float waitTime = 6;
        private float _timer = 0;
        public List<TrafficLane> lanes = new List<TrafficLane>();

        [HideInInspector] public Vector3 lastPosition;

        // Start is called before the first frame update
        void Start()
        {
            lastPosition = transform.position;

            UpdateBlockingLanes();
        }

        // Update is called once per frame
        void Update()
        {
            if (AllLanesBlocked())
            {
                _timer += Time.deltaTime;

                if (_timer > waitTime)
                {
                    TrafficLane laneToUnblock = LanesWithLeastVehicles();
                    if (laneToUnblock != null)
                    {
                        if (laneToUnblock.trafficInLane.Count > 0)
                        {
                            laneToUnblock.trafficInLane[0].vehicleState = VehicleAi.VehicleState.ForcedDrive;
                            Debug.Log("Forcing a car to drive");
                        }
                    }

                    if (_timer > waitTime + 0.1f )
                    {
                        _timer = 0;
                    }
                }
            }
        }

        private bool AllLanesBlocked()
        {
            int blockCount = 0;
            foreach (TrafficLane lane in lanes)
            {
                if (lane.blocked)
                {
                    blockCount++;
                }
            }
            if (blockCount == lanes.Count)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private TrafficLane LanesWithLeastVehicles()
        {
            //Search for lane with least amount of vehicles;
            if (lanes.Count > 0)
            {
                TrafficLane testLane = lanes[0];

                for (int i = 0; i < lanes.Count; i++)
                {
                    if (lanes[i].trafficInLane.Count > testLane.trafficInLane.Count)
                    {
                        testLane = lanes[i];
                    }
                }

                return testLane;
            }
            else
            {
                return null;
            }
        }

        public void UpdateBlockingLanes()
        {
            lanes =new List<TrafficLane>();

            TrafficWaypoint[] waypointsInScene = FindObjectsOfType<TrafficWaypoint>();
            List<TrafficWaypoint> waypoints = new List<TrafficWaypoint>();

            //Lookup waypoints in radius
            foreach (TrafficWaypoint wp in waypointsInScene)
            {
                Vector3 waypointPos = new Vector3(wp.transform.position.x, transform.position.y, wp.transform.position.z);

                if (Vector3.Distance(waypointPos, transform.position) < searchRadius)
                {
                    waypoints.Add(wp);
                }
            }

            //Lookup lanes from waypoints with blocking lanes
            foreach (TrafficWaypoint wp in waypoints)
            {
                if (wp.parentLane)
                {
                    if (wp.parentLane.blockedByLanes != null)
                    {
                        if (wp.parentLane.blockedByLanes.Count > 0 && !lanes.Contains(wp.parentLane))
                        {
                            lanes.Add(wp.parentLane);
                        }
                    }
                }
            }
        }
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(TrafficCrossing))]
    [UnityEditor.CanEditMultipleObjects]
    public class TrafficCrossingEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            TrafficCrossing myTarget = (TrafficCrossing)target;

            UnityEditor.EditorGUILayout.HelpBox("Use this script to prevent open crossings from blocking all lanes", 
                UnityEditor.MessageType.Info);

            DrawDefaultInspector();
        }

        private void OnSceneGUI()
        {
            TrafficCrossing myTarget = (TrafficCrossing)target;

            if (myTarget.transform.position != myTarget.lastPosition)
            {
                myTarget.UpdateBlockingLanes();
            }
        }
    }
#endif
}