using Godot;
using System;

public partial class Player : CharacterBody3D
{
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
    public float WeaponBobSpeed = 14.0f;
    
    [Export]
    public float WeaponBobAmount = 0.05f;
    
    [Export]
    public float WeaponSwayAmount = 0.1f;

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

    public override void _Ready()
    {
        // Get references to nodes
        _cameraMount = GetNode<Node3D>("CameraMount");
        _camera = GetNode<Camera3D>("CameraMount/Camera3D");
        _gun = GetNode<Gun>("CameraMount/Camera3D/Gun");
        _hud = GetNode<HUD>("/root/Main/HUD");
        
        // Set up camera and gun
        _camera.Position = new Vector3(0, CameraHeight, 0);
        _camera.Fov = 75.0f;
        
        if (_gun != null)
        {
            _gunInitialPosition = _gun.Position;
        }
        
        // Initialize HUD
        if (_hud != null)
        {
            _hud.MaxHealth = MaxHealth;
            _hud.CurrentHealth = CurrentHealth;
            _hud.UpdateHealth(CurrentHealth);
        }
        
        // Capture mouse
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _Process(double delta)
    {
        // Handle camera smoothing
        _currentCameraRotation = _currentCameraRotation.Lerp(_targetCameraRotation, (float)delta * CameraSmoothing);
        
        // Handle recoil recovery
        if (_recoilOffset != 0)
        {
            _recoilOffset = Mathf.MoveToward(_recoilOffset, _targetRecoilOffset, RecoilRecoverySpeed * (float)delta);
            _targetRecoilOffset = Mathf.MoveToward(_targetRecoilOffset, 0, RecoilRecoverySpeed * 0.5f * (float)delta);
        }
        
        // Update weapon position
        if (_gun != null)
        {
            UpdateWeaponPosition(delta);
        }
        
        UpdateCameraRotation();
    }

    private void UpdateWeaponPosition(double delta)
    {
        Vector3 targetPos = _gunInitialPosition;
        
        // Add weapon bob when moving
        if (_direction.Length() > 0.1f && _isOnGround)
        {
            _bobTimer += (float)delta * WeaponBobSpeed * (_isSprinting ? 1.5f : 1.0f);
            float bobX = Mathf.Cos(_bobTimer) * WeaponBobAmount;
            float bobY = Mathf.Sin(_bobTimer * 2) * WeaponBobAmount;
            targetPos += new Vector3(bobX, bobY, 0);
        }
        else
        {
            _bobTimer = 0;
        }
        
        // Add weapon sway based on mouse movement
        Vector3 sway = new Vector3(
            -_currentCameraRotation.X * WeaponSwayAmount,
            -_currentCameraRotation.Y * WeaponSwayAmount,
            0
        );
        targetPos += sway;
        
        // Smoothly interpolate to target position
        _currentGunOffset = _currentGunOffset.Lerp(targetPos - _gunInitialPosition, (float)delta * 8.0f);
        _gun.Position = _gunInitialPosition + _currentGunOffset;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            // Horizontal rotation (around Y axis)
            _rotationY = (_rotationY - mouseMotion.Relative.X * MouseSensitivity) % (2 * Mathf.Pi);
            if (_rotationY < -Mathf.Pi)
                _rotationY += 2 * Mathf.Pi;
            else if (_rotationY > Mathf.Pi)
                _rotationY -= 2 * Mathf.Pi;
            
            // Vertical rotation (around X axis) with clamping
            _rotationX = Mathf.Clamp(
                _rotationX - mouseMotion.Relative.Y * MouseSensitivity,
                -Mathf.Pi/2.1f,
                Mathf.Pi/2.1f
            );

            _targetCameraRotation = new Vector3(_rotationX, _rotationY, 0);
        }

        // Handle shooting and mouse capture toggle
        if (@event.IsActionPressed("shoot") && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            _gun?.Shoot();
        }
        else if (@event.IsActionPressed("ui_cancel"))
        {
            Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured 
                ? Input.MouseModeEnum.Visible 
                : Input.MouseModeEnum.Captured;
        }
    }

    private void UpdateCameraRotation()
    {
        // Enhanced camera rotation with improved interpolation
        Quaternion yaw = Quaternion.FromEuler(new Vector3(0, _currentCameraRotation.Y, 0));
        Quaternion pitch = Quaternion.FromEuler(new Vector3(_currentCameraRotation.X + _recoilOffset, 0, 0));
        
        // Apply rotations with enhanced smoothing
        _cameraMount.Quaternion = yaw;
        _camera.Quaternion = pitch;
    }

    public void ApplyRecoil(float recoilAmount)
    {
        _targetRecoilOffset += Mathf.DegToRad(recoilAmount);
    }

