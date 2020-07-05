﻿﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Geometry;
using Containers;


public class GridController : MonoBehaviour
{
    public int currentTime; //current observation step id
    public int t_max = 9; //time after which I have 0 confidence on the previous observation

    public bool logMetrics = false;
    public bool plotMaps = false;

    [HideInInspector] public Cell[,] observationGrid,
        timeConfidenceGrid,
        spatialConfidenceGrid,
        overralConfidenceGrid,
        overralConfidenceGridTime,
        overralConfidenceGridNewObs,
        spatialConfidenceGridNewObs,
        timeConfidenceGridNewObs,
        lastObsGrid,
        observationGridNewObs,
        priorityGrid;

    [HideInInspector] public Texture2D observationTexture,
        timeConfidenceTexture,
        spatialConfidenceTexture,
        overralConfidenceTexture,
        overralConfidenceTextureTime,
        overralConfidenceTextureNewObs,
        spatialConfidenceTextureNewObs,
        timeConfidenceTextureNewObs,
        lastObsTexture,
        observationTextureNewObs,
        priorityTexture;

    public float cellWidth = 1f, cellDepth = 1f;

    public float ped_max = 5;

    [Tooltip(
        "The setting α = 1 causes the network to focus on observing more densely populated areas with no incentive to explore unknown cells.\n" +
        "In contrast, α = 0 causes the network to focus on global coverage only without distinguishing on the crowd density of the cells.")]
    [Range(0.0f, 1.0f)]
    public float alfa = 0.5f;


    // Variable for metrics
    public float spatialThreshold = 0.2f;

    public float GCM;
    public float PCM;
    private int numberOfCellsWidth, numberOfCellsDepth;

    public float peopleThreshold = 0.2f;
    private int peopleTimeCount, peopleHistory, peopleCovered;
    public List<GameObject> people = new List<GameObject>();
    private List<GameObject> totalPeople = new List<GameObject>();
    private List<Vector3> groundProjections = new List<Vector3>();
    private Bounds mapVolume;
    private Bounds map;

    private Text map_name;
    private static readonly int MainTex = Shader.PropertyToID("_MainTex");


    private void
        InitializeGrid(ref Cell[,] grid, bool plot, Vector2 factor, ref Texture2D texture2D,
            string name) //intialise grid values
    {
        MapController map = GameObject.Find("Map").GetComponent<MapController>();

        numberOfCellsWidth = (int) Mathf.Round(map.mapBounds.size.x / cellWidth);
        numberOfCellsDepth = (int) Mathf.Round(map.mapBounds.size.z / cellDepth);

        cellWidth = map.mapBounds.size.x / numberOfCellsWidth;
        cellDepth = map.mapBounds.size.z / numberOfCellsDepth;

        grid = new Cell[numberOfCellsWidth, numberOfCellsDepth];
        texture2D = new Texture2D(numberOfCellsWidth, numberOfCellsDepth);
        texture2D.filterMode = FilterMode.Point;
        texture2D.requestedMipmapLevel = 0;
        var myobj = GameObject.CreatePrimitive(PrimitiveType.Plane);
        myobj.name = name;
        myobj.transform.SetParent(transform);
        myobj.transform.position =
            new Vector3(map.mapBounds.center.x + (map.mapBounds.size.x + 1) * factor.x, 0.1f,
                map.mapBounds.center.z + (map.mapBounds.size.z + 1) * factor.y);
//            new Vector3(map.mapBounds.center.x * factor.x, 0.1f, map.mapBounds.center.z * factor.y);
        myobj.transform.localScale = new Vector3(0.1f * map.mapBounds.size.x, 0.1f, 0.1f * map.mapBounds.size.x);
        myobj.layer = 8;
        myobj.GetComponent<Renderer>().material.mainTexture = texture2D;
        myobj.GetComponent<Renderer>().material.mainTextureScale = -Vector2.one;
        for (int i = 0; i < numberOfCellsWidth; i++)
        {
            for (int j = 0; j < numberOfCellsDepth; j++)
            {
                grid[i, j] = new Cell(
                    new Vector2((cellWidth / 2f + i * cellWidth) - map.mapBounds.extents.x + map.mapBounds.center.x,
                        (cellDepth / 2f + j * cellDepth) - map.mapBounds.extents.z + map.mapBounds.center.z),
                    new Vector2(cellWidth, cellDepth), plot, factor,
                    new Vector2(map.mapBounds.size.x, map.mapBounds.size.z), ref texture2D, i, j);
            }
        }

        //Debug.Log(observationGrid[numberOfCellsWidth -1, numberOfCellsDepth-1].value);
        //Debug.Log("here");
    }

