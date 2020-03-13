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
    public int VisionSize = 4;

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

//        RequestDecision();
    }

    #region Helpers

    private const int k_NoAction = 0; // do nothing!
    private const int k_Up = 1;
    private const int k_Down = 2;
    private const int k_Left = 3;
    private const int k_Right = 4;
    private const int k_TopRight = 5;
    private const int k_TopLeft = 6;
    private const int k_BottomLeft = 7;
    private const int k_BottomRight = 8;

    float GetCellValue(Cell[,] grid, int x, int y)
    {
        if (x < 0 || y < 0 || x >= grid.GetLength(0) || y >= grid.GetLength(1))
            return 1;
        return grid[x, y].value;
    }

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

    (int, int) ActionToXY(int action)
    {
        int x = 0, y = 0;
        switch (action)
        {
            case k_NoAction:
                x = y = 0;
                break;
            case k_Right:
                x = 1;
                break;
            case k_Left:
                x = -1;
                break;
            case k_Up:
                y = 1;
                break;
            case k_Down:
                y = -1;
                break;
            case k_TopRight:
                x = y = 1;
                break;
            case k_TopLeft:
                x = -1;
                y = 1;
                break;
            case k_BottomLeft:
                x = y = -1;
                break;
            case k_BottomRight:
                x = 1;
                y = -1;
                break;
            default:
                throw new ArgumentException("Invalid action value");
        }

        return (x, y);
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
        Texture2D texToUpdate = new Texture2D(tex.width, tex.height);
        Graphics.CopyTexture(tex, texToUpdate);
        texToUpdate.SetPixel(x_coord, y_coord, Color.yellow);
        DroneAgent[] drones = FindObjectsOfType<DroneAgent>();
        foreach (DroneAgent drone in drones)
        {
            if (drone != this)
            {
                (int x, int y) pos = drone.GetCoords();
                texToUpdate.SetPixel(pos.x, pos.y, Color.blue);
            }
        }

        foreach (var pos in PseudoAcademy.Instance.seenPeoplePositions)
        {
            texToUpdate.SetPixel(pos.Item1, pos.Item2, Color.cyan);
        }
        
        texToUpdate.Apply();
        texToUpdate.filterMode = FilterMode.Point;
        texToUpdate.wrapMode = TextureWrapMode.Clamp;
        Graphics.Blit(texToUpdate, rtsc.renderTexture);
        Destroy(texToUpdate);
        SetActionMask(GetActionMask(x_coord, y_coord));
    }

    IEnumerable<int> GetActionMask(int x_coord, int y_coord)
    {
        var actionMask = new List<int>();
        if (x_coord + 1 >= _gridController.priorityGrid.GetLength(0))
            actionMask.AddRange(new[] {k_Right, k_TopRight, k_BottomRight});
        if (x_coord - 1 < 0)
            actionMask.AddRange(new[] {k_Left, k_TopLeft, k_BottomLeft});
        if (y_coord + 1 >= _gridController.priorityGrid.GetLength(1))
            actionMask.AddRange(new[] {k_Up, k_TopLeft, k_TopRight});
        if (y_coord - 1 < 0)
            actionMask.AddRange(new[] {k_Down, k_BottomLeft, k_BottomRight});
        return actionMask.Distinct();
    }

    private float lastTime;

    private int decisions = 0, requestedDecisions = 0;

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
        (int x, int y) = ActionToXY(Mathf.FloorToInt(vectorAction[0]));
        (int xCoord, int yCoord) = GetCoords();
        Cell[,] grid;
        if (PseudoAcademy.Instance.observationTexture == PseudoAcademy.TextureToTrain.OverallConfidence)
            grid = _gridController.overralConfidenceGrid;
        else
            grid = _gridController.priorityGrid;

        _drone.transform.position = new Vector3(
            _gridController.timeConfidenceGrid[xCoord + x, yCoord + y].GetPosition().x,
            _drone.transform.position.y, _gridController.timeConfidenceGrid[xCoord + x, yCoord + y].GetPosition().z);
        float maxSteps = PseudoAcademy.Instance.minimumGoodDecisions + 1f;
        float gridSize = grid.GetLength(0) * grid.GetLength(1);
        if (_gridController.alfa < 1)
        {
            float genReward = 1f / (maxSteps + gridSize);
            if (grid[xCoord + x, yCoord + y].value > 0)
                AddReward(-genReward);
            else if (grid[xCoord + x, yCoord + y].value < 0.1)
                AddReward(genReward);
            if (decisions > gridSize)
                AddReward(-genReward);
            _ccfov.Project();
            float gcm = _gridController.GlobalCoverageMetric_Current();
            if (Math.Abs(gcm - 1) < 0.01f)
            {
                AddReward(1 - genReward * gridSize);
                Done();
            }
        }
        else
        {
            _ccfov.Project();
            if (_ccfov.personHit.Count > 0)
            {
                var pos = new Tuple<int, int>(xCoord + x, yCoord + y);
                if (!PseudoAcademy.Instance.seenPeoplePositions.Contains(pos))
                {
                    PseudoAcademy.Instance.seenPeoplePositions.Add(pos);
                    AddReward(1f/PseudoAcademy.Instance.peopleToSpawn);
                }
            }
            else
            {
                AddReward(-1f/PseudoAcademy.Instance.maxDecisions);
            }
        }
        


        _gridController.UpdateGCMValues(); //We force the update of the values 

        
        if (PseudoAcademy.Instance.logRewards)
            Debug.Log("Agent " + name + " step " + decisions + ": Reward " + GetCumulativeReward() + "\nGCM: " + _gridController.GlobalCoverageMetric_Current());

        PseudoAcademy.Instance.SendAction(this);
        if (decisions >= PseudoAcademy.Instance.maxDecisions && (PseudoAcademy.Instance.isTraining || PseudoAcademy.Instance.resetAllAtInferece))
            PseudoAcademy.Instance.Reset();
        decisions++;
    }

    public override float[]
        Heuristic() // this method allows us to use the drone with the keyboard to check that everything is working
    {
        float[] ret = {k_NoAction};
        if (Input.GetKey(KeyCode.D))
            ret = new float[] {k_Right};
        if (Input.GetKey(KeyCode.W))
            ret = new float[] {k_Up};
        if (Input.GetKey(KeyCode.A))
            ret = new float[] {k_Left};
        if (Input.GetKey(KeyCode.X))
            ret = new float[] {k_Down};
        if (Input.GetKey(KeyCode.E))
            ret = new float[] {k_TopRight};
        if (Input.GetKey(KeyCode.Q))
            ret = new float[] {k_TopLeft};
        if (Input.GetKey(KeyCode.Z))
            ret = new float[] {k_BottomLeft};
        if (Input.GetKey(KeyCode.C))
            ret = new float[] {k_BottomRight};
        (int x, int y) = GetCoords();
        if (GetActionMask(x, y).Contains((int) ret[0]))
            ret = new float[] {k_NoAction};
        return ret;
    }

    public void FixedUpdate()
    {
        WaitTimeInference();
    }

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
                    RequestDecision();
            }
            else
            {
                m_TimeSinceDecision += Time.fixedDeltaTime;
            }
        }
    }

    public override void AgentReset()
    {
        if (!PseudoAcademy.Instance.isTraining && !PseudoAcademy.Instance.resetAllAtInferece)
            return;
        decisions = requestedDecisions = 0;
        transform.position = new Vector3(Random.Range(-21f, 21f), 6.55f, Random.Range(-21f, 21f));
        if (PseudoAcademy.Instance.logRewards)
            Debug.Log("#################### AGENT " + name + " RESET ####################");
        base.AgentReset();
    }
}