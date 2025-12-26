using System.Windows;
using System.Windows.Controls;
using SteamServerBuddy.ViewModels;

namespace SteamServerBuddy.Converters
{
    public class SettingsTemplateSelector : DataTemplateSelector
    {
        public DataTemplate TextTemplate { get; set; }
        public DataTemplate NumberTemplate { get; set; }
        public DataTemplate SelectFieldTemplate { get; set; }
        public DataTemplate ToggleTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is SettingFieldViewModel field)
            {
                switch (field.Type)
                {
                    case "number":
                        return NumberTemplate;
                    case "select":
                        return SelectFieldTemplate;
                    case "toggle":
                    case "boolean":
                        return ToggleTemplate;
                    default:
                        return TextTemplate;
                }
            }
            return base.SelectTemplate(item, container);
        }
    }
}
