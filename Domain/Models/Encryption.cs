namespace Domain.Models
{
	public class Encryption
	{
		public byte[] PublicKey { get; set; }

		public byte[]? IV { get; set; }

		public Encryption(byte[] publicKey, byte[]? iv = null)
		{
			PublicKey = publicKey;
			IV = iv;
		}
	}
}