    private void UpdateTimeConfidenceGrid()
    {
        //Debug.Log("time"+currentTime);
        //Debug.Log(lastObsGrid[0, 0].value);
        for (int i = 0; i < numberOfCellsWidth; i++)
        {
            for (int j = 0; j < numberOfCellsDepth; j++)
            {
                float check_pos = currentTime - lastObsGrid[i, j].value;

                //Debug.Log(check_pos);
                //Debug.Log(t_max);
                float value = 0;
                if (check_pos == 0)
                {
                    value = 1;
                    //Debug.Log(value);
                }

                if (check_pos < t_max)
                {
                    value = 1 - ((check_pos) / t_max);
                    //Debug.Log("value = " + value);
                }
                else if (check_pos > t_max)
                {
                    value = 0;
                    //Debug.Log("value = " + value);
                }


                timeConfidenceGrid[i, j].SetValue(value);
            }
        }
    }

    public void UpdateTimeConfidenceGridNewObs(int i, int j)
    {
        timeConfidenceGridNewObs[i, j].SetValue(1);
        //Debug.Log("timeConfidenceGridNewObs = " + timeConfidenceGridNewObs[i, j].value);
    }

    public void UpdateSpatialConfidenceGridNewObs(int i, int j, float conf)
    {
        //Debug.Log(conf);
        if (spatialConfidenceGridNewObs[i, j].value < conf)
        {
            spatialConfidenceGridNewObs[i, j].SetValue(conf);
            //Debug.Log("spatialConfidenceGridNewObs = " + spatialConfidenceGridNewObs[i, j].value);
        }
    }

    private void UpdateOverralConfidenceGridNewObs()
    {
        for (int i = 0; i < numberOfCellsWidth; i++)
        {
            for (int j = 0; j < numberOfCellsDepth; j++)
            {
                overralConfidenceGridNewObs[i, j]
                    .SetValue(timeConfidenceGridNewObs[i, j].value * spatialConfidenceGridNewObs[i, j].value);
            }
        }
    }

    private void UpdateOverralConfidenceGridTime()
    {
        for (int i = 0; i < numberOfCellsWidth; i++)
        {
            for (int j = 0; j < numberOfCellsDepth; j++)
            {
                if (timeConfidenceGrid[i, j].value == 0 || spatialConfidenceGrid[i, j].value == 0)
                {
                    overralConfidenceGridTime[i, j].SetValue(0);
                    //Debug.Log(" overrall confidence (time) value = " + overralConfidenceGridTime[i, j].value);
                    //Debug.Log(" timeConfidenceGrid value = " + timeConfidenceGrid[i, j].value);
                    //Debug.Log(" spatialConfidenceGrid value = " + spatialConfidenceGrid[i, j].value);
                }
                else
                {
                    overralConfidenceGridTime[i, j]
                        .SetValue(timeConfidenceGrid[i, j].value * spatialConfidenceGrid[i, j].value);
                    //Debug.Log(" overrall confidence (time) value = " + overralConfidenceGridTime[i, j].value);
                }
            }
        }
    }

