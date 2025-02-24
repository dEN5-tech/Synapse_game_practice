using Godot;
using System;

public partial class HUD : Control
{
    private Label _healthLabel;
    private Label _ammoLabel;
    private Control _crosshair;
    private Panel _healthBar;
    private Panel _ammoPanel;
    private Gun _gun;
    private Player _player;
    
    // Menu elements
    private Panel _menuBackdrop;
    private Panel _menuPanel;
    private bool _isMenuOpen = false;
    private const float MENU_ANIMATION_SPEED = 0.3f;
    private const float BACKDROP_OPACITY = 0.4f;
    private Tween _currentTween;
    
    [Export]
    public float MaxHealth = 100.0f;
    
    [Export]
    public float CurrentHealth = 100.0f;
    
    [Export]
    public int MaxAmmo = 30;
    
    [Export]
    public int CurrentAmmo = 30;

    public override void _Ready()
    {
        // Get references to UI elements
        _healthLabel = GetNode<Label>("HealthPanel/HealthLabel");
        _ammoLabel = GetNode<Label>("AmmoPanel/AmmoLabel");
        _crosshair = GetNode<Control>("Crosshair");
        _healthBar = GetNode<Panel>("HealthPanel");
        _ammoPanel = GetNode<Panel>("AmmoPanel");
        _menuBackdrop = GetNode<Panel>("MenuBackdrop");
        _menuPanel = GetNode<Panel>("MenuPanel");
        
        // Get references to game elements
        _gun = GetNode<Gun>("../Player/CameraMount/Camera3D/Gun");
        _player = GetNode<Player>("../Player");
        
        // Initial UI update
        UpdateHealthDisplay();
        UpdateAmmoDisplay();
        
        // Initialize menu in closed state
        _menuBackdrop.Modulate = new Color(1, 1, 1, 0);
        _menuBackdrop.Visible = false;
        _menuPanel.Position = new Vector2(-300, 0); // Start off-screen
        _menuPanel.Visible = false;
        _isMenuOpen = false;
        
        // Ensure mouse is hidden and captured at start
        Input.MouseMode = Input.MouseModeEnum.Captured;
        
        // Ensure game is not paused at start
        GetTree().Paused = false;
    }

    public override void _Process(double delta)
    {
        // Update UI elements
        UpdateHealthDisplay();
        UpdateAmmoDisplay();
        
        // Update UI visibility based on menu state
        if (_crosshair != null) _crosshair.Visible = !_isMenuOpen;
        if (_healthBar != null) _healthBar.Visible = !_isMenuOpen;
        if (_ammoPanel != null) _ammoPanel.Visible = !_isMenuOpen;
    }
    
    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            if (_isMenuOpen)
                CloseMenu();
            else
                OpenMenu();
            
