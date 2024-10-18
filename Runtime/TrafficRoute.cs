using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Random = UnityEngine.Random;

namespace _3Dimensions.TrafficSystem.Runtime
{
    public class TrafficRoute : MonoBehaviour
    {
        public bool loop;
        public TrafficLane startLaneInspector;
        public List<TrafficLane> lanes = new List<TrafficLane>();
        public List<TrafficWaypoint> waypoints = new List<TrafficWaypoint>();
        public Spline spline;
        public TrafficWaypoint LastWaypoint { get { return waypoints[^1]; }}

        public float editorVisualisationDetail = 5;
        public float editorVisualisationSize = 0.1f;
        public float Length { get; private set; }
        
        private void Start()
        {
            CalculateRoute(startLaneInspector);
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
            if (spline == null) return Vector3.zero;
            float t = dist / Length;
            return spline.EvaluatePosition(t);
        }

        public void CalculateRoute(TrafficLane startLane)
        {
            spline.Clear();

            if (startLane)
            {
                //Generate random route from the start lane
                lanes.Clear();
                waypoints.Clear();
                AddLaneToRoute(startLane);
            }
            else
            {
                waypoints.Clear();

                //Generate route from predefined lanes
                for (int l = 0; l < lanes.Count; l++)
                {
                    for (int w = 0; w < lanes[l].waypoints.Count; w++)
                    {
                        waypoints.Add(lanes[l].waypoints[w]);
                    }
                }
            }
            GenerateSpline();
            spline.Closed = loop;
            Length = CalculatedLength();
        }
        
        /// <summary>
        /// Loops through a set of lanes and adds them to the route until it finds no new connected lanes.
        /// </summary>
        /// <param name="laneToAdd"></param>
        private void AddLaneToRoute(TrafficLane laneToAdd)
        {
            if (laneToAdd)
            {
                lanes.Add(laneToAdd);

                //Skip the first waypoint for better smoothing at connections
                for (int i = 1; i < laneToAdd.waypoints.Count; i++)
                {
                    waypoints.Add(laneToAdd.waypoints[i]);
                }
                
                if (laneToAdd.endOfLaneConnections.Count > 0)
                {
                    AddLaneToRoute(laneToAdd.endOfLaneConnections[Random.Range(0, laneToAdd.endOfLaneConnections.Count)]);
                }
            }
        }
        
        private void GenerateSpline()
        {
            spline.SetTangentMode(TangentMode.AutoSmooth);
            spline.Closed = loop;
            
            //Add waypoints to spline
            for (int i = 0; i < waypoints.Count; i++)
            {
                TrafficWaypoint wp = waypoints[i];
                BezierKnot wpKnot = new BezierKnot(wp.transform.position,Vector3.zero, Vector3.zero,
                    wp.transform.rotation);
                spline.Add(wpKnot, TangentMode.AutoSmooth);
            }

            Length = CalculatedLength();
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
            if (spline == null) return 0;
            return spline.GetLength();
        }
        
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            DrawGizmos(false);
        }

        private void OnDrawGizmosSelected()
        {
            DrawGizmos(true);
        }

        private void DrawGizmos(bool selected)
        {
            if (selected)
            {
                if (waypoints.Count > 1)
                {
                    // draw path
                    Gizmos.color = Color.cyan;

                    float steps = Length * editorVisualisationDetail;
                    float step = Length / steps;
                    for (float dist = 0; dist < Length; dist += step)
                    {
                        Vector3 next = GetRoutePosition(dist + step);
                        Gizmos.DrawSphere(next, editorVisualisationSize);
                    }
                }
            }
        }
#endif
    }
    
#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(TrafficRoute))]
    [UnityEditor.CanEditMultipleObjects]
    public class TrafficRouteEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            TrafficRoute myTarget = (TrafficRoute)target;

            DrawDefaultInspector();

            if (GUILayout.Button("Generate Route"))
            {
                myTarget.CalculateRoute(myTarget.startLaneInspector);
            }
        }
    }
#endif
}