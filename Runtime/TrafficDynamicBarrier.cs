using System.Collections;
using UnityEngine;
namespace _3Dimensions.TrafficSystem.Runtime
{
    public class TrafficDynamicBarrier : MonoBehaviour
    {
        [SerializeField] private Transform barrierTransform;
        [SerializeField] private float closedValue;
        [SerializeField] private float openValue;
        [SerializeField] private float duration = 1f;
        
        public bool isOpen;
        private bool _isOpen;
        private float _axisValue;

        private void Start()
        {
            _isOpen = isOpen;
            _axisValue = isOpen ? openValue : closedValue;
        }

        private void LateUpdate()
        {
            if (_isOpen != isOpen)
            {
                StopAllCoroutines();
                _isOpen = isOpen;
                StartCoroutine(OpenCloseRoutine(isOpen, duration));
            }
            
            barrierTransform.localRotation = Quaternion.Euler(new Vector3(_axisValue, 0, 0));
        }
        
        private IEnumerator OpenCloseRoutine(bool open, float lerpDuration)
        {
            float startValue = barrierTransform.localEulerAngles.x;
            float targetValue = open ? openValue : closedValue;
            
            float timeElapsed = 0;
            
            while (timeElapsed < lerpDuration)
            {
                _axisValue = Mathf.Lerp(startValue, targetValue, timeElapsed / lerpDuration);
                timeElapsed += Time.deltaTime;
                yield return null;
            }
            
            _axisValue = targetValue;
            
            yield return null;
        }
    }
}
