namespace SondeFileSynchronizer.ServiceClient
{
    public class ImportResult
    {
        public ImportResultResponse ResultResponse { get; set; } = new ImportResultResponse();

        public ImportStatus ImportStatus { get; set; } = new ImportStatus();

        public bool HasErrors => ResultResponse.errorCount > 0;
        public int SuccessCount => ResultResponse.successCount;
        public int UpdateCount => ResultResponse.updateCount;
        public int ErrorCount => ResultResponse.errorCount;
    }
}
