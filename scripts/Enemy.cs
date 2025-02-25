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
    
    [Signal]
    public delegate void PlayerDetectedEventHandler(Node3D player);
    
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
        // Initialize health
        _currentHealth = MaxHealth;
        
        // Get references to nodes
        _animationPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
        _healthBar = GetNode<ProgressBar>("HealthBar/SubViewport/ProgressBar");
        
        // Set up health bar
        if (_healthBar != null)
        {
            _healthBar.MaxValue = MaxHealth;
            _healthBar.Value = _currentHealth;
        }
        
        // Find player using scene tree
        CallDeferred("FindPlayer");
    }

    private void FindPlayer()
    {
        // Try to find player in the scene tree
        var players = GetTree().GetNodesInGroup("Player");
        if (players != null && players.Count > 0)
        {
            foreach (var node in players)
            {
                if (node is Node3D player)
                {
                    _player = player;
                    EmitSignal(SignalName.PlayerDetected, player);
                    break;
                }
            }
        }
    }

    public override void _Process(double delta)
    {
        // Update attack cooldown
        if (_attackTimer > 0)
        {
            _attackTimer -= (float)delta;
        }

        // Update chase cooldown
        if (_chaseTimer > 0)
        {
            _chaseTimer -= (float)delta;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        // Check if player reference is still valid
        if (_player == null || !IsInstanceValid(_player))
        {
            // Try to find player again using scene tree
            FindPlayer();
            
            // If still no valid player, just handle basic physics and return
            if (_player == null || !IsInstanceValid(_player))
            {
                Vector3 currentVelocity = Velocity;
                if (!IsOnFloor())
                {
                    currentVelocity.Y -= Gravity * (float)delta;
                }
                Velocity = currentVelocity;
                MoveAndSlide();
                return;
            }
        }

        Vector3 velocity = Velocity;
        
        // Apply gravity
        if (!IsOnFloor())
        {
            velocity.Y -= Gravity * (float)delta;
        }
        
        // Get direction to player
        Vector3 toPlayer = Vector3.Zero;
        float distanceToPlayer = float.MaxValue;
        
        try
        {
            toPlayer = _player.GlobalPosition - GlobalPosition;
            distanceToPlayer = toPlayer.Length();
        }
        catch (ObjectDisposedException)
        {
            _player = null;
            Velocity = velocity;
            MoveAndSlide();
            return;
        }
        
        // Look at player (only Y rotation)
        if (!_isAttacking && distanceToPlayer <= ChaseRange)
        {
            try
            {
                Vector3 lookAtPos = new Vector3(_player.GlobalPosition.X, GlobalPosition.Y, _player.GlobalPosition.Z);
                LookAt(lookAtPos);
            }
            catch (ObjectDisposedException)
            {
                _player = null;
                return;
            }
        }
        
        // Handle movement and attacks
        if (distanceToPlayer <= AttackRange && _attackTimer <= 0 && !_isAttacking)
        {
            // Perform attack
            PerformMeleeAttack();
            _attackTimer = AttackCooldown;
        }
        else if (distanceToPlayer <= ChaseRange && distanceToPlayer > MinimumRange && _chaseTimer <= 0)
        {
            // Chase player
            Vector3 direction = toPlayer.Normalized();
            direction.Y = 0; // Keep movement on XZ plane
            
            velocity.X = direction.X * MovementSpeed;
            velocity.Z = direction.Z * MovementSpeed;
            
            _isChasing = true;
            
            // Update walking animation speed based on velocity
            if (_auxAnimationPlayer != null && IsInstanceValid(_auxAnimationPlayer))
            {
                float speedRatio = new Vector2(velocity.X, velocity.Z).Length() / MovementSpeed;
                _auxAnimationPlayer.SpeedScale = speedRatio;
            }
        }
        else
        {
            // Stop movement
            velocity.X = Mathf.MoveToward(velocity.X, 0, MovementSpeed);
            velocity.Z = Mathf.MoveToward(velocity.Z, 0, MovementSpeed);
            
            _isChasing = false;
            
            // Stop walking animation
            if (_auxAnimationPlayer != null && IsInstanceValid(_auxAnimationPlayer))
            {
                _auxAnimationPlayer.SpeedScale = 0;
            }
        }
        
        // Update velocity and move
        Velocity = velocity;
        MoveAndSlide();
    }

    private void PerformMeleeAttack()
    {
        if (_player == null || !IsInstanceValid(_player) || _isAttacking) return;
        
        _isAttacking = true;
        
        if (_animationPlayer != null && IsInstanceValid(_animationPlayer) && _animationPlayer.HasAnimation("attack"))
        {
            // Store current walk animation state
            float previousWalkSpeed = (_auxAnimationPlayer != null && IsInstanceValid(_auxAnimationPlayer)) ? 
                _auxAnimationPlayer.SpeedScale : 0.0f;
            
            // Temporarily pause walk animation
            if (_auxAnimationPlayer != null && IsInstanceValid(_auxAnimationPlayer))
            {
                _auxAnimationPlayer.SpeedScale = 0.0f;
            }
            
            _animationPlayer.Play("attack");
            
            // Apply damage after a slight delay
            GetTree().CreateTimer(0.5f).Timeout += () =>
            {
                if (_player != null && IsInstanceValid(_player))
                {
                    try
                    {
                        Vector3 toPlayer = _player.GlobalPosition - GlobalPosition;
                        if (toPlayer.Length() <= AttackRange)
                        {
                            if (_player is Player player && IsInstanceValid(player))
                            {
                                player.TakeDamage(MeleeDamage);
                            }
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        _player = null;
                    }
                }
                
                _isAttacking = false;
                
                // Restore walk animation
                if (_auxAnimationPlayer != null && IsInstanceValid(_auxAnimationPlayer))
                {
                    _auxAnimationPlayer.SpeedScale = previousWalkSpeed;
                }
            };
        }
        else
        {
            // Fallback if no animation: apply damage immediately
            if (_player is Player player && IsInstanceValid(player))
            {
                try
                {
                    player.TakeDamage(MeleeDamage);
                }
                catch (ObjectDisposedException)
                {
                    _player = null;
                }
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
        // Spawn health potion with 30% chance
        if (HealthPotionScene != null && GD.Randf() < 0.3f)
        {
            var healthPotion = HealthPotionScene.Instantiate<Node3D>();
            GetParent().AddChild(healthPotion);
            healthPotion.GlobalPosition = GlobalPosition + Vector3.Up;
        }
        
        // Create death effect here if desired
        // For example, particles, sound, etc.
        
        // Remove the enemy
        QueueFree();
    }
} 