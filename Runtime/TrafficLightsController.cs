using System;
using UnityEngine;
using Random = UnityEngine.Random;
namespace _3Dimensions.TrafficSystem.Runtime
{
    public class TrafficLightsController : MonoBehaviour
    {
        public TrafficLightSection[] trafficLightSections;
        public bool useDetectors;
        public float sectionDurationWithTriggers = 5f;
        public float sectionDurationWithTriggersMax = 20f;
        public float sectionDuration = 10f;
        public int SectionsCount => trafficLightSections.Length;
        public float SectionSwitchTime {
            get
            {
                if (useDetectors)
                {
                    if (CurrentSection.Triggered) return sectionDurationWithTriggersMax;
                    return sectionDurationWithTriggers;
                }
                return sectionDuration;
            }
        } 
        
        public TrafficLightSection CurrentSection => trafficLightSections[currentSectionIndex];
        public int currentSectionIndex;
        private int _lastSection = -1;

        public event Action<int> OnSectionIndexChanged;
        
        [Tooltip("If true this controller calculates which section should be active. Turn off if controlled by network controller!"), SerializeField]
        public bool Simulate
        {
            get => _simulate;
            set
            {
                // Update only if the value changes.
                if (_simulate != value)
                {
                    _simulate = value;
                    OnSimulateChanged();
                }
            }
        }

        private bool _simulate = true;

        public float _elapsedTime;

        public bool debug;

        private void Start()
        {
            _elapsedTime = 0;
            
            if (Simulate) currentSectionIndex = Random.Range(0, trafficLightSections.Length);
            _lastSection = currentSectionIndex;
        }

        private void LateUpdate()
        {
            if (Simulate)
            {
                _elapsedTime += Time.deltaTime;
                
                if (_elapsedTime >= SectionSwitchTime)
                {
                    _elapsedTime = 0;
                    currentSectionIndex = NextSection();
                    if (trafficLightSections[currentSectionIndex])
                    {
                        for (int i = 0; i < trafficLightSections.Length; i++)
                        {
                            if (i == currentSectionIndex)
                            {
                                ChangeSectionState(i, true);
                            }
                            else
                            {
                                ChangeSectionState(i, false);
                            }
                        }
                    
                        if (debug)Debug.Log("Traffic Light Section set to: " + CurrentSection.name);
                    }
                    else
                    {
                        Debug.LogError("Unknown Traffic Light Section to activate, index: " + currentSectionIndex);
                        Debug.Log("Next Section: " + CurrentSection.name);
                    }
                    
                    OnSectionIndexChanged?.Invoke(currentSectionIndex);
                    _lastSection = currentSectionIndex;
                }
            }
        }

        public void OverrideCurrentSection(int sectionIndex)
        {
            currentSectionIndex = sectionIndex;
            
            if (trafficLightSections[currentSectionIndex])
            {
                for (int i = 0; i < trafficLightSections.Length; i++)
                {
                    if (i == currentSectionIndex)
                    {
                        ChangeSectionState(i, true);
                    }
                    else
                    {
                        ChangeSectionState(i, false);
                    }
                }
                    
                if (debug)Debug.Log("Traffic Light Section set to: " + CurrentSection.name);
            }
            else
            {
                Debug.LogError("Unknown Traffic Light Section to activate, index: " + currentSectionIndex);
                Debug.Log("Next Section: " + CurrentSection.name);
            }
                    
            _lastSection = currentSectionIndex;
        }
        
        private void ChangeSectionState(int sectionIndex, bool newState)
        {
            if (debug) Debug.Log("Traffic Light Section "+ sectionIndex + "set to: " + newState);
            trafficLightSections[sectionIndex].SetSectionState(newState);
        }
        
        private int NextSection()
        {
            if (useDetectors)
            {
                float longestWaitTime = 0;
                TrafficLightSection sectionLongestWaitTime = null;

                foreach (TrafficLightSection t in trafficLightSections)
                {
                    if (t.TimeTriggered > longestWaitTime && t != CurrentSection)
                    {
                        longestWaitTime = t.TimeTriggered;
                        sectionLongestWaitTime = t;
                    }
                }

                if (sectionLongestWaitTime)
                {
                    //Active longest waiting section
                    if (debug) Debug.Log("Active longest waiting section: " + sectionLongestWaitTime.name);
                    return Array.IndexOf(trafficLightSections, sectionLongestWaitTime);
                }
            }
            
            if (currentSectionIndex >= trafficLightSections.Length - 1)
            {
                return 0;
            }

            return currentSectionIndex + 1;
        }

        private void OnSimulateChanged()
        {
            Debug.Log($"Simulate status updated to: {Simulate}");

            // Stel de waarde van `simulate` door op andere trafficLightSections.
            foreach (var section in trafficLightSections)
            {
                if (section != null)
                {
                    section.trigger.simulate = Simulate;
                }
            }
        }
        
        private void OnDrawGizmos()
        {
#if UNITY_EDITOR
            DrawString(CurrentSection.name + " (" + _elapsedTime.ToString("N1") + ")", transform.position + new Vector3(0, TrafficManager.Instance.gizmosHeight, 0));
#endif
        }
        
#if UNITY_EDITOR
        private void DrawString(string text, Vector3 worldPos, Color? colour = null) {
            UnityEditor.Handles.BeginGUI();

            var restoreColor = GUI.color;

            if (colour.HasValue) GUI.color = colour.Value;
            var view = UnityEditor.SceneView.currentDrawingSceneView;
            Vector3 screenPos = view.camera.WorldToScreenPoint(worldPos);

            if (screenPos.y < 0 || screenPos.y > Screen.height || screenPos.x < 0 || screenPos.x > Screen.width || screenPos.z < 0)
            {
                GUI.color = restoreColor;
                UnityEditor.Handles.EndGUI();
                return;
            }
            
            Vector2 size = GUI.skin.label.CalcSize(new GUIContent(text));
            GUI.Label(new Rect(screenPos.x - (size.x / 2), Screen.height - screenPos.y - (size.y * 4), size.x, size.y), text);
            GUI.color = restoreColor;
            UnityEditor.Handles.EndGUI();
        }
#endif
    }
}