    public override void _PhysicsProcess(double delta)
    {
        Vector3 velocity = Velocity;
        bool wasOnGround = _isOnGround;
        _isOnGround = IsOnFloor();
        
        // Enhanced input handling
        float inputX = Input.GetActionStrength("move_right") - Input.GetActionStrength("move_left");
        float inputZ = Input.GetActionStrength("move_forward") - Input.GetActionStrength("move_backward");
        
        // Improved movement direction calculation
        Vector3 forward = -_cameraMount.GlobalTransform.Basis.Z;
        forward.Y = 0;
        forward = forward.Normalized();
        
        Vector3 right = _cameraMount.GlobalTransform.Basis.X;
        right.Y = 0;
        right = right.Normalized();
        
        // Enhanced movement vector combination with better normalization
        _direction = (forward * inputZ + right * inputX);
        if (_direction.LengthSquared() > 1.0f)
            _direction = _direction.Normalized();
        
        // Improved sprint handling with smoother transition
        bool wantToSprint = Input.IsActionPressed("sprint");
        float sprintTransition = Mathf.MoveToward(_isSprinting ? 1.0f : 0.0f, wantToSprint ? 1.0f : 0.0f, (float)delta * 7.0f);
        _isSprinting = sprintTransition > 0.5f;
        float currentSpeed = Mathf.Lerp(BaseMovementSpeed, BaseMovementSpeed * SprintMultiplier, sprintTransition);

        // Ground movement
        if (_isOnGround)
        {
            // Landing impact
            if (!wasOnGround && velocity.Y < -5.0f)
            {
                // Optional: Add landing effect here
                _targetRecoilOffset += Mathf.DegToRad(-velocity.Y * 0.2f);
            }

            // Get horizontal velocity
            Vector2 horizontalVel = new Vector2(velocity.X, velocity.Z);
            float speed = horizontalVel.Length();
            
            // Apply friction
            if (_direction == Vector3.Zero && speed > 0)
            {
                float drop = Friction * (float)delta;
                
                // Use StopSpeed to prevent micro-sliding
                if (speed < StopSpeed)
                {
                    velocity.X = 0;
                    velocity.Z = 0;
                }
                else
                {
                    float scale = Mathf.Max(speed - drop, 0) / speed;
                    velocity.X *= scale;
                    velocity.Z *= scale;
                }
            }
            
            // Accelerate if there's input
            if (_direction != Vector3.Zero)
            {
                float currentSpeedH = new Vector2(velocity.X, velocity.Z).Length();
                float addSpeed = Mathf.Clamp(currentSpeed - currentSpeedH, 0, AccelerationSpeed * (float)delta);
                
                velocity.X += _direction.X * addSpeed;
                velocity.Z += _direction.Z * addSpeed;
                
                // Limit maximum speed
                horizontalVel = new Vector2(velocity.X, velocity.Z);
                if (horizontalVel.LengthSquared() > currentSpeed * currentSpeed)
                {
                    horizontalVel = horizontalVel.Normalized() * currentSpeed;
                    velocity.X = horizontalVel.X;
                    velocity.Z = horizontalVel.Y;
                }
            }
        }
        // Air movement with improved control
        else
        {
            if (_direction != Vector3.Zero)
            {
                Vector3 airVelocity = _direction * (currentSpeed * AirControl);
                float accelerationFactor = AccelerationSpeed * AirControl * (float)delta;
                
                // Preserve more momentum while airborne
                velocity.X = Mathf.MoveToward(velocity.X, airVelocity.X, accelerationFactor);
                velocity.Z = Mathf.MoveToward(velocity.Z, airVelocity.Z, accelerationFactor);
                
                // Apply air resistance
                float airDrag = Mathf.Min(1.0f, 0.01f * (float)delta);
                velocity.X *= (1.0f - airDrag);
                velocity.Z *= (1.0f - airDrag);
            }
        }

        // Handle jumping and gravity
        if (!_isOnGround)
        {
            velocity.Y -= Gravity * (float)delta;
        }
        else
        {
            if (velocity.Y < 0)
                velocity.Y = 0;
                
            if (Input.IsActionJustPressed("jump"))
            {
                velocity.Y = JumpForce;
                _isOnGround = false;
            }
        }

        // Apply final velocity
        Velocity = velocity;
        MoveAndSlide();
    }

    public void TakeDamage(float amount)
    {
        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        if (_hud != null)
        {
            _hud.UpdateHealth(CurrentHealth);
        }
        
        if (CurrentHealth <= 0)
        {
            Die();
        }
    }
    
    public void Heal(float amount)
    {
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
        if (_hud != null)
        {
            _hud.UpdateHealth(CurrentHealth);
        }
    }
    
    private void Die()
    {
        // Handle player death
        // For now, just reset health
        CurrentHealth = MaxHealth;
        if (_hud != null)
        {
            _hud.UpdateHealth(CurrentHealth);
        }
    }
} 