using UnityEngine;
namespace _3Dimensions.TrafficSystem.Runtime
{
    public class TrafficLight : MonoBehaviour
    {
        public Material mat_Off;
        public Material mat_Red;
        public Material mat_Yellow;
        public Material mat_Green;

        public MeshRenderer mesh_Red;
        public MeshRenderer mesh_Yellow;
        public MeshRenderer mesh_Green;
        
        public State LightState { get; private set; }

        public enum State
        {
            Off,
            Red,
            Yellow,
            Green
        }

        private void Awake()
        {
            SetState(State.Off);
        }

        public void SetState(State newState)
        {
            LightState = newState;
            switch (newState)
            {
                case State.Off:
                    ChangeMaterial(mesh_Red, mat_Off);
                    ChangeMaterial(mesh_Yellow, mat_Off);
                    ChangeMaterial(mesh_Green, mat_Off);
                    break;
                case State.Red:
                    ChangeMaterial(mesh_Red, mat_Red);
                    ChangeMaterial(mesh_Yellow, mat_Off);
                    ChangeMaterial(mesh_Green, mat_Off);
                    break;
                case State.Yellow:
                    ChangeMaterial(mesh_Red, mat_Off);
                    ChangeMaterial(mesh_Yellow, mat_Yellow);
                    ChangeMaterial(mesh_Green, mat_Off);
                    break;
                case State.Green:
                    ChangeMaterial(mesh_Red, mat_Off);
                    ChangeMaterial(mesh_Yellow, mat_Off);
                    ChangeMaterial(mesh_Green, mat_Green);
                    break;
            }
        }

        private void ChangeMaterial(MeshRenderer mesh, Material mat)
        {
            mesh.sharedMaterial = mat;
        }
    }
}
