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

        [Header("카메라 얼굴 위치 기준점")]
        public Transform cameraTarget;

        [Header("시점 전환 설정 (V키)")]
        public bool isFirstPerson = true; // true면 1인칭, false면 3인칭
        public float thirdPersonDistance = 3.0f; // 3인칭일 때 등 뒤로 떨어지는 거리

        [Header("1인칭 이동 설정")]
        public float speed = 6.0f; // 이동 속도
        public float jumpSpeed = 5.0f; // 점프 힘
        public float gravity = 9.8f; // 중력 크기

        [Header("마우스 시선 회전 설정")]
        public float mouseSensitivity = 2.0f; // 감도 변수 

        [Header("자유 시선")]
        private bool isFreeLooking = false;
        private float initialBodyY = 180f; // F키 누를 당시의 몸통 방향 백업

        [Header("착지 경직 설정")]
        public float minLandingStun = 0.8f; // 애니메이션이 끊기지 않고 끝까지 재생될 '최소' 보장 시간 (애니메이션 길이에 맞춰 조절!)
        public float maxLandingStun = 1.8f; //  아무리 높은 곳에서 떨어져도 제한되는 '최대' 경직 시간
        private float currentLandingDuration = 0.5f; // 이번 착지에 적용될 동적 경직 시간

        // 자유 시선 상태에서 마우스 움직임을 누적하기 위한 회전 변수
        private float xRotation = 0f;
        private float yRotation = 180f;
        private float freeLookCombinedY = 180f;

        public float freeLookLimitAngle = 80f; // F키를 눌렀을 때 몸통 기준 좌우로 꺾일 수 있는 최대 각도 제한

        public float startKocchi = 2; // 카메라가 캐릭터와 얼마나 가까워졌을 때 캐릭터가 카메라를 쳐다보게 할지 결정하는 기준 거리 변수


        private const float minLandTimeThreshold = 0.85f; // 이 시간(초) 이하면 착지 모션 생략
        private string currentLandingAnim = ""; 

        float second; // 캐릭터가 idle 상태로 머무른 시간을 측정하는 변수

        private Vector3 moveDirection = Vector3.zero; // 캐릭터가 이동하는 방향과 속도를 나타내는 벡터 변수(X, Y, Z축 방향으로의 이동 속도를 각각 저장)

        private float airTime = 0f;
        private const float hardLandingThreshold = 2.0f; // 이 시간(초) 이상 떨어지면 세게 착지(Fall Land)로 판정
        private float landTimer = 0f;       // 착지 후 흘러간 시간을 재는 타이머
        private bool isLandingState = false; // 현재 착지 모션 강제 유지 중인지 체크하는 플래그

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

            // 시점 전환 (V 키)
            if (Input.GetKeyDown(KeyCode.V))
            {
                isFirstPerson = !isFirstPerson;
                Debug.Log("V키 눌림! 현재 모드 -> " + (isFirstPerson ? "1인칭" : "3인칭"));
            }

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
                // [일반 모드]: 마우스 돌리는 대로 시선 회전
                yRotation += mouseX;
                
                if (isFirstPerson)
                {
                    // 1인칭일 때만 몸통이 마우스 회전을 똑같이 따라감
                    transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
                }

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


            // -------------- 입력 및 이동 속도 계산 (지상/공중 공통) --------------
            // GetAxis 대신 GetAxisRaw를 사용하면 키를 누르자마자 딜레이 없이 즉시 최고 속도에 도달합니다(미끄러짐 방지)
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");

            Vector3 inputDir = new Vector3(horizontal, 0f, vertical);
            if (inputDir.magnitude > 1f) inputDir.Normalize();

            Vector3 move = Vector3.zero;

            if (isFirstPerson)
            {
                // 1인칭: 마우스 회전과 캐릭터 정면이 같으므로 자기 '몸통 정면' 기준으로 방향을 잡음
                move = (transform.forward * inputDir.z) + (transform.right * inputDir.x);
            }
            else
            {
                // 3인칭: 카메라가 바라보는 정면/우측 기준으로 방향을 잡음
                Vector3 camForward = cmCamera.transform.forward;
                camForward.y = 0f;
                camForward.Normalize();

                Vector3 camRight = cmCamera.transform.right;
                camRight.y = 0f;
                camRight.Normalize();

                move = (camForward * inputDir.z) + (camRight * inputDir.x);

                // 3인칭 모드에서는 마우스 입력이 아닌 이동하는 방향을 바라보며 몸통이 부드럽게 회전함
                if (move.magnitude > 0.01f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(move);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 15f);
                }
            }

            // 공중 컨트롤을 위해 움직임(X, Z축) 계산을 isGrounded 밖으로 빼냈습니다.
            // Y축(중력 및 점프 힘) 수명은 보존하고, 이동 방향 수치만 갱신합니다.
            float currentY = moveDirection.y;
            moveDirection = isLandingState ? Vector3.zero : move * speed;
            moveDirection.y = currentY;

            // -------------- 바닥 판정 및 애니메이션 처리 --------------
            string currentState = ""; // 현재 상태 로깅용 변수

            if (controller.isGrounded)
            {
                // 중력이 바닥에 무한히 누적되어 파묻히는 현상 보정 및 고정
                if (moveDirection.y < 0)
                {
                    moveDirection.y = -2f;
                }

                animator.speed = 1.0f;

                // 💡 [착지 판단 및 고정 타이머 매커니즘]
                if (isLandingState)
                {
                    landTimer += Time.deltaTime;
                    if (landTimer >= 0.5f)
                    {
                        // 착지 애니메이션 강제 대기 0.5초가 끝나면 완벽하게 지상 상태 초기화
                        isLandingState = false;
                        landTimer = 0f;
                    }
                }

                // 💡 땅에 닿았을 때 Fall 상태에서 누적되던 airTime 계산 및 딱 멈추기
                if (airTime > 0f && moveDirection.y <= 0)
                {
                    animator.SetBool("jumpFlag", false);
                    animator.SetBool("fallFlag", false); // 낙하 궤도에서 완벽 탈출!

                    //  [요구사항 2]: 체공 시간(airTime)이 일정 시간(0.2초) 이상일 때만 Land 발동
                    if (airTime >= minLandTimeThreshold)
                    {
                        animator.SetBool("walkFlag", false);
                        animator.SetBool("idleFlag", false);

                        // 착지 0.5초 락 시동
                        isLandingState = true;
                        landTimer = 0f;

                        currentLandingDuration = Mathf.Clamp(airTime, minLandingStun, maxLandingStun);

                        if (airTime >= hardLandingThreshold)
                        {
                            animator.SetTrigger("fallLandTrigger");
                            currentLandingAnim = "fall land"; // [요구사항 3] 이름 백업
                            currentState = $"쿵! 높은 추락 착지 ({currentLandingAnim}) | airTime: {airTime:F2}s";
                        }
                        else
                        {
                            animator.SetTrigger("jumpLandTrigger");
                            currentLandingAnim = "jump land"; // [요구사항 3] 이름 백업
                            currentState = $"사뿐, 낮은 추락 착지 ({currentLandingAnim}) | airTime: {airTime:F2}s";
                        }
                    }
                    else
                    {
                        // 💡 제자리 점프처럼 airTime이 아주 적은 시간일 때는 Land 애니메이션 락을 걸지 않고 패스
                        currentState = $"짧은 체공 착지 (Land 생략) | airTime: {airTime:F2}s";
                    }

                    airTime = 0f; // 연산 끝났으니 공중 시간 완전 리셋
                }

                // 점프 입력 (착지 경직 중에는 불가능)
                if (Input.GetKeyDown(KeyCode.Space) && !isLandingState)
                {
                    moveDirection.y = jumpSpeed;
                    second = 0f;
                    airTime = 0f;

                    animator.SetBool("walkFlag", false);
                    animator.SetBool("idleFlag", false);
                    animator.SetBool("jumpFlag", true);
                    currentState = "점프 시작!";
                }
                // 경직이 완전히 풀렸을 때만(isLandingState == false) 걷기/대기 허용
                else if (moveDirection.y <= 0 && !isLandingState)
                {
                    animator.SetBool("jumpFlag", false);
                    animator.SetBool("fallFlag", false);

                    if (inputDir.magnitude > 0.1f)
                    {
                        animator.SetBool("walkFlag", true);
                        animator.SetBool("idleFlag", false);
                        second = 0;
                        currentState = "걷기 (Walk)";
                    }
                    else
                    {
                        animator.SetBool("walkFlag", false);
                        animator.SetBool("idleFlag", true);

                        second += Time.deltaTime;
                        if (second >= 15f)
                        {
                            animator.SetTrigger("idleBFlag");
                            second = 0;
                            currentState = "대기 모션 2 (IdleB)";
                        }
                        else
                        {
                            currentState = "대기 중 (Idle)";
                        }
                    }
                }
                else if (isLandingState)
                {
                    // 착지 중일 때는 동적으로 설정된 전체 경직 시간 대비 흐른 시간을 보여줌
                    currentState = $"착지 모션 강제 유지 중 ({currentLandingAnim} | {landTimer:F2}s / {currentLandingDuration:F2}s) - [🛑현재 이동 불가 경직 상태!]";
                }
            }
            else
            {
                second = 0f;

                // 💡 [네 기획안 상태 다이어그램 연산부]
                if (moveDirection.y > 0)
                {
                    // 1단계: 위로 인위적으로 솟구쳐 날아가는 구간 -> 무조건 jumpFlag 독점
                    animator.SetBool("jumpFlag", true);
                    animator.SetBool("fallFlag", false);
                    airTime = 0f; // 상승 중에는 타이머 작동 안 함
                    currentState = "위로 상승 중 (Jump 상태)";
                }
                else
                {
                    // 2단계: 정점 찍고 마이너스로 꺾여서 떨어지는 순간 -> fallFlag 돌입
                    animator.SetBool("jumpFlag", false);
                    animator.SetBool("fallFlag", true);

                    // 💡 딱 이 Fall이 시작된 시점부터 순수하게 체공 시간 측정 시작
                    airTime += Time.deltaTime;
                    currentState = $"중력 낙하 중 (fall 상태 | airTime: {airTime:F2}s)";
                }

                // 💡 공중 70% 모션 홀딩 연동 (fall 상태가 너무 파닥거리면 동결)
                AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.IsName("fall") && stateInfo.normalizedTime >= 0.7f)
                {
                    animator.speed = 0f;
                }
            }

            // 로그 출력
            Debug.Log($"현재 상태: {currentState} | 체공 시간: {airTime:F2}s | isGrounded: {controller.isGrounded}");

            moveDirection.y -= gravity * Time.deltaTime;
            controller.Move(moveDirection * Time.deltaTime);

            // Q키 미소
            animator.SetBool("smileFlag", Input.GetKey("q"));

            // 눈싸움 기능 (시네머신 카메라와의 거리 계산)
            float dist = Vector3.Distance(transform.position, cmCamera.transform.position);
            animator.SetBool("kocchiFlag", dist < startKocchi);
        }

        void LateUpdate()
        {
            if (cmCamera == null) return;

            // cameraTarget이 비어있다면 임시로 자신의 몸통 좌표를 사용합니다 (에러 방지 및 추적 보장)
            Vector3 targetPosition = (cameraTarget != null) ? cameraTarget.position : transform.position + Vector3.up * 1.5f;

            if (isFirstPerson)
            {
                // 1인칭: 카메라의 위치를 매 프레임 일치시킴
                cmCamera.transform.position = targetPosition;
            }
            else
            {
                // 3인칭: 뒤로 거리만큼 떨어짐
                cmCamera.transform.position = targetPosition - (cmCamera.transform.forward * thirdPersonDistance);
            }
        }
    }
}