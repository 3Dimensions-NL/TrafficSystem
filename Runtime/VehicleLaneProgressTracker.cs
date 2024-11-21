using UnityEngine;
namespace _3Dimensions.TrafficSystem.Runtime
{
    public class VehicleLaneProgressTracker : MonoBehaviour
    {
        [SerializeField] private VehicleAi vehicleAi;

        [SerializeField] private TrafficLane trafficLane; // A reference to the waypoint-based route we should follow

        [SerializeField] private float lookAheadForTargetOffset = 5;
        // The offset ahead along the route that the we will aim for

        [SerializeField] private float lookAheadForTargetFactor = .1f;
        // A multiplier adding distance ahead along the route to aim for, based on current speed

        [SerializeField] private float lookAheadForSpeedOffset = 10;
        // The offset ahead only the route for speed adjustments (applied as the rotation of the waypoint target transform)

        [SerializeField] private float lookAheadForSpeedFactor = .2f;
        // A multiplier adding distance ahead along the route for speed adjustments


        public enum ProgressStyle
        {
            SmoothAlongRoute,
            PointToPoint,
        }

        public TrafficLane.RoutePoint targetPoint { get; private set; }
        public TrafficLane.RoutePoint speedPoint { get; private set; }
        public TrafficLane.RoutePoint progressPoint { get; private set; }

        public Transform target;

        public float progressDistance { get; private set; } // The progress round the route, used in smooth mode.
        private Vector3 lastPosition; // Used to calculate current speed (since we may not have a rigidbody component)
        private float speed; // current speed of this object (calculated from delta since last frame)

        private void Awake()
        {
            vehicleAi = GetComponent<VehicleAi>();
        }

        void Start()
        {
            if (target == null)
            {
                target = new GameObject(name + " Waypoint Target").transform;
                target.SetParent(transform);
            }


            if (!vehicleAi)
            {
                Debug.LogError("VehicleLaneProgressTracker " + gameObject.name + " has no VehicleAi assigned!");
            }

            lastPosition = transform.position;

            Reset();
        }

        public void Reset()
        {
            progressDistance = 0;
        }

        private void LateUpdate()
        {
            if (!vehicleAi) return;
            
            if (!trafficLane && vehicleAi.CurrentLane)
            {
                trafficLane = vehicleAi.CurrentLane;
            }
            if (trafficLane != vehicleAi.CurrentLane)
            {
                trafficLane = vehicleAi.CurrentLane;
            }
            if (trafficLane == null)
            {
                Reset();
                return;
            }

            // determine the position we should currently be aiming for
            // (this is different to the current progress position, it is a a certain amount ahead along the route)
            // we use lerp as a simple way of smoothing out the speed over time.
            if (Time.deltaTime > 0)
            {
                speed = Mathf.Lerp(speed, (lastPosition - transform.position).magnitude / Time.deltaTime,
                                   Time.deltaTime);
            }

            target.position = trafficLane.GetRoutePoint(progressDistance + lookAheadForTargetOffset + lookAheadForTargetFactor * speed).Position;
            target.rotation = Quaternion.LookRotation(trafficLane.GetRoutePoint(progressDistance + lookAheadForSpeedOffset + lookAheadForSpeedFactor * speed).Direction);

            // get our current progress along the route
            progressPoint = trafficLane.GetRoutePoint(progressDistance);
            Vector3 progressDelta = progressPoint.Position - transform.position;
            if (Vector3.Dot(progressDelta, progressPoint.Direction) < 0)
            {
                progressDistance += progressDelta.magnitude * 0.5f;
            }
            
            lastPosition = transform.position;
        }
        private void OnDrawGizmos()
        {
            if (Application.isPlaying && trafficLane)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, target.position);
                Gizmos.DrawWireSphere(trafficLane.GetRoutePosition(progressDistance), 1);
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(target.position, target.position + target.forward);
            }
        }

    }
}