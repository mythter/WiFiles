using Microsoft.AspNetCore.SignalR;

namespace Server.Hubs
{
    public class FileHub : Hub
    {
        public async Task SendingFile(byte[] chunk, string whomId)
        {
            await Clients.Client(whomId).SendAsync("ReceivingFile", chunk);
        }

        public async Task StartSendingFile(string fileName, long length, string whomId)
        {
            await Clients.Client(whomId).SendAsync("StartReceivingFile", fileName, length);
        }

        public async Task EndSendingFile(string whomId)
        {
            await Clients.Client(whomId).SendAsync("EndReceivingFile");
        }
    }
}
