using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Barracuda;
using MLAgents;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

[DisallowMultipleComponent]
public class PseudoAcademy : MonoBehaviour
{
    public static PseudoAcademy Instance;
    [Header("Dones")]
    [SerializeField] private GameObject dronePrefab;
    [SerializeField] private int dronesToSpawn;
    private List<DroneAgent> _droneAgents;
    [Header("Reinforcement Learning")]
    [FormerlySerializedAs("_gridController")] [SerializeField]
    private GridController gridController;

    public enum TextureToTrain
    {
        OverallConfidence,
        PriorityTexture
    }

    public TextureToTrain observationTexture;
    private bool[] _droneAction;


    public float timeBetweenDecisionsAtInference = 1f;
    public int minimumGoodDecisions;
    public int maxDecisions;
    [Tooltip("You should not change this")]
    public int currentDecisions = 0;
    [HideInInspector] public bool isTraining;
    [SerializeField] private NNModel inferenceModel;
    public bool resetAllAtInferece;
    public bool logRewards;

    [Header("People")]
    public PeopleSourceMod psm;
    public int minPeopleToSpawn, maxPeopleToSpawn;
    public bool shouldPeopleStandStill;
    [FormerlySerializedAs("seenPeoplePos")] public List<Tuple<int, int>> seenPeoplePositions;

    public void Awake()
    {
        Instance = this;
        isTraining = !inferenceModel;
        if (isTraining)
            CustomAwake();
    }
    public void CustomAwake()
    {
        _droneAgents = new List<DroneAgent>();
        seenPeoplePositions = new List<Tuple<int, int>>();
        for (int i = 0; i < dronesToSpawn; i++)
        {
            DroneAgent drone = Instantiate(dronePrefab).GetComponent<DroneAgent>();
            drone.transform.position = new Vector3(Random.Range(-21f, 21f), 6.55f, Random.Range(-21f, 21f));
            if (inferenceModel)
                drone.GiveModel("Drone", inferenceModel);
            _droneAgents.Add(drone);
        }

        _droneAction = new bool[_droneAgents.Count];
        Reset();
        Academy.Instance.OnEnvironmentReset += Reset;
    }

    void SpawnHuman()
    {
        psm.transform.position =  new Vector3((int)Random.Range(-21f, 21f), 0.05f, (int)Random.Range(-21f, 21f));
        psm.GenerateHuman(shouldPeopleStandStill, true);
    }

    public bool CanDecide(DroneAgent agent)
    {
        return !_droneAction[_droneAgents.IndexOf(agent)];
    }
    
    public void SendAction(DroneAgent agent)
    {
        _droneAction[_droneAgents.IndexOf(agent)] = true;
        if (_droneAction.Any(didPerformAction => !didPerformAction))
            return;
        if (isTraining)
        {
            float gcm = gridController.GlobalCoverageMetric_Current();
            float pcm = gridController.PeopleCoverageMetric();
            if (Math.Abs(gcm - 1) < 0.01f || (PersonCollection.Instance.People.Count > 0 &&
                                              seenPeoplePositions.Count == PersonCollection.Instance.People.Count))
            {
                Debug.Log("Reached Objective");
                foreach (var droneAgent in _droneAgents)
                    droneAgent.Done();
                Reset();
            }

            gridController.UpdateGCMValues();
            gridController.currentTime++;
        }

        currentDecisions++;
        if (currentDecisions >= maxDecisions && (isTraining || resetAllAtInferece))
        {
            Debug.Log("Max steps reached!");
            Reset();
        }

        for (int i = 0; i < _droneAction.Length; ++i)
            _droneAction[i] = false;
    }

    private float timeSinceLast = 0;
    public float TotalGCM = 0, TotalPCM = 0;
    private void FixedUpdate()
    {
        if (!isTraining)
        {
            if (timeSinceLast <= 0)
            {
                gridController.currentTime++;
                gridController.UpdateGCMValues();
                TotalGCM += gridController.GCM;
                TotalPCM += gridController.PCM;
                timeSinceLast = 1f / 15f;
            }
            else
            {
                timeSinceLast -= Time.fixedDeltaTime;
            }
        }
    }

    public void Reset()
    {
        Debug.Log("Academy Reset");
        seenPeoplePositions.Clear();
        if (isTraining)
            gridController.t_max = Int32.MaxValue;
        // gridController.alfa = Random.Range(0, 1);
        if (isTraining)
            PersonCollection.Instance.KillThemAll();
        currentDecisions = 0;
        gridController.Reset();
        foreach (var droneAgent in _droneAgents)
            droneAgent.Done();
        for (int i = 0; i < _droneAction.Length; ++i)
            _droneAction[i] = false;
        for (int i = 0; i < Random.Range(minPeopleToSpawn, maxPeopleToSpawn); i++)
            SpawnHuman();
    }
}