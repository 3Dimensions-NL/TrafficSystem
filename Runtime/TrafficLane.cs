﻿using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.Splines;

namespace _3Dimensions.TrafficSystem.Runtime
{
    [ExecuteAlways]
    public class TrafficLane : MonoBehaviour
    {
        [Header("Lane details")]
        public float speed = 15;
        public List<TrafficWaypoint> waypoints = new List<TrafficWaypoint>();
        public float autoConnectEndpointDistance = .2f;
        public TrafficWaypoint StartPoint { get { return waypoints[0]; } }
        public TrafficWaypoint EndPoint { get { return waypoints[^1]; } }
        public SplineContainer splineContainer;
        private SplineContainer _splineContainer;
        private Spline _spline;

        public float Length { get; private set; }

        [Header("Traffic connections")]
        public List<TrafficLane> endOfLaneConnections = new List<TrafficLane>();

        [Header("Traffic Rules")]
        public bool blocked; //Needed for crossings etc.
        public float blockCooldownTime = 1;
        private float _blockCoolDownTimer;
        public List<TrafficBlocker> blockersToIgnore = new List<TrafficBlocker>();

        public List<TrafficLane> blockedByLanes = new List<TrafficLane>();
        public List<VehicleAi> trafficInLane = new List<VehicleAi>();

        [Header("Spawning on start")] 
        public TrafficSpawnCollection spawnCollection;
        public string spawnTag = "Traffic";

        [Tooltip("Distance between vehicles when spawning multiple vehicles at lane start")]
        public Vector2 spawnDistanceAlongSpline = new Vector2(30, 40);
        public float terrainDetectionHeight = 50;
        private void Awake()
        {
            CollectWaypoints();
            GenerateSpline();
            Length = CalculatedLength();
        }

        private void Start()
        {
            SpawnVehiclesAlongLane();
        }

        // Update is called once per frame
        void Update()
        {
            if (!_splineContainer)
            {
                splineContainer = GetComponent<SplineContainer>()
                    ? GetComponent<SplineContainer>()
                    : gameObject.AddComponent<SplineContainer>();
                _splineContainer = splineContainer;
            }
            
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (waypoints.Count != transform.childCount)
                {
                    waypoints.Clear();
                    //Check children for components
                    CollectWaypoints();
                    GenerateSpline();
                }

                List<TrafficWaypoint> testList = new List<TrafficWaypoint>(GetComponentsInChildren<TrafficWaypoint>());

                int differentCount = 0;
                for (int i = 0; i < testList.Count; i++)
                {
                    if (testList[i] != waypoints[i])
                    {
                        differentCount++;
                    }
                }

                if (differentCount > 0)
                {
                    waypoints = testList;

                    for (int i = 0; i < waypoints.Count; i++)
                    {
                        transform.GetChild(i).gameObject.name = "Waypoint (" + (i + 1) + ")";
                    }
                    
                    GenerateSpline();
                }
            }
#endif

            if (blockedByLanes.Count > 0)
            {
                //Only block automatically if there are other lanes to give way to
                int trafficCount = 0;
                foreach (TrafficLane lane in blockedByLanes)
                {
                    trafficCount += lane.trafficInLane.Count;
                }

                if (trafficCount > 0)
                {
                    blocked = true;
                    _blockCoolDownTimer = 0;
                }
                else if (_blockCoolDownTimer < blockCooldownTime)
                {
                    _blockCoolDownTimer += Time.deltaTime;
                }
                else
                {
                    blocked = false;
                }
            }
        }

        public void CollectWaypoints()
        {
            waypoints.Clear();
            //Check children for components
            for (int i = 0; i < transform.childCount; i++)
            {
                if (transform.GetChild(i).GetComponent<TrafficWaypoint>())
                {
                    waypoints.Add(transform.GetChild(i).GetComponent<TrafficWaypoint>());
                }
                else
                {
                    waypoints.Add(transform.GetChild(i).gameObject.AddComponent<TrafficWaypoint>());
                }
            }
        }

        public void GenerateSpline()
        {
            if (!splineContainer) return;
            if (splineContainer.Spline != null) splineContainer.Spline.Clear();
            if (splineContainer.Spline == null) splineContainer.AddSpline();
            if (splineContainer.Spline == null) return;
            
            splineContainer.Spline.SetTangentMode(TangentMode.AutoSmooth);
            
            //Add waypoints to spline
            for (int i = 0; i < waypoints.Count; i++)
            {
                TrafficWaypoint wp = waypoints[i];
                BezierKnot wpKnot = new BezierKnot(wp.transform.localPosition,Vector3.zero, Vector3.zero,
                    wp.transform.localRotation);
                splineContainer.Spline.Add(wpKnot, TangentMode.AutoSmooth);
            }
            
            Length = CalculatedLength();
        }

