using UnityEngine;

// 유닛 이동 컴포넌트. Rigidbody 기반 물리 이동.
[RequireComponent(typeof(Rigidbody))]
public class UnitMover : MonoBehaviour
{
    [SerializeField] private float _moveSpeed = 4f;
    [SerializeField] private float _stoppingDistance = 0.15f;

    private Rigidbody _rigidbody;
    private Vector3 _destination;
    private bool _hasDestination;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
    }

    // 목적지 설정
    public void SetDestination(Vector3 worldPosition)
    {
        _destination = worldPosition;
        _hasDestination = true;
        _rigidbody.WakeUp();
    }

    private void FixedUpdate()
    {
        // 목적지 없으면 정지
        if (!_hasDestination)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            return;
        }

        var position = _rigidbody.position;
        var toTarget = _destination - position;
        toTarget.y = 0f; // XZ 평면에서만 이동

        // 도착 체크
        if (toTarget.sqrMagnitude <= _stoppingDistance * _stoppingDistance)
        {
            _hasDestination = false;
            _rigidbody.linearVelocity = Vector3.zero;
            return;
        }

        // 이동 및 회전
        var direction = toTarget.normalized;
        _rigidbody.linearVelocity = direction * _moveSpeed;

        if (direction.sqrMagnitude > 0.0001f)
        {
            _rigidbody.MoveRotation(Quaternion.LookRotation(direction, Vector3.up));
        }
    }
}
