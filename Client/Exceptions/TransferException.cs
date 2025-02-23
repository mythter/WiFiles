namespace Client.Exceptions
{
    public class TransferException : Exception
    {
        public TransferException()
        {
        }

        public TransferException(string? message) : base(message)
        {
        }

        public TransferException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
