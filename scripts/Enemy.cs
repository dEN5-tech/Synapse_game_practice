using Godot;
using System;

public partial class Enemy : CharacterBody3D
{
    [Export]
    public float MovementSpeed = 3.0f;
    
    [Export]
    public float Gravity = 30.0f;
    
    [Export]
    public float MaxHealth = 150.0f;
    
    [Export]
    public float MeleeDamage = 25.0f;
    
    [Export]
    public float AttackRange = 2.5f;
    
    [Export]
    public float MinimumRange = 1.5f;  // Minimum distance to maintain from player
    
    [Export]
    public float AttackCooldown = 1.0f;
    
    [Export]
    public float ChaseRange = 12.0f;  // Reduced from 15.0f for less aggressive behavior
    
    [Export]
    public float ChaseCooldown = 2.0f;  // Cooldown between chase attempts
    
    [Export]
    public PackedScene HealthPotionScene { get; set; }
    
    private float _currentHealth;
    private Vector3 _velocity = Vector3.Zero;
    private float _attackTimer = 0.0f;
    private float _chaseTimer = 0.0f;
    private Node3D _player;
    private AnimationPlayer _animationPlayer;
    private AnimationPlayer _auxAnimationPlayer;
    private ProgressBar _healthBar;
    private bool _isAttacking = false;
    private bool _isChasing = false;
    private bool _isAnimationInitialized = false;
    private const string WALK_ANIM = "Armature";
    private const float ANIMATION_BLEND_TIME = 0.2f;
    private const float WALK_ANIM_LENGTH = 2.375f; // Length of walk animation cycle

    public override void _Ready()
    {
        _currentHealth = MaxHealth;
        _player = GetNode<Node3D>("/root/Main/Player");
        _animationPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
        _auxAnimationPlayer = GetNode<AnimationPlayer>("EnemyModel/AuxScene/AnimationPlayer");
        _healthBar = GetNode<ProgressBar>("HealthBar/SubViewport/ProgressBar");
        
        // Initialize health bar
        if (_healthBar != null)
        {
            _healthBar.MaxValue = MaxHealth;
            _healthBar.Value = _currentHealth;
        }
        
        // Initialize animation settings
        if (_auxAnimationPlayer != null)
        {
            if (_auxAnimationPlayer.HasAnimation(WALK_ANIM))
            {
                // Configure animation for proper looping
                var walkAnim = _auxAnimationPlayer.GetAnimation(WALK_ANIM);
                walkAnim.LoopMode = Animation.LoopModeEnum.Linear;
                
                // Start the walking animation with custom playback settings
                _auxAnimationPlayer.Play(WALK_ANIM);
                _auxAnimationPlayer.SpeedScale = 0.0f;
                
                _isAnimationInitialized = true;
            }
        }
    }