    public void UpdateObservationGridNewObs()
    {
        for (int i = 0; i < numberOfCellsWidth; i++)
        {
            for (int j = 0; j < numberOfCellsDepth; j++)
            {
                observationGridNewObs[i, j].SetValue(observationGridNewObs[i, j].value / ped_max);
            }
        }
    }

    public void CountObservationGridNewObs(int i, int j)
    {
        observationGridNewObs[i, j].SetValue(observationGridNewObs[i, j].value + 1);
        //Debug.Log("new obs value = " + observationGridNewObs[i, j].value);
    }

    private void GlobalCoverageMetric()
    {
        int conf = 0;
        for (int i = 0; i < numberOfCellsWidth; i++)
        {
            for (int j = 0; j < numberOfCellsDepth; j++)
            {
                if (overralConfidenceGrid[i, j].value > spatialThreshold)
                {
                    conf += 1;
                }
            }
        }

        GCM = (conf * 100 /
               (numberOfCellsWidth * numberOfCellsDepth)
            ); //(GCM + conf / numberOfCellsWidth * numberOfCellsDepth)/(currentTime + 1);
        if (logMetrics) Debug.Log("GCM = " + GCM / (currentTime + 1));
    }

    public float GlobalCoverageMetric_Current()
    {
        float gcm_curr = 0f;
        int conf = 0;
        for (int i = 0; i < numberOfCellsWidth; i++)
        {
            for (int j = 0; j < numberOfCellsDepth; j++)
            {
                if (overralConfidenceGrid[i, j].value > spatialThreshold)
                {
                    conf += 1;
                }
            }
        }

        gcm_curr = (conf / (float) (numberOfCellsWidth *
                                    numberOfCellsDepth)
            ); //(GCM + conf / numberOfCellsWidth * numberOfCellsDepth)/(currentTime + 1);
        return gcm_curr;
    }

    public List<Vector3> GetMapPeople()
    {
        List<Vector3> gp =new List<Vector3>();
        foreach (GameObject child in PersonCollection.Instance.People)
            if (mapVolume.Contains(child.transform.position))
            {
                gp.Add(map.ClosestPoint(child.transform.position));
            }

        return gp;
    }
    
    public float PCM_CURR()
    {
        float conf = 0;
        List<Vector3> gp = GetMapPeople();
        float totalPeop = gp.Count;

        if (totalPeop > 0)
        {
            foreach (Vector3 pos in gp)
                for (int i = 0; i < numberOfCellsWidth; i++)
                for (int j = 0; j < numberOfCellsDepth; j++)
                    if (observationGrid[i, j].Contains(pos)){
                        conf += overralConfidenceGrid[i, j].value;
												i = numberOfCellsWidth;
												j = numberOfCellsDepth;
										}

            return conf / totalPeop;
        }

        return 0;
    }
    public float PCM_CURR_FROM(int x, int y)
    {
        float conf = 0;
        List<Vector3> gp = GetMapPeople();
        float totalPeop = gp.Count;

        if (totalPeop > 0)
        {
            foreach (Vector3 pos in gp)
                for (int i = 0; i < numberOfCellsWidth; i++)
                for (int j = 0; j < numberOfCellsDepth; j++)
                    if (observationGrid[i, j].Contains(pos)){
                        conf += overralConfidenceGrid[i, j].value/(1+(Mathf.Sqrt(Mathf.Pow(i + x, 2) + Mathf.Pow(j + y, 2)))/10);
												i = numberOfCellsWidth;
												j = numberOfCellsDepth;
										}

            return conf / totalPeop;
        }

        return 0;
    }
    
    public float Priority_Current()
    {
        return  (1 - alfa) * GCM / 100f;
    }

