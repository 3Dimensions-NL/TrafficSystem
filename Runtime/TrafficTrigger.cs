using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace _3Dimensions.TrafficSystem.Runtime
{
    [RequireComponent(typeof(BoxCollider))]
    public class TrafficTrigger : MonoBehaviour
    {
        public string trafficTag = "Traffic";
        
        public bool Triggered
        {
            get { return _objectsInTrigger.Count > 0; }
        }

        public float timeTriggered;

        private BoxCollider _boxCollider;
        private List<GameObject> _objectsInTrigger = new();

        public bool simulate = true;
        public bool debug;
        
        private void Awake()
        {
            _boxCollider = GetComponent<BoxCollider>();
            _boxCollider.isTrigger = true;
            timeTriggered = 0;
        }
        
        private void Update()
        {
            if (!simulate) return;
            
            if (Triggered)
            {
                timeTriggered += Time.deltaTime;
            }
            else
            {
                timeTriggered = 0;
            }
            
            _objectsInTrigger = _objectsInTrigger.Where(item => item).ToList();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!simulate) return;
            if (debug) Debug.Log("Enter with tag: " + other.tag);
            if (other.tag.Equals(trafficTag))
            {
                _objectsInTrigger.Add(other.gameObject);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!simulate) return;
            if (debug) Debug.Log("Exit with tag: " + other.tag);
            if (_objectsInTrigger.Contains(other.gameObject)) _objectsInTrigger.Remove(other.gameObject);
        }

        private void OnDrawGizmos()
        {
            _boxCollider = GetComponent<BoxCollider>()
                ? GetComponent<BoxCollider>()
                : gameObject.AddComponent<BoxCollider>();
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawCube(_boxCollider.center, new Vector3(0.2f,0.2f,0.2f));
            
#if UNITY_EDITOR
            DrawString("Wait time: " + timeTriggered.ToString("N1") + ")", transform.position + new Vector3(0, TrafficManager.Instance.gizmosHeight, 0));
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