    public override void _Process(double delta)
    {
        if (_auxAnimationPlayer != null && _isAnimationInitialized)
        {
            // Update animation position for continuous looping
            var currentPos = _auxAnimationPlayer.CurrentAnimationPosition;
            if (currentPos >= WALK_ANIM_LENGTH)
            {
                _auxAnimationPlayer.Seek(0);
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsInstanceValid(this) || !IsInstanceValid(_player))
            return;

        Vector3 velocity = Velocity;
        Vector3 toPlayer = _player.GlobalPosition - GlobalPosition;
        float distanceToPlayer = toPlayer.Length();
        
        // Movement logic based on distance to player
        if (distanceToPlayer > MinimumRange && distanceToPlayer < ChaseRange)
        {
            Vector3 moveDirection = toPlayer.Normalized();
            moveDirection.Y = 0;  // Keep movement on horizontal plane
            
            // Apply movement
            velocity = velocity.Lerp(moveDirection * MovementSpeed, (float)delta * 5.0f);
        }
        else if (distanceToPlayer <= AttackRange && distanceToPlayer > MinimumRange)
        {
            _attackTimer += (float)delta;
            if (_attackTimer >= AttackCooldown)
            {
                _attackTimer = 0;
                PerformMeleeAttack();
            }
            velocity = Vector3.Zero;
        }
        else if (distanceToPlayer <= MinimumRange)
        {
            // Back away if too close
            Vector3 awayFromPlayer = -toPlayer.Normalized() * MovementSpeed;
            awayFromPlayer.Y = _velocity.Y;
            velocity = awayFromPlayer;
        }
        else
        {
            // Stop moving if player is too far
            velocity.X = 0;
            velocity.Z = 0;
            _isChasing = false;
            _chaseTimer = 0;
        }
        
        // Apply gravity
        if (!IsOnFloor())
        {
            velocity.Y -= Gravity * (float)delta;
        }
        
        // Update velocity and move
        Velocity = velocity;
        MoveAndSlide();
        
        // Look at player
        if (distanceToPlayer > 0.1f)
        {
            Vector3 lookAtPos = new Vector3(_player.GlobalPosition.X, GlobalPosition.Y, _player.GlobalPosition.Z);
            LookAt(lookAtPos);
        }
    }

    private void PerformMeleeAttack()
    {
        if (_player == null || _isAttacking) return;
        
        _isAttacking = true;
        
        if (_animationPlayer != null && _animationPlayer.HasAnimation("attack"))
        {
            // Store current walk animation state
            float previousWalkSpeed = _auxAnimationPlayer?.SpeedScale ?? 0.0f;
            
            // Temporarily pause walk animation
            if (_auxAnimationPlayer != null)
            {
                _auxAnimationPlayer.SpeedScale = 0.0f;
            }
            
            _animationPlayer.Play("attack");
            
            GetTree().CreateTimer(0.5f).Timeout += () =>
            {
                Vector3 toPlayer = _player.GlobalPosition - GlobalPosition;
                if (toPlayer.Length() <= AttackRange)
                {
                    if (_player is Player player)
                    {
                        player.TakeDamage(MeleeDamage);
                    }
                }
                _isAttacking = false;
                
                // Restore walk animation
                if (_auxAnimationPlayer != null)
                {
                    _auxAnimationPlayer.SpeedScale = previousWalkSpeed;
                }
            };
        }
        else
        {
            if (_player is Player player)
            {
                player.TakeDamage(MeleeDamage);
            }
            _isAttacking = false;
        }
    }

    public void TakeDamage(float amount)
    {
        _currentHealth -= amount;
        
        // Update health bar
        if (_healthBar != null)
        {
            _healthBar.Value = _currentHealth;
            // Force viewport update
            var viewport = GetNode<SubViewport>("HealthBar/SubViewport");
            if (viewport != null && IsInstanceValid(viewport))
            {
                viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
                var timer = GetTree().CreateTimer(0.1f);
                timer.Timeout += () =>
                {
                    if (IsInstanceValid(viewport))
                    {
                        viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
                    }
                };
            }
        }
        
        // Visual feedback for damage
        var model = GetNode<Node3D>("EnemyModel/AuxScene");
        if (model != null && IsInstanceValid(model))
        {
            // Create tween for flash effect
            var tween = CreateTween();
            if (tween != null)
            {
                tween.TweenCallback(Callable.From(() => 
                {
                    if (IsInstanceValid(model)) model.Visible = false;
                }));
                tween.TweenInterval(0.05f);
                tween.TweenCallback(Callable.From(() => 
                {
                    if (IsInstanceValid(model)) model.Visible = true;
                }));
                tween.TweenInterval(0.05f);
                tween.TweenCallback(Callable.From(() => 
                {
                    if (IsInstanceValid(model)) model.Visible = false;
                }));
                tween.TweenInterval(0.05f);
                tween.TweenCallback(Callable.From(() => 
                {
                    if (IsInstanceValid(model)) model.Visible = true;
                }));
            }
        }
        
        if (_currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        // Drop health potion
        if (HealthPotionScene != null)
        {
            var potion = HealthPotionScene.Instantiate<Area3D>();
            GetTree().Root.AddChild(potion);
            potion.GlobalPosition = GlobalPosition + Vector3.Up * 0.5f; // Spawn slightly above the ground
        }

        // Optional: Play death animation
        if (_animationPlayer != null && _animationPlayer.HasAnimation("death"))
        {
            _animationPlayer.Play("death");
            // Wait for animation to finish before freeing
            GetTree().CreateTimer(1.0f).Timeout += () => QueueFree();
        }
        else
        {
            // If no death animation, just free the node
            QueueFree();
        }
    }
} 