        public RoutePoint GetRoutePoint(float dist)
        {
            // position and direction
            Vector3 p1 = GetRoutePosition(dist);
            Vector3 p2 = GetRoutePosition(dist + 0.1f);
            Vector3 delta = p2 - p1;
            return new RoutePoint(p1, delta.normalized);
        }

        public Vector3 GetRoutePosition(float dist)
        {
            if (!splineContainer) return Vector3.zero;
            if (splineContainer.Spline == null) return Vector3.zero;
            float t = dist / Length;
            return splineContainer.Spline.EvaluatePosition(t);
        }

        public struct RoutePoint
        {
            public Vector3 Position;
            public Vector3 Direction;

            public RoutePoint(Vector3 position, Vector3 direction)
            {
                Position = position;
                Direction = direction;
            }
        }

        public float CalculatedLength()
        {
            if (!splineContainer) return 0;
            if (splineContainer.Spline == null) return 0;
            return splineContainer.Spline.GetLength();
        }

        public void SpawnVehiclesAlongLane()
        {
            if (spawnCollection && Application.isPlaying && gameObject.activeInHierarchy)
            {
                // Skip start of lane and spawn vehicles along the lane
                for (float dist = Random.Range(spawnDistanceAlongSpline.x, spawnDistanceAlongSpline.y); dist < Length; dist += Random.Range(spawnDistanceAlongSpline.x, spawnDistanceAlongSpline.y))
                {
                    int randomIndex = Random.Range(0, spawnCollection.prefabs.Length);
                    GameObject vehicle = spawnCollection.prefabs[randomIndex];
                    SpawnVehicle(vehicle, dist);
                }
            }            
        }
        
        private void SpawnVehicle(GameObject prefab, float distance)
        {
            Vector3 testPosition = GetRoutePosition(distance);
            RaycastHit[] hits = Physics.RaycastAll(testPosition + (Vector3.up * (terrainDetectionHeight * 0.5f)), Vector3.down, terrainDetectionHeight);
            RaycastHit? firstTrafficSurfaceHit = hits.FirstOrDefault(hit => hit.collider.GetComponent<TrafficSurface>() != null);

            if (!firstTrafficSurfaceHit.Value.collider)
            {
                Debug.LogWarning("Could not spawn vehicle, no TrafficSurface found.", this);
                return;
            }

            Vector3 startPosition = firstTrafficSurfaceHit.Value.point;
            Vector3 startDirectionPosition = GetRoutePosition(distance + 1);
            Quaternion startRotation = Quaternion.LookRotation(startDirectionPosition - testPosition);
            
            GameObject spawnedVehicle = Instantiate(prefab, startPosition, startRotation);
            spawnedVehicle.tag = spawnTag;
            spawnedVehicle.name = spawnedVehicle.name + "(lane started)";
            
            //Route setup
            TrafficRoute route = spawnedVehicle.GetComponent<TrafficRoute>();
            route.CalculateRoute(this);
                
            //AI Setup
            VehicleAi vehicleAi = spawnedVehicle.GetComponent<VehicleAi>();
            vehicleAi.currentSpeed = speed;
            vehicleAi.targetVehicleState = VehicleAi.VehicleState.Driving;
            vehicleAi.SetTraveledDistance(distance);
            
            trafficInLane.Add(vehicleAi);
        }

#if UNITY_EDITOR
        public void ReverseLane()
        {
            waypoints.Reverse();

            for (int i = 0; i < waypoints.Count; i++)
            {
                waypoints[i].transform.SetSiblingIndex(i);
                waypoints[i].gameObject.name = "Waypoint (" + (i + 1) + ")";
            }
        }

        public void ConnectToNearestStartPoint()
        {
            endOfLaneConnections.Clear();
            TrafficLane[] lanes = FindObjectsOfType<TrafficLane>(true);
            for (int i = 0; i < lanes.Length; i++)
            {
                TrafficWaypoint startPoint = lanes[i].StartPoint;
                float distance = Vector3.Distance(EndPoint.transform.position, startPoint.transform.position);
                
                if (distance < autoConnectEndpointDistance)
                {
                    endOfLaneConnections.Add(startPoint.parentLane);
                }
            }
        }

