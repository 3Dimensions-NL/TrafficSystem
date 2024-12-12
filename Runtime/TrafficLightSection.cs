using System.Collections;
using UnityEngine;
namespace _3Dimensions.TrafficSystem.Runtime
{
    public class TrafficLightSection : MonoBehaviour
    {
        [SerializeField] private TrafficBlocker[] blockers;
        [SerializeField] private TrafficLight[] trafficLights;

        public bool isActive;
        private bool _isActive;

        private void Start()
        {
            SetBlockers(true);
            SetLights(TrafficLight.State.Red);
        }

        public void SetSectionState(bool newActive)
        {
            StopAllCoroutines();
            if (isActive != newActive)
            {
                isActive = newActive;
                StartCoroutine(SectionRoutine());
            }
        }

        private IEnumerator SectionRoutine()
        {
            if (isActive)
            {
                yield return new WaitForSeconds(2);
                SetBlockers(false);
                SetLights(TrafficLight.State.Green);
                yield return null;
            }
            else
            {
                SetBlockers(true);
                SetLights(TrafficLight.State.Yellow);
                yield return new WaitForSeconds(1);
                SetLights(TrafficLight.State.Red);
                yield return null;
            }

            yield return null;
        } 

        private void SetBlockers(bool newState)
        {
            foreach (var t in blockers)
            {
                t.SetBlockerState(newState);
            }
        }

        private void SetLights(TrafficLight.State newState)
        {
            foreach (var t in trafficLights)
            {
                t.SetState(newState);
            }
        }
    }
}
