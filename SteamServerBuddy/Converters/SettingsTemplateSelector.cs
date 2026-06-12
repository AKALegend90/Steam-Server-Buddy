using Avalonia.Controls;
using Avalonia.Controls.Templates;
using SteamServerBuddy.ViewModels;

namespace SteamServerBuddy.Converters
{
    public class SettingsTemplateSelector : IDataTemplate
    {
        public IDataTemplate? TextTemplate { get; set; }
        public IDataTemplate? NumberTemplate { get; set; }
        public IDataTemplate? SelectFieldTemplate { get; set; }
        public IDataTemplate? ToggleTemplate { get; set; }

        public Control? Build(object? param)
        {
            var template = param is SettingFieldViewModel field
                ? field.Type switch
                {
                    "number" => NumberTemplate,
                    "select" => SelectFieldTemplate,
                    "toggle" or "boolean" => ToggleTemplate,
                    _ => TextTemplate
                }
                : TextTemplate;

            return template?.Build(param);
        }

        public bool Match(object? data)
        {
            return data is SettingFieldViewModel;
        }
    }
}
