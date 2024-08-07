using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Windows;
using StarterAssets;
using UnityEngine.InputSystem;
using UnityEngine.AI;

public class PlayerController : MonoBehaviour
{
    IKFootBehavior _ikFoot;
    Animator _animator;
    StarterAssetsInputs _input;
    Rigidbody _rigid;
    GameObject _mainCamera;

    public bool LockCameraPosition = false;
    private const float Threshold = 0.01f;

    private float targetRotation;
    private float rotationVelocity;

    private Vector3 targetEuler;

    public GameObject CinemachineCameraTarget;

    public float TopClamp = 70.0f;
    public float BottomClamp = -30.0f;
    public float CameraAngleOverride = 0.0f;

    private float _cinemachineTargetYaw;
    private float _cinemachineTargetPitch;

    private float moveSpeed
    {
        get
        {
            return _input.sprint ? 5.3f : 2.5f;
        }
    }

    private float curMoveSpeed;

    private int animIDMoveSpeed = Animator.StringToHash("MoveSpeed");

    private void Awake()
    {
        _mainCamera = Camera.main.gameObject;
    }

    void Start()
    {
        _input = GetComponent<StarterAssetsInputs>();
        _animator = GetComponent<Animator>();
        _rigid = GetComponent<Rigidbody>();
        _ikFoot = GetComponent<IKFootBehavior>();
    }

    void Update()
    {
        
    }

    private void FixedUpdate()
    {
        _ikFoot.OnIKUpdate();
        Move();
        BodyRotation();
    }

    private void LateUpdate()
    {
        CameraRotation();
    }

    private void BodyRotation()
    {
        Vector3 ikEuler = _ikFoot.targetEulerAngle;
        
        transform.eulerAngles = new Vector3(ikEuler.x,targetEuler.y,ikEuler.z);
    }

    private void Move()
    {
        float targetSpeed = moveSpeed;
        if (_input.move == Vector2.zero)
            targetSpeed = 0f;

        float curHorizontalSpeed = new Vector3(_rigid.velocity.x,0f, _rigid.velocity.z).magnitude;
        float speedOffset = 0.3f;

        if (curHorizontalSpeed < targetSpeed - speedOffset ||
                curHorizontalSpeed > targetSpeed + speedOffset)
        {
            curMoveSpeed = Mathf.Lerp(curHorizontalSpeed, targetSpeed, Time.deltaTime * 10f);

            curMoveSpeed = Mathf.Round(curMoveSpeed * 1000f) / 1000f;
        }
        else
        {
            curMoveSpeed = targetSpeed;
        }

        _animator.SetFloat(animIDMoveSpeed, curMoveSpeed);

        Vector3 inputDirection = new Vector3(_input.move.x,0f,_input.move.y).normalized;

        if (_input.move != Vector2.zero)
        {
            targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg +
                              _mainCamera.transform.eulerAngles.y;
            float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetRotation, ref rotationVelocity,
                    0.12f);

            Quaternion yRot = Quaternion.Euler(0f,rotation, 0f);

            targetEuler = yRot.eulerAngles;
        }

        Vector3 targetDirection = Quaternion.Euler(0f,targetRotation,0f) * Vector3.forward;

        _rigid.velocity = new Vector3(targetDirection.x * curMoveSpeed, _rigid.velocity.y, targetDirection.z * curMoveSpeed);
    }

    private void CameraRotation()
    {
        if (_input.look.sqrMagnitude >= Threshold && !LockCameraPosition)
        {
            float deltaTimeMultiplier = 1.0f;

            _cinemachineTargetYaw += _input.look.x * deltaTimeMultiplier;
            _cinemachineTargetPitch += _input.look.y * deltaTimeMultiplier;
        }

        _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
        _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

        CinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch + CameraAngleOverride,
            _cinemachineTargetYaw, 0.0f);
    }

    private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
    {
        if (lfAngle < -360f)
            lfAngle += 360f;
        if (lfAngle > 360f)
            lfAngle -= 360f;
        return Mathf.Clamp(lfAngle, lfMin, lfMax);
    }
}
