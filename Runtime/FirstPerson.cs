using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class LookAtSwitchEvent : UnityEvent<GameObject> { }
public class LookAtClearEvent : UnityEvent { }

[RequireComponent(typeof(CharacterController))]
public class FirstPerson : MonoBehaviour
{
    // events
    public static LookAtSwitchEvent lookAtSwitchEvent = new LookAtSwitchEvent();
    public static LookAtClearEvent lookAtClearEvent = new LookAtClearEvent();

    // input
    private InputAction actionJump;
    private InputAction actionLook;
    private InputAction actionMove;
    private InputAction actionSprint;

    // data
    private Vector2 _axis;
    private float _axisH;
    private float _axisV;
    private Vector2 _mouse;
    private float _mouseX;
    private float _mouseY;
    private Ray lookRay;

    // components
    private CharacterController cc;
    private Camera cam;

    #region Editor
    [Header("Movement Settings")]

    [Tooltip("The default movement speed modifier.")]
    public float walkSpeed = 0.1f;
    [Tooltip("The default run speed modifier.")]
    public float runSpeed = 0.2f;
    [Tooltip("The force multiplier for jumping.")]
    public float jumpForce = 0.2f;
    [Tooltip("The force multiplier for falling.")]
    public float fallForce = 0.6f;

    [Header("Camera Settings")]

    [Tooltip("The default camera speed X modifier.")]
    public float cameraSpeedX = 1.0f;
    [Tooltip("The default camera speed Y modifier.")]
    public float cameraSpeedY = 1.0f;
    [Tooltip("The minimum clamp value for the camera.")]
    public float cameraClampMin = -45.0f;
    [Tooltip("The maximum clamp value for the camera.")]
    public float cameraClampMax = 45.0f;

    [Header("LookAt Settings")]

    [Tooltip("The maximum look distance for interactables.")]
    public float lookDistance = 3.0f;
    [Tooltip("The LayerMask for lookAt objects.")]
    public LayerMask lookAtMask;

    [Header("Movement Vectors (private)")]
    [SerializeField] private Vector3 _cameraVector = new Vector3(0, 0, 0);
    [SerializeField] private Vector2 _cameraRotation = new Vector2(0, 0);
    [SerializeField] private float _gravity = Physics.gravity.y;

    [Header("Movement Bools (private)")]
    [SerializeField] private bool _isRunning;
    [SerializeField] private bool _isGrounded;
    [SerializeField] private bool _isJumping;

    [Header("Movement Data (private)")]
    [SerializeField] private bool _parseInput = true;
    [SerializeField] private bool _canJump;
    [SerializeField] private float _jumpValue;
    [SerializeField] private Vector3 _motionVector = new Vector3(0, 0, 0);
    [SerializeField] private Vector3 _moveVector = new Vector3(0, 0, 0);

    [Header("LookAt Data (private)")]
    [SerializeField] private GameObject _lookTarget;
    [SerializeField] private List<GameObject> _looktargets = new List<GameObject>();
    [SerializeField] private string _lookAtName;
    #endregion

    #region #Getters
    public bool isRunning { get { return this._isRunning; } }
    public bool isGrounded { get { return this._isGrounded; } }
    public bool isJumping { get { return this._isJumping; } }
    public Vector2 cameraVector { get { return this._cameraVector; } }
    public Vector2 cameraRotation { get { return this._cameraRotation; } }
    public float gravity { get { return this._gravity; } }
    public bool parseInput { get { return this._parseInput; } }
    public bool canJump { get { return this._canJump; } }
    public float jumpValue { get { return this._jumpValue; } }
    public Vector3 motionVector { get { return this._motionVector; } }
    public Vector3 moveVector { get { return this._moveVector; } }
    #endregion

    #region #Mono Behavior
    // monobehavior
    private void Awake()
    {
        Application.targetFrameRate = 60;
        
        // components
        this.cc = GetComponent<CharacterController>();
        this.cam = GetComponentInChildren<Camera>();

        // input binding
        this.actionJump = InputSystem.actions.FindAction("Jump");
        this.actionLook = InputSystem.actions.FindAction("Look");
        this.actionMove = InputSystem.actions.FindAction("Move");
        this.actionSprint = InputSystem.actions.FindAction("Sprint");
    }
    private void Update()
    {
        if (this.parseInput)
        {
            this.ReadInput();
            this.SetCamera();
            this.SetMovement();
            this.SetJump();
            this.Move();
            this.SetLookAt();
            //ActivateLookAt();
        }
    }
    #endregion

