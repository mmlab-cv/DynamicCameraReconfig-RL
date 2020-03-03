using System;
using System.Collections;
using System.Collections.Generic;
using Barracuda;
using MLAgents;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

[DisallowMultipleComponent]
public class PseudoAcademy : MonoBehaviour
{
    public static PseudoAcademy Instance;
    
    [SerializeField]
    private GameObject dronePrefab;
    [SerializeField]
    private int dronesToSpawn;
    private List<DroneAgent> _droneAgents;
    [FormerlySerializedAs("_gridController")] [SerializeField]
    private GridController gridController;
    private bool[] _droneAction;
    
    
    public float timeBetweenDecisionsAtInference = 1f;
    public int minimumGoodDecisions;
    public int maxDecisions;
    [HideInInspector]
    public bool isTraining;
    [SerializeField] private NNModel inferenceModel;
    private void Awake()
    {
        Instance = this;
        Academy.Instance.OnEnvironmentReset += Reset;
        _droneAgents = new List<DroneAgent>();
        isTraining = !inferenceModel;
        for (int i = 0; i < dronesToSpawn; i++)
        {
            DroneAgent drone = Instantiate(dronePrefab).GetComponent<DroneAgent>();
            drone.transform.position = new Vector3(Random.Range(-21f, 21f), 6.55f, Random.Range(-21f, 21f));
            if (inferenceModel)
                drone.GiveModel("Drone", inferenceModel);
            _droneAgents.Add(drone);
        }
        _droneAction = new bool[_droneAgents.Count];
    }

    public bool CanDecide(DroneAgent agent)
    {
        return !_droneAction[_droneAgents.IndexOf(agent)];
    }
    
    public void SendAction(DroneAgent agent)
    {
        _droneAction[_droneAgents.IndexOf(agent)] = true;
        for (int i = 0; i < _droneAction.Length; ++i)
            if (!_droneAction[i])
                return;
        float gcm = gridController.GlobalCoverageMetric_Current();
        if (Math.Abs(gcm - 1) < 0.01f){
            foreach (var droneAgent in _droneAgents)
            {
                droneAgent.Done();
            }
        }
        gridController.currentTime++;
        for (int i = 0; i < _droneAction.Length; ++i)
            _droneAction[i] = false;
    }

    public void Reset()
    {
        gridController.alfa = Random.Range(0, 1);
        gridController.Reset();
        foreach (var droneAgent in _droneAgents)
            droneAgent.AgentReset();
    }
}
