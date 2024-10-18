using UnityEngine;
namespace _3Dimensions.TrafficSystem.Runtime
{
    [CreateAssetMenu(fileName = "New Traffic Type", menuName = "3Dimensions/Traffic/New Traffic Spawn Collection", order = 1)]
    public class TrafficSpawnCollection : ScriptableObject
    {
        public GameObject[] prefabs;
    }
}
