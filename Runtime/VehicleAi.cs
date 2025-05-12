using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
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
        public float detectionHeight = 1;
        public float trafficSurfaceDetectionHeight = 1;
        
        private Quaternion _lastRotation;
        private Vector3 _lastPosition;
        private float _traveledDistance;
        private float DistanceLeft => _route ? _route.Length - _traveledDistance : 0;

        public float SteeringAngle { get; private set; }
        public bool SteerToLeft { get; private set; }

        public Transform modelTransform;
        
        private TrafficRoute _route;
        private float _deltaTime;

        public bool simulate = true;
        public bool debug = false;
        
        private Rigidbody _rigidbody;
        private VehicleWheel[] _wheels;
        
        private VehicleWheel _farthestFrontLeftWheel;
        private VehicleWheel _farthestFrontRightWheel;
        private VehicleWheel _farthestRearLeftWheel;
        private VehicleWheel _farthestRearRightWheel;

        
        private void Awake()
        {
            _route = GetComponent<TrafficRoute>();
            _rigidbody = GetComponent<Rigidbody>();
            _wheels = GetComponentsInChildren<VehicleWheel>();

            if (!modelTransform)
            {
                modelTransform = transform.Find("Model");
                if (!modelTransform)
                {
                    if (debug) Debug.LogWarning("No child GameObject named 'Model' found.", this);
                }
            }
            
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
            
            if (_wheels == null || _wheels.Length == 0)
            {
                Debug.LogWarning("No wheels assigned to the vehicle; cannot align with ground.", this);
                return;
            }
            
            // Lookup wheels in local space
            // Stap 1: Categoriseer wielen in groepen
            List<VehicleWheel> frontLeftWheels = new List<VehicleWheel>();
            List<VehicleWheel> frontRightWheels = new List<VehicleWheel>();
            List<VehicleWheel> rearLeftWheels = new List<VehicleWheel>();
            List<VehicleWheel> rearRightWheels = new List<VehicleWheel>();

            foreach (VehicleWheel wheel in _wheels)
            {
                // Bepaal de lokale positie van het wiel ten opzichte van het voertuig
                Vector3 localPosition = transform.InverseTransformPoint(wheel.transform.position);

                // Plaats het wiel in de juiste groep
                if (localPosition.z >= 0) // Voorwielen
                {
                    if (localPosition.x < 0)
                    {
                        frontLeftWheels.Add(wheel); // Linksvoor
                    }
                    else
                    {
                        frontRightWheels.Add(wheel); // Rechtsvoor
                    }
                }
                else // Achterwielen
                {
                    if (localPosition.x < 0)
                    {
                        rearLeftWheels.Add(wheel); // Linksonder
                    }
                    else
                    {
                        rearRightWheels.Add(wheel); // Rechtsonder
                    }
                }
            }

            // Stap 2: Bepaal welk wiel het verste naar voren of achteren zit binnen elke groep
            _farthestFrontLeftWheel = GetFarthestWheel(frontLeftWheels, true); // Verste vooraan
            _farthestFrontRightWheel = GetFarthestWheel(frontRightWheels, true);
            _farthestRearLeftWheel = GetFarthestWheel(rearLeftWheels, false); // Verste achteraan
            _farthestRearRightWheel = GetFarthestWheel(rearRightWheels, false);

            // Hulpmethode om het verste wiel in een lijst te vinden
            VehicleWheel GetFarthestWheel(List<VehicleWheel> wheels, bool isFront)
            {
                VehicleWheel farthestWheel = null;
                float extremeZ = isFront ? float.MinValue : float.MaxValue;

                foreach (VehicleWheel wheel in wheels)
                {
                    Vector3 localPosition = transform.InverseTransformPoint(wheel.transform.position);

                    // Voor voorwielen zoeken we de grootste Z-waarde (meest vooraan).
                    // Voor achterwielen zoeken we de kleinste Z-waarde (meest achteraan).
                    if ((isFront && localPosition.z > extremeZ) || (!isFront && localPosition.z < extremeZ))
                    {
                        extremeZ = localPosition.z;
                        farthestWheel = wheel;
                    }
                }

                return farthestWheel;
            }

            // Check if we found all the needed wheels
            if (_farthestFrontLeftWheel == null || _farthestRearRightWheel == null || _farthestFrontRightWheel == null || _farthestRearLeftWheel == null)
            {
                if (debug) Debug.LogError("Could not determine all extreme wheels for alignment.", this);
            }
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
                Vector3 objectDetectorPos = DetectionStart();
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
            Vector3 steerTarget = DistanceLeft < stoppingDistance + collisionDetectionMin
                ? _route.GetRoutePosition(_route.Length)
                : _route.GetRoutePosition(_traveledDistance + stoppingDistance + collisionDetectionMin);
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
            Vector3 detectionStart = DetectionStart();
            Vector3 detectionTarget = DetectionTarget();
            Vector3 direction = (detectionTarget - detectionStart).normalized;
            float distance = Vector3.Distance(detectionTarget, detectionStart);

            if (Physics.SphereCast(detectionStart, collisionDiameter, direction, out RaycastHit hit, distance))
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
            if (_farthestFrontLeftWheel == null || _farthestRearRightWheel == null || _farthestFrontRightWheel == null || _farthestRearLeftWheel == null)
            {
                Debug.LogError("Could not determine all extreme wheels for alignment.", this);
                return;
            }

            // Raycast to determine hit points for the extreme wheels
            Vector3[] hitPoints = new Vector3[4];
            VehicleWheel[] extremeWheels = { _farthestFrontLeftWheel, _farthestRearRightWheel, _farthestFrontRightWheel, _farthestRearLeftWheel };

            for (int i = 0; i < extremeWheels.Length; i++)
            {
                Vector3 rayStartPoint = extremeWheels[i].transform.position + Vector3.up * trafficSurfaceDetectionHeight;
                if (Physics.Raycast(rayStartPoint, Vector3.down, out RaycastHit hit, trafficSurfaceDetectionHeight * 2))
                {
                    hitPoints[i] = hit.point;
                }
                else
                {
                    Debug.LogWarning($"Could not find ground for wheel {extremeWheels[i].name}.", this);
                    return;
                }
            }

            // Calculate the average hit points for front and rear wheels
            Vector3 frontAverageHitPoint = (hitPoints[0] + hitPoints[2]) / 2; // Front left and front right
            Vector3 rearAverageHitPoint = (hitPoints[1] + hitPoints[3]) / 2;  // Rear left and rear right

            // Calculate forward direction based on height difference between front and rear
            Vector3 forwardDirection = (frontAverageHitPoint - rearAverageHitPoint).normalized;

            // Calculate the average hit point for left and right wheels (determines roll)
            Vector3 leftAverageHitPoint = (hitPoints[0] + hitPoints[3]) / 2; // Front left and rear left
            Vector3 rightAverageHitPoint = (hitPoints[2] + hitPoints[1]) / 2; // Front right and rear right

            // Calculate the right direction (horizontal alignment)
            Vector3 rightDirection = (rightAverageHitPoint - leftAverageHitPoint).normalized;

            // Correct the up vector using the cross product of forward and right direction
            Vector3 upDirection = Vector3.Cross(forwardDirection, rightDirection).normalized;

            // Optionally, adjust the vehicle's position to match the terrain
            Vector3 averagePosition = (frontAverageHitPoint + rearAverageHitPoint + leftAverageHitPoint + rightAverageHitPoint) / 4;
            transform.position = new Vector3(transform.position.x, averagePosition.y, transform.position.z);

            // Apply the calculated rotation based on forward and up direction
            Quaternion targetRotation = Quaternion.LookRotation(forwardDirection, upDirection);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _deltaTime * 150);

            Debug.DrawLine(frontAverageHitPoint, rearAverageHitPoint, Color.blue); // Forward direction
            Debug.DrawLine(leftAverageHitPoint, rightAverageHitPoint, Color.red);  // Right direction
            Debug.DrawRay(transform.position, upDirection * 2, Color.green);       // Up direction
            Debug.Log($"Forward: {forwardDirection}, Right: {rightDirection}, Up: {upDirection}");
            
            _lastRotation = transform.rotation;
        }

        private float DetectionDistance()
        {
            if (Application.isPlaying)
            {
                return Mathf.Clamp(currentSpeed * collisionDetectionSpeedFactor, stoppingDistance + collisionDetectionMin,
                    collisionDetectionMax);
            }

            return collisionDetectionMax;
        }

        private Vector3 DetectionStart()
        {
            Vector3 target = _route
                ? DistanceLeft < collisionDetectionMin
                    ? _route.GetRoutePosition(_route.Length)
                    : _route.GetRoutePosition(_traveledDistance + collisionDetectionMin)
                : transform.position + (transform.forward * collisionDetectionMin);
            
            target.y = transform.position.y + detectionHeight;
            return target;
        }
        
        private Vector3 DetectionTarget()
        {
            float detectionDistance = DetectionDistance();
            Vector3 start = DetectionStart();
            
            Vector3 target = _route ? DistanceLeft < detectionDistance
                ? _route.GetRoutePosition(_route.Length)
                : _route.GetRoutePosition(_traveledDistance + detectionDistance) :
                start + (transform.forward * detectionDistance);
            
            target.y = transform.position.y + detectionHeight;
            return target;
        }

        private void OnDrawGizmos()
        {
            //Detector Gizmo
            Gizmos.color = hasObstacleInFront ? Color.red : Color.green;
            Vector3 start = DetectionStart();
            Vector3 target = DetectionTarget();
            Gizmos.DrawLine(start, target);
            Gizmos.DrawSphere(target, collisionDiameter);
            
            //Stopping distance gizmo
            Gizmos.color = Color.yellow;
            Vector3 stopDirection = (target - start).normalized;
            Vector3 stopPosition = start + (stopDirection * stoppingDistance);
            Gizmos.DrawSphere(stopPosition, collisionDiameter);
            
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