    public void PeopleCoverageMetric()
    {
        var peop = PersonCollection.Instance.People;
        int conf = 0;

        foreach (GameObject child in peop)
        {
            if (mapVolume.Contains(child.transform.position))
            {
                totalPeople.Add(child);

                groundProjections.Add(map.ClosestPoint(child.transform.position));
                //map.ClosestPoint(child.transform.position);
                //observationGrid[i,j].Contains(;
            }
        }

        if (totalPeople.Count != 0)
        {
            foreach (Vector3 pos in groundProjections)
                for (int i = 0; i < numberOfCellsWidth; i++)
                for (int j = 0; j < numberOfCellsDepth; j++)
                        if (observationGrid[i, j].Contains(pos) && overralConfidenceGrid[i, j].value > peopleThreshold){
                            conf += 1;
														i = numberOfCellsWidth;
														j = numberOfCellsDepth;
												}

            //Debug.Log ("tot people count =" + totalPeople.Count);
            peopleHistory = peopleHistory + totalPeople.Count;
            peopleCovered = peopleCovered + conf;

            PCM = 100 * (float) peopleCovered / (float) peopleHistory;
            peopleTimeCount += 1;
            if (logMetrics) Debug.Log("PCM = " + PCM);
        }
    }


    private void ResetNewObs()
    {
        for (int i = 0; i < numberOfCellsWidth; i++)
        {
            for (int j = 0; j < numberOfCellsDepth; j++)
            {
                observationGridNewObs[i, j].SetValue(0);
                overralConfidenceGridNewObs[i, j].SetValue(0);
                spatialConfidenceGridNewObs[i, j].SetValue(0);
                timeConfidenceGridNewObs[i, j].SetValue(0);
            }
        }
    }

    public void Reset()
    {
        for (int i = 0; i < numberOfCellsWidth; i++)
        {
            for (int j = 0; j < numberOfCellsDepth; j++)
            {
                observationGrid[i, j].SetValue(0);
                timeConfidenceGrid[i, j].SetValue(0);
                spatialConfidenceGrid[i, j].SetValue(0);
                overralConfidenceGrid[i, j].SetValue(0);
                overralConfidenceGridTime[i, j].SetValue(0);
                overralConfidenceGridNewObs[i, j].SetValue(0);
                spatialConfidenceGridNewObs[i, j].SetValue(0);
                timeConfidenceGridNewObs[i, j].SetValue(0);
                lastObsGrid[i, j].SetValue(-t_max);
                observationGridNewObs[i, j].SetValue(0);
                priorityGrid[i,j].SetValue(0);
            }
        }

        currentTime = 0;
        GCM = 0;
        PCM = 0;
        peopleHistory = 0;
        peopleCovered = 0;
        peopleTimeCount = 0;
    }

    private void Awake()
    {
        InitializeGrid(ref observationGrid, plotMaps, new Vector2(-1f, 0f),
            ref observationTexture, "Observation");
        InitializeGrid(ref observationGridNewObs, plotMaps, new Vector2(-1f, 1f),
            ref observationTextureNewObs, "Observation New Observations");
        InitializeGrid(ref timeConfidenceGrid, plotMaps, new Vector2(0f, 0f),
            ref timeConfidenceTexture, "Time Confidence");
        InitializeGrid(ref timeConfidenceGridNewObs, plotMaps, new Vector2(0f, 1f),
            ref timeConfidenceTextureNewObs, "Time Confidence new Observations");
        InitializeGrid(ref spatialConfidenceGrid, plotMaps, new Vector2(1f, 0f),
            ref spatialConfidenceTexture, "Spatial Confidence");
        InitializeGrid(ref spatialConfidenceGridNewObs, plotMaps, new Vector2(1f, 1f),
            ref spatialConfidenceTextureNewObs, "Spatial Confidence New Observations");
        InitializeGrid(ref overralConfidenceGrid, plotMaps, new Vector2(2f, 0f),
            ref overralConfidenceTexture, "Overall Confidence");
        InitializeGrid(ref overralConfidenceGridNewObs, plotMaps, new Vector2(2f, 1f),
            ref overralConfidenceTextureNewObs, "Overall confidence New Observations");
        InitializeGrid(ref overralConfidenceGridTime, plotMaps, new Vector2(2f, -1f),
            ref overralConfidenceTextureTime, "Overall Confidence Grid Time");
        InitializeGrid(ref priorityGrid, plotMaps, new Vector2(-2f, 0f),
            ref priorityTexture, "Priority");
        InitializeGrid(ref lastObsGrid, false, new Vector2(0f, 0f),
            ref lastObsTexture, "Last Observation");
        //GenerateCameras()

        mapVolume = GameObject.Find("Map").GetComponent<MapController>().mapBounds;
        map = GameObject.Find("Floor").GetComponent<BoxCollider>().bounds;

        Reset();
    }

