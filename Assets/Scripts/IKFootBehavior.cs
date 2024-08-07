using System.Collections;
using System.Collections.Generic;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class IKFootBehavior : MonoBehaviour
{
    enum Foot
    {
        RF,
        RB,
        LB,
        LF,
        Count,
    }

    [Header("[Components]")]
    [SerializeField] private Animator animator;
    [SerializeField] private CapsuleCollider capCol;

    [Header("[Foot Transforms]")]
    [SerializeField] private Transform footTransformRF;
    [SerializeField] private Transform footTransformRB;
    [SerializeField] private Transform footTransformLB;
    [SerializeField] private Transform footTransformLF;
    private Transform[] allFootTransforms;

    [Header("[Foot Targets Transforms]")]
    [SerializeField] private Transform footTargetTransformRF;
    [SerializeField] private Transform footTargetTransformRB;
    [SerializeField] private Transform footTargetTransformLB;
    [SerializeField] private Transform footTargetTransformLF;
    private Transform[] allTargetTransforms;

    [Header("[Foot Rigs]")]
    [SerializeField] private GameObject footRigRF;
    [SerializeField] private GameObject footRigRB;
    [SerializeField] private GameObject footRigLB;
    [SerializeField] private GameObject footRigLF;
    private TwoBoneIKConstraint[] allFootIKConstrains;

    private LayerMask groundLayerMask;

    [Header("[Variable]")]
    [SerializeField] private float maxHitDistance = 5f;
    [SerializeField] private float addedHeight = 3f;
    [SerializeField,Range(-0.5f, 2)] private float upperFootYLimit = 0.3f;
    [SerializeField,Range(-2f,0.5f)] private float lowerFootYLimit = -0.1f;
    private float[] allFootWeight;
    private float yOffset;

    private bool[] allGroundSpherecastHits;
    private LayerMask hitLayer;
    private Vector3[] allHitNormals;
    private Vector3 aveHitNormal;

    private float angleAboutX;
    private float angleAboutZ;

    private int[] checkLocalTargetY;

    // animHash
    private int animIDRFFootWeight = Animator.StringToHash("RFFootWeight");
    private int animIDRBFootWeight = Animator.StringToHash("RBFootWeight");
    private int animIDLBFootWeight = Animator.StringToHash("LBFootWeight");
    private int animIDLFFootWeight = Animator.StringToHash("LFFootWeight");

    public Vector3 targetEulerAngle;
    // Start is called before the first frame update
    void Start()
    {
        allFootTransforms = new Transform[(int)Foot.Count]
        {
            footTransformRF,
            footTransformRB,
            footTransformLB,
            footTransformLF,
        };

        allTargetTransforms = new Transform[(int)Foot.Count]
        {
            footTargetTransformRF,
            footTargetTransformRB,
            footTargetTransformLB,
            footTargetTransformLF,
        };

        allFootIKConstrains = new TwoBoneIKConstraint[(int)Foot.Count]
        {
            footRigRF.GetComponent<TwoBoneIKConstraint>(),
            footRigRB.GetComponent<TwoBoneIKConstraint>(),
            footRigLB.GetComponent<TwoBoneIKConstraint>(),
            footRigLF.GetComponent<TwoBoneIKConstraint>(),
        };

        groundLayerMask = LayerMask.NameToLayer("Ground");

        allGroundSpherecastHits = new bool[(int)Foot.Count + 1];

        allHitNormals = new Vector3[(int)Foot.Count];

        allFootWeight = new float[(int)Foot.Count];

        checkLocalTargetY = new int[(int)Foot.Count];
    }

    public void OnIKUpdate()
    {
        RotateCharacterFeet();
        RotateCharacterBody();
        CharacterHeightAdjustments();
    }

    private void LateUpdate()
    {
        OnUpdateAllFootWeight();
    }

    private void CheckGroundBelow(out Vector3 hitPoint, out bool gotGroundSphereCastHit, out Vector3 hitNormal, out LayerMask hitLayer,
        out float currentHitDistance, Transform objectTransform, int checkForLayerMask, float maxHitDistance, float addedHeight)
    {

        RaycastHit hit;
        Vector3 startSpherecast = objectTransform.position + new Vector3(0f,addedHeight,0f);
        if (checkForLayerMask == -1)
        {
            Debug.LogError("Layer does not exist!");
            gotGroundSphereCastHit = false;
            currentHitDistance = 0f;
            hitLayer = LayerMask.NameToLayer("Player");
            hitNormal = Vector3.up;
            hitPoint = objectTransform.position;
        }
        else
        {
            int layerMask = (1 << checkForLayerMask);
            if (Physics.SphereCast(startSpherecast, 0.2f, Vector3.down, out hit, maxHitDistance, layerMask, QueryTriggerInteraction.UseGlobal))
            {
                hitLayer = hit.transform.gameObject.layer;
                currentHitDistance = hit.distance - addedHeight;
                hitNormal = hit.normal;
                gotGroundSphereCastHit = true;
                hitPoint = hit.point;
            }
            else
            {
                gotGroundSphereCastHit = false;
                currentHitDistance = 0f;
                hitLayer = LayerMask.NameToLayer("Player");
                hitNormal = Vector3.up;
                hitPoint = objectTransform.position;
            }
        }
    }

    private Vector3 ProjectOnContractPlane(Vector3 vector, Vector3 hitNormal)
    {
        // vector를 Plane에 투영하는 메서드
        // hitNormal == Plane에 수직인 벡터
        // 1. vector와 hitNormal의 내적(Vector3.Dot)을 구한다.
        // 2. 법선 벡터(hitNormal)에 내적 값을 곱하여 벡터의 법선 방향 성분을 구한다.
        // 3. vector에서 법선 방향 성분을 빼면 평면에 투영된 벡터(위치벡터)를 얻을 수 있다.
        // * 법선방향벡터: 어떠한 평면이나 곡면에 대하여 수직(직교)하는 벡터

        return vector - hitNormal * Vector3.Dot(vector, hitNormal);
    }

    private void ProjectAxisAngles(out float angleAboutX, out float angleAboutZ, Transform footTargetTransform, Vector3 hitNormal)
    {
        Vector3 xAxisProjected = ProjectOnContractPlane(footTargetTransform.forward, hitNormal).normalized;
        Vector3 zAxisProjected = ProjectOnContractPlane(footTargetTransform.right, hitNormal).normalized;

        angleAboutX = Vector3.SignedAngle(footTargetTransform.forward, xAxisProjected, footTargetTransform.right);
        angleAboutZ = Vector3.SignedAngle(footTargetTransform.right, zAxisProjected, footTargetTransform.forward);
    }

    private void RotateCharacterFeet()
    {
        for (int i = 0; i < (int)Foot.Count; ++i)
        {
            allFootIKConstrains[i].weight = allFootWeight[i];

            CheckGroundBelow(out Vector3 hitPoint, out allGroundSpherecastHits[i], out allHitNormals[i], out hitLayer, out _,
                allFootTransforms[i], groundLayerMask, maxHitDistance, addedHeight);

            if (allGroundSpherecastHits[i] == true)
            {
                yOffset = 0.15f;

                if (allFootTransforms[i].position.y < allTargetTransforms[i].position.y - 0.1f)
                {
                    yOffset += allTargetTransforms[i].position.y - allFootTransforms[i].position.y;
                }

                ProjectAxisAngles(out angleAboutX, out angleAboutZ, allFootTransforms[i], allHitNormals[i]);

                allTargetTransforms[i].position = new Vector3(allFootTransforms[i].position.x, hitPoint.y + yOffset, allFootTransforms[i].position.z);

                allTargetTransforms[i].rotation = allFootTransforms[i].rotation;

                allTargetTransforms[i].localEulerAngles = new Vector3(allTargetTransforms[i].localEulerAngles.x + angleAboutX,
                    allTargetTransforms[i].localEulerAngles.y, allTargetTransforms[i].localEulerAngles.z + angleAboutZ);
            }
            else
            {
                // 스페어캐스트가 실패했을 때 IK 타겟의 위치를 실제 발의 위치와 동기화한다.
                allTargetTransforms[i].position = allFootTransforms[i].position;

                allTargetTransforms[i].rotation = allFootTransforms[i].rotation;
            }
        }
    }

    private void OnUpdateAllFootWeight()
    {
        allFootWeight[(int)Foot.RF] = animator.GetFloat(animIDRFFootWeight);
        allFootWeight[(int)Foot.RB] = animator.GetFloat(animIDRBFootWeight);
        allFootWeight[(int)Foot.LB] = animator.GetFloat(animIDLBFootWeight);
        allFootWeight[(int)Foot.LF] = animator.GetFloat(animIDLFFootWeight);
    }

    private void RotateCharacterBody()
    {
        float maxrotationStep = 1f;
        float aveHitNormalX = 0f;
        float aveHitNormalY = 0f;
        float aveHitNormalZ = 0f;
        for (int i = 0; i < (int)Foot.Count; ++i)
        {
            aveHitNormalX += allHitNormals[i].x;
            aveHitNormalY += allHitNormals[i].y;
            aveHitNormalZ += allHitNormals[i].z;
        }

        aveHitNormal = new Vector3(aveHitNormalX / (int)Foot.Count, aveHitNormalY / 4, aveHitNormalZ / 4).normalized;

        ProjectAxisAngles(out angleAboutX, out angleAboutZ, transform, aveHitNormal);

        float maxRotationX = 50f;
        float maxRotationZ = 20f;

        float characterXRotation = transform.eulerAngles.x;
        float characterZRotation = transform.eulerAngles.z;

        if (characterXRotation > 180f)
        {
            characterXRotation -= 360f;
        }
        if (characterZRotation > 180f)
        {
            characterZRotation -= 360f;
        }

        if (characterXRotation + angleAboutX < -maxRotationX)
        {
            angleAboutX = maxRotationX + characterXRotation;
        }
        else if (characterXRotation + angleAboutX > maxRotationX)
        {
            angleAboutX = maxRotationX - characterXRotation;
        }
        if (characterZRotation + angleAboutZ < -maxRotationZ)
        {
            angleAboutZ = maxRotationZ + characterZRotation;
        }
        else if (characterZRotation + angleAboutZ > maxRotationZ)
        {
            angleAboutZ = maxRotationZ - characterZRotation;
        }

        float bodyEulerX = Mathf.MoveTowardsAngle(0, angleAboutX, maxrotationStep);
        float bodyEulerZ = Mathf.MoveTowardsAngle(0, angleAboutZ, maxrotationStep);

        Quaternion curRot = transform.rotation;

        Quaternion xRot = Quaternion.Euler(transform.eulerAngles.x + bodyEulerX,0f,0f);
        Quaternion zRot = Quaternion.Euler(0f,0f,transform.eulerAngles.z +bodyEulerZ);

        targetEulerAngle = new Vector3(transform.eulerAngles.x + bodyEulerX, transform.eulerAngles.y, transform.eulerAngles.z + bodyEulerZ);

        // transform.eulerAngles = new Vector3(transform.eulerAngles.x + bodyEulerX, transform.eulerAngles.y, transform.eulerAngles.z + bodyEulerZ);
    }

    private void CharacterHeightAdjustments()
    {
        for (int i = 0; i < (int)Foot.Count; ++i)
        {
            if (allTargetTransforms[i].localPosition.y < upperFootYLimit && allTargetTransforms[i].localPosition.y > lowerFootYLimit)
            {
                checkLocalTargetY[i] = 0;
            }
            else if (allTargetTransforms[i].localPosition.y > upperFootYLimit)
            {
                // squishy leg
                checkLocalTargetY[i] = 1;
            }
            else
            {
                // stretchy leg
                checkLocalTargetY[i] = -1;
            }
        }

        if (checkLocalTargetY[(int)Foot.RF] == 1 && checkLocalTargetY[(int)Foot.LB] == 1 ||
            checkLocalTargetY[(int)Foot.RB] == 1 && checkLocalTargetY[(int)Foot.LF] == 1)
        {
            if (capCol.center.y > -1.4f)
            {
                capCol.center -= new Vector3(0f, 0.05f, 0f);
            }
            else
            {
                capCol.center = new Vector3(0f, 3.4f, 0f);
            }
        }
        else if (checkLocalTargetY[(int)Foot.RF] == -1 && checkLocalTargetY[(int)Foot.LB] == -1 ||
            checkLocalTargetY[(int)Foot.RB] == -1 && checkLocalTargetY[(int)Foot.LF] == -1)
        {
            if (capCol.center.y < 1.5f)
            {
                capCol.center += new Vector3(0f, 0.05f, 0f);
            }
        }
    }
}
