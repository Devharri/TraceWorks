using TraceWorks.Shared.Models;

namespace TraceWorks.Shared.Services;
public sealed class TagConfigurationService
{
    private readonly List<TagDefinition> _tags = [];
    public event Action? TagsChanged;
    public TagConfigurationService()
    {
        // Initialize with some default tags
        _tags.Add(new TagDefinition
        {
            Id = 1,
            Name = "bool",
            Address = "DB100.DBX0.0",
            DataType = TagDataType.Bool,
            PollingIntervalMs = TagPollingInterval.Ms1000
        });
        /*
        _tags.Add(new TagDefinition
        {
            Id = 2,
            Name = "real",
            Address = "DB100.DBD2",
            DataType = TagDataType.Float,
            PollingIntervalMs = PollingInterval.Ms2000
        });
        _tags.Add(new TagDefinition
        {
            Id = 3,
            Name = "int",
            Address = "DB100.DBW6",
            DataType = TagDataType.Int,
            PollingIntervalMs = PollingInterval.Ms3000
        }); 
        */
    }
    public IReadOnlyList<TagDefinition> GetTags()
    {
        return _tags.AsReadOnly();
    }
    public void AddTag(TagDefinition tag)
    {
        _tags.Add(tag);
        TagsChanged?.Invoke();
    }
    public void RemoveTag(int tagId)
    {
        var tag = _tags.FirstOrDefault(t => t.Id == tagId);
        if (tag is null)
        {
            return;
        }
        _tags.Remove(tag);
        TagsChanged?.Invoke();
    }
    public void UpdateTag(TagDefinition updatedTag)
    {
        var index = _tags.FindIndex(t => t.Id == updatedTag.Id);
        if (index < 0)
        {
            return;
        }
        _tags[index] = updatedTag;
        TagsChanged?.Invoke();
    }
}