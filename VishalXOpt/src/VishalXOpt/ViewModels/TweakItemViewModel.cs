using VishalXOpt.Models;
using VishalXOpt.Mvvm;

namespace VishalXOpt.ViewModels;

public sealed class TweakItemViewModel : ViewModelBase
{
    private readonly bool? _originalState;
    private bool _touched;

    public TweakDefinition Definition { get; }

    /// <param name="currentState">The live state read from the system: true = optimization
    /// applied, false = Windows default, null = unknown/custom value (or not readable).</param>
    public TweakItemViewModel(TweakDefinition definition, bool? currentState)
    {
        Definition = definition;
        _originalState = currentState;
        _isOn = currentState ?? false;
    }

    public string Name => Definition.Name;
    public string Description => Definition.Description;
    public bool HasWarning => !string.IsNullOrWhiteSpace(Definition.DependencyWarning);
    public string? DependencyWarning => Definition.DependencyWarning;
    public bool IsRisky => Definition.Risk != RiskLevel.None;
    public string RiskTooltip => Definition.Risk switch
    {
        RiskLevel.High => "This setting may meaningfully reduce system security. Apply at your own risk.",
        RiskLevel.Medium => "This setting trades off some protection or stability for performance.",
        RiskLevel.Low => "Low risk, but not perfectly free of trade-offs.",
        _ => ""
    };

    private bool _isOn;
    public bool IsOn
    {
        get => _isOn;
        set
        {
            if (!SetField(ref _isOn, value)) return;
            _touched = true;
            OnPropertyChanged(nameof(StateLabel));
        }
    }

    /// <summary>True only when the person (or a preset) actually changed this row. Apply skips
    /// every row that isn't dirty, so an unknown/"Custom" value is never silently overwritten with
    /// a Windows default just because Apply was clicked.</summary>
    public bool IsDirty => _originalState is null ? _touched : _isOn != _originalState.Value;

    public string StateLabel => _originalState is null && !_touched ? "Custom" : IsOn ? "Applied" : "Default";
}
