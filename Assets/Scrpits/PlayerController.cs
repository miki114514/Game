using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float walkSpeed = 1f;
    public float runSpeed = 2f;

    // ★ 新增：是否允许移动
    public bool canMove = true;

    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;

    private Vector2 movement;
    private bool isRunning;

    // 音效
    public AudioClip walkSound;
    public AudioClip runSound;
    private AudioSource audioSource;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
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

            animator.SetFloat("Speed", 0);
            animator.SetFloat("MoveX", 0);
            animator.SetFloat("MoveY", 0);

            return;
        }

        // 获取输入
        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");

        // Shift检测
        isRunning = Input.GetKey(KeyCode.LeftShift);

        // 防止对角线更快
        movement = movement.normalized;

        // 更新动画参数
        animator.SetBool("isRunning", isRunning);
        animator.SetFloat("MoveX", movement.x);
        animator.SetFloat("MoveY", movement.y);
        animator.SetFloat("Speed", movement.sqrMagnitude);

        // 左右翻转
        if (movement.x != 0)
        {
            spriteRenderer.flipX = movement.x < 0;
        }
    }

    void FixedUpdate()
    {
        if (!canMove) return;

        float speed = isRunning ? runSpeed : walkSpeed;
        rb.MovePosition(rb.position + movement * speed * Time.fixedDeltaTime);
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
}