using System;
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
        public float trafficSurfaceDetectionHeight = 2;
        
        private Quaternion _lastRotation;
        private Vector3 _lastPosition;
        private float _traveledDistance;
        private float DistanceLeft => _route ? _route.Length - _traveledDistance : 0;

        public float SteeringAngle { get; private set; }
        public bool SteerToLeft { get; private set; }

        public Transform modelTransform;
        public float modelAligningSpeed = 100f;
        
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

        private Vector3 _frontLeftOffset, _frontRightOffset, _rearLeftOffset, _rearRightOffset;
        private float _startHeight;
        
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
            
            SimulationMode = Physics.simulationMode;
            
            if (debug) Debug.Log("Position on Start = " + transform.position);
            
            if (_wheels == null || _wheels.Length == 0)
            {
                Debug.LogWarning("No wheels assigned to the vehicle; cannot align with ground.", this);
                return;
            }

            // Assign wheels and determine offsets
            CategorizeAndAssignWheels();

            // Calculate offsets for wheel heights in local space
            if (_farthestFrontLeftWheel) _frontLeftOffset = transform.InverseTransformPoint(_farthestFrontLeftWheel.transform.position);
            if (_farthestFrontRightWheel) _frontRightOffset = transform.InverseTransformPoint(_farthestFrontRightWheel.transform.position);
            if (_farthestRearLeftWheel) _rearLeftOffset = transform.InverseTransformPoint(_farthestRearLeftWheel.transform.position);
            if (_farthestRearRightWheel) _rearRightOffset = transform.InverseTransformPoint(_farthestRearRightWheel.transform.position);

            if (_farthestFrontLeftWheel)  _farthestFrontLeftWheel.groundDetectionDistance = trafficSurfaceDetectionHeight;
            if (_farthestFrontRightWheel)  _farthestFrontRightWheel.groundDetectionDistance = trafficSurfaceDetectionHeight;
            if (_farthestRearLeftWheel)  _farthestRearLeftWheel.groundDetectionDistance = trafficSurfaceDetectionHeight;
            if (_farthestRearRightWheel)  _farthestRearRightWheel.groundDetectionDistance = trafficSurfaceDetectionHeight;

            
            _startHeight = (_frontLeftOffset.y + _frontRightOffset.y + _rearLeftOffset.y + _rearRightOffset.y) / 4;
            AlignWithGround();
        }

        private void OnEnable()
        {
            if (TrafficManager.Instance == null) return;
            if (TrafficManager.Instance.spawnedVehicles == null) return;
            if (!TrafficManager.Instance.spawnedVehicles.Contains(gameObject))
            {
                TrafficManager.Instance.spawnedVehicles.Add(gameObject);
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
                if (_route.waypoints.Count != 0)
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

        public void SetTraveledDistance(float distance)
        {
            _traveledDistance = distance;
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
        
        private void CategorizeAndAssignWheels()
        {
            // Step 1: Categorize wheels into groups
            List<VehicleWheel> frontLeftWheels = new List<VehicleWheel>();
            List<VehicleWheel> frontRightWheels = new List<VehicleWheel>();
            List<VehicleWheel> rearLeftWheels = new List<VehicleWheel>();
            List<VehicleWheel> rearRightWheels = new List<VehicleWheel>();

            foreach (VehicleWheel wheel in _wheels)
            {
                // Determine the local position of the wheel relative to the vehicle
                Vector3 localPosition = transform.InverseTransformPoint(wheel.transform.position);

                // Place the wheel in the correct group
                if (localPosition.z >= 0) // Front wheels
                {
                    if (localPosition.x < 0)
                    {
                        frontLeftWheels.Add(wheel); // Front left
                    }
                    else
                    {
                        frontRightWheels.Add(wheel); // Front right
                    }
                }
                else // Rear wheels
                {
                    if (localPosition.x < 0)
                    {
                        rearLeftWheels.Add(wheel); // Rear left
                    }
                    else
                    {
                        rearRightWheels.Add(wheel); // Rear right
                    }
                }
            }

            // Step 2: Determine which wheel is the furthest forward or backward within each group
            _farthestFrontLeftWheel = GetFarthestWheel(frontLeftWheels, true); // Furthest forward
            _farthestFrontRightWheel = GetFarthestWheel(frontRightWheels, true);
            _farthestRearLeftWheel = GetFarthestWheel(rearLeftWheels, false); // Furthest backward
            _farthestRearRightWheel = GetFarthestWheel(rearRightWheels, false);

            // Helper method to find the furthest wheel in a list
            VehicleWheel GetFarthestWheel(List<VehicleWheel> wheels, bool isFront)
            {
                VehicleWheel farthestWheel = null;
                float extremeZ = isFront ? float.MinValue : float.MaxValue;

                foreach (VehicleWheel wheel in wheels)
                {
                    Vector3 localPosition = transform.InverseTransformPoint(wheel.transform.position);

                    // For front wheels we look for the largest Z value (furthest forward).
                    // For rear wheels we look for the smallest Z value (furthest backward).
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

        private void AlignWithGround()
        {
            // Step 0: Perform multiple raycasts and track the nearest valid hit with a TrafficSurface
            Ray groundRay = new Ray(transform.position + (Vector3.up * trafficSurfaceDetectionHeight), Vector3.down);
            RaycastHit[] hits = Physics.RaycastAll(groundRay, trafficSurfaceDetectionHeight * 2);

            RaycastHit? nearestValidHit = null;
            float nearestDistance = float.MaxValue;

            foreach (var hit in hits)
            {
                if (hit.collider.GetComponent<TrafficSurface>())
                {
                    float distance = Vector3.Distance(transform.position, hit.point);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestValidHit = hit;
                    }
                }
            }

            // If we found a valid hit, adjust the position
            if (nearestValidHit.HasValue)
            {
                // Vector3 pos = new Vector3(transform.position.x, nearestValidHit.Value.point.y, transform.position.z);
                // transform.position = pos;
            }
            else
            {
                Debug.LogError("Could not find a valid TrafficSurface for alignment.", this);
                return;
            }

            // Ensure wheels exist
            if (_farthestFrontLeftWheel == null || _farthestFrontRightWheel == null ||
                _farthestRearLeftWheel == null || _farthestRearRightWheel == null)
            {
                Debug.LogError("Could not determine all extreme wheels for alignment.", this);
                return;
            }

            // Step 1: Calculate heights for the four wheels
            float frontLeftHeight = _farthestFrontLeftWheel.transform.position.y;
            float frontRightHeight = _farthestFrontRightWheel.transform.position.y;
            float rearLeftHeight = _farthestRearLeftWheel.transform.position.y;
            float rearRightHeight = _farthestRearRightWheel.transform.position.y;

            // Step 2: Calculate average heights for front, rear, left, and right wheels
            float averageFrontHeight = (frontLeftHeight + frontRightHeight) / 2;
            float averageRearHeight = (rearLeftHeight + rearRightHeight) / 2;
            float averageLeftHeight = (frontLeftHeight + rearLeftHeight) / 2;
            float averageRightHeight = (frontRightHeight + rearRightHeight) / 2;

            // Step 3: Adjust the model's position based on wheel average height
            float averageHeight = ((averageFrontHeight + averageRearHeight) / 2);
            if (debug) Debug.Log("Average height = " + averageHeight);
            averageHeight -= _startHeight;
            modelTransform.position = Vector3.Lerp(modelTransform.position, new Vector3(modelTransform.position.x, averageHeight, modelTransform.position.z), _deltaTime * modelAligningSpeed);
            
            // Vector3 localPosition = modelTransform.localPosition;
            // localPosition.y = Mathf.Lerp(modelTransform.localPosition.y, averageHeight, _deltaTime * modelAligningSpeed); // Offset for the root object
            // modelTransform.localPosition = localPosition;

            // Step 4: Calculate pitch (front-to-back tilt)
            float pitchAngle = -Mathf.Atan2(averageFrontHeight - averageRearHeight, _frontLeftOffset.z - _rearLeftOffset.z) * Mathf.Rad2Deg;

            // Step 5: Calculate roll (left-to-right tilt) - Adjust for inverted roll by negating the difference
            float rollAngle = -Mathf.Atan2(averageRightHeight - averageLeftHeight, _frontLeftOffset.x - _frontRightOffset.x) * Mathf.Rad2Deg;
            rollAngle += 180f;

            // Step 6: Combine pitch and roll into a single rotation while preserving yaw
            Quaternion targetRotation = Quaternion.Euler(
                pitchAngle,
                modelTransform.localEulerAngles.y, // Preserve yaw (heading)
                rollAngle
            );

            // Step 7: Smoothly apply the calculated rotation to modelTransform
            modelTransform.localRotation = Quaternion.RotateTowards(modelTransform.localRotation, targetRotation, _deltaTime * modelAligningSpeed);

            // Debugging visualization
            if (debug)
            {
                Debug.DrawLine(_farthestFrontLeftWheel.transform.position, _farthestFrontRightWheel.transform.position, Color.blue); // Front
                Debug.DrawLine(_farthestRearLeftWheel.transform.position, _farthestRearRightWheel.transform.position, Color.red);   // Rear
                Debug.DrawLine(_farthestFrontLeftWheel.transform.position, _farthestRearLeftWheel.transform.position, Color.green); // Left
                Debug.DrawLine(_farthestFrontRightWheel.transform.position, _farthestRearRightWheel.transform.position, Color.yellow); // Right
                Debug.Log($"Aligning vehicle: Pitch={pitchAngle}, Roll={rollAngle}, Nearest TrafficSurface Distance={nearestDistance}");
            }
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