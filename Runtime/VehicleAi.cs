using System.Globalization;
using UnityEngine;
using UnityEngine.Serialization;
using Quaternion = UnityEngine.Quaternion;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace _3Dimensions.TrafficSystem.Runtime
{
    [SelectionBase]
    public class VehicleAi : MonoBehaviour
    {
        public SimulationMode SimulationMode { get; private set; } = SimulationMode.FixedUpdate;
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
        public bool hasObstacleInFront;
        public VehicleState targetVehicleState;
        public VehicleState CurrentVehicleState { get; private set; }
        public bool alignWithGround = true;
        public float currentSpeed;

        public float accelerationSpeed = 5f;
        public float brakeSpeed = 10f;
        public float steeringSpeed = 1f;
        public float collisionDetectionSpeedFactor = 1;
        public float collisionDetectionMin = 4;
        public float collisionDetectionMax = 30;
        public float collisionDiameter = 1f;
        public float stoppingDistance = 2;
        public Transform objectDetector;
        public float trafficSurfaceDetectionHeight = 1;
        
        private Quaternion _lastRotation;
        private Vector3 _lastPosition;
        private float _traveledDistance;
        private float DistanceLeft => _route ? _route.Length - _traveledDistance : 0;

        public float SteeringAngle { get; private set; }
        public bool SteerToLeft { get; private set; }
        
        private TrafficRoute _route;
        private float _deltaTime;

        public bool simulate = true;
        public bool debug = false;
        
        private Rigidbody _rigidbody;
        
        private void Awake()
        {
            _route = GetComponent<TrafficRoute>();
            _rigidbody = GetComponent<Rigidbody>();
            
            if (debug) Debug.Log("Position on Awake = " + transform.position);
        }

        // Start is called before the first frame update
        void Start()
        {
            _lastRotation = transform.rotation;
            _lastPosition = transform.position;
            _traveledDistance = 0;
            
            SimulationMode = Physics.simulationMode;
            
            if (debug) Debug.Log("Position on Start = " + transform.position);
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

        void Update()
        {
            if (!simulate) return;
            if (SimulationMode == SimulationMode.Update)
            {
                ApplyUpdate(Time.deltaTime);
            }
        }
        
        void FixedUpdate()
        {
            if (!simulate) return;
            if (SimulationMode == SimulationMode.FixedUpdate)
            {
                ApplyUpdate(Time.fixedDeltaTime);
            }
        }

        public void ApplyUpdate(float deltaTime)
        {
            _deltaTime = deltaTime;

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
                hasObstacleInFront = ObstacleInFront();
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
            if (targetVehicleState == VehicleState.ForcedDrive) return VehicleState.ForcedDrive;
            if (hasObstacleInFront) return VehicleState.ObstacleInFront;
            if (targetVehicleState == VehicleState.ChangingLanes) return VehicleState.ChangingLanes; //todo: workout lane changes
            if (CurrentLane.blocked) return VehicleState.Blocked;
            return targetVehicleState;
        }

        private void HandleCarState()
        {
            CurrentVehicleState = ActiveVehicleState();
            switch (CurrentVehicleState)
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
                //todo: Handle changing lanes
                case VehicleState.ForcedDrive:
                    targetVehicleState = VehicleState.Driving;
                    Drive();
                    break;
            }
        }

        private void HandleLaneBlocked()
        {
            if (!hasObstacleInFront)
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
                currentSpeed += accelerationSpeed * _deltaTime;
            }
            else if (currentSpeed > maxSpeed)
            {
                currentSpeed -= accelerationSpeed * _deltaTime;
            }
        }

        private void Brake()
        {
            float maxSpeed = CurrentLane.speed;
            currentSpeed = currentSpeed <= 0.01f ? 0 : Mathf.Clamp(Mathf.Lerp(currentSpeed, 0, _deltaTime * brakeSpeed), 0, maxSpeed);
        }

        private void Steer()
        {
            // Body rotation
            Vector3 routePointDirection = _route.GetRoutePoint(_traveledDistance).Direction;
            Quaternion targetRotation = Quaternion.LookRotation(routePointDirection);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, steeringSpeed * currentSpeed * _deltaTime);

            // Steer rotation
            Vector3 steerTarget = DistanceLeft < stoppingDistance
                ? _route.GetRoutePosition(_route.Length)
                : _route.GetRoutePosition(_traveledDistance + stoppingDistance);
            Vector3 steerDirection = steerTarget - transform.position;
            Quaternion steerRotation = Quaternion.LookRotation(steerDirection);

            // Steer angle
            var dotToRight = Vector3.Dot(steerDirection, transform.right);
            if (dotToRight > 0)
            {
                SteerToLeft = false;
            }
            else
            {
                SteerToLeft = true;
            }
            SteeringAngle = Quaternion.Angle(_lastRotation, steerRotation); //degrees we must travel
            // float angle = SteeringAngle;
            // if (SteerToLeft)
            // {
            //     angle = -angle;
            // }

            _lastRotation = transform.rotation;
        }

        private void HandleMovement()
        {
            if (_route)
            {
                if (debug) Debug.Log("currentSpeed = " + currentSpeed);
                if (currentSpeed <= 0.01f) return;
                
                _traveledDistance += currentSpeed * _deltaTime;
                transform.position = _route.GetRoutePosition(_traveledDistance);
            }
        }


        private bool ObstacleInFront()
        {
            Vector3 detectionPoint = DetectionPoint();
            Vector3 direction = (detectionPoint - objectDetector.position).normalized;
            float distance = Vector3.Distance(detectionPoint, objectDetector.position);

            if (Physics.SphereCast(objectDetector.position, collisionDiameter, direction, out RaycastHit hit, distance))
            {
                VehicleAi otherVehicle = hit.collider.GetComponent<VehicleAi>();
                if (otherVehicle == this) otherVehicle = null;
                
                if (otherVehicle)
                {
                    if (debug)  Debug.Log(otherVehicle.name + " is in front of vehicle", this);
                    return true;
                }
                
                TrafficBlocker blocker = hit.collider.GetComponent<TrafficBlocker>();
                if (blocker)
                {
                    if (CurrentLane.blockersToIgnore.Contains(blocker))
                    {
                        return false;
                    }
                    
                    if (debug) Debug.Log("blocker in front of vehicle", this);
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
            transform.rotation = Quaternion.RotateTowards(_lastRotation, targetNormalRotation, _deltaTime * 5);

            _lastRotation = transform.rotation;
        }

        private float DetectionDistance()
        {
            if (Application.isPlaying)
            {
                return Mathf.Clamp(currentSpeed * collisionDetectionSpeedFactor, collisionDetectionMin,
                    collisionDetectionMax);
            }

            return collisionDetectionMax;
        }
        
        private Vector3 DetectionPoint()
        {
            float detectionDistance = DetectionDistance(); 
            if (_route == null) return objectDetector.position + (objectDetector.forward * detectionDistance);
            
            Vector3 target = DistanceLeft < detectionDistance
                ? _route.GetRoutePosition(_route.Length)
                : _route.GetRoutePosition(_traveledDistance + detectionDistance);
            
            target.y = objectDetector.position.y;
            return target;
        }

        private void OnDrawGizmos()
        {
            //Detector Gizmo
            Gizmos.color = hasObstacleInFront ? Color.red : Color.green;
            Vector3 point = DetectionPoint();
            Vector3 offset = point - objectDetector.position;
            Gizmos.DrawLine(objectDetector.position, objectDetector.position + offset);
            Gizmos.DrawSphere(point, collisionDiameter);
            
            //Surface Gizmo
            Gizmos.color = Color.blue;
            Vector3 surfaceStart = transform.position + (Vector3.up * trafficSurfaceDetectionHeight);
            Vector3 surfaceEnd = surfaceStart - new Vector3(0, 2 * trafficSurfaceDetectionHeight, 0);
            Gizmos.DrawLine(surfaceStart, surfaceEnd);
            
#if UNITY_EDITOR
            DrawString(CurrentVehicleState + " (" + currentSpeed.ToString("n2", CultureInfo.InvariantCulture) + ")", transform.position + new Vector3(0, TrafficManager.Instance.gizmosHeight, 0));
#endif
        }
        
#if UNITY_EDITOR
        private void DrawString(string text, Vector3 worldPos, Color? colour = null) 
        {
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