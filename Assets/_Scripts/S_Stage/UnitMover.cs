using System.Reflection;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class UnitMover : MonoBehaviour
{
    [SerializeField] private float _moveSpeed = 4f;
    [SerializeField] private float _stoppingDistance = 0.15f;
    [SerializeField] private bool _faceMoveDirection = true;
    [SerializeField] private float _turnSpeed = 720f;

    private Rigidbody _rigidbody;
    private Vector3 _destination;
    private bool _hasDestination;

    private static bool _checkedLinearVelocity;
    private static bool _hasLinearVelocity;
    private static PropertyInfo _linearVelocityProperty;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        ApplyRigidbodyDefaults();
        CacheLinearVelocitySupport();
    }

    public void SetDestination(Vector3 worldPosition)
    {
        _destination = worldPosition;
        _hasDestination = true;
        _rigidbody?.WakeUp();
    }

    private void FixedUpdate()
    {
        if (!_hasDestination)
        {
            SetLinearVelocity(Vector3.zero);
            return;
        }

        var position = _rigidbody.position;
        var toTarget = _destination - position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude <= _stoppingDistance * _stoppingDistance)
        {
            _hasDestination = false;
            SetLinearVelocity(Vector3.zero);
            return;
        }

        var direction = toTarget.normalized;
        SetLinearVelocity(direction * _moveSpeed);

        if (_faceMoveDirection && direction.sqrMagnitude > 0.0001f)
        {
            var targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            var rotation = Quaternion.RotateTowards(_rigidbody.rotation, targetRotation, _turnSpeed * Time.fixedDeltaTime);
            _rigidbody.MoveRotation(rotation);
        }
    }

    private void SetLinearVelocity(Vector3 velocity)
    {
        if (_hasLinearVelocity && _linearVelocityProperty != null)
        {
            _linearVelocityProperty.SetValue(_rigidbody, velocity);
        }
        else
        {
            _rigidbody.linearVelocity = velocity;
        }
    }

    private static void CacheLinearVelocitySupport()
    {
        if (_checkedLinearVelocity)
            return;

        _checkedLinearVelocity = true;
        _linearVelocityProperty = typeof(Rigidbody).GetProperty("linearVelocity");
        _hasLinearVelocity = _linearVelocityProperty != null && _linearVelocityProperty.CanWrite;
    }

    private void ApplyRigidbodyDefaults()
    {
        if (_rigidbody == null)
            return;

        _rigidbody.isKinematic = false;
        _rigidbody.useGravity = false;
        _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        _rigidbody.constraints = RigidbodyConstraints.FreezePositionY |
                                 RigidbodyConstraints.FreezeRotationX |
                                 RigidbodyConstraints.FreezeRotationZ;
    }
}
