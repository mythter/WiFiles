using Domain.Models.Device;

namespace Domain.Models.Response
{
	public class LocalResponseModel(bool isAccepted, LocalDeviceModel receiver) : ResponseModel<LocalDeviceModel>(isAccepted, receiver)
	{
	}
}
