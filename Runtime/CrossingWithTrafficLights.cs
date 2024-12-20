using UnityEngine;
namespace _3Dimensions.TrafficSystem.Runtime
{
    public class CrossingWithTrafficLights : MonoBehaviour
    {
        [SerializeField] private TrafficLightSection[] trafficLightSections;
        public int SectionsCount => trafficLightSections.Length;

        public int currentSection;
        private int _lastSection = -1;

        private void LateUpdate()
        {
            if (_lastSection != currentSection)
            {
                if (trafficLightSections[currentSection])
                {
                    for (int i = 0; i < trafficLightSections.Length; i++)
                    {
                        if (i == currentSection)
                        {
                            ChangeSectionState(i, true);
                        }
                        else
                        {
                            ChangeSectionState(i, false);
                        }
                    }
                    
                    // Debug.Log("Traffic Light Section set to: " + currentSection);
                }
                else
                {
                    Debug.LogError("Unknown Traffic Light Section to activate, index: " + currentSection);
                }
                
                _lastSection = currentSection;
            }
        }

        private void ChangeSectionState(int sectionIndex, bool newState)
        {
            trafficLightSections[sectionIndex].SetSectionState(newState);
        } 
    }
}
