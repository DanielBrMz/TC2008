using UnityEngine;
using System.Collections.Generic;
using System.Net.Mail;
using System.Security.Cryptography.X509Certificates;
using Unity.VisualScripting;
using UnityEditor.SearchService;

public class EnviromentSpawner : MonoBehaviour
{
    [Header("Simulation Parameters")]
    public int agents = 4;
    public int items = 5;
    public int obstacles = 0;

    [Header("Object Parameters")]
    public GameObject AgentPrefab;


    [Header("Eviroment Parameters")]
    [SerializeField] private int nTiles = 10;
    [SerializeField] private int mTiles = 10;
    [SerializeField] private float tileSize = 3;
    [SerializeField] private float gap = 0.05f;
    [SerializeField] private float yOffset = 0.1f;
    [SerializeField] private Vector3 center = Vector3.zero;

    [Header("Base and Walls")]
    [SerializeField] private float wallHeight = 5f;
    [SerializeField] private float wallGirth = 0.4f;
    [SerializeField] private float baseThickness = 0.3f;

    [Header("Materials")]
    [SerializeField] private Material tileMaterial;
    [SerializeField] private Material groundMaterial;
    [SerializeField] private Material wallMaterial;


    private GameObject[,] tiles;
    private Vector3 bounds;
    // We add all tile renderers to this list so they can be updated later
    private List<MeshRenderer> tileRenderers = new List<MeshRenderer>();
    private static bool tilesVisible = false;

    // Update is called once per frame
    private void Awake()
    {
        GenerateTiles(tileSize, nTiles, mTiles);
        GenerateWarehouse();
        InitializeObjects();
    }

    private void Update()
    {
        UpdateTileVisibility();
    }


    private void InitializeObjects()
    {
        GameObject agentsWrapper = new("Agents");


        int agentCount = agents - 1;
        int objectCount = items - 1;
        int[,] randomPositions = GenerateUniqueRandomPositions();

        for (int i = 0; i < nTiles; i++)
            for (int j = 0; j < mTiles; j++)
                if (randomPositions[i, j] != 0)
                {
                    if (agentCount >= 0)
                    {
                        SpawnAgent(i, j, agentCount + 1).transform.parent = agentsWrapper.transform;
                        agentCount -= 1;
                    }
                    // else if (objectCount >= 0)
                    // {
                    //     SpawnObject(i, j);
                    //     objectCount -= 1;
                    // }
                }
    }

    private GameObject SpawnAgent(int x, int y, int id)
    {
        Vector3 position = CalculateObjectPosition(x, y);
        GameObject agentObject = Instantiate(AgentPrefab, position, Quaternion.identity);

        if(agentObject.TryGetComponent<Agent>(out var agent))
        {
            agent.pos = new Vector2(x, y);
            agent.id = id;
            agent.name = "Agent: " + id;
        }

        return agentObject;
    }

    private Vector3 CalculateObjectPosition(int x, int y)
    {
        Vector3 tilePosition = new Vector3(x * tileSize, yOffset, y * tileSize) - bounds;
        Vector3 centeredPosition = tilePosition + new Vector3(tileSize / 2, 0, tileSize / 2);

        return centeredPosition;
    }

    private void GenerateTiles(float tileSize, int nTiles, int mTiles)
    {
        //Wrapper
        GameObject tilesWrapper = new GameObject();
        tilesWrapper.name = "Tiles";
        tilesWrapper.transform.parent = transform;


        //Center
        yOffset += transform.position.y;
        bounds = new Vector3(nTiles * tileSize / 2, 0, mTiles * tileSize / 2) + center;

        tiles = new GameObject[nTiles, mTiles];
        for (int n = 0; n < nTiles; n++)
        {
            for (int m = 0; m < mTiles; m++)
            {
                tiles[n, m] = GenerateTile(tileSize, n, m, gap);
                tiles[n, m].transform.parent = tilesWrapper.transform;
            }
        }
    }

    private GameObject GenerateTile(float tileSize, int n, int m, float gap)
    {
        GameObject tileObject = new(string.Format("X:{0} Y:{1}", n, m));
        tileObject.transform.parent = transform;
        Mesh mesh = new();
        tileObject.AddComponent<MeshFilter>().mesh = mesh;
        MeshRenderer tileRenderer = tileObject.AddComponent<MeshRenderer>();
        tileRenderer.material = tileMaterial;

        float effectiveTileSize = tileSize - gap;
        float offset = gap / 2;

        Vector3[] vertices = new Vector3[4];
        vertices[0] = new Vector3(n * tileSize + offset, yOffset, m * tileSize + offset) - bounds;
        vertices[1] = new Vector3(n * tileSize + offset, yOffset, m * tileSize + effectiveTileSize + offset) - bounds;
        vertices[2] = new Vector3(n * tileSize + effectiveTileSize + offset, yOffset, m * tileSize + offset) - bounds;
        vertices[3] = new Vector3(n * tileSize + effectiveTileSize + offset, yOffset, m * tileSize + effectiveTileSize + offset) - bounds;

        int[] tris = new int[] { 0, 1, 2, 1, 3, 2 };
        mesh.vertices = vertices;
        mesh.triangles = tris;
        mesh.RecalculateNormals();

        // Adjust the box collider to match the new tile size
        BoxCollider collider = tileObject.AddComponent<BoxCollider>();
        collider.size = new Vector3(effectiveTileSize, 0.1f, effectiveTileSize); // Adjust the Y value as needed
        collider.center = new Vector3(effectiveTileSize / 2 + offset, 0, effectiveTileSize / 2 + offset);

        // Add tile renderer to array
        tileRenderers.Add(tileRenderer);

        return tileObject;
    }

