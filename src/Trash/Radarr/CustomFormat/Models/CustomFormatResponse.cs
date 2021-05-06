namespace Trash.Radarr.CustomFormat.Models
{
    public class CustomFormatResponse
    {
        public CustomFormatResponse(ApiOperation operation, int? customFormatId, string trashId,
            string customFormatName)
        {
            Operation = operation;
            CustomFormatId = customFormatId;
            TrashId = trashId;
            CustomFormatName = customFormatName;
        }

        public ApiOperation Operation { get; }
        public int? CustomFormatId { get; }
        public string TrashId { get; }
        public string CustomFormatName { get; }
    }
}
