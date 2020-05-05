using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using Geometry;
using MLAgents;
using MLAgents.Sensor;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

[RequireComponent(typeof(RenderTextureSensorComponent))]
public class DroneAgent : Agent
{
    [Header("Specific to Drone cameras")] private CameraControllerFOV _ccfov;

    private Rigidbody _drone;

    private Bounds _map;

    private Rigidbody _rb;
    private GridController _gridController;
    public int VisionSize = 10;

    private float m_TimeSinceDecision;

    private RenderTextureSensorComponent rtsc;

    private void Awake()
    {
        _gridController = GameObject.Find("Map").GetComponent<GridController>();
        _map = GameObject.Find("Floor").GetComponent<BoxCollider>().bounds;
        _ccfov = GetComponentInChildren<CameraControllerFOV>();
        rtsc = GetComponent<RenderTextureSensorComponent>();
        rtsc.renderTexture = RenderTexture.GetTemporary(84, 84);
    }

    public override void InitializeAgent()
    {
        _drone = GetComponent<Rigidbody>();
    }

    #region Helpers

    (int, int) GetCoords()
    {
        int xCoord = 0, yCoord = 0;
        Vector3 onTheGroundProjection = _map.ClosestPoint(_drone.transform.position);
        for (int i = 0; i < _gridController.priorityGrid.GetLength(0); i++)
        {
            for (int j = 0; j < _gridController.priorityGrid.GetLength(1); j++)
            {
                if (_gridController.priorityGrid[i, j].Contains(onTheGroundProjection))
                {
                    xCoord = i;
                    yCoord = j;
                }
            }
        }

        return (xCoord, yCoord);
    }

    (int, int) GetRelativeWindowLocationFromActions(float[] act)
    {
        int x = (int) (1 + Mathf.Clamp(act[0], -1, 1)) * VisionSize;
        int y = (int) (1 + Mathf.Clamp(act[1], -1, 1)) * VisionSize;
        return (x, y);
    }

    (int, int) RelativeToAbsolute(int abs_x, int abs_y, int x, int y)
    {
        return (x + abs_x - VisionSize / 2, y + abs_y - VisionSize / 2);
    }

    public override float[] Heuristic()
    {
        return new[] {Input.GetAxis("Horizontal"), Input.GetAxis("Vertical")};
    }

    #endregion

    public override void CollectObservations()
    {
        (int x_coord, int y_coord) = GetCoords();
        Cell[,] grid;
        Texture2D tex;
        if (PseudoAcademy.Instance.observationTexture == PseudoAcademy.TextureToTrain.OverallConfidence)
        {
            grid = _gridController.overralConfidenceGrid;
            tex = _gridController.overralConfidenceTexture;
        }
        else
        {
            grid = _gridController.priorityGrid;
            tex = _gridController.priorityTexture;
        }

        for (int i = x_coord - 1; i <= x_coord + 1; i++)
        for (int j = y_coord - 1; j <= y_coord + 1; j++)
            if (i >= 0 && j >= 0 && i < grid.GetLength(0) &&
                j < grid.GetLength(0))
                grid[i, j].UpdateColor();
        Texture2D texToUpdate = new Texture2D(VisionSize, VisionSize);
        for (int i = 0; i < VisionSize; i++)
        {
            for (int j = 0; j < VisionSize; j++)
            {
                (int x, int y) = RelativeToAbsolute(x_coord, y_coord, i, j);
                if (x == x_coord && y == y_coord)
                    texToUpdate.SetPixel(x_coord, y_coord, Color.yellow);
                else if (x < 0 || y < 0 || x >= grid.GetLength(0) || y >= grid.GetLength(1))
                    texToUpdate.SetPixel(i, j, Color.green);
                else
                    texToUpdate.SetPixel(i, j, tex.GetPixel(x, y));
            }
        }
        // DroneAgent[] drones = FindObjectsOfType<DroneAgent>();
        // foreach (DroneAgent drone in drones)
        // {
        //     if (drone != this)
        //     {
        //         (int x, int y) pos = drone.GetCoords();
        //         texToUpdate.SetPixel(pos.x, pos.y, Color.blue);
        //     }
        // }
        //
        // foreach (var pos in PseudoAcademy.Instance.seenPeoplePositions)
        // {
        //     texToUpdate.SetPixel(pos.Item1, pos.Item2, Color.cyan);
        // }

        texToUpdate.Apply();
        texToUpdate.filterMode = FilterMode.Point;
        texToUpdate.wrapMode = TextureWrapMode.Clamp;
        Graphics.Blit(texToUpdate, rtsc.renderTexture);
        Destroy(texToUpdate);
    }

    private float lastTime;