        public void AddSpawnerAtStart()
        {
            int spawnerCount = FindObjectsOfType<TrafficSpawner>().Length;
            spawnerCount++;
            GameObject newSpawner = new GameObject("Spawner (" + spawnerCount + ")");
            TrafficSpawner trafficSpawner = newSpawner.AddComponent<TrafficSpawner>();
            trafficSpawner.laneToSpawnIn = this;
            trafficSpawner.canSpawn = true;
        }

        private void OnDrawGizmos()
        {
            if (endOfLaneConnections.Count > 0)
            {
                for (int i = 0; i < endOfLaneConnections.Count; i++)
                {
                    if (endOfLaneConnections[i].waypoints.Count > 0)
                    {
                        Gizmos.color = Color.green;
                        Gizmos.DrawLine(waypoints[waypoints.Count - 1].transform.position, endOfLaneConnections[i].waypoints[0].transform.position);
                    }
                }
            }

            if (waypoints.Count > 0)
            {
                Vector3 pos = waypoints[0].transform.position + new Vector3(0, TrafficManager.Instance.gizmosHeight, 0);
                DrawString(speed.ToString(CultureInfo.InvariantCulture), pos, Color.yellow);
                // Gizmos.color = Color.green;
                // Gizmos.DrawSphere(pos, 0.1f);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (blockedByLanes.Count > 0)
            {
                Gizmos.color = Color.red;
                foreach (TrafficLane lane in blockedByLanes)
                {
                    if (lane.waypoints.Count > 1)
                    {
                        for (int i = 0; i < lane.waypoints.Count; i++)
                        {
                            Gizmos.DrawSphere(
                                lane.waypoints[i].transform.position + new Vector3(0, TrafficManager.Instance.gizmosHeight, 0), 
                                0.1f * TrafficManager.Instance.gizmosScale);
                        }
                    }
                }
            }
            
            if (spawnCollection)
            {
                GenerateSpline();
                Length = CalculatedLength();
                
                // Skip start of lane and spawn vehicles along the lane
                for (float dist = Random.Range(spawnDistanceAlongSpline.x, spawnDistanceAlongSpline.y); dist < Length; dist += Random.Range(spawnDistanceAlongSpline.x, spawnDistanceAlongSpline.y))
                {
                    RoutePoint next = GetRoutePoint(dist);
                    Gizmos.DrawSphere(next.Position, 0.5f * TrafficManager.Instance.gizmosScale);
                }
            }
        }
        
        private void DrawString(string text, Vector3 worldPos, Color? colour = null) {
            UnityEditor.Handles.BeginGUI();

            var restoreColor = GUI.color;

            if (colour.HasValue) GUI.color = colour.Value;
            var view = UnityEditor.SceneView.currentDrawingSceneView;
            Vector3 screenPos = view.camera.WorldToScreenPoint(worldPos);

            if (screenPos.y < 0 || screenPos.y > Screen.height || screenPos.x < 0 || screenPos.x > Screen.width || screenPos.z < 0)
            {
                GUI.color = restoreColor;
                UnityEditor.Handles.EndGUI();
                return;
            }

            Vector2 size = GUI.skin.label.CalcSize(new GUIContent(text));
            GUI.Label(new Rect(screenPos.x - (size.x / 2), Screen.height - screenPos.y - (size.y * 4), size.x, size.y), text);
            GUI.color = restoreColor;
            UnityEditor.Handles.EndGUI();
        }
#endif
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(TrafficLane))]
    [UnityEditor.CanEditMultipleObjects]
    public class TrafficLaneEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            TrafficLane myTarget = (TrafficLane)target;

            if (myTarget.waypoints.Count < 4)
            {
                UnityEditor.EditorGUILayout.HelpBox("To work properly with the smooth curve you need at least 4 waypoints!", 
                    UnityEditor.MessageType.Warning);
            }

            DrawDefaultInspector();

            if (GUILayout.Button("Reverse Lane"))
            {
                myTarget.ReverseLane();
            }

            if (GUILayout.Button("Connect to nearest start point"))
            {
                myTarget.ConnectToNearestStartPoint();
            }

            if (GUILayout.Button("Calculate Spline"))
            {
                myTarget.GenerateSpline();
            }
            
            if (GUILayout.Button("Add spawner at start"))
            {
                myTarget.AddSpawnerAtStart();
            }
        }
    }
#endif
}