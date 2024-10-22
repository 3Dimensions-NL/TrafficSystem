using System.Globalization;
using UnityEngine;
namespace _3Dimensions.TrafficSystem.Runtime
{

    [SelectionBase]
    public class VehicleAi : MonoBehaviour
    {
        public int currentWaypoint = 0;

        public TrafficLane CurrentLane
        {
            get
            {
                if (currentWaypoint >= _route.waypoints.Count)
                {
                    if (!_route.loop)
                    {
                        Destroy(gameObject);
                    }
                    else
                    {
                        currentWaypoint = 0;
                    }
                    return _lastLane;
                }

                _lastLane = _route.waypoints[currentWaypoint].parentLane;
                return _route.waypoints[currentWaypoint].parentLane;
            }
        }
        private TrafficLane _lastLane;
        public VehicleState vehicleState;
        public bool alignWithGround = true;
        public float currentSpeed;

        public float accelerationSpeed = 5f;
        public float brakeSpeed = 10f;
        public float steeringSpeed = .2f;
        public float collisionDetectionSpeedFactor = 1;
        public float collisionDetectionMin = 1;
        public float collisionDetectionMax = 30;
        public float collisionDiameter = 1f;
        public float stoppingDistance = 2;
        public Transform objectDetector;
        public float trafficSurfaceDetectionHeight = 1;
        
        private Quaternion _lastRotation;
        private Vector3 _lastPosition;
        private float _traveledDistance;
        public float SteeringAngle { get; private set; }
        public bool SteerToLeft { get; private set; }
        
        private TrafficRoute _route;
        private float _delta;

        public bool simulate = true; //TODO true when server, false when not server
        
        private void Awake()
        {
            _route = gameObject.GetComponent<TrafficRoute>();
        }

        // Start is called before the first frame update
        void Start()
        {
            _lastRotation = transform.rotation;
            _lastPosition = transform.position;
            _traveledDistance = 0;
        }

        private void OnDestroy()
        {
            if (TrafficManager.Instance == null) return;
            if (TrafficManager.Instance.spawnedVehicles == null) return;
            if (TrafficManager.Instance.spawnedVehicles.Contains(gameObject))
            {
                TrafficManager.Instance.spawnedVehicles.Remove(gameObject);
            }
            if (_lastLane) _lastLane.trafficInLane.Remove(this);
        }

        void LateUpdate()
        {
            if (!simulate) return;
            ApplyUpdate(Time.deltaTime);
        }

        public void ApplyUpdate(float delta)
        {
            _delta = delta;

            if (_route)
            {
                //Check waypoint
                Vector3 waypointRelativePosition = _route.waypoints[currentWaypoint].transform.position;
                waypointRelativePosition.y = transform.position.y;
                if (Vector3.Distance(transform.position, waypointRelativePosition) < stoppingDistance)
                {
                    currentWaypoint++;
                }
                
                //Check for end of route
                if (_traveledDistance >= _route.Length)
                {
                    if (_route.loop)
                    {
                        _traveledDistance -= _route.Length;
                    }
                    else
                    {
                        Destroy(gameObject);
                    }
                }

                //Handle movement and steering
                Steer();
                HandleCarState();
                HandleMovement();
            }

            if (alignWithGround) AlignWithGround();

            _lastPosition = transform.position;
            _lastRotation = transform.rotation;
        }

        public enum VehicleState {
            Stopped = 0,
            Driving = 1,
            Blocked = 2,
            ObstacleInFront = 3,
            ChangingLanes = 10,
            ForcedDrive = 99
        }
        public VehicleState ActiveVehicleState()
        {
            if (vehicleState == VehicleState.ForcedDrive) return VehicleState.ForcedDrive;
            if (vehicleState == VehicleState.ChangingLanes) return VehicleState.ChangingLanes; //TODO workout lane changes
            if (ObstacleInFront()) return VehicleState.ObstacleInFront;
            if (CurrentLane.blocked) return VehicleState.Blocked;
            return vehicleState;
        }

        private void HandleCarState()
        {
            switch (ActiveVehicleState())
            {
                case VehicleState.Stopped:
                    Brake();
                    break;
                case VehicleState.Driving:
                    Drive();
                    break;
                case VehicleState.Blocked:
                    HandleLaneBlocked();
                    break;
                case VehicleState.ObstacleInFront:
                    Brake();
                    break;
                //TODO Changing lanes
                case VehicleState.ForcedDrive:
                    vehicleState = VehicleState.Driving;
                    Drive();
                    break;
            }
        }

        private void HandleLaneBlocked()
        {
            if (!ObstacleInFront())
            {
                Vector3 objectDetectorPos = objectDetector.transform.position;
                objectDetectorPos.y = 0;
                Vector3 endOfLanePos = CurrentLane.EndPoint.transform.position;
                endOfLanePos.y = 0;

                float distanceToEnd = Vector3.Distance(objectDetectorPos, endOfLanePos);

                if (distanceToEnd < stoppingDistance)
                {
                    Brake();
                }
                else
                {
                    Drive();
                }
            }
        }

        private void Drive()
        {
            float maxSpeed = CurrentLane.speed;

            if (currentSpeed < maxSpeed)
            {
                currentSpeed += accelerationSpeed * _delta;
            }
            else if (currentSpeed > maxSpeed)
            {
                currentSpeed -= (accelerationSpeed) *_delta;
            }
        }