    private void OnDrawGizmosSelected()
    {
        if (!_gridController)
            return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position,
            new Vector3(_gridController.cellWidth * VisionSize, 0.1f, _gridController.cellDepth * VisionSize));
        Gizmos.color = Color.blue;
        var drones = FindObjectsOfType<DroneAgent>();
        foreach (var drone in drones)
        {
            if (drone != this)
                Gizmos.DrawWireCube(drone.transform.position,
                    new Vector3(_gridController.cellDepth * drone.VisionSize, 0.1f,
                        _gridController.cellDepth * drone.VisionSize));
        }
    }

    public override void AgentAction(float[] vectorAction)
    {
        (int x, int y) = GetRelativeWindowLocationFromActions(vectorAction);
        (int xCoord, int yCoord) = GetCoords();
        Cell[,] grid;
        if (PseudoAcademy.Instance.observationTexture == PseudoAcademy.TextureToTrain.OverallConfidence)
            grid = _gridController.overralConfidenceGrid;
        else
            grid = _gridController.priorityGrid;

        (int final_x, int final_y) = RelativeToAbsolute(xCoord, yCoord, x, y);
        final_x = (int) Math.Max(Math.Min(final_x, grid.GetLength(0) - 1), 0);
        final_y = (int) Math.Max(Math.Min(final_y, grid.GetLength(1) - 1), 0);
        if (PseudoAcademy.Instance.isTraining)
            _drone.transform.position = new Vector3(
                _gridController.timeConfidenceGrid[final_x, final_y].GetPosition().x,
                _drone.transform.position.y, _gridController.timeConfidenceGrid[final_x, final_y].GetPosition().z);
        else
            nextPosition = new Vector3(
                _gridController.timeConfidenceGrid[final_x, final_y].GetPosition().x,
                _drone.transform.position.y, _gridController.timeConfidenceGrid[final_x, final_y].GetPosition().z);
        float maxSteps = PseudoAcademy.Instance.minimumGoodDecisions + 1f;
        float gridSize = grid.GetLength(0) * grid.GetLength(1);
        if (_gridController.alfa < 1)
        {
            float gcm_t = _gridController.GlobalCoverageMetric_Current();
            _ccfov.Project();
            _gridController.UpdateGCMValues(true);
            float gcm_t1 = _gridController.GlobalCoverageMetric_Current();
            float genReward = 1f / (maxSteps + gridSize);
            if (PseudoAcademy.Instance.logRewards)
                Debug.Log("Delta GCM: " + (gcm_t1 - gcm_t));
            AddReward(gcm_t1 - gcm_t);
            // if (gcm_t1-gcm_t < 0)//grid[xCoord + x, yCoord + y].value > 0)//drone si sposta su una cella verde
            //     AddReward(-genReward);
            // else if (gcm_t1 - gcm_t > 0) //grid[xCoord + x, yCoord + y].value < 0.1)//drone si sposta su una cella rossa
            //     AddReward(genReward);
            // else if (gcm_t1-gcm_t == 0)
            //     AddReward(-genReward/2f);
            // if (PseudoAcademy.Instance.currentDecisions > gridSize)
            //     AddReward(-genReward);

            if (Math.Abs(gcm_t1 - 1) < 0.01f)
            {
                // AddReward(1 - genReward * gridSize);
                Done();
            }
        }
        else
        {
            _ccfov.Project();
            var pos = new Tuple<int, int>(xCoord + x, yCoord + y);
            if (_ccfov.personHit.Count > 0 && !PseudoAcademy.Instance.seenPeoplePositions.Contains(pos))
            {
                for (int i = 0; i < _ccfov.personHit.Count; i++)
                {
                    PseudoAcademy.Instance.seenPeoplePositions.Add(pos);
                    AddReward(1f / PersonCollection.Instance.People.Count);
                }
            }
            else
            {
                AddReward(-1f / PseudoAcademy.Instance.maxDecisions);
            }
        }


        _gridController.UpdateGCMValues(true); //We force the update of the values 


        if (PseudoAcademy.Instance.logRewards)
            Debug.Log("Agent " + name + " step " + PseudoAcademy.Instance.currentDecisions + ": Reward " +
                      GetCumulativeReward() + "\nGCM: " +
                      _gridController.GlobalCoverageMetric_Current());

        PseudoAcademy.Instance.SendAction(this);
    }

    public void FixedUpdate()
    {
        WaitTimeInference();
    }

    private Vector3 nextPosition;
    private float movementForwardSpeedMission = 20;
    private bool mission = false;

    private void WaitTimeInference()
    {
        if (PseudoAcademy.Instance.isTraining)
        {
            if (PseudoAcademy.Instance.CanDecide(this))
                RequestDecision();
        }
        else
        {
            if (m_TimeSinceDecision >= PseudoAcademy.Instance.timeBetweenDecisionsAtInference)
            {
                m_TimeSinceDecision = 0f;
                if (PseudoAcademy.Instance.CanDecide(this))
                {
                    RequestDecision();
                    mission = true;
                }
            }
            else
            {
                m_TimeSinceDecision += Time.fixedDeltaTime;
            }

            if (mission)
            {
                _drone.transform.position = Vector3.MoveTowards(_drone.transform.position, nextPosition,
                    movementForwardSpeedMission * Time.fixedDeltaTime);
                var dist = Vector3.Distance(_drone.transform.position, nextPosition);
                if (dist == 0)
                {
                    mission = false;
                }
            }
        }
    }

    public override void AgentReset()
    {
        if (!PseudoAcademy.Instance.isTraining && !PseudoAcademy.Instance.resetAllAtInferece)
            return;
        transform.position = new Vector3(Random.Range(-21f, 21f), 6.55f, Random.Range(-21f, 21f));
        if (PseudoAcademy.Instance.logRewards)
            Debug.Log("#################### AGENT " + name + " RESET ####################");
        base.AgentReset();
    }
}