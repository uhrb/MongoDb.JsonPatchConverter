using Microsoft.AspNetCore.JsonPatch.Operations;

namespace MongoDb.JsonPatchConverter
{
    public class OperationError
    {
        public OperationError(string message, OperationErrorType operationErrorType, Operation operation)
        {
            Message = message;
            Operation = operation;
            OperationErrorType = operationErrorType;
        }

        public string Message { get; }
        public OperationErrorType OperationErrorType { get; }
        public Operation Operation { get; }
    }
}