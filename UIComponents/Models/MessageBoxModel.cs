using UIComponents.Enums;

namespace UIComponents.Models
{
	public class MessageBoxModel
	{
		public Guid Id { get; } = Guid.NewGuid();
		public MessageType Type { get; set; }
		public string? Title { get; set; }
		public string? Message { get; set; }
		public int ButtonsCount { get; set; } = 1;
		public bool IsVisible { get; set; } = true;
		public bool IsModal { get; set; }
		public TaskCompletionSource<bool>? CompletionSource { get; set; }
	}
}