    #region #Firstperson
    protected void ReadInput()
    {
        this._isGrounded = cc.isGrounded;
        
        this._axis = actionMove.ReadValue<Vector2>();
        this._axisH = this._axis.x;
        this._axisV = this._axis.y;

        this._mouse = actionLook.ReadValue<Vector2>();
        this._mouseX = this._mouse.x;
        this._mouseY = this._mouse.y;
    }
    protected void SetCamera()
    {
        if (cam && cam.enabled)
        {
            this._cameraRotation.y += _mouseX * cameraSpeedY;
            this._cameraRotation.x += -_mouseY * cameraSpeedX;
            this._cameraRotation.x = Mathf.Clamp(this._cameraRotation.x, cameraClampMin, cameraClampMax);
            cam.transform.eulerAngles = (Vector2)this._cameraRotation * 1;
            Camera camera = Camera.main;
            Vector3 forward = camera.transform.forward;
            Vector3 right = camera.transform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();
            this._cameraVector = (forward * this._axis.y) + (right * this._axis.x);
            //cameraVector.y = 0;
        }
    }
    protected void SetMovement()
    {
        this._motionVector = this._cameraVector.normalized;
        this._motionVector.y = this._gravity;
        this._moveVector = this._motionVector;

        bool sprintingPressed = InputSystem.actions.FindAction("Sprint").IsPressed();

        if (sprintingPressed && this._axis.y > 0 && this._isGrounded)
        {
            this._moveVector *= runSpeed;
            this._isRunning = true;
        }
        else
        {
            this._moveVector *= walkSpeed;
            this._isRunning = false;
        }
    }
    protected void SetJump()
    {
        if (_isGrounded)
        {
            this._canJump = true;
            this._isJumping = false;
        }
        else
        {
            this._canJump = false;
            this._isJumping = true;
        }

        if (actionJump.WasPressedThisFrame())
        {
            if (canJump && !isJumping)
            {
                this._canJump = false;
                this._isJumping = true;
                this._jumpValue = Mathf.Sqrt(jumpForce * Mathf.Abs(this._gravity));
            }
        }
        else if (this._isGrounded)
        {
            this._jumpValue = 0;
        }
        else
        {
            this._jumpValue -= Mathf.Abs(this._gravity * fallForce) * Time.fixedDeltaTime * 0.5f;
            this._jumpValue = Mathf.Clamp(this._jumpValue, 0, this._jumpValue);
        }
        this._moveVector.y += this._jumpValue;
    }
    protected void SetLookAt()
    {
        this._looktargets.Clear();
        this._lookTarget = null;
        if (cam && cam.enabled)
        {
            lookRay = new Ray(cam.transform.position, cam.transform.forward.normalized * lookDistance);
            RaycastHit[] hits = Physics.RaycastAll(lookRay, lookDistance, lookAtMask);
            if (hits.Length > 0)
            {
                foreach (RaycastHit hit in hits)
                {
                    if (!this._looktargets.Contains(hit.collider.gameObject))
                    {
                        this._looktargets.Add(hit.collider.gameObject);
                    }
                }
                if (this._looktargets.Count > 0 && this._looktargets[0] != this._lookTarget)
                {
                    this._lookTarget = this._looktargets[0];
                    this.OnLookAtSwitch(this._lookTarget);
                }
            }
            else
            {
                if (this._lookTarget)
                {
                    this.OnLookAtClear();
                }
                this._lookTarget = null;
            }

            if (!this._lookTarget)
            {
                this.OnLookAtClear();
                this._lookTarget = null;
            }
        }
    }
    protected void Move()
    {
        if (this.cc.enabled) { cc.Move(this.moveVector); }
    }
    protected void OnLookAtSwitch(GameObject obj)
    {
        this._lookAtName = obj.name;
        lookAtSwitchEvent.Invoke(obj);
    }
    protected void OnLookAtClear()
    {
        this._lookAtName = "";
        lookAtClearEvent.Invoke();
    }
    public void DisableInput()
    {
        this._parseInput = false;
        this.cc.enabled = false;
    }
    public void EnableInput()
    {
        this._parseInput = true;
        this.cc.enabled = true;
    }
    #endregion
}
