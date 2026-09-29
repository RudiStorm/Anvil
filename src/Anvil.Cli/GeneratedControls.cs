namespace Anvil.Cli;

internal sealed record GeneratedControl(
    string Name,
    string Folder,
    string[] Files,
    string ShowcaseLabel,
    bool RequiresBrowserEnhancement);

internal static class GeneratedControlCatalog
{
    private static readonly HashSet<string> EnhancedControls =
    [
        "AlertDialog", "Calendar", "Carousel", "Combobox", "Command", "ContextMenu", "DatePicker", "Dialog",
        "Drawer", "DropdownMenu", "HoverCard", "Popover", "Resizable", "Sheet", "Sidebar", "Toast", "Tooltip"
    ];

    private static readonly string[] Names =
    [
        "Accordion", "Alert", "AlertDialog", "AspectRatio", "Attachment", "Avatar", "Badge", "Breadcrumb",
        "Bubble", "Button", "ButtonGroup", "Calendar", "Card", "Carousel", "Chart", "Checkbox", "Collapsible",
        "Combobox", "Command", "ContextMenu", "DataTable", "DatePicker", "Dialog", "Direction", "Drawer",
        "DropdownMenu", "Empty", "Field", "Form", "HoverCard", "Input", "InputGroup", "InputOtp", "Item",
        "Kbd", "Label", "Marker", "Menubar", "Message", "MessageScroller", "NativeSelect", "NavigationMenu",
        "Pagination", "Popover", "Progress", "Questionnaire", "RadioGroup", "Resizable", "ScrollArea", "Select",
        "Separator", "Sheet", "Sidebar", "Skeleton", "Slider", "Spinner", "Switch", "Table", "Tabs", "Textarea",
        "Toast", "Toggle", "ToggleGroup", "Tooltip", "Typography"
    ];

    internal static IReadOnlyList<GeneratedControl> Items { get; } = Names
        .Select(name => new GeneratedControl(
            name,
            name,
            [$"Anvil{name}.razor"],
            SplitWords(name),
            EnhancedControls.Contains(name)))
        .ToArray();

    private static string SplitWords(string value)
    {
        var words = new List<string>();
        var start = 0;
        for (var index = 1; index < value.Length; index++)
        {
            if (char.IsUpper(value[index]))
            {
                words.Add(value[start..index]);
                start = index;
            }
        }

        words.Add(value[start..]);
        return string.Join(' ', words);
    }
}
