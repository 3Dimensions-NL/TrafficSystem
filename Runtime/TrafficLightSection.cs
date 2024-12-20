using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

namespace _3Dimensions.TrafficSystem.Runtime
{
    public class TrafficLightSection : MonoBehaviour
    {
        public TrafficBlocker[] blockers;
        public TrafficTrigger trigger;
        public TrafficLight[] trafficLights;

        public float unblockDelay = 2;
        public float lightSwitchDelay = 1;
        public float TimeTriggered => trigger != null ? trigger.timeTriggered : 0;
        public bool Triggered => trigger != null && trigger.Triggered;

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
            if (newActive) trigger.timeTriggered = 0;
            
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
                yield return new WaitForSeconds(unblockDelay);
                SetBlockers(false);
                SetLights(TrafficLight.State.Green);
                yield return null;
            }
            else
            {
                SetBlockers(true);
                SetLights(TrafficLight.State.Yellow);
                yield return new WaitForSeconds(lightSwitchDelay);
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
