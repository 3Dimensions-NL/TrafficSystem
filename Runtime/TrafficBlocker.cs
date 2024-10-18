using UnityEngine;
namespace _3Dimensions.TrafficSystem.Runtime
{
    [RequireComponent(typeof(BoxCollider))]
    public class TrafficBlocker : MonoBehaviour
    {
        private BoxCollider _boxCollider;
        private void Awake()
        {
            _boxCollider = GetComponent<BoxCollider>();
        }

        public void SetBlockerState(bool blocked)
        {
            _boxCollider.enabled = blocked;
        }
    }
}
