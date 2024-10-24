using UnityEngine;
namespace _3Dimensions.TrafficSystem.Runtime
{
    public class VehicleWheel : MonoBehaviour
    {
        public Transform wheelMeshParentTransform;
        public Transform wheelMeshRotatableTransform;
        public SnapAxis steeringAxis = SnapAxis.Y;
        public SnapAxis rotationAxis = SnapAxis.X;
        public float wheelRadius = 0.5f;

        public bool steering;
        public float steeringFactor = 0.8f;
        public float steeringMaxAngle = 45;
        
        private VehicleAi _vehicleAi;
        private Quaternion _oldSteering;
        private Vector3 _oldPos;
        
        // Start is called before the first frame update
        void Start()
        {
            _oldPos = transform.position;
            _vehicleAi = GetComponentInParent<VehicleAi>();
            wheelMeshParentTransform.SetParent(transform);

            _oldSteering = transform.localRotation;
        }

        // Update is called once per frame
        void LateUpdate()
        {
            if (steering)
            {
                float angle = Mathf.Clamp(_vehicleAi.SteeringAngle * steeringFactor, -steeringMaxAngle, steeringMaxAngle);
                if (_vehicleAi.SteerToLeft)
                {
                    angle = -angle;
                }

                Quaternion targetRotation = Quaternion.identity;

                switch (steeringAxis)
                {
                    case SnapAxis.X:
                        targetRotation = Quaternion.Euler(new Vector3(angle, 0, 0));
                        break;
                    case SnapAxis.Y:
                        targetRotation = Quaternion.Euler(new Vector3(0, angle, 0));
                        break;
                    case SnapAxis.Z:
                        targetRotation = Quaternion.Euler(new Vector3(0, 0, angle));
                        break;
                }

                transform.localRotation = Quaternion.RotateTowards(_oldSteering, targetRotation, Time.deltaTime * 40);
                _oldSteering = transform.localRotation;
            }

            Vector3 displacement = transform.position - _oldPos;
            float speed = displacement.magnitude / Time.deltaTime;
            float angularVelocity = speed / wheelRadius;
            float angularVelocityDegrees = angularVelocity * Mathf.Rad2Deg * Time.deltaTime;
            
            switch (rotationAxis)
            {
                case SnapAxis.X:
                    wheelMeshRotatableTransform.Rotate(new Vector3(-angularVelocityDegrees, 0, 0), Space.Self);
                    break;
                case SnapAxis.Y:
                    wheelMeshRotatableTransform.Rotate(new Vector3(0, -angularVelocityDegrees, 0), Space.Self);
                    break;
                case SnapAxis.Z:
                    wheelMeshRotatableTransform.Rotate(new Vector3(0, 0, -angularVelocityDegrees), Space.Self);
                    break;
            }
            
            _oldPos = transform.position;
        }

        public void Recenter()
        {
            if (wheelMeshParentTransform == null) return;
            
            transform.position = wheelMeshParentTransform.position;
        }
        
        #if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, wheelRadius);
        }
        #endif
    }
    
#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(VehicleWheel))]
    [UnityEditor.CanEditMultipleObjects]
    public class VehicleWheelEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            VehicleWheel myTarget = (VehicleWheel)target;

            DrawDefaultInspector();

            if (GUILayout.Button("Recenter on WheelMesh Parent"))
            {
                myTarget.Recenter();
            }
        }
    }
#endif
}