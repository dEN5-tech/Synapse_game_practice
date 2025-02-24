using Godot;
using System;

public partial class Gun : Node3D
{
    [Export]
    public float FireRate = 0.15f;  // Time between shots in seconds
    
    [Export]
    public float RecoilForce = 2.0f;
    
    [Export]
    public float RecoilRecovery = 5.0f;
    
    [Export]
    public float MaxRange = 1000.0f;  // Maximum shooting range
    
    [Export]
    public PackedScene BulletScene { get; set; }
    
    [Export]
    public int MaxAmmo = 30;
    
    [Export]
    public int CurrentAmmo = 30;
    
    [Export]
    public float RecoilSpread = 0.02f;  // Maximum bullet spread due to recoil
    
    private float _timeSinceLastShot = 0.0f;
    private Node3D _muzzle;
    private AnimationPlayer _animationPlayer;
    private Camera3D _camera;
    private Node3D _rootNode;
    private float _currentRecoil = 0.0f;
    private float _targetRecoil = 0.0f;
    private float _currentSpread = 0.0f;
    private HUD _hud;
    private Player _player;
    private RandomNumberGenerator _rng = new RandomNumberGenerator();

    public override void _Ready()
    {
        _muzzle = GetNode<Node3D>("Muzzle");
        _animationPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
        _rng.Randomize();
        
        // Find the camera and root node based on the scene structure
        Node parent = GetParent();
        while (parent != null)
        {
            if (parent is Camera3D camera)
            {
                _camera = camera;
            }
            else if (parent is Player player)
            {
                _player = player;
                _rootNode = player;
                break;
            }
            parent = parent.GetParent();
        }
        
        // Get HUD reference
        _hud = GetNode<HUD>("/root/Main/HUD");
        
        // Initialize HUD ammo display
        if (_hud != null)
        {
            _hud.MaxAmmo = MaxAmmo;
            _hud.CurrentAmmo = CurrentAmmo;
            _hud.UpdateAmmo(CurrentAmmo);
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("reload"))
        {
            Reload();
        }
    }

    public override void _Process(double delta)
    {
        // Handle shooting cooldown
        _timeSinceLastShot += (float)delta;
        
        // Handle recoil and spread recovery
        if (_currentRecoil != _targetRecoil && _player != null)
        {
            _currentRecoil = Mathf.MoveToward(_currentRecoil, _targetRecoil, RecoilRecovery * (float)delta);
            _player.ApplyRecoil(_currentRecoil);
        }
        
        // Reset target recoil and spread
        _targetRecoil = Mathf.MoveToward(_targetRecoil, 0, RecoilRecovery * 0.5f * (float)delta);
        _currentSpread = Mathf.MoveToward(_currentSpread, 0, RecoilRecovery * (float)delta);
    }

    public void Shoot()
    {
        if (_timeSinceLastShot < FireRate || BulletScene == null || CurrentAmmo <= 0)
            return;

        // Reset shooting cooldown
        _timeSinceLastShot = 0;
        
        // Decrease ammo
        CurrentAmmo--;
        if (_hud != null)
        {
            _hud.UpdateAmmo(CurrentAmmo);
        }

        // Get base shooting direction
        Vector3 shootDirection;
        if (_camera != null)
        {
            // Player gun: Use camera for aiming
            Vector2 mousePos = _camera.GetViewport().GetMousePosition();
            var from = _camera.ProjectRayOrigin(mousePos);
            var to = from + _camera.ProjectRayNormal(mousePos) * MaxRange;
            
            var space = GetWorld3D().DirectSpaceState;
            var query = new PhysicsRayQueryParameters3D
            {
                From = from,
                To = to,
                CollideWithAreas = true,
                CollideWithBodies = true,
                CollisionMask = 1 | 2  // Layer 1 (default) and 2 (environment)
            };
            
            var result = space.IntersectRay(query);
            Vector3 targetPoint = result.Count > 0 ? (Vector3)result["position"] : to;
            shootDirection = (targetPoint - _muzzle.GlobalPosition).Normalized();
            
            // Apply spread based on current recoil
            if (_currentSpread > 0)
            {
                float spreadX = (_rng.Randf() - 0.5f) * _currentSpread;
                float spreadY = (_rng.Randf() - 0.5f) * _currentSpread;
                
                Basis spreadBasis = new Basis();
                spreadBasis = spreadBasis.Rotated(Vector3.Right, spreadX)
                                      .Rotated(Vector3.Up, spreadY);
                shootDirection = spreadBasis * shootDirection;
            }
        }
        else
        {
            // Enemy gun: Use forward direction
            shootDirection = -GlobalTransform.Basis.Z;
        }

        // Create bullet instance
        var bullet = BulletScene.Instantiate<CharacterBody3D>();
        GetTree().Root.AddChild(bullet);
        
        // Set bullet position and initialize
        bullet.GlobalPosition = _muzzle.GlobalPosition;
        if (bullet is Bullet bulletScript)
        {
            bulletScript.Initialize(shootDirection);
        }

        // Apply recoil and increase spread
        _targetRecoil += RecoilForce;
        _currentSpread = Mathf.Min(_currentSpread + RecoilSpread, RecoilSpread * 3);
        
        // Play shooting animation if available
        if (_animationPlayer != null && _animationPlayer.HasAnimation("shoot"))
        {
            _animationPlayer.Stop();
            _animationPlayer.Play("shoot");
        }
    }
    
    public void Reload()
    {
        if (CurrentAmmo == MaxAmmo) return;
        
        // Play reload animation here if available
        
        CurrentAmmo = MaxAmmo;
        if (_hud != null)
        {
            _hud.UpdateAmmo(CurrentAmmo);
        }
    }
} 