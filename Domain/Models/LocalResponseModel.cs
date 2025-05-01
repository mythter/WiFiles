namespace Domain.Models
{
	public class LocalResponseModel(bool isAccepted, LocalDeviceModel receiver) : ResponseModel<LocalDeviceModel>(isAccepted, receiver)
	{
	}
}
