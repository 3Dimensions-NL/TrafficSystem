using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace _3Dimensions.TrafficSystem
{
    [RequireComponent(typeof(BoxCollider))]
    public class TrafficTrigger : MonoBehaviour
    {
        [SerializeField] private LayerMask layerMask;

        public bool Triggered
        {
            get { return _objectsInTrigger.Count > 0; }
        }

        public float TimeTriggered => _timeTriggered;
        private float _timeTriggered = 0;

        private BoxCollider _boxCollider;
        private List<GameObject> _objectsInTrigger = new();
        
        private void Awake()
        {
            _boxCollider = GetComponent<BoxCollider>();
            _boxCollider.isTrigger = true;
            _timeTriggered = 0;
        }

        private void Update()
        {
            if (Triggered)
            {
                _timeTriggered += Time.deltaTime;
            }
            else
            {
                _timeTriggered = 0;
            }

            _objectsInTrigger = _objectsInTrigger.Where(item => item != null).ToList();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsInLayerMask(other.gameObject, layerMask))
            {
                _objectsInTrigger.Add(other.gameObject);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (_objectsInTrigger.Contains(other.gameObject)) _objectsInTrigger.Remove(other.gameObject);
        }

        public bool IsInLayerMask(GameObject obj, LayerMask layerMask)
        {
            return ((layerMask.value & (1 << obj.layer)) > 0);
        }

        private void OnDrawGizmos()
        {
            _boxCollider = GetComponent<BoxCollider>()
                ? GetComponent<BoxCollider>()
                : gameObject.AddComponent<BoxCollider>();
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawCube(_boxCollider.center, new Vector3(0.2f,0.2f,0.2f));
        }
    }
}