    private void LateUpdate()
    {
        if (!PseudoAcademy.Instance)
        {
            UpdateGCMValues();
            currentTime += 1;
        }
    }

    public void UpdateGCMValues()
    {
        UpdateTimeConfidenceGrid();
        UpdateObservationGridNewObs();
        UpdateOverralConfidenceGridTime();
        UpdateOverralConfidenceGridNewObs();

        for (int i = 0; i < numberOfCellsWidth; i++)
        {
            for (int j = 0; j < numberOfCellsDepth; j++)
            {
                // spatialConfidenceGrid[i, j].SetValue(1);
                if (overralConfidenceGridTime[i, j].value != 0)
                {
                    //Debug.Log("newobs = " + overralConfidenceGridNewObs[i, j].value);
                    //Debug.Log("Time = " + overralConfidenceGridTime[i, j].value);
                }

                if (overralConfidenceGridNewObs[i, j].value >= overralConfidenceGridTime[i, j].value)
                {
                    if (overralConfidenceGridNewObs[i, j].value != 0)
                    {
                        lastObsGrid[i, j].SetValue(currentTime);
                    }

                    overralConfidenceGrid[i, j].SetValue(overralConfidenceGridNewObs[i, j].value);
                    observationGrid[i, j].SetValue(observationGridNewObs[i, j].value);
                    spatialConfidenceGrid[i, j].SetValue(spatialConfidenceGridNewObs[i, j].value);

                    //Debug.Log("newobs");
                }
                else
                {
                    overralConfidenceGrid[i, j].SetValue(overralConfidenceGridTime[i, j].value);
                    //Debug.Log("HEREEEEEE: Time");
                }
                
                priorityGrid[i, j].SetValue(alfa * observationGrid[i, j].value +
                                            (1 - alfa) * (1 - overralConfidenceGrid[i, j].value));

                if (plotMaps)
                {
                    priorityGrid[i, j].UpdateColorPriority();
                    observationGrid[i, j].UpdateColor();
                    timeConfidenceGrid[i, j].UpdateColor();
                    spatialConfidenceGrid[i, j].UpdateColor();
                    overralConfidenceGrid[i, j].UpdateColor();
                    overralConfidenceGridTime[i, j].UpdateColor();
                    overralConfidenceGridNewObs[i, j].UpdateColor();
                    spatialConfidenceGridNewObs[i, j].UpdateColor();
                    timeConfidenceGridNewObs[i, j].UpdateColor();
                    observationGridNewObs[i, j].UpdateColor();
                }


                //lastObsGrid,
            }
        }

        if (plotMaps)
        {
            observationTexture.Apply();
            overralConfidenceTextureNewObs.Apply();
            observationTextureNewObs.Apply();
            timeConfidenceTexture.Apply();
            timeConfidenceTextureNewObs.Apply();
            spatialConfidenceTexture.Apply();
            spatialConfidenceTextureNewObs.Apply();
            overralConfidenceTexture.Apply();
            overralConfidenceTextureNewObs.Apply();
            overralConfidenceTextureTime.Apply();
            priorityTexture.Apply();
            lastObsTexture.Apply();
        }

        GlobalCoverageMetric();
        PeopleCoverageMetric();
        people.Clear();
        totalPeople.Clear();
        groundProjections.Clear();
        ResetNewObs();
    }
}