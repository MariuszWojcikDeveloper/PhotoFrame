using System.ComponentModel.DataAnnotations;

namespace PhotoFrame.DataAccess;

public class MediaFile
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public string Path { get; set; } = string.Empty;
    
    public int TimesShown { get; set; } = 0;
}
