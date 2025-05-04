namespace Domain.Models.Exceptions
{
	public class HandledException
	{
		public Exception Exception { get; set; }

		public string Message { get; set; }

		public HandledException(Exception ex, string? message = null)
		{
			Exception = ex;
			Message = message ?? ex.Message;
		}
	}
}
