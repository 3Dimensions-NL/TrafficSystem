using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using Random = UnityEngine.Random;

namespace _3Dimensions.TrafficSystem.Runtime
{
    public class TrafficSpawner : MonoBehaviour
    {
        public TrafficSpawnCollection spawnCollection;
        public TrafficLane laneToSpawnIn;
        public string spawnTag = "Traffic";

        public bool spawnAtStart = true;
        public bool spawnContinuously = true;
        public float minSpawnTime = 5;
        public float maxSpawnTime = 15;
        public float terrainDetectionHeight = 50;
        public VehicleAi.VehicleState startState = VehicleAi.VehicleState.Driving;
        
        public UnityEvent onSpawn; 

        public bool canSpawn = true; //TODO true when server

        public Action<GameObject> OnInstantiateGameObject;

        private static int vehicleCounter = 0;
        private List<GameObject> _vehiclesInTrigger = new List<GameObject>();

        public bool debug;
        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                if (laneToSpawnIn)
                { 
                    StartCoroutine(SpawnVehicleRoutine());
                }
            }
        }

        private void Start()
        {
            if (Application.isPlaying)
            {
                if (laneToSpawnIn)
                {
                    if (spawnAtStart)
                    {
                        if (canSpawn)
                        {
                            if (TrafficManager.Instance.spawnedVehicles.Count < TrafficManager.Instance.spawnLimit)
                            {
                                GameObject newVehicle =
                                    spawnCollection.prefabs[Random.Range(0, spawnCollection.prefabs.Length)];
                                SpawnVehicle(newVehicle);
                            }
                        }
                    }
                }
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
        }

        private void Update()
        {
            _vehiclesInTrigger = _vehiclesInTrigger.Where(item => item).ToList();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(spawnTag))
            {
                _vehiclesInTrigger.Add(other.gameObject);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(spawnTag))
            {
                if (_vehiclesInTrigger.Contains(other.gameObject)) _vehiclesInTrigger.Remove(other.gameObject);
            }
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
            if (_vehiclesInTrigger.Count > 0)
            {
                Debug.LogWarning("Could not spawn vehicle, other vehicle in trigger.", this);
                return;
            }
            
            RaycastHit[] hits = Physics.RaycastAll(laneToSpawnIn.waypoints[0].transform.position + (Vector3.up * (terrainDetectionHeight * 0.5f)), Vector3.down, terrainDetectionHeight);
            RaycastHit? firstTrafficSurfaceHit = hits.FirstOrDefault(hit => hit.collider.GetComponent<TrafficSurface>() != null);

            if (!firstTrafficSurfaceHit.Value.collider)
            {
                Debug.LogWarning("Could not spawn vehicle, no TrafficSurface found.", this);
                return;
            }

            vehicleCounter++;
            Vector3 startPosition = firstTrafficSurfaceHit.Value.point;
            if (debug) Debug.Log("startPosition: " + startPosition, this);
            Quaternion startRotation = Quaternion.LookRotation(laneToSpawnIn.waypoints[1].transform.position - laneToSpawnIn.waypoints[0].transform.position);
            
            GameObject spawnedVehicle = Instantiate(prefab, startPosition, startRotation);
            spawnedVehicle.tag = spawnTag;
            spawnedVehicle.name = spawnedVehicle.name + "(" + vehicleCounter + ")";
            if (debug) Debug.Log("Spawned position = " + spawnedVehicle.transform.position, spawnedVehicle);
            
            //Invoke spawn action
            OnInstantiateGameObject?.Invoke(spawnedVehicle);
            
            //Route setup
            TrafficRoute route = spawnedVehicle.GetComponent<TrafficRoute>();
            route.CalculateRoute(laneToSpawnIn);
                
            //AI Setup
            VehicleAi vehicleAi = spawnedVehicle.GetComponent<VehicleAi>();
            vehicleAi.currentSpeed = laneToSpawnIn.speed;
            vehicleAi.targetVehicleState = startState;

            if (debug) Debug.Log("Spawned position after Ai setup = " + spawnedVehicle.transform.position, spawnedVehicle);

            laneToSpawnIn.trafficInLane.Add(vehicleAi);
            TrafficManager.Instance.spawnedVehicles.Add(spawnedVehicle);
            
            onSpawn?.Invoke();
        }
    }
}