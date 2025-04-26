namespace Domain.Models
{
	public class ResponseModel
	{
		public bool IsAccepted { get; set; }

		public ResponseModel(bool isAccepted)
		{
			IsAccepted = isAccepted;
		}
	}
}
