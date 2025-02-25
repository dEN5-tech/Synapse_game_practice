using Godot;
using System;

public partial class Player : CharacterBody3D
{
    [Signal]
    public delegate void PlayerDiedEventHandler();
    
    [Signal]
    public delegate void PlayerRespawnedEventHandler();
    
    [Export]
    public float BaseMovementSpeed = 7.0f;
    
    [Export]
    public float SprintMultiplier = 1.5f;
    
    [Export]
    public float AccelerationSpeed = 25.0f;
    
    [Export]
    public float Friction = 15.0f;  // Increased friction for better stopping
    
    [Export]
    public float StopSpeed = 2.0f;  // Lower stop speed threshold
    
    [Export]
    public float AirControl = 0.3f;
    
    [Export]
    public float JumpForce = 6.0f;
    
    [Export]
    public float Gravity = 25.0f;
    
    [Export]
    public float MouseSensitivity = 0.002f;
    
    [Export]
    public float CameraHeight = 1.7f;
    
    [Export]
    public float CameraSmoothing = 10.0f;
    
    [Export]
    public float RecoilRecoverySpeed = 5.0f;
    
    [Export]
    public float MaxHealth = 100.0f;
    
    [Export]
    public float CurrentHealth = 100.0f;
    
    [Export]
    public float HealCooldownTime = 0.5f;
    
    [Export]
    public float WeaponBobSpeed = 14.0f;
    
    [Export]
    public float WeaponBobAmount = 0.05f;
    
    [Export]
    public float WeaponSwayAmount = 0.1f;
    
    [Export]
    public float RespawnDelay = 3.0f;  // Time before respawn in seconds
    
    [Export]
    public NodePath RespawnPointPath;  // Path to the respawn point node

    private bool _isOnGround = false;
    private bool _isSprinting = false;
    private Camera3D _camera;
    private Node3D _cameraMount;
    private float _rotationX = 0;
    private float _rotationY = 0;
    private Vector3 _velocity = Vector3.Zero;
    private Vector3 _direction = Vector3.Zero;
    private Gun _gun;
    private HUD _hud;
    private float _recoilOffset = 0.0f;
    private float _targetRecoilOffset = 0.0f;
    private Vector3 _targetCameraRotation = Vector3.Zero;
    private Vector3 _currentCameraRotation = Vector3.Zero;
    private Vector3 _gunInitialPosition;
    private float _bobTimer = 0.0f;
    private Vector3 _currentGunOffset = Vector3.Zero;
    private float _healCooldown = 0.0f;
    private Vector2 _lastMouseMotion = Vector2.Zero;
    private bool _isDead = false;
    private Vector3 _initialPosition;
    private Vector3 _initialRotation;
    private Node3D _respawnPoint;

    public override void _Ready()
    {
        // Get node references
        _camera = GetNode<Camera3D>("CameraMount/Camera3D");
        _cameraMount = GetNode<Node3D>("CameraMount");
        _gun = GetNode<Gun>("CameraMount/Camera3D/Gun");
        
        // Store initial transform for respawning
        _initialPosition = GlobalPosition;
        _initialRotation = Rotation;
        
        // Get respawn point if specified
        if (!string.IsNullOrEmpty(RespawnPointPath))
        {
            _respawnPoint = GetNode<Node3D>(RespawnPointPath);
        }
        
        // Capture mouse for FPS controls
        Input.MouseMode = Input.MouseModeEnum.Captured;
        
        if (_gun != null)
        {
            _gunInitialPosition = _gun.Position;
        }
        
        // Initialize HUD reference
        _hud = GetNode<HUD>("/root/Main/HUD");
        if (_hud != null)
        {
            _hud.UpdateHealth(CurrentHealth);
        }
    }

    public override void _Process(double delta)
    {

    }

