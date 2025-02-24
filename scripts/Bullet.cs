using Godot;
using System;

public partial class Bullet : CharacterBody3D
{
    [Export]
    public float Speed = 50.0f;
    
    [Export]
    public float Damage = 10.0f;
    
    [Export]
    public float Lifetime = 2.0f;
    
    [Export]
    public float GravityScale = 0.1f;  // How much gravity affects the bullet
    
    private float _timer = 0.0f;
    private Vector3 _direction = Vector3.Forward;
    private Vector3 _velocity;

    public override void _Ready()
    {
        // No need for BodyEntered signal as we'll handle collisions differently
    }

    public override void _PhysicsProcess(double delta)
    {
        // Apply gravity
        _velocity += Vector3.Down * GravityScale * (float)delta;
        
        // Set the velocity for movement
        Velocity = _velocity;
        
        // Move and check for collision
        var collision = MoveAndSlide();
        
        // Check if we collided with anything
        for (int i = 0; i < GetSlideCollisionCount(); i++)
        {
            var collisionInfo = GetSlideCollision(i);
            if (collisionInfo.GetCollider() is Node3D node)
            {
                HandleCollision(node);
                return;
            }
        }
        
        // Update rotation to match trajectory
        var lookDir = _velocity.Normalized();
        if (lookDir != Vector3.Zero)
        {
            LookAt(GlobalPosition + lookDir);
        }
        
        // Handle lifetime
        _timer += (float)delta;
        if (_timer >= Lifetime)
        {
            QueueFree();
        }
    }

    public void Initialize(Vector3 direction)
    {
        _direction = direction.Normalized();
        _velocity = _direction * Speed;
        // Initial rotation to face direction
        LookAt(GlobalPosition + _direction);
    }

    private void HandleCollision(Node3D body)
    {
        // Handle collision with objects
        if (body is RigidBody3D rigidBody)
        {
            // Apply force to rigidbodies
            rigidBody.ApplyCentralImpulse(_velocity * 0.5f);
        }
        
        // Check if it's an enemy and apply damage
        if (body is Enemy enemy)
        {
            enemy.TakeDamage(Damage);
        }
        
        // Create impact effect here if desired
        // For example, particles, sound, decal, etc.
        
        // Destroy the bullet on impact
        QueueFree();
    }
} 