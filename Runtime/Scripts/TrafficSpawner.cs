using System;
using System.Collections;
using FishNet;
using UnityEngine;
using Random = UnityEngine.Random;

namespace _3Dimensions.TrafficSystem
{
    public class TrafficSpawner : MonoBehaviour
    {
        [SerializeField] private TrafficSpawnCollection spawnCollection;
        [SerializeField] TrafficLane laneToSpawnIn;

        [SerializeField] bool spawnAtStart = true;
        [SerializeField] bool spawnContinuously = false;
        [SerializeField] float minSpawnTime = 5;
        [SerializeField] float maxSpawnTime = 15;
        [SerializeField] private VehicleAi.VehicleState startState = VehicleAi.VehicleState.Driving;

        public bool canSpawn; //TODO true when server

        public Action Spawn; //TODO let server listen to this action to spawn a vehicle
        
        void OnEnable()
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

            if (TrafficSystem.Instance.spawnedVehicles.Count < TrafficSystem.Instance.spawnLimit)
            {
                GameObject newVehicle =
                    spawnCollection.prefabs[Random.Range(0, spawnCollection.prefabs.Length)];
                SpawnVehicle(newVehicle);
            }

            if (spawnContinuously)
            {
                yield return SpawnVehicleRoutine();
            }
        }

        public void SpawnVehicle(GameObject prefab)
        {
            RaycastHit hit;
            Physics.Raycast(laneToSpawnIn.waypoints[0].transform.position, Vector3.down, out hit, 500);

            Vector3 startPosition = hit.point;
            Quaternion startRotation = Quaternion.LookRotation(laneToSpawnIn.waypoints[1].transform.position - laneToSpawnIn.waypoints[0].transform.position);
            
            GameObject spawnedVehicle = Instantiate(prefab, startPosition, startRotation);
            
            //Network
            if (canSpawn)
            {
                Spawn?.Invoke();
            }
            
            //Route setup
            TrafficRoute route = spawnedVehicle.GetComponent<TrafficRoute>();
            route.CalculateRoute(laneToSpawnIn);
                
            //AI Setup
            VehicleAi vehicleAi = spawnedVehicle.GetComponent<VehicleAi>();
            vehicleAi.currentSpeed = laneToSpawnIn.speed;
            vehicleAi.vehicleState = startState;

            laneToSpawnIn.trafficInLane.Add(vehicleAi);
            TrafficSystem.Instance.spawnedVehicles.Add(spawnedVehicle);
        }
    }
}