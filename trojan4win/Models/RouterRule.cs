using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace trojan4win.Models;

public class RouterRule : INotifyPropertyChanged
{
    private string _policy = "";
    public string Policy { get => _policy; set { _policy = value; OnPropertyChanged(); } }

    private string _type = "";
    public string Type { get => _type; set { _type = value; OnPropertyChanged(); } }

    private string _value = "";
    public string Value { get => _value; set { _value = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public RouterRule Clone() => new()
    {
        Policy = Policy,
        Type = Type,
        Value = Value
    };
}
