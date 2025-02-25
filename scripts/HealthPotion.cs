using Godot;
using System;
using Godot.NativeInterop;

public partial class HealthPotion : Area3D
{
    [Export]
    public float HealAmount = 25.0f;

    [Export]
    public float RotationSpeed = 2.0f;

    [Export]
    public AudioStream PickupSound;

    private AudioStreamPlayer3D _audioPlayer;
    private GpuParticles3D _particles;
    private Node3D _model;
    private bool _isPickedUp = false;

    public override void _Ready()
    {
        // Connect the body entered signal
        BodyEntered += OnBodyEntered;

        // Setup audio player
        _audioPlayer = new AudioStreamPlayer3D();
        AddChild(_audioPlayer);
        _audioPlayer.Stream = PickupSound;
        _audioPlayer.UnitSize = 3.0f; // Adjust for 3D falloff
        _audioPlayer.MaxDistance = 10.0f;

        // Get references
        _particles = GetNode<GpuParticles3D>("HealingParticles");
        _model = GetNode<Node3D>("RootNode");
    }

    public override void _Process(double delta)
    {
        // Rotate the potion only if not picked up
        if (!_isPickedUp)
        {
            Rotate(Vector3.Up, RotationSpeed * (float)delta);
        }
    }

    private void OnBodyEntered(Node3D body)
    {
        // Prevent multiple pickups
        if (_isPickedUp) return;

        if (body is Player player && player.CanBeHealed())
        {
            _isPickedUp = true;
            
            // Disable further collisions immediately
            SetDeferred("monitoring", false);
            SetDeferred("monitorable", false);
            
            player.Heal(HealAmount);
            
            // Play sound before removing
            if (PickupSound != null)
            {
                // Reparent audio player to not destroy it with the potion
                var parent = GetParent();
                RemoveChild(_audioPlayer);
                parent.AddChild(_audioPlayer);
                _audioPlayer.Play();
                
                // Create a timer to free the audio player after sound finishes
                var timer = GetTree().CreateTimer(PickupSound.GetLength());
                timer.Timeout += () => _audioPlayer.QueueFree();
            }

            // Create pickup effect
            if (_particles != null)
            {
                // Reparent particles to not destroy them with the potion
                var parent = GetParent();
                RemoveChild(_particles);
                parent.AddChild(_particles);
                
                // Burst effect
                _particles.Amount = 64;
                _particles.Emitting = true;
                _particles.OneShot = true;
                
                // Create a timer to free the particles after effect finishes
                var timer = GetTree().CreateTimer(_particles.Lifetime);
                timer.Timeout += () => _particles.QueueFree();
            }

            // Fade out model
            if (_model != null)
            {
                var tween = CreateTween();
                tween.TweenProperty(_model, "scale", Vector3.Zero, 0.2f)
                    .SetTrans(Tween.TransitionType.Sine)
                    .SetEase(Tween.EaseType.Out);
            }
            
            // Delay the actual removal slightly to allow effects to play
            var removalTimer = GetTree().CreateTimer(0.2f);
            removalTimer.Timeout += () => QueueFree();
        }
    }
} 