using UnityEngine;
using UnityEngine.Splines;
namespace _3Dimensions.TrafficSystem.Runtime
{
    [ExecuteAlways]
    public class SplineTest : MonoBehaviour
    {
        public float distance;
        public SplineContainer splineContainer;
        public float length;
        public float t;
        public Vector3 position;
        void Update()
        {
            if (!splineContainer) splineContainer = GetComponent<SplineContainer>();

            if (splineContainer.Spline != null)
            {
                Spline spline = splineContainer.Spline;
                length = spline.GetLength();
                t = distance / length;

                position = spline.EvaluatePosition(t);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(position, 0.2f);
        }
    }
}
