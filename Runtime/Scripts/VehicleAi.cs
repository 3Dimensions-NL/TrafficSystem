using FishNet;
using UnityEngine;

namespace _3Dimensions.TrafficSystem
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
        public LayerMask groundLayerMask;
        public LayerMask obstacleLayerMask;
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
        
        private Quaternion lastRotation;
        private Vector3 lastPosition;
        private float traveledDistance;
        public float SteeringAngle { get; private set; }
        public bool steerToLeft { get; private set; }

        public RaycastHit obstacleHit;
        private float _obstacleHitDistance;
        
        private TrafficRoute _route;
        private float _delta;

        public bool updateTime; //TODO true when server, false when not server
        
        private void Awake()
        {
            _route = gameObject.GetComponent<TrafficRoute>();
        }

        // Start is called before the first frame update
        void Start()
        {
            lastRotation = transform.rotation;
            lastPosition = transform.position;
            traveledDistance = 0;
        }

        private void OnDestroy()
        {
            if (TrafficSystem.Instance == null) return;
            if (TrafficSystem.Instance.spawnedVehicles == null) return;
            if (TrafficSystem.Instance.spawnedVehicles.Contains(gameObject))
            {
                TrafficSystem.Instance.spawnedVehicles.Remove(gameObject);
            }
            if (_lastLane) _lastLane.trafficInLane.Remove(this);
        }

        void LateUpdate()
        {
            if (!updateTime) return;
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
                if (traveledDistance >= _route.Length)
                {
                    if (_route.loop)
                    {
                        traveledDistance -= _route.Length;
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
            float distanceLeft = _route.Length - traveledDistance;

            if (distanceLeft > stoppingDistance)
            {
                target = _route.GetRoutePosition(traveledDistance + stoppingDistance);
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
                steerToLeft = false;
            }
            else
            {
                steerToLeft = true;
            }

            Vector3 steerTargetDirection = new Vector3(target.x, transform.position.y, target.z);
            Quaternion targetRotation = Quaternion.LookRotation(steerTargetDirection - transform.position);
            SteeringAngle = Quaternion.Angle(transform.rotation, targetRotation); //degrees we must travel
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, SteeringAngle * steeringSpeed * currentSpeed * _delta); //steeringTime test left out and replaces by speed
            float angle = SteeringAngle;
            if (steerToLeft)
            {
                angle = -angle;
            }
            objectDetector.transform.localEulerAngles = new Vector3(0, angle * 1.5f, 0);
            lastRotation = transform.rotation;
        }

        private void HandleMovement()
        {
            if (_route)
            {
                // Debug.Log("currentSpeed = " + currentSpeed);
                if (currentSpeed <= 0.01f) return;
                
                traveledDistance += currentSpeed * _delta;
                transform.position = _route.GetRoutePosition(traveledDistance);
                transform.position += transform.forward * (currentSpeed * _delta);
            }
        }

        private bool ObstacleInFront()
        {
            if (Physics.SphereCast(objectDetector.position, collisionDiameter, objectDetector.forward, out obstacleHit, DetectionDistance()))
            {
                if (IsInLayerMask(obstacleHit.collider.gameObject, obstacleLayerMask))
                {
                    TrafficBlocker blocker = obstacleHit.collider.gameObject.GetComponent<TrafficBlocker>();
                    
                    if (blocker)
                    {
                        if (CurrentLane.blockersToIgnore.Contains(blocker))
                        {
                            return false;
                        }
                    }
                    _obstacleHitDistance = Vector3.Distance(objectDetector.transform.position, obstacleHit.point);
                    return true;
                }

                _obstacleHitDistance = float.PositiveInfinity;
                return false;
            }

            _obstacleHitDistance = float.PositiveInfinity;
            return false;
        }
        
        private void AlignWithGround()
        {
            RaycastHit hit;
            Vector3 rayStartpoint = transform.position + (Vector3.up * .5f);
            if (Physics.Raycast(rayStartpoint, Vector3.down, out hit, 10, groundLayerMask))
            {
                transform.position = hit.point;

                Quaternion targetNormalRotation = Quaternion.FromToRotation(transform.up, hit.normal) * transform.rotation;
                transform.rotation = Quaternion.RotateTowards(lastRotation, targetNormalRotation, _delta * 5);

                lastRotation = transform.rotation;
            }
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
            Gizmos.color = Color.red;
            Vector3 point = DetectionPoint();
            Gizmos.DrawLine(objectDetector.position, point);
            Gizmos.DrawSphere(point, collisionDiameter);
        }
        
        public bool IsInLayerMask(GameObject obj, LayerMask layerMask)
        {
            return ((layerMask.value & (1 << obj.layer)) > 0);
        }
    }
}