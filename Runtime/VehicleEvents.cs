using UnityEngine;
using UnityEngine.Events;

namespace _3Dimensions.TrafficSystem.Runtime
{
    public class VehicleEvents : MonoBehaviour
    {
        public VehicleAi vehicleAi;
        
        public float obstacleDetectionCooldown = 0.5f;
        public UnityEvent onObstacleDetected;
        public UnityEvent onNoObstacleDetected;

        private bool _previousHasObstacleInFront;
        private float _cooldownTimer;

        private void Update()
        {
            if (!vehicleAi) return;
            
            if (vehicleAi.hasObstacleInFront != _previousHasObstacleInFront)
            {
                _cooldownTimer = 0;
                _previousHasObstacleInFront = vehicleAi.hasObstacleInFront;

                if (vehicleAi.hasObstacleInFront)
                {
                    onObstacleDetected?.Invoke();
                }
                else if (_cooldownTimer >= obstacleDetectionCooldown)
                {
                    onNoObstacleDetected?.Invoke();
                }
            }
            else
            {
                _cooldownTimer += Time.deltaTime;
                if (_cooldownTimer >= obstacleDetectionCooldown && !vehicleAi.hasObstacleInFront)
                {
                    _previousHasObstacleInFront = vehicleAi.hasObstacleInFront;
                    onNoObstacleDetected?.Invoke();
                }
            }
        }
    }
}
