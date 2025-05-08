using _3Dimensions.TrafficSystem.Runtime;
using UnityEditor;
namespace _3Dimensions.TrafficSystem.Editor
{
    [CustomEditor(typeof(TrafficTrigger))]
    public class TrafficTriggerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // Draw all serialized fields (including public fields) as normal
            DrawDefaultInspector();

            // Then draw your custom read-only field
            TrafficTrigger trafficTrigger = (TrafficTrigger)target;

            // Display property in a disabled state so it's not editable
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.Toggle("Triggered", trafficTrigger.Triggered);
            EditorGUI.EndDisabledGroup();
        }
    }
}