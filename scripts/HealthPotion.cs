using Godot;
using System;

public partial class HealthPotion : Area3D
{
    [Export]
    public float HealAmount = 25.0f;

    [Export]
    public float RotationSpeed = 2.0f;

    public override void _Ready()
    {
        // Connect the body entered signal
        BodyEntered += OnBodyEntered;
    }

    public override void _Process(double delta)
    {
        // Rotate the potion
        Rotate(Vector3.Up, RotationSpeed * (float)delta);
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body is Player player)
        {
            player.Heal(HealAmount);
            QueueFree(); // Remove the potion after use
        }
    }
} 