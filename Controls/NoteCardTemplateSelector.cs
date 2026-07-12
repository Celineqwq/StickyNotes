using System.Windows;
using System.Windows.Controls;
using StickyNotes.Models;

namespace StickyNotes.Controls;

public class NoteCardTemplateSelector : DataTemplateSelector
{
    public DataTemplate? TextTemplate { get; set; }
    public DataTemplate? ImageTemplate { get; set; }
    public DataTemplate? FileTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
    {
        if (item is NoteItem note)
        {
            return note.Type switch
            {
                NoteType.Text => TextTemplate,
                NoteType.Image => ImageTemplate,
                NoteType.File => FileTemplate,
                _ => TextTemplate
            };
        }
        return TextTemplate;
    }
}