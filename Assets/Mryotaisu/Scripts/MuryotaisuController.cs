using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

namespace Muryotaisu
{
    public class MuryotaisuController : MonoBehaviour
    {
        private Animator animator; // 캐릭터의 걷기, 점프, idle 모션을 제어하는 애니메이터
        private CharacterController controller;

        private CinemachineCamera cmCamera;
        private CinemachinePanTilt panTilt;

        [Header("1인칭 이동 설정")]
        public float speed = 6.0f; // 이동 속도
        public float jumpSpeed = 5.0f; // 점프 힘
        public float gravity = 9.8f; // 중력 크기

        [Header("마우스 시선 회전 설정")]
        public float mouseSensitivity = 2.0f; // 감도 변수 

        [Header("자유 시선")]
        private bool isFreeLooking = false;
        private float initialBodyY = 180f; // F키 누를 당시의 몸통 방향 백업

        // 자유 시선 상태에서 마우스 움직임을 누적하기 위한 회전 변수
        private float xRotation = 0f;
        private float yRotation = 180f;
        private float freeLookCombinedY = 180f;

        public float freeLookLimitAngle = 80f; // F키를 눌렀을 때 몸통 기준 좌우로 꺾일 수 있는 최대 각도 제한

        public float startKocchi = 2; // 카메라가 캐릭터와 얼마나 가까워졌을 때 캐릭터가 카메라를 쳐다보게 할지 결정하는 기준 거리 변수

        float second; // 캐릭터가 idle 상태로 머무른 시간을 측정하는 변수

        private Vector3 moveDirection = Vector3.zero; // 캐릭터가 이동하는 방향과 속도를 나타내는 벡터 변수(X, Y, Z축 방향으로의 이동 속도를 각각 저장)

        // Start is called before the first frame update
        void Start()
        {
            animator = GetComponent<Animator>();
            controller = GetComponent<CharacterController>();

            cmCamera = FindFirstObjectByType<CinemachineCamera>();

            if (cmCamera != null)
            {
                panTilt = cmCamera.GetComponent<CinemachinePanTilt>();

                // 초기 각도를 시네머신의 현재 값과 동기화
                if (panTilt != null)
                {
                    yRotation = panTilt.PanAxis.Value;
                    xRotation = panTilt.TiltAxis.Value;
                }
            }

            // 마우스 커서를 화면 중앙에 고정시키고 보이지 않도록 설정
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Update is called once per frame
        void Update()
        {
            if (cmCamera == null) return;

            // 마우스 입력을 직접 가로채어 시네머신 카메라와 몸통을 직접 제어합니다.
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -60f, 60f); // 목 너무 안 꺾이게 고정

            // F 키 입력 실시간 감지
            if (Input.GetKeyDown(KeyCode.F))
            {
                isFreeLooking = true;
                // F키를 누르는 순간, 현재 몸통이 바라보던 Y축 각도를 고정하기 위해 백업
                initialBodyY = transform.eulerAngles.y;
                freeLookCombinedY = yRotation;
            }

            if (Input.GetKeyUp(KeyCode.F))
            {
                isFreeLooking = false;
                // F키를 떼면, 자유시선으로 둘러보던 카메라의 정면 방향으로 몸통을 즉시 정렬
                yRotation = freeLookCombinedY;
                transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
            }

            if (!isFreeLooking)
            {
                // [일반 모드]: 마우스 돌리는 대로 시선과 몸통이 함께 360도 회전
                yRotation += mouseX;
                transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
                cmCamera.transform.rotation = Quaternion.Euler(xRotation, yRotation, 0f);

                // 시네머신 컴포넌트 내부 데이터도 함께 갱신하여 튀는 현상 방지
                if (panTilt != null)
                {
                    panTilt.PanAxis.Value = yRotation;
                    panTilt.TiltAxis.Value = xRotation;
                }
            }
            else
            {
                // [F키 자유시선 모드]: 몸통은 F키 누를 당시 각도로 강제 고정
                freeLookCombinedY += mouseX;

                // 고개가 360도 돌아가지 않도록 현재 몸통(yRotation) 기준 좌우 일정 각도 내로 제한
                float angleDiff = Mathf.DeltaAngle(yRotation, freeLookCombinedY);
                angleDiff = Mathf.Clamp(angleDiff, -freeLookLimitAngle, freeLookLimitAngle);
                freeLookCombinedY = yRotation + angleDiff;

                // 카메라만 꺾인 각도를 적용합니다.
                cmCamera.transform.rotation = Quaternion.Euler(xRotation, freeLookCombinedY, 0f);

                if (panTilt != null)
                {
                    panTilt.PanAxis.Value = freeLookCombinedY;
                    panTilt.TiltAxis.Value = xRotation;
                }
                transform.rotation = Quaternion.Euler(0f, initialBodyY, 0f);
            }


            // 바닥 기준 물리 이동 및 애니메이션 처리

            if (controller.isGrounded)
            {
                float horizontal = Input.GetAxis("Horizontal");
                float vertical = Input.GetAxis("Vertical");

                Vector3 inputDir = new Vector3(horizontal, 0f, vertical);
                if (inputDir.magnitude > 1f) inputDir.Normalize();

                // 캐릭터는 항상 마우스 시선과 별개로 자기 '몸통 정면' 기준으로 이동
                Vector3 move = (transform.forward * inputDir.z) + (transform.right * inputDir.x);

                float currentY = moveDirection.y;
                moveDirection = move * speed;
                moveDirection.y = currentY;

                if (Input.GetKeyDown(KeyCode.Space))
                {
                    moveDirection.y = jumpSpeed;
                }

                // 애니메이션 Bleed 차단용 조건문
                second += Time.deltaTime;
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    animator.SetBool("jumpFlag", true);
                    animator.SetBool("walkFlag", false);
                    animator.SetBool("idleFlag", false);
                }
                else if (inputDir.magnitude > 0.1f)
                {
                    animator.SetBool("jumpFlag", false);
                    animator.SetBool("walkFlag", true);
                    animator.SetBool("idleFlag", false);
                    second = 0;
                }
                else if (second >= 15f)
                {
                    animator.SetBool("jumpFlag", false);
                    animator.SetBool("walkFlag", false);
                    animator.SetBool("idleFlag", false);
                    animator.SetTrigger("idleBFlag");
                    second = 0;
                }
                else
                {
                    animator.SetBool("jumpFlag", false);
                    animator.SetBool("walkFlag", false);
                    animator.SetBool("idleFlag", true);
                }
            }

            moveDirection.y -= gravity * Time.deltaTime;
            controller.Move(moveDirection * Time.deltaTime);

            // Q키 미소
            animator.SetBool("smileFlag", Input.GetKey("q"));

            // 눈싸움 기능 (시네머신 카메라와의 거리 계산)
            float dist = Vector3.Distance(transform.position, cmCamera.transform.position);
            animator.SetBool("kocchiFlag", dist < startKocchi);
        }
    }
}