    private void UpdateWeaponPosition(double delta)
    {
        if (_gun == null) return;
        
        // Calculate weapon bob
        float bobSpeed = WeaponBobSpeed;
        float bobAmount = WeaponBobAmount;
        
        if (_direction != Vector3.Zero && IsOnFloor())
        {
            _bobTimer += (float)delta * bobSpeed;
            if (Input.IsActionPressed("sprint"))
            {
                _bobTimer += (float)delta * bobSpeed * 0.5f;
            }
        }
        else
        {
            _bobTimer = 0;
        }
        
        // Calculate weapon position
        Vector3 targetPos = _gunInitialPosition;
        targetPos.Y += Mathf.Sin(_bobTimer) * bobAmount;
        targetPos.X += Mathf.Cos(_bobTimer * 0.5f) * bobAmount * 0.5f;
        
        // Apply weapon sway based on stored mouse motion
        targetPos.X -= _lastMouseMotion.X * WeaponSwayAmount * 0.001f;
        targetPos.Y -= _lastMouseMotion.Y * WeaponSwayAmount * 0.001f;
        
        // Reset mouse motion (smooth decay)
        _lastMouseMotion = _lastMouseMotion.Lerp(Vector2.Zero, (float)delta * 10.0f);
        
        // Smooth weapon movement
        _currentGunOffset = _currentGunOffset.Lerp(targetPos, (float)delta * 10.0f);
        _gun.Position = _currentGunOffset;
        
        // Handle recoil recovery
        if (_recoilOffset > 0)
        {
            _recoilOffset = Mathf.MoveToward(_recoilOffset, 0, RecoilRecoverySpeed * (float)delta);
            _camera.Rotation = new Vector3(_targetCameraRotation.X - _recoilOffset, 
                _targetCameraRotation.Y, _targetCameraRotation.Z);
        }
    }

    public override void _Input(InputEvent @event)
    {
        // Handle mouse look
        if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            _rotationX -= mouseMotion.Relative.Y * MouseSensitivity;
            _rotationY -= mouseMotion.Relative.X * MouseSensitivity;
            
            // Store mouse motion for weapon sway
            _lastMouseMotion = mouseMotion.Relative;
            
            // Clamp vertical rotation to prevent over-rotation
            _rotationX = Mathf.Clamp(_rotationX, Mathf.DegToRad(-89), Mathf.DegToRad(89));
            
            UpdateCameraRotation();
        }
        
        // Toggle mouse capture with Escape
        if (@event.IsActionPressed("ui_cancel"))
        {
            Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured ? 
                Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
        }
    }

    private void UpdateCameraRotation()
    {
        // Update camera rotation
        _targetCameraRotation = new Vector3(_rotationX, 0, 0);
        _cameraMount.Rotation = new Vector3(0, _rotationY, 0);
        _camera.Rotation = _targetCameraRotation;
    }

    public void ApplyRecoil(float recoilAmount)
    {
        _targetRecoilOffset += Mathf.DegToRad(recoilAmount);
    }

    public override void _PhysicsProcess(double delta)
    {
        Vector3 velocity = Velocity;
        
        // Add gravity
        if (!IsOnFloor())
        {
            velocity.Y -= Gravity * (float)delta;
        }
        
        // Handle jump
        if (Input.IsActionPressed("jump") && IsOnFloor())
        {
            velocity.Y = JumpForce;
        }
        
        // Get input direction
        Vector2 inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
        _direction = new Vector3(inputDir.X, 0, inputDir.Y).Normalized();
        
        if (_direction != Vector3.Zero)
        {
            // Transform direction relative to camera rotation
            _direction = _direction.Rotated(Vector3.Up, _cameraMount.Rotation.Y);
            
            // Calculate target velocity
            float speed = BaseMovementSpeed;
            if (Input.IsActionPressed("sprint"))
            {
                speed *= SprintMultiplier;
            }
            
            Vector3 targetVelocity = _direction * speed;
            
            // Apply acceleration
            float accel = IsOnFloor() ? AccelerationSpeed : (AccelerationSpeed * AirControl);
            velocity.X = Mathf.MoveToward(velocity.X, targetVelocity.X, accel * (float)delta);
            velocity.Z = Mathf.MoveToward(velocity.Z, targetVelocity.Z, accel * (float)delta);
        }
        else
        {
            // Apply friction when no input
            if (IsOnFloor())
            {
                float speedH = new Vector2(velocity.X, velocity.Z).Length();
                if (speedH < StopSpeed)
                {
                    velocity.X = 0;
                    velocity.Z = 0;
                }
                else
                {
                    float drop = speedH * Friction * (float)delta;
                    velocity.X *= Mathf.Max(0, speedH - drop) / speedH;
                    velocity.Z *= Mathf.Max(0, speedH - drop) / speedH;
                }
            }
        }
        
        // Update weapon position
        UpdateWeaponPosition(delta);
        
        // Update velocity
        Velocity = velocity;
        MoveAndSlide();
        
        // Update health cooldown
        if (_healCooldown > 0)
        {
            _healCooldown -= (float)delta;
        }
    }

    public void TakeDamage(float amount)
    {
        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        _healCooldown = HealCooldownTime;

        if (CurrentHealth <= 0)
        {
            Die();
        }
    }
    
    public bool CanBeHealed()
    {
        return CurrentHealth < MaxHealth && _healCooldown <= 0;
    }
    
    public void Heal(float amount)
    {
        if (CanBeHealed())
        {
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
        }
    }
    
    private void Die()
    {
        // Implement death behavior here
        QueueFree();
    }
} 