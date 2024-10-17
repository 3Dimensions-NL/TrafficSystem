﻿using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace _3Dimensions.TrafficSystem
{
#if UNITY_EDITOR
    [ExecuteAlways]
    [UnityEditor.CanEditMultipleObjects]
#endif
    public class TrafficLane : MonoBehaviour
    {
        [Header("Lane details")]
        public float speed = 15;
        public List<TrafficWaypoint> waypoints = new List<TrafficWaypoint>();
        public float autoConnectEndpointDistance = .2f;
        public TrafficWaypoint StartPoint => waypoints[0];
        public TrafficWaypoint EndPoint => waypoints[^1];
        public SplineContainer splineContainer;
        private SplineContainer _splineContainer;
        private Spline _spline;

        public float Length { get; private set; }

        [Header("Traffic connections")]
        public List<TrafficLane> endOfLaneConnections = new List<TrafficLane>();

        [Header("Traffic Rules")]
        public bool blocked = false; //Needed for crossings etc.
        public float blockCooldownTime = 1;
        private float blockCoolDownTimer = 0;
        public List<TrafficBlocker> blockersToIgnore = new();

        public List<TrafficLane> blockedByLanes = new List<TrafficLane>();
        public List<VehicleAi> trafficInLane = new List<VehicleAi>();

        private void Awake()
        {
            CollectWaypoints();
            GenerateSpline();
            Length = CalculatedLength();
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
                    Debug.Log("updating waypoints...");
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
                //Only block automaticly if there are other lanes to give way to
                int trafficCount = 0;
                foreach (TrafficLane lane in blockedByLanes)
                {
                    trafficCount += lane.trafficInLane.Count;
                }

                if (trafficCount > 0)
                {
                    blocked = true;
                    blockCoolDownTimer = 0;
                }
                else if (blockCoolDownTimer < blockCooldownTime)
                {
                    blockCoolDownTimer += Time.deltaTime;
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
            public Vector3 position;
            public Vector3 direction;

            public RoutePoint(Vector3 position, Vector3 direction)
            {
                this.position = position;
                this.direction = direction;
            }
        }

        public float CalculatedLength()
        {
            if (!splineContainer) return 0;
            if (splineContainer.Spline == null) return 0;
            return splineContainer.Spline.GetLength();
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

        private void OnDrawGizmos()
        {
            DrawGizmos(false);

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
        }

        private void OnDrawGizmosSelected()
        {
            DrawGizmos(true);

            if (blockedByLanes.Count > 0)
            {
                Gizmos.color = Color.red;
                foreach (TrafficLane lane in blockedByLanes)
                {
                    if (lane.waypoints.Count > 1)
                    {
                        for (int i = 0; i < lane.waypoints.Count; i++)
                        {
                            Gizmos.DrawSphere(lane.waypoints[i].transform.position, 0.1f);
                        }
                    }
                }
            }
        }

        private void DrawGizmos(bool selected)
        {
            // if (waypoints.Count > 1)
            // {
            //     CachePositionsAndDistances();
            //     Length = CalculatedLength();
            //     // draw path
            //     Gizmos.color = Color.yellow;
            //     Vector3 prev = points[0];
            //
            //     float step = Length / editorVisualisationSubsteps;
            //     for (float dist = 0; dist < Length; dist += step)
            //     {
            //         Vector3 next = GetRoutePosition(dist + step);
            //         Gizmos.DrawLine(prev, next);
            //         prev = next;
            //     }
            // }
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
        }
    }
#endif
}