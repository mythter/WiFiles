using System.Net;
using Domain.Enums;

namespace Domain.Models.File
{
	public class FileModel
	{
		public string Path { get; set; }
		public long Size { get; set; }

		public FileModel(string path, long size)
		{
			Path = path;
			Size = size;
		}
	}
}
