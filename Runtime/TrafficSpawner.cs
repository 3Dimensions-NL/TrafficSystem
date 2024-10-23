using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Random = UnityEngine.Random;

namespace _3Dimensions.TrafficSystem.Runtime
{
    public class TrafficSpawner : MonoBehaviour
    {
        public TrafficSpawnCollection spawnCollection;
        public TrafficLane laneToSpawnIn;

        public bool spawnAtStart = true;
        public bool spawnContinuously = true;
        public float minSpawnTime = 5;
        public float maxSpawnTime = 15;
        public VehicleAi.VehicleState startState = VehicleAi.VehicleState.Driving;
        
        public UnityEvent onSpawn; 

        public bool canSpawn = true; //TODO true when server

        public Action<GameObject> OnInstantiateGameObject; 

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                if (laneToSpawnIn)
                {
                    if (spawnAtStart)
                    {
                        StartCoroutine(SpawnVehicleRoutine());
                    }
                }
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
        }

        private void OnDrawGizmos()
        {
            if (laneToSpawnIn)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawSphere(laneToSpawnIn.waypoints[0].transform.position, .2f);
                if (transform.position != laneToSpawnIn.waypoints[0].transform.position)
                {
                    transform.position = laneToSpawnIn.waypoints[0].transform.position;
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (laneToSpawnIn)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawSphere(laneToSpawnIn.waypoints[0].transform.position, .4f);
                if (transform.position != laneToSpawnIn.waypoints[0].transform.position)
                {
                    transform.position = laneToSpawnIn.waypoints[0].transform.position;
                }
            }
        }

        private IEnumerator SpawnVehicleRoutine()
        {
            yield return new WaitForSeconds(Random.Range(minSpawnTime, maxSpawnTime));

            if (canSpawn)
            {
                if (TrafficManager.Instance.spawnedVehicles.Count < TrafficManager.Instance.spawnLimit)
                {
                    GameObject newVehicle =
                        spawnCollection.prefabs[Random.Range(0, spawnCollection.prefabs.Length)];
                    SpawnVehicle(newVehicle);
                }
            }

            if (spawnContinuously)
            {
                yield return SpawnVehicleRoutine();
            }
        }

        public void SpawnVehicle(GameObject prefab)
        {
            Physics.Raycast(laneToSpawnIn.waypoints[0].transform.position, Vector3.down, out RaycastHit hit, 500);

            Vector3 startPosition = hit.point;
            Quaternion startRotation = Quaternion.LookRotation(laneToSpawnIn.waypoints[1].transform.position - laneToSpawnIn.waypoints[0].transform.position);
            
            GameObject spawnedVehicle = Instantiate(prefab, startPosition, startRotation);
            
            //Invoke spawn action
            OnInstantiateGameObject?.Invoke(spawnedVehicle);
            
            //Route setup
            TrafficRoute route = spawnedVehicle.GetComponent<TrafficRoute>();
            route.CalculateRoute(laneToSpawnIn);
                
            //AI Setup
            VehicleAi vehicleAi = spawnedVehicle.GetComponent<VehicleAi>();
            vehicleAi.currentSpeed = laneToSpawnIn.speed;
            vehicleAi.vehicleState = startState;

            laneToSpawnIn.trafficInLane.Add(vehicleAi);
            TrafficManager.Instance.spawnedVehicles.Add(spawnedVehicle);
            
            onSpawn?.Invoke();
        }
    }
}