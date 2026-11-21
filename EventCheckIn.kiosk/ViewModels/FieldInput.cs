using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using EventCheckIn.Models;

namespace EventCheckIn.ViewModels;

/// <summary>One host-configured question on the welcome form, with its validation rules.</summary>
public partial class FieldInput : ObservableObject
{
    static readonly Regex EmailRx = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
    static readonly Regex PhoneRx = new(@"^\+?[0-9 ()\-.]{7,20}$", RegexOptions.Compiled);

    public FieldDef Def { get; }
    public string Label => Def.Required ? $"{Def.Label} *" : Def.Label;
    public string Placeholder => Def.Placeholder;
    public ObservableCollection<OptionItem> Options { get; }
    public IList<string> OptionValues => Def.Options;

    [ObservableProperty] string text = string.Empty;
    [ObservableProperty] string? selectedOption;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasError))] string error = string.Empty;
    public bool HasError => !string.IsNullOrEmpty(Error);

    public Keyboard Keyboard => Def.Validation.Kind switch
    {
        ValidationKinds.Email => Keyboard.Email,
        ValidationKinds.Phone => Keyboard.Telephone,
        ValidationKinds.Number => Keyboard.Numeric,
        _ => Keyboard.Default
    };
    public int MaxLength => Def.Validation.MaxLength ?? int.MaxValue;

    public FieldInput(FieldDef def, FieldAnswer? previous = null)
    {
        Def = def;
        Options = new(def.Options.Select(o => new OptionItem(o)));
        if (previous is not null)
        {
            if (def.Type == FieldTypes.Text) Text = previous.Values.FirstOrDefault() ?? "";
            else if (def.Type == FieldTypes.Dropdown) SelectedOption = previous.Values.FirstOrDefault();
            else foreach (var o in Options) o.IsSelected = previous.Values.Contains(o.Value);
        }
        foreach (var o in Options) o.Owner = this;
    }

    // Clear the error as soon as the attendee fixes the value.
    partial void OnTextChanged(string value) { if (HasError) Validate(); }
    partial void OnSelectedOptionChanged(string? value) { if (HasError) Validate(); }
    internal void OnOptionToggled() { if (HasError) Validate(); }

    public List<string> Values => Def.Type switch
    {
        FieldTypes.Text => string.IsNullOrWhiteSpace(Text) ? [] : [Text.Trim()],
        FieldTypes.Radio => Options.Where(o => o.IsSelected).Select(o => o.Value).Take(1).ToList(),
        FieldTypes.Dropdown => SelectedOption is null ? [] : [SelectedOption],
        _ => Options.Where(o => o.IsSelected).Select(o => o.Value).ToList()
    };

    public FieldAnswer ToAnswer() => new() { FieldId = Def.Id, Label = Def.Label, Values = Values };

    public bool Validate()
    {
        Error = Check() ?? string.Empty;
        return !HasError;
    }

    string? Check()
    {
        var values = Values;
        if (values.Count == 0)
            return Def.Required ? (Def.Type == FieldTypes.Checkbox ? $"Select at least one option for {Def.Label}." : $"{Def.Label} is required.") : null;
        if (Def.Type != FieldTypes.Text) return null;

        var v = Def.Validation;
        var value = values[0];
        string Fail(string fallback) => string.IsNullOrWhiteSpace(v.Message) ? fallback : v.Message;

        if (v.MinLength is { } min && value.Length < min) return Fail($"{Def.Label} must be at least {min} characters.");
        if (v.MaxLength is { } max && value.Length > max) return Fail($"{Def.Label} must be at most {max} characters.");

        switch (v.Kind)
        {
            case ValidationKinds.Email when !EmailRx.IsMatch(value):
                return Fail("Enter a valid email address.");
            case ValidationKinds.Phone when !PhoneRx.IsMatch(value):
                return Fail("Enter a valid phone number.");
            case ValidationKinds.Number:
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var n) &&
                    !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out n))
                    return Fail($"{Def.Label} must be a number.");
                if (v.Min is { } lo && n < lo) return Fail($"{Def.Label} must be at least {lo}.");
                if (v.Max is { } hi && n > hi) return Fail($"{Def.Label} must be at most {hi}.");
                break;
            case ValidationKinds.Regex:
                try
                {
                    if (!Regex.IsMatch(value, $"^(?:{v.Pattern})$", RegexOptions.None, TimeSpan.FromMilliseconds(200)))
                        return Fail($"{Def.Label} is not in the expected format.");
                }
                catch (Exception) { return null; } // bad pattern from host: don't block the attendee
                break;
        }
        return null;
    }
}

public partial class OptionItem(string value) : ObservableObject
{
    public string Value { get; } = value;
    [ObservableProperty] bool isSelected;
    public FieldInput? Owner { get; set; }
    partial void OnIsSelectedChanged(bool value) => Owner?.OnOptionToggled();
}

/// <summary>Picks the input control for each question type.</summary>
public class FieldTemplateSelector : DataTemplateSelector
{
    public DataTemplate? TextTemplate { get; set; }
    public DataTemplate? RadioTemplate { get; set; }
    public DataTemplate? CheckboxTemplate { get; set; }
    public DataTemplate? DropdownTemplate { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container) =>
        ((FieldInput)item).Def.Type switch
        {
            FieldTypes.Radio => RadioTemplate!,
            FieldTypes.Checkbox => CheckboxTemplate!,
            FieldTypes.Dropdown => DropdownTemplate!,
            _ => TextTemplate!
        };
}
