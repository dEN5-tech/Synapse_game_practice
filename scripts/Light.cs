using Godot;
using System;

public partial class Light : Node3D
{
    [Export]
    public float BaseIntensity = 2.0f;

    [Export]
    public float IntensityMultiplier = 1.0f;

    [Export]
    public Color LightColor = new Color(1.0f, 0.98f, 0.9f, 1.0f);

    [Export]
    public float Range = 25.0f;

    [Export]
    public bool IsDynamic = false;

    [Export]
    public float PulseSpeed = 0.5f;

    [Export]
    public float PulseIntensity = 0.2f;

    private OmniLight3D _omniLight;
    private float _time = 0.0f;
    private float _baseEnergy;

    public override void _Ready()
    {
        _omniLight = GetNode<OmniLight3D>("OmniLight3D");
        
        if (_omniLight != null)
        {
            _omniLight.LightColor = LightColor;
            _baseEnergy = BaseIntensity * IntensityMultiplier;
            _omniLight.LightEnergy = _baseEnergy;
            _omniLight.OmniRange = Range;
        }
    }

    public override void _Process(double delta)
    {
        if (_omniLight == null || !IsDynamic) return;

        _time += (float)delta;
        
        // Subtle pulse effect if dynamic is enabled
        float pulseEffect = IsDynamic ? Mathf.Sin(_time * PulseSpeed) * PulseIntensity : 0;
        float finalIntensity = _baseEnergy + pulseEffect;
        
        _omniLight.LightEnergy = finalIntensity;
    }

    public void SetColor(Color color)
    {
        LightColor = color;
        if (_omniLight != null)
        {
            _omniLight.LightColor = color;
        }
    }

    public void SetIntensity(float intensity)
    {
        BaseIntensity = intensity;
        if (_omniLight != null)
        {
            _baseEnergy = BaseIntensity * IntensityMultiplier;
            _omniLight.LightEnergy = _baseEnergy;
        }
    }

    public void SetRange(float range)
    {
        Range = range;
        if (_omniLight != null)
        {
            _omniLight.OmniRange = range;
        }
    }
} 