        private void Brake()
        {
            float maxSpeed = CurrentLane.speed;
            currentSpeed = currentSpeed <= 0.01f ? 0 : Mathf.Clamp(Mathf.Lerp(currentSpeed, 0, _delta * brakeSpeed), 0, maxSpeed);
        }

        private void Steer()
        {
            Vector3 target = Vector3.forward;
            Vector3 heading = Vector3.forward;
            float distanceLeft = _route.Length - _traveledDistance;

            if (distanceLeft > stoppingDistance)
            {
                target = _route.GetRoutePosition(_traveledDistance + stoppingDistance);
                heading = target - transform.position;
            }
            else if (_route.loop && distanceLeft <= stoppingDistance)
            {
                //Correction for end of line in a loop
                target = _route.GetRoutePosition(stoppingDistance);
            }
            
            var dotDiright = Vector3.Dot(heading, transform.right);
            if (dotDiright > 0)
            {
                SteerToLeft = false;
            }
            else
            {
                SteerToLeft = true;
            }

            Vector3 steerTargetDirection = new Vector3(target.x, transform.position.y, target.z);
            Quaternion targetRotation = Quaternion.LookRotation(steerTargetDirection - transform.position);
            SteeringAngle = Quaternion.Angle(transform.rotation, targetRotation); //degrees we must travel
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, SteeringAngle * steeringSpeed * currentSpeed * _delta); //steeringTime test left out and replaces by speed
            float angle = SteeringAngle;
            if (SteerToLeft)
            {
                angle = -angle;
            }
            objectDetector.transform.localEulerAngles = new Vector3(0, angle * 1.5f, 0);
            
            _lastRotation = transform.rotation;
        }

        private void HandleMovement()
        {
            if (_route)
            {
                // Debug.Log("currentSpeed = " + currentSpeed);
                if (currentSpeed <= 0.01f) return;
                
                _traveledDistance += currentSpeed * _delta;
                transform.position = _route.GetRoutePosition(_traveledDistance);
                transform.position += transform.forward * (currentSpeed * _delta);
            }
        }


        private bool ObstacleInFront()
        {
            if (Physics.SphereCast(objectDetector.position, collisionDiameter, objectDetector.forward, out RaycastHit hit, DetectionDistance()))
            {
                TrafficBlocker blocker = hit.collider.GetComponent<TrafficBlocker>();
                VehicleAi otherVehicle = hit.collider.GetComponent<VehicleAi>();
                
                if (otherVehicle)
                {
                    return true;
                }
                
                if (blocker)
                {
                    if (CurrentLane.blockersToIgnore.Contains(blocker))
                    {
                        return false;
                    }
                    
                    return true;
                }
                
                return false;
            }
            return false;
        }
        
        private void AlignWithGround()
        {
            Vector3 rayStartPoint = transform.position + (Vector3.up * trafficSurfaceDetectionHeight);
            RaycastHit[] hits = Physics.RaycastAll(rayStartPoint, Vector3.down, trafficSurfaceDetectionHeight * 2);

            if (hits.Length == 0)
            {
                Debug.LogWarning("Could not align with ground, no colliders found.", this);
                return;
            }
            
            bool hitFound = false;
            Vector3 closestHitPoint = Vector3.zero;
            Vector3 closestHitNormal = Vector3.up;
            float shortestHitDistance = float.PositiveInfinity;

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.GetComponent<TrafficSurface>())
                {
                    if (hit.distance < shortestHitDistance)
                    {
                        shortestHitDistance = hit.distance;
                        hitFound = true;
                        closestHitPoint = hit.point;
                        closestHitNormal = hit.normal;
                    }
                }
            }

            if (!hitFound)
            {
                Debug.LogWarning("Could not align with ground, non of the colliders contained a TrafficSurface component.", this);
                return;
            }
            
            transform.position = closestHitPoint;

            Quaternion targetNormalRotation = Quaternion.FromToRotation(transform.up, closestHitNormal) * transform.rotation;
            transform.rotation = Quaternion.RotateTowards(_lastRotation, targetNormalRotation, _delta * 5);

            _lastRotation = transform.rotation;
        }

        private float DetectionDistance()
        {
            return Mathf.Clamp(currentSpeed * collisionDetectionSpeedFactor, collisionDetectionMin,
                collisionDetectionMax);
        }
        
        private Vector3 DetectionPoint()
        {
            return objectDetector.position + (objectDetector.forward * DetectionDistance());
        }

        private void OnDrawGizmos()
        {
            //Detector Gizmo
            Gizmos.color = Color.red;
            Vector3 point = DetectionPoint();
            Gizmos.DrawLine(objectDetector.position, point);
            Gizmos.DrawSphere(point, collisionDiameter);
            
            //Surface Gizmo
            Gizmos.color = Color.blue;
            Vector3 surfaceStart = transform.position + (Vector3.up * trafficSurfaceDetectionHeight);
            Vector3 surfaceEnd = surfaceStart - new Vector3(0, 2 * trafficSurfaceDetectionHeight, 0);
            Gizmos.DrawLine(surfaceStart, surfaceEnd);
            
#if UNITY_EDITOR
            DrawString(vehicleState + "(" + currentSpeed.ToString("n2", CultureInfo.InvariantCulture) + ")", transform.position + new Vector3(0, TrafficManager.Instance.gizmosHeight, 0));
#endif
        }
        
#if UNITY_EDITOR
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
}