    private void GenerateWarehouse()
    {
        GameObject warehouseWrapper = new GameObject("Warehouse");
        warehouseWrapper.transform.parent = transform;

        GenerateFloor().transform.parent = warehouseWrapper.transform;
        GenerateWalls().transform.parent = warehouseWrapper.transform;
    }

    private GameObject GenerateFloor()
    {
        GameObject floorObject = new GameObject("Floor");
        floorObject.transform.parent = transform;

        MeshFilter meshFilter = floorObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = floorObject.AddComponent<MeshRenderer>();

        Mesh mesh = new Mesh();
        meshFilter.mesh = mesh;
        meshRenderer.material = groundMaterial;

        float totalWidth = nTiles * tileSize;
        float totalLength = mTiles * tileSize;

        Vector3[] vertices = new Vector3[8];
        vertices[0] = new Vector3(-gap, -baseThickness, -gap);
        vertices[1] = new Vector3(totalWidth + gap, -baseThickness, -gap);
        vertices[2] = new Vector3(-gap, 0, -gap);
        vertices[3] = new Vector3(totalWidth + gap, 0, -gap);
        vertices[4] = new Vector3(-gap, -baseThickness, totalLength + gap);
        vertices[5] = new Vector3(totalWidth + gap, -baseThickness, totalLength + gap);
        vertices[6] = new Vector3(-gap, 0, totalLength + gap);
        vertices[7] = new Vector3(totalWidth + gap, 0, totalLength + gap);

        int[] triangles = new int[]
        {
        0, 2, 1, 2, 3, 1,
        1, 3, 5, 3, 7, 5,
        5, 7, 4, 7, 6, 4,
        4, 6, 0, 6, 2, 0,
        2, 6, 3, 6, 7, 3,
        0, 1, 4, 1, 5, 4
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        floorObject.transform.position = new Vector3(-totalWidth / 2, 0, -totalLength / 2) + center;
        return floorObject;
    }
    private GameObject GenerateWalls()
    {
        GameObject wallsWrapper = new GameObject("Walls");

        float totalWidth = nTiles * tileSize;
        float totalLength = mTiles * tileSize;

        // Left wall
        GenerateWall(new Vector3(-totalWidth / 2 - wallGirth / 2, wallHeight / 2, 0),
                     new Vector3(wallGirth, wallHeight, totalLength + 2 * wallGirth)).transform.parent = wallsWrapper.transform;

        // Right wall
        GenerateWall(new Vector3(totalWidth / 2 + wallGirth / 2, wallHeight / 2, 0),
                     new Vector3(wallGirth, wallHeight, totalLength + 2 * wallGirth)).transform.parent = wallsWrapper.transform;

        // Back wall
        GenerateWall(new Vector3(0, wallHeight / 2, -totalLength / 2 - wallGirth / 2),
                     new Vector3(totalWidth + 2 * wallGirth, wallHeight, wallGirth)).transform.parent = wallsWrapper.transform;

        // Front wall
        GenerateWall(new Vector3(0, wallHeight / 2, totalLength / 2 + wallGirth / 2),
                     new Vector3(totalWidth + 2 * wallGirth, wallHeight, wallGirth)).transform.parent = wallsWrapper.transform;

        return wallsWrapper;
    }

    private GameObject GenerateWall(Vector3 position, Vector3 size)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.parent = transform;
        wall.transform.localPosition = position + center;
        wall.transform.localScale = size;
        wall.GetComponent<Renderer>().material = wallMaterial;

        return wall;
    }

    private void UpdateTileVisibility()
    {
        foreach (MeshRenderer renderer in tileRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = tilesVisible;
            }
        }
    }

    private int[,] GenerateUniqueRandomPositions()
    {
        int[,] randomPositions = new int[nTiles, mTiles];
        HashSet<int[,]> usedPositions = new HashSet<int[,]>();
        int i = 0, j = 0;

        while (i < agents + items)
        {
            // Generate a new random position
            int k = Random.Range(0, nTiles);
            int l = Random.Range(0, mTiles);
            int[,] newPosition = new int[k, l];

            // Check if the position is already used
            if (!usedPositions.Contains(newPosition))
            {
                // Add the new position to the array and the set of used positions
                randomPositions[k, l] = 1;
                usedPositions.Add(newPosition);
                i++; j++;
            }
        }

        return randomPositions;
    }

    public static void ToggleTileVisibility()
    {
        tilesVisible = !tilesVisible;
    }
}
