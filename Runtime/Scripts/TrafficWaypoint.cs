using System.Collections.Generic;
using UnityEngine;

namespace _3Dimensions.TrafficSystem
{
    public class TrafficWaypoint : MonoBehaviour
    {
        public TrafficLane parentLane;

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
            if (!selected)
            {
                Gizmos.color = Color.gray;
                Gizmos.DrawSphere(transform.position, 0.03f);
            }
            else
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(transform.position, 0.05f);

                if (parentLane)
                {
                    if (parentLane.waypoints.Count > 1)
                    {
                        for (int i = 0; i < parentLane.waypoints.Count; i++)
                        {
                            if (parentLane.waypoints[i].transform != transform)
                            {
                                Gizmos.color = Color.green;
                                Gizmos.DrawSphere(parentLane.waypoints[i].transform.position, 0.05f);
                            }
                        }
                    }

                    if (parentLane.blockedByLanes.Count > 0)
                    {
                        Gizmos.color = Color.red;
                        foreach (TrafficLane lane in parentLane.blockedByLanes)
                        {
                            if (lane.waypoints.Count > 1)
                            {
                                for (int i = 0; i < lane.waypoints.Count; i++)
                                {
                                    Gizmos.DrawSphere(lane.waypoints[i].transform.position, 0.03f);
                                    if (i < lane.waypoints.Count - 1)
                                    {
                                        Gizmos.DrawLine(lane.waypoints[i].transform.position,
                                            lane.waypoints[i + 1].transform.position);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
#endif
    }


#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(TrafficWaypoint))]
    public class TrafficWaypointrClass : UnityEditor.Editor
    {
        bool leftControl = false;
        bool rightControl = false;
        bool mouseLeft = false;


        public override void OnInspectorGUI()
        {
            TrafficWaypoint myTarget = (TrafficWaypoint)target;

            myTarget.parentLane = myTarget.GetComponentInParent<TrafficLane>() ? myTarget.GetComponentInParent<TrafficLane>() : null;

            if (myTarget.parentLane)
            {
                if (myTarget.parentLane.waypoints.Count < 4)
                {
                    UnityEditor.EditorGUILayout.HelpBox("To work properly with the smooth curve you need at least 4 waypoints!", 
                        UnityEditor.MessageType.Warning);
                }
            }
            else
            {
                UnityEditor.EditorGUILayout.HelpBox("Waypoint has no parent TrafficLane!", 
                    UnityEditor.MessageType.Error);
            }

            DrawDefaultInspector();
        }

        private void OnSceneGUI()
        {
            TrafficWaypoint myTarget = (TrafficWaypoint)target;

            myTarget.parentLane = myTarget.GetComponentInParent<TrafficLane>() ? myTarget.GetComponentInParent<TrafficLane>() : null;

            Event e = Event.current;

            switch (e.type)
            {
                case EventType.KeyDown:
                    if (Event.current.keyCode == (KeyCode.LeftControl))
                    {
                        leftControl = true;
                    }
                    if (Event.current.keyCode == (KeyCode.RightControl))
                    {
                        rightControl = true;
                    }

                    break;
                case EventType.KeyUp:
                    if (Event.current.keyCode == (KeyCode.LeftControl))
                    {
                        leftControl = false;
                    }
                    if (Event.current.keyCode == (KeyCode.RightControl))
                    {
                        rightControl = false;
                    }
                    break;
                case EventType.MouseDown:
                    if (Event.current.button == 0)
                    {
                        mouseLeft = true;
                    }
                    break;
                case EventType.MouseUp:
                    if (Event.current.button == 0)
                    {
                        mouseLeft = false;
                    }
                    break;
            }

            if (myTarget.parentLane)
            {
                if (leftControl && mouseLeft)
                {
                    List<TrafficWaypoint> selectedWaypoints = new List<TrafficWaypoint>();

                    if (UnityEditor.Selection.transforms.Length > 0)
                    {
                        foreach (Transform tf in UnityEditor.Selection.transforms)
                        {
                            if (tf.GetComponent<TrafficWaypoint>())
                            {
                                selectedWaypoints.Add(tf.GetComponent<TrafficWaypoint>());
                            }
                        }
                    }

                    if (selectedWaypoints.Count > 0)
                    {
                        foreach (TrafficWaypoint waypoint in selectedWaypoints)
                        {
                            waypoint.parentLane = waypoint.GetComponentInParent<TrafficLane>() ? waypoint.GetComponentInParent<TrafficLane>() : null;
                            if (waypoint.parentLane)
                            {
                                if (waypoint.parentLane != myTarget.parentLane && !myTarget.parentLane.endOfLaneConnections.Contains(waypoint.parentLane))
                                {
                                    myTarget.parentLane.endOfLaneConnections.Add(waypoint.parentLane);
                                    break;
                                }
                                else if (waypoint.parentLane != myTarget.parentLane && myTarget.parentLane.endOfLaneConnections.Contains(waypoint.parentLane))
                                {
                                    myTarget.parentLane.endOfLaneConnections.Remove(waypoint.parentLane);
                                    break;
                                }
                            }
                        }
                    }
                }

                if (rightControl && mouseLeft)
                {
                    List<TrafficWaypoint> selectedWaypoints = new List<TrafficWaypoint>();

                    if (UnityEditor.Selection.transforms.Length > 0)
                    {
                        foreach (Transform tf in UnityEditor.Selection.transforms)
                        {
                            if (tf.GetComponent<TrafficWaypoint>())
                            {
                                selectedWaypoints.Add(tf.GetComponent<TrafficWaypoint>());
                            }
                        }
                    }

                    if (selectedWaypoints.Count > 0)
                    {
                        foreach (TrafficWaypoint waypoint in selectedWaypoints)
                        {
                            waypoint.parentLane = waypoint.GetComponentInParent<TrafficLane>() ? waypoint.GetComponentInParent<TrafficLane>() : null;
                            if (waypoint.parentLane)
                            {
                                if (waypoint.parentLane != myTarget.parentLane && !myTarget.parentLane.blockedByLanes.Contains(waypoint.parentLane))
                                {
                                    myTarget.parentLane.blockedByLanes.Add(waypoint.parentLane);
                                    break;
                                }
                                else if (waypoint.parentLane != myTarget.parentLane && myTarget.parentLane.blockedByLanes.Contains(waypoint.parentLane))
                                {
                                    myTarget.parentLane.blockedByLanes.Remove(waypoint.parentLane);
                                    break;
                                }
                            }
                        }
                    }
                }

            }
        }
    }
#endif
}