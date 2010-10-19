using System;
namespace AutoTest.Messages
{
	[Serializable]
	public class InformationMessage : IMessage
    {
        private string _message;

        public string Message { get { return _message; } }

        public InformationMessage(string message)
        {
            _message = message;
        }
    }
}