            // Prevent the event from propagating
            GetViewport().SetInputAsHandled();
        }
    }
    
    private void OpenMenu()
    {
        if (_isMenuOpen) return;
        
        _isMenuOpen = true;
        
        // Show menu elements
        _menuBackdrop.Visible = true;
        _menuPanel.Visible = true;
        
        // Kill any existing animation
        _currentTween?.Kill();
        _currentTween = CreateTween();
        
        // Animate backdrop fade in
        _currentTween.Parallel().TweenProperty(
            _menuBackdrop,
            "modulate",
            new Color(1, 1, 1, BACKDROP_OPACITY),
            MENU_ANIMATION_SPEED
        ).SetTrans(Tween.TransitionType.Sine);
        
        // Animate menu slide in
        _currentTween.Parallel().TweenProperty(
            _menuPanel,
            "position",
            new Vector2(0, 0),
            MENU_ANIMATION_SPEED
        ).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        
        // Show mouse cursor
        Input.MouseMode = Input.MouseModeEnum.Visible;
        
        // Pause game
        GetTree().Paused = true;
    }
    
    private void CloseMenu()
    {
        if (!_isMenuOpen) return;
        
        _isMenuOpen = false;
        
        // Kill any existing animation
        _currentTween?.Kill();
        _currentTween = CreateTween();
        
        // Animate backdrop fade out
        _currentTween.Parallel().TweenProperty(
            _menuBackdrop,
            "modulate",
            new Color(1, 1, 1, 0),
            MENU_ANIMATION_SPEED
        ).SetTrans(Tween.TransitionType.Sine);
        
        // Animate menu slide out
        _currentTween.Parallel().TweenProperty(
            _menuPanel,
            "position",
            new Vector2(-300, 0),
            MENU_ANIMATION_SPEED
        ).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        
        // Hide elements when animation completes
        _currentTween.TweenCallback(Callable.From(() => {
            _menuBackdrop.Visible = false;
            _menuPanel.Visible = false;
        }));
        
        // Hide and capture mouse cursor
        Input.MouseMode = Input.MouseModeEnum.Captured;
        
        // Unpause game
        GetTree().Paused = false;
    }
    
    private void UpdateHealthDisplay()
    {
        if (_healthLabel != null)
        {
            _healthLabel.Text = $"HEALTH: {CurrentHealth:F0}";
        }
        
        if (_healthBar != null)
        {
            // Update health bar color based on health percentage
            float healthPercentage = CurrentHealth / MaxHealth;
            Color barColor = new Color(
                Mathf.Lerp(1.0f, 0.0f, healthPercentage),  // Red component
                Mathf.Lerp(0.0f, 1.0f, healthPercentage),  // Green component
                0.0f  // Blue component
            );
            
            // Animate health bar
            var tween = CreateTween();
            tween.TweenProperty(_healthBar, "modulate", barColor, 0.2f)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.Out);
            
            // Add pulse effect when health is low
            if (healthPercentage < 0.3f)
            {
                var pulseTween = CreateTween();
                pulseTween.SetLoops(); // Set to infinite loops
                pulseTween.TweenProperty(_healthBar, "modulate:a", 0.6f, 0.5f)
                    .SetTrans(Tween.TransitionType.Sine)
                    .SetEase(Tween.EaseType.InOut);
                pulseTween.TweenProperty(_healthBar, "modulate:a", 1.0f, 0.5f)
                    .SetTrans(Tween.TransitionType.Sine)
                    .SetEase(Tween.EaseType.InOut);
            }
        }
    }
    
    private void UpdateAmmoDisplay()
    {
        if (_ammoLabel != null)
        {
            _ammoLabel.Text = $"AMMO: {CurrentAmmo}/{MaxAmmo}";
            
            // Add shake effect when low on ammo
            if (CurrentAmmo < MaxAmmo * 0.3f)
            {
                var tween = CreateTween();
                tween.TweenProperty(_ammoLabel, "position:x", _ammoLabel.Position.X + 2, 0.1f)
                    .SetTrans(Tween.TransitionType.Sine);
                tween.TweenProperty(_ammoLabel, "position:x", _ammoLabel.Position.X - 2, 0.1f)
                    .SetTrans(Tween.TransitionType.Sine);
                tween.TweenProperty(_ammoLabel, "position:x", _ammoLabel.Position.X, 0.1f)
                    .SetTrans(Tween.TransitionType.Sine);
            }
        }
    }
    
    public void UpdateHealth(float newHealth)
    {
        CurrentHealth = Mathf.Clamp(newHealth, 0, MaxHealth);
        UpdateHealthDisplay();
    }
    
    public void UpdateAmmo(int newAmmo)
    {
        CurrentAmmo = Mathf.Clamp(newAmmo, 0, MaxAmmo);
        UpdateAmmoDisplay();
    }
    
    private void OnResumePressed()
    {
        CloseMenu();
    }
    
    private void OnOptionsPressed()
    {
        // Implement options menu functionality
    }
    
    private void OnQuitPressed()
    {
        GetTree().Quit();
    }
} 