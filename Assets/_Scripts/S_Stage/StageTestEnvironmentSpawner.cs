using UnityEngine;

public class StageTestEnvironmentSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WorldMapState _worldMapState;
    [SerializeField] private GameObject _unitPrefab;
    [SerializeField] private GameObject _buildingPrefab;

    [Header("Units")]
    [SerializeField] private int _unitCount = 6;
    [SerializeField] private int _unitRowCount = 3;
    [SerializeField] private Vector3 _unitStart = new(-4f, 0f, -2f);
    [SerializeField] private float _unitSpacing = 1.6f;
    [SerializeField] private float _unitHeightOffset = 1f;

    [Header("Buildings")]
    [SerializeField] private int _buildingCount = 3;
    [SerializeField] private Vector3 _buildingStart = new(3f, 0f, 2f);
    [SerializeField] private float _buildingSpacing = 2.5f;
    [SerializeField] private float _buildingHeightOffset = 0.5f;

    [Header("Spawn")]
    [SerializeField] private bool _skipIfSpawned = true;
    [SerializeField] private string _spawnRootName = "TestEnvironment";

    private void Awake()
    {
        AutoWireIfNeeded();
    }

    private void Start()
    {
        if (_unitPrefab == null && _buildingPrefab == null)
            return;

        var root = GetOrCreateRoot();
        if (_skipIfSpawned && root.childCount > 0)
            return;

        SpawnBuildings(root);
        SpawnUnits(root);
    }

    private Transform GetOrCreateRoot()
    {
        var existing = transform.Find(_spawnRootName);
        if (existing != null)
            return existing;

        var root = new GameObject(_spawnRootName);
        root.transform.SetParent(transform, false);
        return root.transform;
    }

    private void SpawnUnits(Transform parent)
    {
        if (_unitPrefab == null || _unitCount <= 0)
            return;

        var rowCount = Mathf.Max(1, _unitRowCount);
        var y = GetGroundHeight(_unitStart.y) + _unitHeightOffset;

        for (var i = 0; i < _unitCount; i++)
        {
            var x = i % rowCount;
            var z = i / rowCount;
            var pos = _unitStart + new Vector3(x * _unitSpacing, 0f, z * _unitSpacing);
            pos.y = y;
            Instantiate(_unitPrefab, pos, Quaternion.identity, parent);
        }
    }

    private void SpawnBuildings(Transform parent)
    {
        if (_buildingPrefab == null || _buildingCount <= 0)
            return;

        var y = GetGroundHeight(_buildingStart.y) + _buildingHeightOffset;
        for (var i = 0; i < _buildingCount; i++)
        {
            var pos = _buildingStart + new Vector3(i * _buildingSpacing, 0f, 0f);
            pos.y = y;
            Instantiate(_buildingPrefab, pos, Quaternion.identity, parent);
        }
    }

    private float GetGroundHeight(float fallback)
    {
        if (_worldMapState != null)
            return _worldMapState.TileHeight;

        return fallback;
    }

    private void AutoWireIfNeeded()
    {
        if (_worldMapState == null)
            _worldMapState = FindFirstObjectByType<WorldMapState>();
    }
}
