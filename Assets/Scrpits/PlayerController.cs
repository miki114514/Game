using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float walkSpeed = 1f;
    public float runSpeed = 2f;

    // ★ 新增：是否允许移动
    public bool canMove = true;

    private Rigidbody rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;

    [Header("动画朝向")]
    public bool useSpriteFlipX = false;

    private Vector2 movement;
    private Vector2 lastFacing = Vector2.down;
    private bool isRunning;

    private bool externalAnimationControl;
    private Vector2 externalFacing = Vector2.down;
    private float externalSpeed;
    private bool externalIsRunning;

    // 音效
    public AudioClip walkSound;
    public AudioClip runSound;
    private AudioSource audioSource;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        audioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        // ★ 如果被锁定移动
        if (!canMove)
        {
            movement = Vector2.zero;

            if (externalAnimationControl)
            {
                animator.SetBool("isRunning", externalIsRunning);
                animator.SetFloat("Speed", externalSpeed);
                animator.SetFloat("MoveX", externalFacing.x);
                animator.SetFloat("MoveY", externalFacing.y);
                return;
            }

            if (DialogueManager.IsDialogueActive)
            {
                // 对话系统会直接驱动动画参数/状态，避免这里覆盖。
                return;
            }

            // 锁移动时保留最后朝向，避免动画方向被重置。
            animator.SetBool("isRunning", false);
            animator.SetFloat("Speed", 0);
            animator.SetFloat("MoveX", lastFacing.x);
            animator.SetFloat("MoveY", lastFacing.y);

            return;
        }

        // 获取输入
        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");

        // Shift检测
        isRunning = Input.GetKey(KeyCode.LeftShift);

        // 防止对角线更快
        movement = movement.normalized;

        if (movement.sqrMagnitude > 0.0001f)
        {
            lastFacing = movement;
        }

        // 更新动画参数
        animator.SetBool("isRunning", isRunning);
        animator.SetFloat("MoveX", movement.sqrMagnitude > 0.0001f ? movement.x : lastFacing.x);
        animator.SetFloat("MoveY", movement.sqrMagnitude > 0.0001f ? movement.y : lastFacing.y);
        animator.SetFloat("Speed", movement.sqrMagnitude);

        // 当左右方向有独立动画时，不要再做flipX，否则会出现左右反转。
        if (useSpriteFlipX && movement.x != 0)
        {
            spriteRenderer.flipX = movement.x < 0;
        }
    }

    void FixedUpdate()
    {
        if (!canMove) return;

        float speed = isRunning ? runSpeed : walkSpeed;
        Vector3 moveDelta = new Vector3(movement.x, 0f, movement.y) * speed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + moveDelta);
    }

    // 动画事件触发脚步声
    public void PlayFootstep()
    {
        if (movement == Vector2.zero) return;

        AudioClip clip = isRunning ? runSound : walkSound;

        if (clip != null)
        {
            audioSource.pitch = Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(clip);
        }
    }

    public void SetExternalAnimationControl(Vector2 facing, float speed, bool running)
    {
        externalAnimationControl = true;

        if (facing.sqrMagnitude > 0.0001f)
        {
            externalFacing = facing.normalized;
            lastFacing = externalFacing;
        }

        externalSpeed = Mathf.Max(0f, speed);
        externalIsRunning = running;
    }

    public void ClearExternalAnimationControl()
    {
        externalAnimationControl = false;
        externalSpeed = 0f;
        externalIsRunning = false;
    }
}