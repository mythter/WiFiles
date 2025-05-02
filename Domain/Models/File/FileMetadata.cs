using System.Text.Json.Serialization;

namespace Domain.Models.File
{
	public class FileMetadata
	{
		public Guid FileId { get; }

		public string Name { get; set; }

		public long Size { get; set; }

		public FileMetadata(string name, long size)
		{
			FileId = Guid.NewGuid();
			Name = name;
			Size = size;
		}

		[JsonConstructor]
		public FileMetadata(Guid fileId, string name, long size)
		{
			FileId = fileId;
			Name = name;
			Size = size;
		}
	}
}
